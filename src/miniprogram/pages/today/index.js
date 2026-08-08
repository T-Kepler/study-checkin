const domain = require("../../services/domain");
const studyService = require("../../services/studyService");

const categoryLabels = {
  mathematics: "数学一",
  english: "英语一",
  signalSystems843: "843",
  competition: "竞赛",
  coursework: "课程",
  temporary: "临时"
};

const statusLabels = {
  notStarted: "未开始",
  inProgress: "进行中",
  completed: "已完成",
  adjusted: "已调整"
};

function dateParts(date) {
  const parsed = new Date(`${date}T00:00:00`);
  return {
    label: `${parsed.getMonth() + 1}月${parsed.getDate()}日`,
    weekday: ["星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六"][parsed.getDay()]
  };
}

Page({
  data: {
    loading: true,
    day: null,
    phaseLabel: "学习计划",
    dateLabel: "",
    weekday: "",
    tasks: [],
    recapDraft: "",
    progressPercent: 0,
    actualHours: "0.0",
    plannedHours: "0.0",
    statusLabel: "未开始",
    pendingCount: 0,
    syncLabel: "本地 Mock",
    hasPrevious: false,
    hasNext: false
  },

  async onLoad() {
    await studyService.initialize();
    await this.loadDay();
  },

  async onShow() {
    await this.loadDay();
    this.startTicker();
  },

  onHide() {
    this.stopTicker();
  },

  onUnload() {
    this.stopTicker();
  },

  async onPullDownRefresh() {
    try {
      await studyService.retryPending();
      await this.loadDay();
    } finally {
      wx.stopPullDownRefresh();
    }
  },

  async loadDay() {
    const state = await studyService.getState();
    const index = Math.max(0, state.days.findIndex((item) => item.date === state.selectedDate));
    const day = state.days[index] || null;
    if (!day) {
      this.setData({ loading: false, day: null, tasks: [] });
      return;
    }
    const snapshot = domain.calculateCompletion(day);
    const parts = dateParts(day.date);
    const tasks = day.tasks.map((task) => this.toTaskView(task));
    this.setData({
      loading: false,
      day,
      phaseLabel: day.phase || "学习计划",
      dateLabel: parts.label,
      weekday: parts.weekday,
      tasks,
      recapDraft: day.recap || "",
      progressPercent: Math.round(snapshot.rate * 100),
      actualHours: (snapshot.actualMinutes / 60).toFixed(1),
      plannedHours: (snapshot.plannedMinutes / 60).toFixed(1),
      statusLabel: statusLabels[snapshot.status] || "未开始",
      pendingCount: state.pendingOperations.length,
      syncLabel: state.settings.mode === "cloud" ? "CloudBase" : "本地 Mock",
      hasPrevious: index > 0,
      hasNext: index < state.days.length - 1
    });
  },

  toTaskView(task) {
    return {
      ...task,
      categoryLabel: categoryLabels[task.category] || "任务",
      categoryClass: `category-${task.category}`,
      timerAction: task.startedAtUtc ? "停止" : "开始",
      timerSymbol: task.startedAtUtc ? "■" : "▶",
      timerLabel: task.startedAtUtc ? `计时中 ${domain.formatElapsed(task.startedAtUtc)}` : "",
      canStart: !task.isPaused && !task.isCompleted
    };
  },

  startTicker() {
    this.stopTicker();
    this.ticker = setInterval(() => {
      if (!this.data.tasks.some((task) => task.startedAtUtc)) {
        return;
      }
      this.setData({
        tasks: this.data.tasks.map((task) => ({
          ...task,
          timerLabel: task.startedAtUtc ? `计时中 ${domain.formatElapsed(task.startedAtUtc)}` : ""
        }))
      });
    }, 1000);
  },

  stopTicker() {
    if (this.ticker) {
      clearInterval(this.ticker);
      this.ticker = null;
    }
  },

  async moveDay(offset) {
    const state = await studyService.getState();
    const index = state.days.findIndex((item) => item.date === state.selectedDate);
    const target = state.days[index + offset];
    if (!target) {
      return;
    }
    await studyService.selectDate(target.date);
    await this.loadDay();
  },

  onPreviousDay() {
    return this.moveDay(-1);
  },

  onNextDay() {
    return this.moveDay(1);
  },

  async onToggleTask(event) {
    const taskId = event.currentTarget.dataset.id;
    const task = this.data.tasks.find((item) => item.id === taskId);
    try {
      if (task && task.startedAtUtc) {
        const stopped = await this.stopWithConfirmation(taskId);
        if (!stopped) {
          return;
        }
      }
      await studyService.toggleTask(taskId);
      await this.loadDay();
    } catch (error) {
      this.showError(error);
    }
  },

  async onTimerAction(event) {
    const taskId = event.currentTarget.dataset.id;
    const task = this.data.tasks.find((item) => item.id === taskId);
    try {
      if (task && task.startedAtUtc) {
        await this.stopWithConfirmation(taskId);
      } else {
        await studyService.startTask(taskId);
      }
      await this.loadDay();
    } catch (error) {
      this.showError(error);
    }
  },

  async stopWithConfirmation(taskId) {
    const result = await studyService.stopTask(taskId, Date.now(), false);
    if (!result.requiresConfirmation) {
      return true;
    }
    const answer = await wx.showModal({
      title: "确认学习时长",
      content: `本次计时 ${result.minutes} 分钟，是否保留？`,
      confirmText: "保留",
      cancelText: "继续计时"
    });
    if (!answer.confirm) {
      return false;
    }
    await studyService.stopTask(taskId, Date.now(), true);
    return true;
  },

  async onMinutesBlur(event) {
    const taskId = event.currentTarget.dataset.id;
    try {
      await studyService.setTaskMinutes(taskId, event.detail.value || 0);
      await this.loadDay();
    } catch (error) {
      this.showError(error);
    }
  },

  onRecapInput(event) {
    this.setData({ recapDraft: event.detail.value });
  },

  async onSaveRecap() {
    try {
      await studyService.setRecap(this.data.day.date, this.data.recapDraft);
      await this.loadDay();
      wx.showToast({ title: "复盘已保存", icon: "success" });
    } catch (error) {
      this.showError(error);
    }
  },

  async onAddTemporaryTask() {
    const answer = await wx.showModal({
      title: "新增临时任务",
      editable: true,
      placeholderText: "输入任务内容",
      confirmText: "添加"
    });
    if (!answer.confirm || !String(answer.content || "").trim()) {
      return;
    }
    try {
      await studyService.addTemporaryTask(this.data.day.date, answer.content);
      await this.loadDay();
    } catch (error) {
      this.showError(error);
    }
  },

  showError(error) {
    wx.showToast({
      title: error.message || "操作失败",
      icon: "none",
      duration: 2600
    });
  }
});
