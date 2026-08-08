const studyService = require("./services/studyService");

App({
  globalData: {
    initialized: false
  },

  async onLaunch() {
    await studyService.initialize();
    this.globalData.initialized = true;
  },

  async onShow() {
    if (this.globalData.initialized) {
      await studyService.retryPending();
    }
  }
});
