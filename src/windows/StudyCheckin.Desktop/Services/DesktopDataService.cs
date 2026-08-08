using System.IO;
using StudyCheckin.Core;

namespace StudyCheckin.Desktop.Services;

public sealed record SyncResult(bool Succeeded, string Message, int PendingWrites);

public sealed class DesktopDataService
{
    private readonly string _dataDirectory;
    private readonly JsonFileStore<StudyState> _stateStore;
    private readonly JsonFileStore<StudyState> _mockCloudStore;
    private readonly JsonFileStore<AppSettings> _settingsStore;
    private readonly ExcelSyncService _excel = new();

    public DesktopDataService()
    {
        _dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StudyCheckin");
        _stateStore = new JsonFileStore<StudyState>(Path.Combine(_dataDirectory, "state.json"));
        _mockCloudStore = new JsonFileStore<StudyState>(Path.Combine(_dataDirectory, "mock-cloud.json"));
        _settingsStore = new JsonFileStore<AppSettings>(Path.Combine(_dataDirectory, "settings.json"));
    }

    public StudyState State { get; private set; } = new();
    public AppSettings Settings { get; private set; } = new();
    public string DataDirectory => _dataDirectory;

    public async Task InitializeAsync()
    {
        Settings = await _settingsStore.LoadAsync();
        if (string.IsNullOrWhiteSpace(Settings.ExcelPath))
        {
            Settings.ExcelPath = DefaultPathLocator.FindPlanWorkbook();
            await _settingsStore.SaveAsync(Settings);
        }

        State = await _stateStore.LoadAsync();
        if (State.Days.Count == 0 && File.Exists(Settings.ExcelPath))
        {
            State.Days = await _excel.ImportAsync(Settings.ExcelPath);
            await _stateStore.SaveAsync(State);
            await _mockCloudStore.SaveAsync(State);
        }

        if (State.Days.Count == 0)
        {
            State.Days.Add(CreateFallbackDay());
            await _stateStore.SaveAsync(State);
        }
    }

    public DailyRecord? GetDay(DateTime date) => State.Days.FirstOrDefault(day => day.Date.Date == date.Date);

    public DateTime GetInitialDate(DateTime today)
    {
        return State.Days.Where(day => day.Date.Date >= today.Date).Select(day => day.Date.Date).FirstOrDefault()
            is var future && future != default
            ? future
            : State.Days.OrderBy(day => day.Date).Last().Date.Date;
    }

    public async Task SaveLocalAsync()
    {
        await _stateStore.SaveAsync(State);
    }

    public async Task SaveSettingsAsync()
    {
        await _settingsStore.SaveAsync(Settings);
    }

    public async Task<SyncResult> SyncDayAsync(DailyRecord record)
    {
        record.Status = CompletionService.Calculate(record).Status;
        record.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await SaveLocalAsync();

        var cloud = await _mockCloudStore.LoadAsync();
        var existing = cloud.Days.FirstOrDefault(day => day.Date.Date == record.Date.Date);
        if (existing is null)
        {
            cloud.Days.Add(record);
        }
        else if (record.UpdatedAtUtc >= existing.UpdatedAtUtc)
        {
            cloud.Days[cloud.Days.IndexOf(existing)] = record;
        }
        cloud.LastSyncedAtUtc = DateTimeOffset.UtcNow;
        await _mockCloudStore.SaveAsync(cloud);

        if (string.IsNullOrWhiteSpace(Settings.ExcelPath))
        {
            return new SyncResult(false, "尚未选择 Excel 文件。", State.PendingExcelWrites.Count);
        }

        var excelResult = await _excel.WriteBackAsync(Settings.ExcelPath, record);
        if (!excelResult.Succeeded)
        {
            if (State.PendingExcelWrites.All(item => item.Date.Date != record.Date.Date))
            {
                State.PendingExcelWrites.Add(new PendingExcelWrite { Date = record.Date.Date, LastError = excelResult.Message });
            }
        }
        else
        {
            State.PendingExcelWrites.RemoveAll(item => item.Date.Date == record.Date.Date);
        }

        State.LastSyncedAtUtc = DateTimeOffset.UtcNow;
        await SaveLocalAsync();
        return new SyncResult(excelResult.Succeeded, excelResult.Message, State.PendingExcelWrites.Count);
    }

    public async Task<int> RetryPendingAsync()
    {
        foreach (var pending in State.PendingExcelWrites.ToList())
        {
            var record = GetDay(pending.Date);
            if (record is null)
            {
                State.PendingExcelWrites.Remove(pending);
                continue;
            }

            var result = await _excel.WriteBackAsync(Settings.ExcelPath, record);
            if (result.Succeeded)
            {
                State.PendingExcelWrites.Remove(pending);
            }
            else
            {
                pending.Attempts++;
                pending.LastError = result.Message;
            }
        }

        await SaveLocalAsync();
        return State.PendingExcelWrites.Count;
    }

    private static DailyRecord CreateFallbackDay() => new()
    {
        Id = "20260915",
        Date = new DateTime(2026, 9, 15),
        Phase = "竞赛优先",
        Courses = "无固定课",
        PlannedMinutes = 120,
        Tasks = new List<PlanTask>
        {
            new() { Id = "demo-math", Date = new DateTime(2026, 9, 15), Category = StudyTaskCategory.Mathematics, Title = "函数、极限概念摸底：概念+例题", PlannedMinutes = 90 },
            new() { Id = "demo-english", Date = new DateTime(2026, 9, 15), Category = StudyTaskCategory.English, Title = "核心词汇150：30分钟", PlannedMinutes = 30 }
        }
    };
}
