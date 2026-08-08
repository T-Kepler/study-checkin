using System.Text.Json.Serialization;

namespace StudyCheckin.Core;

public enum StudyTaskCategory
{
    Mathematics,
    English,
    SignalSystems843,
    Competition,
    Coursework,
    Temporary
}

public enum CheckInStatus
{
    NotStarted,
    InProgress,
    Completed,
    Adjusted
}

public enum ReminderKind
{
    MorningPlan,
    StartNudge,
    IncompleteNudge,
    WeeklyReview
}

public sealed class PlanTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Date { get; set; }
    public StudyTaskCategory Category { get; set; }
    public string Title { get; set; } = string.Empty;
    public int PlannedMinutes { get; set; }
    public int ActualMinutes { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsPaused { get; set; }
    public bool IsTemporary { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class DailyRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Date { get; set; }
    public string Phase { get; set; } = string.Empty;
    public string Courses { get; set; } = string.Empty;
    public int PlannedMinutes { get; set; }
    public List<PlanTask> Tasks { get; set; } = new();
    public string Recap { get; set; } = string.Empty;
    public CheckInStatus Status { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public HashSet<string> AppliedRequestIds { get; set; } = new(StringComparer.Ordinal);

    [JsonIgnore]
    public PlanTask? ActiveTask => Tasks.FirstOrDefault(task => task.StartedAtUtc.HasValue);
}

public sealed class ReminderSettings
{
    public TimeSpan MorningPlanTime { get; set; } = new(7, 30, 0);
    public TimeSpan StartNudgeTime { get; set; } = new(19, 0, 0);
    public TimeSpan IncompleteNudgeTime { get; set; } = new(22, 30, 0);
    public TimeSpan WeeklyReviewTime { get; set; } = new(21, 30, 0);
    public bool Enabled { get; set; } = true;
    public string WxPusherUid { get; set; } = string.Empty;
}

public sealed class AppSettings
{
    public string ExcelPath { get; set; } = string.Empty;
    public string CloudApiBaseUrl { get; set; } = "mock://local";
    public string DeviceToken { get; set; } = string.Empty;
    public bool StartWithWindows { get; set; } = true;
    public int SyncIntervalSeconds { get; set; } = 60;
    public ReminderSettings Reminders { get; set; } = new();
}

public sealed class StudyState
{
    public int SchemaVersion { get; set; } = 1;
    public List<DailyRecord> Days { get; set; } = new();
    public DateTimeOffset LastSyncedAtUtc { get; set; }
    public List<PendingExcelWrite> PendingExcelWrites { get; set; } = new();
}

public sealed class PendingExcelWrite
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Date { get; set; }
    public int Attempts { get; set; }
    public string LastError { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed record CompletionSnapshot(
    int PlannedMinutes,
    int ActualMinutes,
    double Rate,
    int CompletedTasks,
    int ActiveTasks,
    CheckInStatus Status);

public sealed record ReminderDecision(bool ShouldSend, string Title, string Content);

public sealed record TimerStopResult(int AddedMinutes, bool WasLongSession);

