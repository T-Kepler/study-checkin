function ensureCloud(envId) {
  if (!envId) {
    throw new Error("请先填写 CloudBase 环境 ID。");
  }
  if (!wx.cloud || typeof wx.cloud.callFunction !== "function") {
    throw new Error("当前基础库不支持 CloudBase。");
  }
  wx.cloud.init({ env: envId, traceUser: true });
}

async function call(envId, action, payload) {
  ensureCloud(envId);
  const response = await wx.cloud.callFunction({
    name: "studyApi",
    data: { action, ...payload }
  });
  const result = response.result || {};
  if (result.ok === false) {
    throw new Error(result.message || "CloudBase 请求失败。");
  }
  return result;
}

async function dispatch(operation, state) {
  return call(state.settings.cloudEnvId, "applyOperation", { operation });
}

async function pairDevice(code, state) {
  return call(state.settings.cloudEnvId, "pairDevice", { code });
}

module.exports = {
  dispatch,
  pairDevice
};
