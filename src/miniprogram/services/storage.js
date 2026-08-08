const STATE_KEY = "study-checkin:state:v1";

function loadState() {
  return wx.getStorageSync(STATE_KEY) || null;
}

function saveState(state) {
  wx.setStorageSync(STATE_KEY, state);
}

function clearState() {
  wx.removeStorageSync(STATE_KEY);
}

module.exports = {
  STATE_KEY,
  clearState,
  loadState,
  saveState
};
