const assert = require("node:assert/strict");
const domain = require("../../src/miniprogram/services/domain");
const { createMockState } = require("../../src/miniprogram/services/mockData");

function test(name, action) {
  try {
    action();
    console.log(`PASS  ${name}`);
  } catch (error) {
    console.error(`FAIL  ${name}`);
    throw error;
  }
}

test("mock plan contains exact first study week", () => {
  const state = createMockState();
  assert.equal(state.days.length, 7);
  assert.equal(state.days[0].date, "2026-09-15");
  assert.equal(state.days[0].plannedMinutes, 150);
  assert.match(state.days[0].tasks[1].title, /读规则、定题、分工/);
});

test("timer prevents concurrent tasks", () => {
  const state = createMockState();
  const first = state.days[0].tasks[0];
  const second = state.days[0].tasks[1];
  const started = domain.startTask(state, first.id, Date.parse("2026-09-15T10:00:00Z")).state;
  assert.throws(
    () => domain.startTask(started, second.id, Date.parse("2026-09-15T10:01:00Z")),
    /同一时间只能运行一个学习任务/
  );
});

test("timer records elapsed minutes", () => {
  const state = createMockState();
  const task = state.days[0].tasks[0];
  const start = Date.parse("2026-09-15T10:00:00Z");
  const started = domain.startTask(state, task.id, start).state;
  const stopped = domain.stopTask(started, task.id, start + 92 * 60000);
  assert.equal(stopped.addedMinutes, 92);
  assert.equal(stopped.state.days[0].tasks[0].actualMinutes, 92);
  assert.equal(stopped.state.days[0].tasks[0].startedAtUtc, null);
});

test("timer requires four-hour confirmation", () => {
  const state = createMockState();
  const task = state.days[0].tasks[0];
  const start = Date.parse("2026-09-15T10:00:00Z");
  const started = domain.startTask(state, task.id, start).state;
  const pending = domain.stopTask(started, task.id, start + 5 * 3600000);
  assert.equal(pending.requiresConfirmation, true);
  assert.equal(pending.minutes, 300);
  const confirmed = domain.stopTask(started, task.id, start + 5 * 3600000, true);
  assert.equal(confirmed.addedMinutes, 300);
});

test("completion ignores paused tasks", () => {
  const state = createMockState();
  const day = state.days[0];
  day.tasks[0].actualMinutes = 150;
  day.tasks[0].isCompleted = true;
  day.tasks[1].isPaused = true;
  const snapshot = domain.calculateCompletion(day);
  assert.equal(snapshot.activeTasks, 1);
  assert.equal(snapshot.status, "completed");
  assert.equal(snapshot.rate, 1);
});

test("manual minutes and recap update a cloned state", () => {
  const state = createMockState();
  const task = state.days[0].tasks[0];
  const withMinutes = domain.setTaskMinutes(state, task.id, 45).state;
  const withRecap = domain.setRecap(withMinutes, state.days[0].date, "完成摸底").state;
  assert.equal(state.days[0].tasks[0].actualMinutes, 0);
  assert.equal(withRecap.days[0].tasks[0].actualMinutes, 45);
  assert.equal(withRecap.days[0].recap, "完成摸底");
});

test("temporary task participates in statistics", () => {
  const state = createMockState();
  const result = domain.addTemporaryTask(state, state.days[0].date, "补做错题").state;
  const totals = domain.calculateStatistics(result.days);
  assert.equal(result.days[0].tasks.at(-1).isTemporary, true);
  assert.equal(totals.activeTasks, state.days.reduce((sum, day) => sum + day.tasks.length, 0) + 1);
});

console.log("7/7 mini program domain tests passed");
