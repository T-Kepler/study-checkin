const domain = require("../../services/domain");
const studyService = require("../../services/studyService");

const filters = [
  { key: "all", label: "全部" },
  { key: "notStarted", label: "未开始" },
  { key: "inProgress", label: "进行中" },
  { key: "completed", label: "已完成" }
];

const statusLabels = {
  notStarted: "未开始",
  inProgress: "进行中",
  completed: "已完成",
  adjusted: "已调整"
};

function formatDate(date) {
  const parsed = new Date(`${date}T00:00:00`);
  const weekday = ["周日", "周一", "周二", "周三", "周四", "周五", "周六"][parsed.getDay()];
  return `${parsed.getMonth() + 1}月${parsed.getDate()}日 · ${weekday}`;
}

Page({
  data: {
    filters,
    activeFilter: "all",
    days: [],
    totalDays: 0,
    completedDays: 0
  },

  async onShow() {
    await this.loadHistory();
  },

  async loadHistory() {
    const state = await studyService.getState();
    const allDays = [...state.days].reverse().map((day) => {
      const snapshot = domain.calculateCompletion(day);
      return {
        ...day,
        status: snapshot.status,
        statusLabel: statusLabels[snapshot.status],
        dateLabel: formatDate(day.date),
        progressPercent: Math.round(snapshot.rate * 100),
        actualHours: (snapshot.actualMinutes / 60).toFixed(1),
        plannedHours: (snapshot.plannedMinutes / 60).toFixed(1),
        taskSummary: `${snapshot.completedTasks}/${snapshot.activeTasks} 项完成`
      };
    });
    this.allDays = allDays;
    this.setData({
      totalDays: allDays.length,
      completedDays: allDays.filter((day) => day.status === "completed").length
    });
    this.applyFilter();
  },

  onFilterTap(event) {
    this.setData({ activeFilter: event.currentTarget.dataset.key }, () => this.applyFilter());
  },

  applyFilter() {
    const active = this.data.activeFilter;
    const days = active === "all"
      ? this.allDays
      : this.allDays.filter((day) => day.status === active);
    this.setData({ days });
  },

  async onOpenDay(event) {
    try {
      await studyService.selectDate(event.currentTarget.dataset.date);
      wx.switchTab({ url: "/pages/today/index" });
    } catch (error) {
      wx.showToast({ title: error.message || "无法打开日期", icon: "none" });
    }
  }
});
