const storage = require("./storage");

const MOCK_CLOUD_KEY = "study-checkin:mock-cloud:v1";

async function dispatch(operation, state) {
  const cloudState = wx.getStorageSync(MOCK_CLOUD_KEY) || state;
  const nextState = {
    ...cloudState,
    days: state.days,
    settings: state.settings,
    lastSyncedAtUtc: new Date().toISOString()
  };
  wx.setStorageSync(MOCK_CLOUD_KEY, nextState);
  return { ok: true, requestId: operation.requestId };
}

async function pairDevice(code) {
  if (!/^\d{6}$/.test(code)) {
    throw new Error("请输入 6 位配对码。");
  }
  return {
    deviceToken: `mock-device-${code}`,
    deviceName: "Windows 自律台（Mock）"
  };
}

async function reset() {
  wx.removeStorageSync(MOCK_CLOUD_KEY);
  storage.clearState();
}

module.exports = {
  dispatch,
  pairDevice,
  reset
};
