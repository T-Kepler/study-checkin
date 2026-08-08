const LONG_SESSION_MINUTES = 240;

function clone(value) {
  return JSON.parse(JSON.stringify(value));
}

function findTask(state, taskId) {
  for (const day of state.days) {
    const task = day.tasks.find((item) => item.id === taskId);
    if (task) {
      return { day, task };
    }
  }
  throw new Error(`找不到任务：${taskId}`);
}

function findActiveTask(state) {
  for (const day of state.days) {
    const task = day.tasks.find((item) => Boolean(item.startedAtUtc));
    if (task) {
      return { day, task };
    }
  }
  return null;
}

function calculateCompletion(day) {
  const activeTasks = day.tasks.filter((task) => !task.isPaused);
  const plannedMinutes = day.plannedMinutes > 0
    ? day.plannedMinutes
    : activeTasks.reduce((sum, task) => sum + Math.max(0, task.plannedMinutes || 0), 0);
  const actualMinutes = activeTasks.reduce((sum, task) => sum + Math.max(0, task.actualMinutes || 0), 0);
  const completedTasks = activeTasks.filter((task) => task.isCompleted).length;
  let rate = plannedMinutes === 0 ? 0 : Math.min(1, actualMinutes / plannedMinutes);
  let status = "notStarted";

  if (activeTasks.length > 0 && completedTasks === activeTasks.length) {
    status = "completed";
    rate = Math.max(rate, 1);
  } else if (actualMinutes > 0 || completedTasks > 0 || activeTasks.some((task) => task.startedAtUtc)) {
    status = "inProgress";
  }

  return {
    plannedMinutes,
    actualMinutes,
    completedTasks,
    activeTasks: activeTasks.length,
    rate,
    status
  };
}

function refreshDay(day, nowIso) {
  const snapshot = calculateCompletion(day);
  day.status = snapshot.status;
  day.updatedAtUtc = nowIso;
  return snapshot;
}

function startTask(currentState, taskId, nowMs = Date.now()) {
  const state = clone(currentState);
  const { day, task } = findTask(state, taskId);
  if (task.isPaused) {
    throw new Error("暂停任务不能开始计时。");
  }
  if (task.isCompleted) {
    throw new Error("已完成任务不能开始计时。");
  }
  const active = findActiveTask(state);
  if (active && active.task.id !== taskId) {
    throw new Error("同一时间只能运行一个学习任务。");
  }
  const nowIso = new Date(nowMs).toISOString();
  task.startedAtUtc = task.startedAtUtc || nowIso;
  task.updatedAtUtc = nowIso;
  refreshDay(day, nowIso);
  return { state };
}

function stopTask(currentState, taskId, nowMs = Date.now(), confirmLongSession = false) {
  const state = clone(currentState);
  const { day, task } = findTask(state, taskId);
  if (!task.startedAtUtc) {
    return { state, addedMinutes: 0, wasLongSession: false };
  }
  const startedMs = Date.parse(task.startedAtUtc);
  if (!Number.isFinite(startedMs) || nowMs < startedMs) {
    throw new Error("结束时间不能早于开始时间。");
  }
  const minutes = Math.max(1, Math.round((nowMs - startedMs) / 60000));
  const wasLongSession = minutes > LONG_SESSION_MINUTES;
  if (wasLongSession && !confirmLongSession) {
    return { state, requiresConfirmation: true, minutes };
  }
  const nowIso = new Date(nowMs).toISOString();
  task.actualMinutes = Math.max(0, task.actualMinutes || 0) + minutes;
  task.startedAtUtc = null;
  task.updatedAtUtc = nowIso;
  refreshDay(day, nowIso);
  return { state, addedMinutes: minutes, wasLongSession };
}

function setTaskMinutes(currentState, taskId, minutes, nowMs = Date.now()) {
  const state = clone(currentState);
  const { day, task } = findTask(state, taskId);
  const parsed = Number(minutes);
  if (!Number.isFinite(parsed) || parsed < 0) {
    throw new Error("学习分钟必须是非负数。");
  }
  const nowIso = new Date(nowMs).toISOString();
  task.actualMinutes = Math.round(parsed);
  task.updatedAtUtc = nowIso;
  refreshDay(day, nowIso);
  return { state };
}

function toggleTask(currentState, taskId, nowMs = Date.now()) {
  const state = clone(currentState);
  const { day, task } = findTask(state, taskId);
  if (task.isPaused) {
    throw new Error("暂停任务不参与打卡。");
  }
  if (task.startedAtUtc) {
    throw new Error("请先停止计时再完成任务。");
  }
  const nowIso = new Date(nowMs).toISOString();
  task.isCompleted = !task.isCompleted;
  task.updatedAtUtc = nowIso;
  refreshDay(day, nowIso);
  return { state };
}

function setRecap(currentState, date, recap, nowMs = Date.now()) {
  const state = clone(currentState);
  const day = state.days.find((item) => item.date === date);
  if (!day) {
    throw new Error(`找不到日期：${date}`);
  }
  const nowIso = new Date(nowMs).toISOString();
  day.recap = String(recap || "").trim();
  refreshDay(day, nowIso);
  return { state };
}

function addTemporaryTask(currentState, date, title, plannedMinutes = 30, nowMs = Date.now()) {
  const state = clone(currentState);
  const day = state.days.find((item) => item.date === date);
  const cleanTitle = String(title || "").trim();
  if (!day || !cleanTitle) {
    throw new Error("临时任务名称不能为空。");
  }
  const nowIso = new Date(nowMs).toISOString();
  day.tasks.push({
    id: `temporary-${nowMs}`,
    date,
    category: "temporary",
    title: cleanTitle,
    plannedMinutes: Math.max(0, Math.round(Number(plannedMinutes) || 0)),
    actualMinutes: 0,
    isCompleted: false,
    isPaused: false,
    isTemporary: true,
    startedAtUtc: null,
    updatedAtUtc: nowIso
  });
  refreshDay(day, nowIso);
  return { state };
}

function calculateStatistics(days) {
  const snapshots = days.map((day) => ({ day, snapshot: calculateCompletion(day) }));
  const plannedMinutes = snapshots.reduce((sum, item) => sum + item.snapshot.plannedMinutes, 0);
  const actualMinutes = snapshots.reduce((sum, item) => sum + item.snapshot.actualMinutes, 0);
  const completedTasks = snapshots.reduce((sum, item) => sum + item.snapshot.completedTasks, 0);
  const activeTasks = snapshots.reduce((sum, item) => sum + item.snapshot.activeTasks, 0);
  return {
    plannedMinutes,
    actualMinutes,
    completedTasks,
    activeTasks,
    taskRate: activeTasks === 0 ? 0 : completedTasks / activeTasks,
    hourRate: plannedMinutes === 0 ? 0 : Math.min(1, actualMinutes / plannedMinutes)
  };
}

function formatElapsed(startedAtUtc, nowMs = Date.now()) {
  if (!startedAtUtc) {
    return "";
  }
  const seconds = Math.max(0, Math.floor((nowMs - Date.parse(startedAtUtc)) / 1000));
  const twoDigits = (value) => (value < 10 ? `0${value}` : String(value));
  const hours = twoDigits(Math.floor(seconds / 3600));
  const minutes = twoDigits(Math.floor((seconds % 3600) / 60));
  const remainder = twoDigits(seconds % 60);
  return `${hours}:${minutes}:${remainder}`;
}

module.exports = {
  LONG_SESSION_MINUTES,
  addTemporaryTask,
  calculateCompletion,
  calculateStatistics,
  clone,
  findActiveTask,
  formatElapsed,
  setRecap,
  setTaskMinutes,
  startTask,
  stopTask,
  toggleTask
};
