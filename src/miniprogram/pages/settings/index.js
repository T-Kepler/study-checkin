const studyService = require("../../services/studyService");

function formatSyncTime(value) {
  const date = new Date(value);
  const twoDigits = (number) => (number < 10 ? `0${number}` : String(number));
  return `${date.getFullYear()}-${twoDigits(date.getMonth() + 1)}-${twoDigits(date.getDate())} ${twoDigits(date.getHours())}:${twoDigits(date.getMinutes())}`;
}

Page({
  data: {
    mode: "mock",
    cloudEnvId: "",
    pairCode: "",
    pairedDeviceName: "",
    wxPusherUid: "",
    remindersEnabled: true,
    morningPlanTime: "07:30",
    startNudgeTime: "19:00",
    incompleteNudgeTime: "22:30",
    weeklyReviewTime: "21:30",
    pendingCount: 0,
    lastSyncedLabel: "尚未同步"
  },

  async onShow() {
    await this.loadSettings();
  },

  async loadSettings() {
    const state = await studyService.getState();
    const settings = state.settings;
    this.setData({
      mode: settings.mode,
      cloudEnvId: settings.cloudEnvId || "",
      pairedDeviceName: settings.pairedDeviceName || "",
      wxPusherUid: settings.wxPusherUid || "",
      remindersEnabled: settings.reminders.enabled,
      morningPlanTime: settings.reminders.morningPlanTime,
      startNudgeTime: settings.reminders.startNudgeTime,
      incompleteNudgeTime: settings.reminders.incompleteNudgeTime,
      weeklyReviewTime: settings.reminders.weeklyReviewTime,
      pendingCount: state.pendingOperations.length,
      lastSyncedLabel: state.lastSyncedAtUtc
        ? formatSyncTime(state.lastSyncedAtUtc)
        : "尚未同步"
    });
  },

  onCloudEnvInput(event) {
    this.setData({ cloudEnvId: event.detail.value.trim() });
  },

  async onModeChange(event) {
    const useCloud = event.detail.value;
    if (useCloud && !this.data.cloudEnvId) {
      this.setData({ mode: "mock" });
      wx.showToast({ title: "请先填写环境 ID", icon: "none" });
      return;
    }
    const mode = useCloud ? "cloud" : "mock";
    try {
      await studyService.saveSettings({ mode, cloudEnvId: this.data.cloudEnvId });
      this.setData({ mode });
      await this.loadSettings();
    } catch (error) {
      wx.showToast({ title: error.message || "切换失败", icon: "none" });
    }
  },

  async onSaveCloudEnv() {
    await studyService.saveSettings({ cloudEnvId: this.data.cloudEnvId });
    wx.showToast({ title: "环境 ID 已保存", icon: "success" });
  },

  onPairCodeInput(event) {
    this.setData({ pairCode: event.detail.value.replace(/\D/g, "").slice(0, 6) });
  },

  async onPairDevice() {
    try {
      const result = await studyService.pairDevice(this.data.pairCode);
      this.setData({ pairedDeviceName: result.deviceName, pairCode: "" });
      wx.showToast({ title: "配对成功", icon: "success" });
    } catch (error) {
      wx.showToast({ title: error.message || "配对失败", icon: "none" });
    }
  },

  onWxPusherInput(event) {
    this.setData({ wxPusherUid: event.detail.value.trim() });
  },

  onReminderToggle(event) {
    this.setData({ remindersEnabled: event.detail.value });
  },

  onTimeChange(event) {
    const field = event.currentTarget.dataset.field;
    this.setData({ [field]: event.detail.value });
  },

  async onSaveReminderSettings() {
    try {
      const state = await studyService.getState();
      await studyService.saveSettings({
        wxPusherUid: this.data.wxPusherUid,
        reminders: {
          ...state.settings.reminders,
          enabled: this.data.remindersEnabled,
          morningPlanTime: this.data.morningPlanTime,
          startNudgeTime: this.data.startNudgeTime,
          incompleteNudgeTime: this.data.incompleteNudgeTime,
          weeklyReviewTime: this.data.weeklyReviewTime
        }
      });
      await this.loadSettings();
      wx.showToast({ title: "设置已保存", icon: "success" });
    } catch (error) {
      wx.showToast({ title: error.message || "保存失败", icon: "none" });
    }
  },

  async onRetry() {
    const result = await studyService.retryPending();
    await this.loadSettings();
    wx.showToast({
      title: result.pendingCount === 0 ? "同步完成" : "仍有待同步项",
      icon: result.pendingCount === 0 ? "success" : "none"
    });
  },

  async onResetMock() {
    const answer = await wx.showModal({
      title: "重置 Mock 数据",
      content: "这会清除小程序内的打卡、计时和设置，恢复首次打开状态。",
      confirmText: "重置",
      confirmColor: "#B34A3C"
    });
    if (!answer.confirm) {
      return;
    }
    await studyService.resetMockData();
    await this.loadSettings();
    wx.showToast({ title: "已重置", icon: "success" });
  }
});
