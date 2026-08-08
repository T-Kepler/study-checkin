const domain = require("../../services/domain");
const studyService = require("../../services/studyService");

const ranges = [
  { key: "7", label: "近 7 天", count: 7 },
  { key: "30", label: "近 30 天", count: 30 },
  { key: "all", label: "全部", count: 0 }
];

const categoryLabels = {
  mathematics: "数学一",
  english: "英语一",
  signalSystems843: "843",
  competition: "竞赛",
  coursework: "课程",
  temporary: "临时"
};

Page({
  data: {
    ranges,
    activeRange: "7",
    plannedHours: "0.0",
    actualHours: "0.0",
    taskRate: 0,
    hourRate: 0,
    completedTasks: 0,
    activeTasks: 0,
    dailyRows: [],
    categories: []
  },

  async onShow() {
    await this.loadStatistics();
  },

  onRangeTap(event) {
    this.setData({ activeRange: event.currentTarget.dataset.key }, () => this.loadStatistics());
  },

  async loadStatistics() {
    const state = await studyService.getState();
    const range = ranges.find((item) => item.key === this.data.activeRange) || ranges[0];
    const days = range.count > 0 ? state.days.slice(-range.count) : state.days;
    const totals = domain.calculateStatistics(days);
    const dailyRows = days.map((day) => {
      const snapshot = domain.calculateCompletion(day);
      return {
        date: day.date.slice(5).replace("-", "/"),
        percent: Math.round(snapshot.rate * 100),
        actualHours: (snapshot.actualMinutes / 60).toFixed(1)
      };
    });
    const categoryMap = {};
    days.forEach((day) => {
      day.tasks.filter((task) => !task.isPaused).forEach((task) => {
        const key = task.category;
        categoryMap[key] = categoryMap[key] || { planned: 0, actual: 0 };
        categoryMap[key].planned += Math.max(0, task.plannedMinutes || 0);
        categoryMap[key].actual += Math.max(0, task.actualMinutes || 0);
      });
    });
    const maxPlanned = Math.max(1, ...Object.values(categoryMap).map((item) => item.planned));
    const categories = Object.entries(categoryMap).map(([key, value]) => ({
      key,
      label: categoryLabels[key] || "任务",
      plannedHours: (value.planned / 60).toFixed(1),
      actualHours: (value.actual / 60).toFixed(1),
      width: Math.max(4, Math.round((value.planned / maxPlanned) * 100))
    }));
    this.setData({
      plannedHours: (totals.plannedMinutes / 60).toFixed(1),
      actualHours: (totals.actualMinutes / 60).toFixed(1),
      taskRate: Math.round(totals.taskRate * 100),
      hourRate: Math.round(totals.hourRate * 100),
      completedTasks: totals.completedTasks,
      activeTasks: totals.activeTasks,
      dailyRows,
      categories
    });
  }
});
