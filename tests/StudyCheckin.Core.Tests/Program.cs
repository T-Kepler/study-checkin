using StudyCheckin.Core;

var tests = new List<(string Name, Func<Task> Run)>
{
    ("timer prevents concurrent tasks", TimerPreventsConcurrentTasks),
    ("timer records elapsed minutes", TimerRecordsElapsedMinutes),
    ("timer requires long-session confirmation", TimerRequiresLongSessionConfirmation),
    ("completion ignores paused tasks", CompletionIgnoresPausedTasks),
    ("check-in requests are idempotent", CheckInRequestsAreIdempotent),
    ("incomplete reminder excludes paused tasks", IncompleteReminderExcludesPausedTasks),
    ("json store round-trips atomically", JsonStoreRoundTrips)
};

var failed = 0;
foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS  {test.Name}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.WriteLine($"FAIL  {test.Name}");
        Console.WriteLine(exception);
    }
}

Console.WriteLine($"{tests.Count - failed}/{tests.Count} tests passed");
return failed == 0 ? 0 : 1;

static DailyRecord MakeRecord() => new()
{
    Date = new DateTime(2026, 9, 15),
    PlannedMinutes = 120,
    Tasks = new List<PlanTask>
    {
        new() { Id = "math", Title = "极限概念摸底", PlannedMinutes = 90 },
        new() { Id = "english", Title = "核心词汇", PlannedMinutes = 30 }
    }
};

static Task TimerPreventsConcurrentTasks()
{
    var record = MakeRecord();
    var service = new TimerService();
    var now = DateTimeOffset.Parse("2026-09-15T11:00:00Z");
    service.Start(record, "math", now);
    AssertThrows<InvalidOperationException>(() => service.Start(record, "english", now));
    return Task.CompletedTask;
}

static Task TimerRecordsElapsedMinutes()
{
    var record = MakeRecord();
    var service = new TimerService();
    var start = DateTimeOffset.Parse("2026-09-15T11:00:00Z");
    service.Start(record, "math", start);
    var result = service.Stop(record, "math", start.AddMinutes(92));
    AssertEqual(92, result.AddedMinutes);
    AssertEqual(92, record.Tasks[0].ActualMinutes);
    AssertTrue(record.ActiveTask is null, "timer should be stopped");
    return Task.CompletedTask;
}

static Task TimerRequiresLongSessionConfirmation()
{
    var record = MakeRecord();
    var service = new TimerService();
    var start = DateTimeOffset.Parse("2026-09-15T11:00:00Z");
    service.Start(record, "math", start);
    AssertThrows<LongSessionConfirmationRequiredException>(
        () => service.Stop(record, "math", start.AddHours(5)));
    var result = service.Stop(record, "math", start.AddHours(5), true);
    AssertTrue(result.WasLongSession, "five-hour session should be marked long");
    AssertEqual(300, result.AddedMinutes);
    return Task.CompletedTask;
}

static Task CompletionIgnoresPausedTasks()
{
    var record = MakeRecord();
    record.PlannedMinutes = 90;
    record.Tasks[0].ActualMinutes = 90;
    record.Tasks[0].IsCompleted = true;
    record.Tasks[1].IsPaused = true;
    var snapshot = CompletionService.Calculate(record);
    AssertEqual(1d, snapshot.Rate);
    AssertEqual(CheckInStatus.Completed, snapshot.Status);
    AssertEqual(1, snapshot.ActiveTasks);
    return Task.CompletedTask;
}

static Task CheckInRequestsAreIdempotent()
{
    var record = MakeRecord();
    var service = new CheckInService();
    var now = DateTimeOffset.Parse("2026-09-15T14:00:00Z");
    AssertTrue(service.Apply(record, "request-1", "math", 60, false, "第一轮", now), "first request should apply");
    AssertTrue(!service.Apply(record, "request-1", "math", 120, true, "duplicate", now), "duplicate request should be ignored");
    AssertEqual(60, record.Tasks[0].ActualMinutes);
    AssertEqual("第一轮", record.Recap);
    return Task.CompletedTask;
}

static Task IncompleteReminderExcludesPausedTasks()
{
    var record = MakeRecord();
    record.Tasks[0].ActualMinutes = 30;
    record.Tasks[1].IsPaused = true;
    var decision = new ReminderDecisionService().Evaluate(ReminderKind.IncompleteNudge, record);
    AssertTrue(decision.ShouldSend, "30/120 should trigger a reminder");
    AssertTrue(decision.Content.Contains("极限概念摸底"), "active task should be listed");
    AssertTrue(!decision.Content.Contains("核心词汇"), "paused task should not be listed");
    return Task.CompletedTask;
}

static async Task JsonStoreRoundTrips()
{
    var directory = Path.Combine(Path.GetTempPath(), "study-checkin-tests", Guid.NewGuid().ToString("N"));
    var path = Path.Combine(directory, "state.json");
    try
    {
        var store = new JsonFileStore<StudyState>(path);
        var state = new StudyState { Days = new List<DailyRecord> { MakeRecord() } };
        await store.SaveAsync(state);
        var loaded = await store.LoadAsync();
        AssertEqual(1, loaded.Days.Count);
        AssertEqual("极限概念摸底", loaded.Days[0].Tasks[0].Title);
        AssertTrue(!File.Exists(path + ".tmp"), "temporary file should be replaced");
    }
    finally
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}

static void AssertEqual<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected {expected}, got {actual}.");
    }
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertThrows<T>(Action action) where T : Exception
{
    try
    {
        action();
    }
    catch (T)
    {
        return;
    }

    throw new InvalidOperationException($"Expected exception {typeof(T).Name}.");
}
