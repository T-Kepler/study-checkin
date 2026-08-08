using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using StudyCheckin.Core;

namespace StudyCheckin.Desktop.Services;

public sealed record ExcelWriteResult(bool Succeeded, bool IsLocked, string Message);

public sealed class ExcelSyncService
{
    private const string SheetName = "每日执行_2026";

    public Task<List<DailyRecord>> ImportAsync(string workbookPath) =>
        RunStaAsync(() => Import(workbookPath));

    public Task<ExcelWriteResult> WriteBackAsync(string workbookPath, DailyRecord record) =>
        RunStaAsync(() => WriteBack(workbookPath, record));

    public bool IsWorkbookLocked(string workbookPath)
    {
        try
        {
            using var stream = File.Open(workbookPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }

    private static List<DailyRecord> Import(string workbookPath)
    {
        if (!File.Exists(workbookPath))
        {
            throw new FileNotFoundException("找不到规划 Excel。", workbookPath);
        }

        dynamic? excel = null;
        dynamic? workbook = null;
        dynamic? sheet = null;
        try
        {
            excel = CreateExcel();
            excel.Visible = false;
            excel.DisplayAlerts = false;
            workbook = excel.Workbooks.Open(workbookPath, ReadOnly: true);
            sheet = workbook.Worksheets[SheetName];

            var records = new List<DailyRecord>();
            for (var row = 2; row < 5000; row++)
            {
                var dateValue = sheet.Cells[row, 1].Value2;
                if (dateValue is null)
                {
                    break;
                }

                var date = ParseExcelDate(dateValue);
                var plannedMinutes = (int)Math.Round(ReadDouble(sheet.Cells[row, 10].Value2) * 60);
                var record = new DailyRecord
                {
                    Id = date.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                    Date = date.Date,
                    Courses = ReadText(sheet.Cells[row, 4].Value2),
                    Phase = ReadText(sheet.Cells[row, 5].Value2),
                    PlannedMinutes = plannedMinutes,
                    Recap = ReadText(sheet.Cells[row, 14].Value2),
                    Status = ParseStatus(ReadText(sheet.Cells[row, 13].Value2))
                };

                AddTask(record, StudyTaskCategory.Mathematics, "数学一", ReadText(sheet.Cells[row, 6].Value2));
                AddTask(record, StudyTaskCategory.English, "英语一", ReadText(sheet.Cells[row, 7].Value2));
                AddTask(record, StudyTaskCategory.SignalSystems843, "843", ReadText(sheet.Cells[row, 8].Value2));
                var other = ReadText(sheet.Cells[row, 9].Value2);
                var otherCategory = other.Contains("竞赛", StringComparison.Ordinal)
                    ? StudyTaskCategory.Competition
                    : StudyTaskCategory.Coursework;
                AddTask(record, otherCategory, "竞赛/课程", other);

                var active = record.Tasks.Where(task => !task.IsPaused).ToList();
                var each = active.Count == 0 ? 0 : plannedMinutes / active.Count;
                foreach (var task in active)
                {
                    task.PlannedMinutes = each;
                }

                var actualMinutes = (int)Math.Round(ReadDouble(sheet.Cells[row, 11].Value2) * 60);
                if (actualMinutes > 0 && active.Count > 0)
                {
                    active[0].ActualMinutes = actualMinutes;
                }

                records.Add(record);
            }

            return records;
        }
        finally
        {
            CloseExcel(excel, workbook, sheet, save: false);
        }
    }

    private ExcelWriteResult WriteBack(string workbookPath, DailyRecord record)
    {
        if (!File.Exists(workbookPath))
        {
            return new ExcelWriteResult(false, false, "规划 Excel 不存在。");
        }

        if (IsWorkbookLocked(workbookPath))
        {
            return new ExcelWriteResult(false, true, "Excel 正在使用，已加入待写队列。");
        }

        dynamic? excel = null;
        dynamic? workbook = null;
        dynamic? sheet = null;
        try
        {
            excel = CreateExcel();
            excel.Visible = false;
            excel.DisplayAlerts = false;
            workbook = excel.Workbooks.Open(workbookPath, ReadOnly: false);
            sheet = workbook.Worksheets[SheetName];

            var targetRow = FindDateRow(sheet, record.Date);
            if (targetRow == 0)
            {
                return new ExcelWriteResult(false, false, "Excel 中找不到对应日期。");
            }

            var snapshot = CompletionService.Calculate(record);
            sheet.Cells[targetRow, 11].Value2 = snapshot.ActualMinutes / 60d;
            sheet.Cells[targetRow, 13].Value2 = StatusText(snapshot.Status);
            sheet.Cells[targetRow, 14].Value2 = record.Recap;
            workbook.Save();
            return new ExcelWriteResult(true, false, "Excel 已更新。");
        }
        catch (COMException exception)
        {
            return new ExcelWriteResult(false, true, $"Excel 写入失败：{exception.Message}");
        }
        finally
        {
            CloseExcel(excel, workbook, sheet, save: false);
        }
    }

    private static dynamic CreateExcel()
    {
        var type = Type.GetTypeFromProgID("Excel.Application")
            ?? throw new InvalidOperationException("本机未安装 Microsoft Excel。");
        return Activator.CreateInstance(type)
            ?? throw new InvalidOperationException("无法启动 Microsoft Excel。");
    }

    private static int FindDateRow(dynamic sheet, DateTime date)
    {
        for (var row = 2; row < 5000; row++)
        {
            var raw = sheet.Cells[row, 1].Value2;
            if (raw is null)
            {
                return 0;
            }

            if (ParseExcelDate(raw).Date == date.Date)
            {
                return row;
            }
        }

        return 0;
    }

    private static void AddTask(DailyRecord record, StudyTaskCategory category, string prefix, string title)
    {
        if (string.IsNullOrWhiteSpace(title) || title == "-")
        {
            return;
        }

        record.Tasks.Add(new PlanTask
        {
            Id = $"{record.Id}-{category}",
            Date = record.Date,
            Category = category,
            Title = title,
            IsPaused = string.Equals(title, "暂停新内容", StringComparison.Ordinal),
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
    }

    private static string ReadText(object? value) => Convert.ToString(value, CultureInfo.CurrentCulture)?.Trim() ?? string.Empty;

    private static double ReadDouble(object? value) => value is null
        ? 0
        : Convert.ToDouble(value, CultureInfo.InvariantCulture);

    private static DateTime ParseExcelDate(object value)
    {
        if (value is double oa)
        {
            return DateTime.FromOADate(oa);
        }

        return Convert.ToDateTime(value, CultureInfo.CurrentCulture);
    }

    private static CheckInStatus ParseStatus(string value) => value switch
    {
        "进行中" => CheckInStatus.InProgress,
        "已完成" => CheckInStatus.Completed,
        "调整" => CheckInStatus.Adjusted,
        _ => CheckInStatus.NotStarted
    };

    private static string StatusText(CheckInStatus status) => status switch
    {
        CheckInStatus.InProgress => "进行中",
        CheckInStatus.Completed => "已完成",
        CheckInStatus.Adjusted => "调整",
        _ => "未开始"
    };

    private static void CloseExcel(dynamic? excel, dynamic? workbook, dynamic? sheet, bool save)
    {
        try { workbook?.Close(save); } catch { }
        try { excel?.Quit(); } catch { }
        ReleaseCom(sheet);
        ReleaseCom(workbook);
        ReleaseCom(excel);
    }

    private static void ReleaseCom(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            try { Marshal.FinalReleaseComObject(value); } catch { }
        }
    }

    private static Task<T> RunStaAsync<T>(Func<T> action)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { completion.SetResult(action()); }
            catch (Exception exception) { completion.SetException(exception); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        return completion.Task;
    }
}
