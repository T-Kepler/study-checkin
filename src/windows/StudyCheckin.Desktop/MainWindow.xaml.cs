using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using StudyCheckin.Core;
using StudyCheckin.Desktop.Services;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using Forms = System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using TextBox = System.Windows.Controls.TextBox;

namespace StudyCheckin.Desktop;

public partial class MainWindow : Window
{
    private readonly DesktopDataService _data = new();
    private readonly TimerService _timerService = new();
    private readonly ReminderDecisionService _reminderService = new();
    private readonly DispatcherTimer _displayTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _syncTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private readonly HashSet<string> _sentReminderKeys = new(StringComparer.Ordinal);
    private readonly Forms.NotifyIcon _trayIcon;
    private bool _loaded;
    private bool _allowClose;
    private DateTime _selectedDate;
    private DailyRecord? _current;

    public MainWindow()
    {
        InitializeComponent();
        _trayIcon = CreateTrayIcon();
        _displayTimer.Tick += (_, _) =>
        {
            if (_current?.ActiveTask is not null && Keyboard.FocusedElement is not TextBox)
            {
                RenderTasksOnly();
            }
        };
        _syncTimer.Tick += async (_, _) => await BackgroundTickAsync();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            SyncStateText.Text = "正在读取规划...";
            await _data.InitializeAsync();
            _selectedDate = _data.GetInitialDate(DateTime.Today);
            ExcelPathBox.Text = _data.Settings.ExcelPath;
            StartupCheck.IsChecked = StartupService.IsEnabled();
            DataPathText.Text = _data.DataDirectory;
            _loaded = true;
            SelectDate(_selectedDate);
            _displayTimer.Start();
            _syncTimer.Start();
            SyncStateText.Text = "本地 Mock 已连接";
            if (Environment.GetCommandLineArgs().Any(arg => arg == "--tray"))
            {
                Hide();
            }
        }
        catch (Exception exception)
        {
            SyncStateText.Text = "初始化失败";
            MessageBox.Show(exception.Message, "自律台", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SelectDate(DateTime date)
    {
        _selectedDate = date.Date;
        _current = _data.GetDay(_selectedDate);
        SidebarDateText.Text = _selectedDate.ToString("MM月dd日");
        SidebarWeekdayText.Text = _selectedDate.ToString("dddd");

        if (_current is null)
        {
            PhaseText.Text = "无计划";
            CourseText.Text = "该日期没有任务";
            HeaderSummaryText.Text = "请选择规划范围内的日期";
            TaskList.ItemsSource = Array.Empty<TaskDisplayItem>();
            RecapBox.Text = string.Empty;
            RenderProgress();
            return;
        }

        PhaseText.Text = _current.Phase;
        CourseText.Text = string.IsNullOrWhiteSpace(_current.Courses) ? "无固定课" : _current.Courses.Replace("；", "\n");
        HeaderSummaryText.Text = $"{_selectedDate:yyyy年M月d日} · {_current.Tasks.Count(task => !task.IsPaused)} 项可执行任务";
        RecapBox.Text = _current.Recap;
        Render();
    }

    private void Render()
    {
        RenderTasksOnly();
        RenderProgress();
        PendingText.Text = _data.State.PendingExcelWrites.Count == 0
            ? string.Empty
            : $"{_data.State.PendingExcelWrites.Count} 条 Excel 写入等待重试";
    }

    private void RenderTasksOnly()
    {
        if (_current is null)
        {
            return;
        }

        TaskList.ItemsSource = new ObservableCollection<TaskDisplayItem>(
            _current.Tasks.Select(task => TaskDisplayItem.From(task, DateTimeOffset.UtcNow)));
    }

    private void RenderProgress()
    {
        if (_current is null)
        {
            DailyProgress.Value = 0;
            ProgressPercentText.Text = "0%";
            HoursText.Text = "0 / 0 小时";
            StatusText.Text = "无计划";
            return;
        }

        var snapshot = CompletionService.Calculate(_current);
        var percent = Math.Round(snapshot.Rate * 100);
        DailyProgress.Value = percent;
        ProgressPercentText.Text = $"{percent:0}%";
        HoursText.Text = $"实际 {snapshot.ActualMinutes / 60d:0.0} / 计划 {snapshot.PlannedMinutes / 60d:0.0} 小时";
        StatusText.Text = StatusLabel(snapshot.Status);
    }

    private void PreviousDay_Click(object sender, RoutedEventArgs e) => SelectDate(_selectedDate.AddDays(-1));
    private void NextDay_Click(object sender, RoutedEventArgs e) => SelectDate(_selectedDate.AddDays(1));

    private async void TaskCheck_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded || _current is null || sender is not CheckBox checkBox || checkBox.Tag is not string taskId)
        {
            return;
        }

        var task = _current.Tasks.First(item => item.Id == taskId);
        if (task.StartedAtUtc.HasValue && checkBox.IsChecked == true)
        {
            await StopTimerAsync(task, false);
        }
        task.IsCompleted = checkBox.IsChecked == true;
        task.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await SaveAndRenderAsync(syncExcel: false);
    }

    private async void TimerButton_Click(object sender, RoutedEventArgs e)
    {
        if (_current is null || sender is not Button button || button.Tag is not string taskId)
        {
            return;
        }

        var task = _current.Tasks.First(item => item.Id == taskId);
        try
        {
            if (task.StartedAtUtc.HasValue)
            {
                await StopTimerAsync(task, false);
            }
            else
            {
                _timerService.Start(_current, taskId, DateTimeOffset.UtcNow);
            }
            await SaveAndRenderAsync(syncExcel: false);
        }
        catch (InvalidOperationException exception)
        {
            MessageBox.Show(exception.Message, "计时", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async Task StopTimerAsync(PlanTask task, bool confirmed)
    {
        if (_current is null)
        {
            return;
        }

        try
        {
            _timerService.Stop(_current, task.Id, DateTimeOffset.UtcNow, confirmed);
        }
        catch (LongSessionConfirmationRequiredException exception)
        {
            var answer = MessageBox.Show($"检测到 {exception.Minutes} 分钟计时，是否保留？", "确认学习时长", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Yes)
            {
                _timerService.Stop(_current, task.Id, DateTimeOffset.UtcNow, true);
            }
            else
            {
                task.StartedAtUtc = null;
            }
        }
        await _data.SaveLocalAsync();
    }

    private async void Minutes_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_current is null || sender is not TextBox textBox || textBox.Tag is not string taskId)
        {
            return;
        }

        var task = _current.Tasks.First(item => item.Id == taskId);
        if (!int.TryParse(textBox.Text, out var minutes) || minutes < 0)
        {
            textBox.Text = task.ActualMinutes.ToString();
            return;
        }

        task.ActualMinutes = minutes;
        task.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await SaveAndRenderAsync(syncExcel: false);
    }

    private async void SaveRecap_Click(object sender, RoutedEventArgs e)
    {
        if (_current is null)
        {
            return;
        }
        _current.Recap = RecapBox.Text.Trim();
        await SaveAndRenderAsync(syncExcel: true);
    }

    private async void Sync_Click(object sender, RoutedEventArgs e)
    {
        if (_current is null)
        {
            return;
        }
        await SyncCurrentAsync();
    }

    private async Task SaveAndRenderAsync(bool syncExcel)
    {
        if (_current is null)
        {
            return;
        }
        _current.Status = CompletionService.Calculate(_current).Status;
        _current.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _data.SaveLocalAsync();
        Render();
        if (syncExcel)
        {
            await SyncCurrentAsync();
        }
    }

    private async Task SyncCurrentAsync()
    {
        if (_current is null)
        {
            return;
        }

        SyncStateText.Text = "正在同步...";
        var result = await _data.SyncDayAsync(_current);
        SyncStateText.Text = result.Message;
        PendingText.Text = result.PendingWrites == 0 ? string.Empty : $"{result.PendingWrites} 条 Excel 写入等待重试";
        if (!result.Succeeded && !result.Message.Contains("正在使用", StringComparison.Ordinal))
        {
            _trayIcon.ShowBalloonTip(4000, "同步未完成", result.Message, Forms.ToolTipIcon.Warning);
        }
    }

    private async void BrowseExcel_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Excel 工作簿 (*.xlsx)|*.xlsx",
            CheckFileExists = true,
            InitialDirectory = string.IsNullOrWhiteSpace(_data.Settings.ExcelPath)
                ? Environment.CurrentDirectory
                : Path.GetDirectoryName(_data.Settings.ExcelPath)
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }
        _data.Settings.ExcelPath = dialog.FileName;
        ExcelPathBox.Text = dialog.FileName;
        await _data.SaveSettingsAsync();
        SyncStateText.Text = "Excel 路径已保存";
    }

    private async void StartupCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loaded)
        {
            return;
        }
        try
        {
            var enabled = StartupCheck.IsChecked == true;
            StartupService.SetEnabled(enabled);
            _data.Settings.StartWithWindows = enabled;
            await _data.SaveSettingsAsync();
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "开机启动", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task BackgroundTickAsync()
    {
        if (!_loaded)
        {
            return;
        }
        await _data.RetryPendingAsync();
        Render();
        CheckReminder();
    }

    private void CheckReminder()
    {
        var now = DateTime.Now;
        var record = _data.GetDay(now.Date);
        if (record is null || !_data.Settings.Reminders.Enabled)
        {
            return;
        }

        var schedule = new[]
        {
            (ReminderKind.MorningPlan, _data.Settings.Reminders.MorningPlanTime),
            (ReminderKind.StartNudge, _data.Settings.Reminders.StartNudgeTime),
            (ReminderKind.IncompleteNudge, _data.Settings.Reminders.IncompleteNudgeTime)
        };
        foreach (var item in schedule)
        {
            if (Math.Abs((now.TimeOfDay - item.Item2).TotalMinutes) > 1)
            {
                continue;
            }
            SendReminderOnce(now.Date, item.Item1, record);
        }

        if (now.DayOfWeek == DayOfWeek.Sunday && Math.Abs((now.TimeOfDay - _data.Settings.Reminders.WeeklyReviewTime).TotalMinutes) <= 1)
        {
            SendReminderOnce(now.Date, ReminderKind.WeeklyReview, record);
        }
    }

    private void SendReminderOnce(DateTime date, ReminderKind kind, DailyRecord record)
    {
        var key = $"{date:yyyyMMdd}-{kind}";
        if (!_sentReminderKeys.Add(key))
        {
            return;
        }
        var decision = _reminderService.Evaluate(kind, record);
        if (decision.ShouldSend)
        {
            _trayIcon.ShowBalloonTip(6000, decision.Title, decision.Content, Forms.ToolTipIcon.Info);
        }
    }

    private Forms.NotifyIcon CreateTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("打开自律台", null, (_, _) => ShowFromTray());
        menu.Items.Add("立即同步", null, async (_, _) => await Dispatcher.InvokeAsync(SyncCurrentAsync));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => Dispatcher.Invoke(ExitApplication));

        var icon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty)
                ?? System.Drawing.SystemIcons.Information,
            Text = "自律台",
            Visible = true,
            ContextMenuStrip = menu
        };
        icon.DoubleClick += (_, _) => ShowFromTray();
        return icon;
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApplication()
    {
        _allowClose = true;
        _displayTimer.Stop();
        _syncTimer.Stop();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        Application.Current.Shutdown();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }
        e.Cancel = true;
        Hide();
        _trayIcon.ShowBalloonTip(2500, "自律台仍在运行", "提醒和同步将在托盘继续运行。", Forms.ToolTipIcon.Info);
    }

    private static string StatusLabel(CheckInStatus status) => status switch
    {
        CheckInStatus.InProgress => "进行中",
        CheckInStatus.Completed => "已完成",
        CheckInStatus.Adjusted => "已调整",
        _ => "未开始"
    };
}

public sealed class TaskDisplayItem
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string CategoryLabel { get; init; } = string.Empty;
    public System.Windows.Media.Brush CategoryBrush { get; init; } = System.Windows.Media.Brushes.Gray;
    public int ActualMinutes { get; init; }
    public bool IsCompleted { get; init; }
    public bool CanInteract { get; init; }
    public bool CanStart { get; init; }
    public double Opacity { get; init; } = 1;
    public string TimerAction { get; init; } = string.Empty;
    public string TimerIcon { get; init; } = string.Empty;
    public string TimerDetail { get; init; } = string.Empty;

    public static TaskDisplayItem From(PlanTask task, DateTimeOffset nowUtc)
    {
        var running = task.StartedAtUtc.HasValue;
        var elapsed = running ? nowUtc - task.StartedAtUtc!.Value : TimeSpan.Zero;
        return new TaskDisplayItem
        {
            Id = task.Id,
            Title = task.Title,
            CategoryLabel = CategoryLabelFor(task.Category, task.IsPaused),
            CategoryBrush = CategoryBrushFor(task.Category, task.IsPaused),
            ActualMinutes = task.ActualMinutes,
            IsCompleted = task.IsCompleted,
            CanInteract = !task.IsPaused,
            CanStart = !task.IsPaused && !task.IsCompleted,
            Opacity = task.IsPaused ? 0.5 : 1,
            TimerAction = running ? "停止" : "开始",
            TimerIcon = running ? "\uE769" : "\uE768",
            TimerDetail = running ? $"计时中 {elapsed.Hours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}" : (task.IsPaused ? "该阶段暂停" : string.Empty)
        };
    }

    private static string CategoryLabelFor(StudyTaskCategory category, bool paused)
    {
        if (paused) return "已暂停";
        return category switch
        {
            StudyTaskCategory.Mathematics => "数学一",
            StudyTaskCategory.English => "英语一",
            StudyTaskCategory.SignalSystems843 => "843",
            StudyTaskCategory.Competition => "竞赛",
            StudyTaskCategory.Coursework => "课程",
            _ => "临时任务"
        };
    }

    private static System.Windows.Media.Brush CategoryBrushFor(StudyTaskCategory category, bool paused)
    {
        if (paused) return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(142, 151, 154));
        var color = category switch
        {
            StudyTaskCategory.Mathematics => System.Windows.Media.Color.FromRgb(23, 107, 104),
            StudyTaskCategory.English => System.Windows.Media.Color.FromRgb(52, 91, 146),
            StudyTaskCategory.SignalSystems843 => System.Windows.Media.Color.FromRgb(122, 71, 117),
            StudyTaskCategory.Competition => System.Windows.Media.Color.FromRgb(198, 144, 43),
            StudyTaskCategory.Coursework => System.Windows.Media.Color.FromRgb(74, 105, 83),
            _ => System.Windows.Media.Color.FromRgb(96, 112, 120)
        };
        return new System.Windows.Media.SolidColorBrush(color);
    }
}
