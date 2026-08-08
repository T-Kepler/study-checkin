const adapter = require("./adapter");
const domain = require("./domain");
const { createMockState } = require("./mockData");
const storage = require("./storage");

let cachedState = null;
let operationChain = Promise.resolve();

function createRequestId() {
  return `${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

function runExclusive(work) {
  const next = operationChain.then(work, work);
  operationChain = next.catch(() => undefined);
  return next;
}

function normalize(state) {
  const defaults = createMockState();
  const normalized = state || defaults;
  normalized.days = Array.isArray(normalized.days) ? normalized.days : [];
  normalized.pendingOperations = Array.isArray(normalized.pendingOperations)
    ? normalized.pendingOperations
    : [];
  normalized.settings = {
    ...defaults.settings,
    ...(normalized.settings || {}),
    reminders: {
      ...defaults.settings.reminders,
      ...((normalized.settings && normalized.settings.reminders) || {})
    }
  };
  if (!normalized.days.some((day) => day.date === normalized.selectedDate)) {
    normalized.selectedDate = normalized.days[0] ? normalized.days[0].date : "";
  }
  return normalized;
}

async function initialize() {
  if (!cachedState) {
    cachedState = normalize(storage.loadState());
    storage.saveState(cachedState);
  }
  return domain.clone(cachedState);
}

async function getState() {
  await initialize();
  return domain.clone(cachedState);
}

function save(state) {
  cachedState = normalize(state);
  storage.saveState(cachedState);
}

function queueOperation(state, action, payload) {
  state.pendingOperations.push({
    requestId: createRequestId(),
    action,
    payload,
    attempts: 0,
    createdAtUtc: new Date().toISOString(),
    lastError: ""
  });
}

async function mutate(action, payload, transform) {
  return runExclusive(async () => {
    const current = await getState();
    const result = transform(current);
    if (result.requiresConfirmation) {
      return result;
    }
    queueOperation(result.state, action, payload);
    save(result.state);
    await retryPendingInternal();
    return { ...result, state: domain.clone(cachedState) };
  });
}

async function retryPendingInternal() {
  if (!cachedState) {
    return { pendingCount: 0 };
  }
  for (const operation of [...cachedState.pendingOperations]) {
    try {
      await adapter.select(cachedState).dispatch(operation, cachedState);
      cachedState.pendingOperations = cachedState.pendingOperations.filter(
        (item) => item.requestId !== operation.requestId
      );
      cachedState.lastSyncedAtUtc = new Date().toISOString();
    } catch (error) {
      const pending = cachedState.pendingOperations.find(
        (item) => item.requestId === operation.requestId
      );
      if (pending) {
        pending.attempts += 1;
        pending.lastError = error.message || "同步失败";
      }
      break;
    }
  }
  save(cachedState);
  return { pendingCount: cachedState.pendingOperations.length };
}

async function retryPending() {
  return runExclusive(async () => {
    await initialize();
    return retryPendingInternal();
  });
}

function startTask(taskId, nowMs) {
  return mutate("startTask", { taskId }, (state) => domain.startTask(state, taskId, nowMs));
}

function stopTask(taskId, nowMs, confirmLongSession = false) {
  return mutate("stopTask", { taskId, confirmLongSession }, (state) =>
    domain.stopTask(state, taskId, nowMs, confirmLongSession)
  );
}

function setTaskMinutes(taskId, minutes) {
  return mutate("setTaskMinutes", { taskId, minutes }, (state) =>
    domain.setTaskMinutes(state, taskId, minutes)
  );
}

function toggleTask(taskId) {
  return mutate("toggleTask", { taskId }, (state) => domain.toggleTask(state, taskId));
}

function setRecap(date, recap) {
  return mutate("setRecap", { date, recap }, (state) => domain.setRecap(state, date, recap));
}

function addTemporaryTask(date, title) {
  return mutate("addTemporaryTask", { date, title }, (state) =>
    domain.addTemporaryTask(state, date, title)
  );
}

async function selectDate(date) {
  return runExclusive(async () => {
    const state = await getState();
    if (!state.days.some((day) => day.date === date)) {
      throw new Error("该日期不在当前规划中。");
    }
    state.selectedDate = date;
    save(state);
    return domain.clone(state);
  });
}

async function saveSettings(settings) {
  return runExclusive(async () => {
    const state = await getState();
    state.settings = { ...state.settings, ...settings };
    queueOperation(state, "saveSettings", { settings: state.settings });
    save(state);
    await retryPendingInternal();
    return domain.clone(cachedState);
  });
}

async function pairDevice(code) {
  return runExclusive(async () => {
    const state = await getState();
    const result = await adapter.select(state).pairDevice(String(code || "").trim(), state);
    state.settings.deviceToken = result.deviceToken;
    state.settings.pairedDeviceName = result.deviceName;
    save(state);
    return result;
  });
}

async function resetMockData() {
  return runExclusive(async () => {
    await adapter.select(createMockState()).reset();
    cachedState = createMockState();
    save(cachedState);
    return domain.clone(cachedState);
  });
}

module.exports = {
  addTemporaryTask,
  getState,
  initialize,
  pairDevice,
  resetMockData,
  retryPending,
  saveSettings,
  selectDate,
  setRecap,
  setTaskMinutes,
  startTask,
  stopTask,
  toggleTask
};
