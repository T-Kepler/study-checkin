const assert = require("node:assert/strict");

const memory = new Map();
global.wx = {
  getStorageSync(key) {
    return memory.get(key);
  },
  setStorageSync(key, value) {
    memory.set(key, value);
  },
  removeStorageSync(key) {
    memory.delete(key);
  }
};

const studyService = require("../../src/miniprogram/services/studyService");

async function main() {
  await studyService.resetMockData();
  let state = await studyService.getState();
  const firstTask = state.days[0].tasks[0];

  await studyService.setTaskMinutes(firstTask.id, 30);
  state = await studyService.getState();
  assert.equal(state.days[0].tasks[0].actualMinutes, 30);
  assert.equal(state.pendingOperations.length, 0);
  console.log("PASS  Mock adapter flushes local operations");

  await studyService.saveSettings({ mode: "cloud", cloudEnvId: "cloud-test" });
  await studyService.setTaskMinutes(firstTask.id, 45);
  state = await studyService.getState();
  assert.equal(state.days[0].tasks[0].actualMinutes, 45);
  assert.ok(state.pendingOperations.length >= 1);
  assert.match(state.pendingOperations[0].lastError, /CloudBase/);
  console.log("PASS  Cloud failure keeps operations in the offline queue");

  await studyService.saveSettings({ mode: "mock" });
  state = await studyService.getState();
  assert.equal(state.pendingOperations.length, 0);
  console.log("PASS  Retry clears queued operations after connectivity returns");

  const paired = await studyService.pairDevice("260915");
  assert.equal(paired.deviceName, "Windows 自律台（Mock）");
  state = await studyService.getState();
  assert.equal(state.settings.deviceToken, "mock-device-260915");
  console.log("PASS  Six-digit Mock pairing persists a device token");

  console.log("4/4 mini program service tests passed");
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
