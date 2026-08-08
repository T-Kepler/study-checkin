using System.IO;

namespace StudyCheckin.Desktop.Services;

public static class DefaultPathLocator
{
    private const string WorkbookName = "2028考研_竞赛_课程详细规划.xlsx";

    public static string FindPlanWorkbook()
    {
        var candidates = new List<string>();
        AddParents(candidates, Environment.CurrentDirectory);
        AddParents(candidates, AppContext.BaseDirectory);
        candidates.Add(@"D:\.日常\大三上\规划\" + WorkbookName);

        return candidates.FirstOrDefault(File.Exists) ?? string.Empty;
    }

    private static void AddParents(ICollection<string> candidates, string start)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(start));
        for (var depth = 0; directory is not null && depth < 10; depth++, directory = directory.Parent)
        {
            candidates.Add(Path.Combine(directory.FullName, "规划", WorkbookName));
        }
    }
}
