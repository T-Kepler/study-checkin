namespace StudyCheckin.Core;

public sealed class TimerService
{
    public const int LongSessionMinutes = 240;

    public void Start(DailyRecord record, string taskId, DateTimeOffset nowUtc)
    {
        var task = FindTask(record, taskId);
        if (task.IsPaused)
        {
            throw new InvalidOperationException("暂停任务不能开始计时。");
        }

        var active = record.ActiveTask;
        if (active is not null && active.Id != taskId)
        {
            throw new InvalidOperationException("同一时间只能运行一个学习任务。");
        }

        task.StartedAtUtc ??= nowUtc;
        task.UpdatedAtUtc = nowUtc;
        record.UpdatedAtUtc = nowUtc;
    }

    public TimerStopResult Stop(
        DailyRecord record,
        string taskId,
        DateTimeOffset nowUtc,
        bool confirmLongSession = false)
    {
        var task = FindTask(record, taskId);
        if (!task.StartedAtUtc.HasValue)
        {
            return new TimerStopResult(0, false);
        }

        var elapsed = nowUtc - task.StartedAtUtc.Value;
        if (elapsed < TimeSpan.Zero)
        {
            throw new InvalidOperationException("结束时间不能早于开始时间。");
        }

        var minutes = Math.Max(1, (int)Math.Round(elapsed.TotalMinutes));
        var isLong = minutes > LongSessionMinutes;
        if (isLong && !confirmLongSession)
        {
            throw new LongSessionConfirmationRequiredException(minutes);
        }

        task.ActualMinutes += minutes;
        task.StartedAtUtc = null;
        task.UpdatedAtUtc = nowUtc;
        record.UpdatedAtUtc = nowUtc;
        return new TimerStopResult(minutes, isLong);
    }

    private static PlanTask FindTask(DailyRecord record, string taskId) =>
        record.Tasks.FirstOrDefault(task => task.Id == taskId)
        ?? throw new KeyNotFoundException($"找不到任务：{taskId}");
}

public sealed class LongSessionConfirmationRequiredException : Exception
{
    public LongSessionConfirmationRequiredException(int minutes)
        : base($"本次计时为 {minutes} 分钟，超过四小时，需要确认。")
    {
        Minutes = minutes;
    }

    public int Minutes { get; }
}

public sealed class CheckInService
{
    public bool Apply(
        DailyRecord record,
        string requestId,
        string taskId,
        int actualMinutes,
        bool isCompleted,
        string? recap,
        DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            throw new ArgumentException("请求 ID 不能为空。", nameof(requestId));
        }

        if (!record.AppliedRequestIds.Add(requestId))
        {
            return false;
        }

        var task = record.Tasks.FirstOrDefault(item => item.Id == taskId)
            ?? throw new KeyNotFoundException($"找不到任务：{taskId}");

        task.ActualMinutes = Math.Max(0, actualMinutes);
        task.IsCompleted = isCompleted;
        task.StartedAtUtc = null;
        task.UpdatedAtUtc = nowUtc;
        if (recap is not null)
        {
            record.Recap = recap.Trim();
        }

        record.UpdatedAtUtc = nowUtc;
        record.Status = CompletionService.Calculate(record).Status;
        return true;
    }
}

public static class CompletionService
{
    public static CompletionSnapshot Calculate(DailyRecord record)
    {
        var active = record.Tasks.Where(task => !task.IsPaused).ToList();
        var planned = record.PlannedMinutes > 0
            ? record.PlannedMinutes
            : active.Sum(task => task.PlannedMinutes);
        var actual = active.Sum(task => Math.Max(0, task.ActualMinutes));
        var rate = planned == 0 ? 0 : Math.Clamp((double)actual / planned, 0, 1);
        var completed = active.Count(task => task.IsCompleted);

        var status = CheckInStatus.NotStarted;
        if (active.Count > 0 && completed == active.Count)
        {
            status = CheckInStatus.Completed;
            rate = Math.Max(rate, 1);
        }
        else if (actual > 0 || completed > 0 || active.Any(task => task.StartedAtUtc.HasValue))
        {
            status = CheckInStatus.InProgress;
        }

        return new CompletionSnapshot(planned, actual, rate, completed, active.Count, status);
    }
}

public sealed class ReminderDecisionService
{
    public ReminderDecision Evaluate(ReminderKind kind, DailyRecord record)
    {
        var snapshot = CompletionService.Calculate(record);
        var available = record.Tasks.Where(task => !task.IsPaused).ToList();

        return kind switch
        {
            ReminderKind.MorningPlan => new ReminderDecision(
                available.Count > 0,
                "今日学习计划",
                BuildPlanMessage(record, available, snapshot)),
            ReminderKind.StartNudge => new ReminderDecision(
                snapshot.Status == CheckInStatus.NotStarted && available.Count > 0,
                "今晚还没有开始",
                $"今天有 {available.Count} 项任务，先完成最小的一项。"),
            ReminderKind.IncompleteNudge => BuildIncompleteDecision(available, snapshot),
            ReminderKind.WeeklyReview => new ReminderDecision(
                true,
                "本周复盘",
                "请检查本周实际小时、完成率和唯一瓶颈，并安排下周第一任务。"),
            _ => new ReminderDecision(false, string.Empty, string.Empty)
        };
    }

    private static ReminderDecision BuildIncompleteDecision(
        IReadOnlyCollection<PlanTask> tasks,
        CompletionSnapshot snapshot)
    {
        var incomplete = tasks.Where(task => !task.IsCompleted).Select(task => task.Title).ToList();
        if (snapshot.Rate >= 0.8 || incomplete.Count == 0)
        {
            return new ReminderDecision(false, string.Empty, string.Empty);
        }

        var content = $"今日完成率 {snapshot.Rate:P0}。未完成：{string.Join("；", incomplete.Take(3))}";
        return new ReminderDecision(true, "今日任务尚未收口", content);
    }

    private static string BuildPlanMessage(
        DailyRecord record,
        IReadOnlyCollection<PlanTask> tasks,
        CompletionSnapshot snapshot)
    {
        var course = string.IsNullOrWhiteSpace(record.Courses) ? "今日无固定课程" : record.Courses;
        return $"{course}\n学习任务 {tasks.Count} 项，计划 {snapshot.PlannedMinutes / 60d:0.#} 小时。";
    }
}

