using System.Globalization;
using System.Runtime.InteropServices;
using StudyCheckin.Core;
using StudyCheckin.Desktop.Services;

const string workbookName = "2028考研_竞赛_课程详细规划.xlsx";
var sourcePath = args.FirstOrDefault()
    ?? Path.Combine(@"D:\.日常\大三上\规划", workbookName);

if (!File.Exists(sourcePath))
{
    Console.Error.WriteLine($"FAIL  找不到测试工作簿：{sourcePath}");
    return 1;
}

var sourceInfo = new FileInfo(sourcePath);
var originalLength = sourceInfo.Length;
var originalWriteTime = sourceInfo.LastWriteTimeUtc;
var testDirectory = Path.Combine(Path.GetTempPath(), "study-checkin-excel-tests", Guid.NewGuid().ToString("N"));
var copyPath = Path.Combine(testDirectory, workbookName);

Directory.CreateDirectory(testDirectory);
File.Copy(sourcePath, copyPath);

try
{
    var before = WorkbookSnapshot.Capture(copyPath);
    var service = new ExcelSyncService();
    var records = await service.ImportAsync(copyPath);
    var record = records.FirstOrDefault(item => item.Date.Date == new DateTime(2026, 9, 15))
        ?? throw new InvalidOperationException("测试副本中找不到 2026-09-15。 ");

    var activeTasks = record.Tasks.Where(task => !task.IsPaused).ToList();
    if (activeTasks.Count == 0)
    {
        throw new InvalidOperationException("测试日期没有可执行任务。");
    }

    foreach (var task in activeTasks)
    {
        task.ActualMinutes = 0;
        task.IsCompleted = true;
    }
    activeTasks[0].ActualMinutes = 90;
    record.Recap = "Excel 集成测试：仅允许更新 K、M、N。";

    var writeResult = await service.WriteBackAsync(copyPath, record);
    Assert(writeResult.Succeeded, writeResult.Message);

    var after = WorkbookSnapshot.Capture(copyPath);
    var changedCells = WorkbookSnapshot.FindChanges(before, after);
    var expected = new HashSet<CellAddress>(new[]
    {
        new CellAddress("每日执行_2026", 2, 11),
        new CellAddress("每日执行_2026", 2, 13),
        new CellAddress("每日执行_2026", 2, 14)
    });

    Assert(changedCells.All(expected.Contains),
        $"检测到非 K/M/N 单元格变化：{string.Join(", ", changedCells.Where(cell => !expected.Contains(cell)))}");
    Assert(changedCells.ToHashSet().SetEquals(expected),
        $"K/M/N 未按预期全部更新，实际变化：{string.Join(", ", changedCells)}");
    Assert(after.Get("每日执行_2026", 2, 11) == "1.5", "K2 实际小时应为 1.5。");
    Assert(after.Get("每日执行_2026", 2, 13) == "已完成", "M2 状态应为已完成。");
    Assert(after.Get("每日执行_2026", 2, 14) == record.Recap, "N2 复盘内容不正确。");
    Assert(after.Get("每日执行_2026", 2, 12) == before.Get("每日执行_2026", 2, 12), "L2 完成率公式被改变。");

    sourceInfo.Refresh();
    Assert(sourceInfo.Length == originalLength && sourceInfo.LastWriteTimeUtc == originalWriteTime,
        "原始规划工作簿被意外修改。");

    Console.WriteLine($"PASS  导入 {records.Count} 天计划");
    Console.WriteLine("PASS  仅 K2、M2、N2 发生变化");
    Console.WriteLine("PASS  公式与其他工作表内容保持不变");
    Console.WriteLine("PASS  原始规划工作簿未修改");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine("FAIL  Excel 副本集成测试");
    Console.Error.WriteLine(exception);
    return 1;
}
finally
{
    GC.Collect();
    GC.WaitForPendingFinalizers();
    if (Directory.Exists(testDirectory))
    {
        Directory.Delete(testDirectory, true);
    }
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

internal readonly record struct CellAddress(string Sheet, int Row, int Column)
{
    public override string ToString() => $"{Sheet}!R{Row}C{Column}";
}

internal sealed class WorkbookSnapshot
{
    private readonly Dictionary<CellAddress, string> _cells;

    private WorkbookSnapshot(Dictionary<CellAddress, string> cells)
    {
        _cells = cells;
    }

    public string Get(string sheet, int row, int column) =>
        _cells.GetValueOrDefault(new CellAddress(sheet, row, column), string.Empty);

    public static WorkbookSnapshot Capture(string path)
    {
        dynamic? excel = null;
        dynamic? workbook = null;
        try
        {
            var excelType = Type.GetTypeFromProgID("Excel.Application")
                ?? throw new InvalidOperationException("本机未安装 Microsoft Excel。");
            excel = Activator.CreateInstance(excelType)
                ?? throw new InvalidOperationException("无法启动 Microsoft Excel。");
            excel.Visible = false;
            excel.DisplayAlerts = false;
            workbook = excel.Workbooks.Open(path, ReadOnly: true);

            var cells = new Dictionary<CellAddress, string>();
            foreach (dynamic sheet in workbook.Worksheets)
            {
                dynamic? usedRange = null;
                try
                {
                    usedRange = sheet.UsedRange;
                    var firstRow = (int)usedRange.Row;
                    var firstColumn = (int)usedRange.Column;
                    var rowCount = (int)usedRange.Rows.Count;
                    var columnCount = (int)usedRange.Columns.Count;
                    for (var row = firstRow; row < firstRow + rowCount; row++)
                    {
                        for (var column = firstColumn; column < firstColumn + columnCount; column++)
                        {
                            dynamic? cell = null;
                            try
                            {
                                cell = sheet.Cells[row, column];
                                var formula = Convert.ToString(cell.Formula, CultureInfo.InvariantCulture) ?? string.Empty;
                                cells[new CellAddress((string)sheet.Name, row, column)] = formula;
                            }
                            finally
                            {
                                ReleaseCom(cell);
                            }
                        }
                    }
                }
                finally
                {
                    ReleaseCom(usedRange);
                    ReleaseCom(sheet);
                }
            }
            return new WorkbookSnapshot(cells);
        }
        finally
        {
            try { workbook?.Close(false); } catch { }
            try { excel?.Quit(); } catch { }
            ReleaseCom(workbook);
            ReleaseCom(excel);
        }
    }

    public static IReadOnlyList<CellAddress> FindChanges(WorkbookSnapshot before, WorkbookSnapshot after)
    {
        return before._cells.Keys.Union(after._cells.Keys)
            .Where(address => before._cells.GetValueOrDefault(address) != after._cells.GetValueOrDefault(address))
            .OrderBy(address => address.Sheet, StringComparer.Ordinal)
            .ThenBy(address => address.Row)
            .ThenBy(address => address.Column)
            .ToList();
    }

    private static void ReleaseCom(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            try { Marshal.FinalReleaseComObject(value); } catch { }
        }
    }
}
