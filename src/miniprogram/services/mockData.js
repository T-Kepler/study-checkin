const categoryOrder = ["mathematics", "english", "signalSystems843", "competition", "coursework"];

function createTask(dayId, index, category, title, plannedMinutes) {
  return {
    id: `${dayId}-${category}-${index}`,
    date: dayId,
    category,
    title,
    plannedMinutes,
    actualMinutes: 0,
    isCompleted: false,
    isPaused: title === "暂停新内容",
    isTemporary: false,
    startedAtUtc: null,
    updatedAtUtc: new Date().toISOString()
  };
}

function createDay(date, phase, courses, plannedMinutes, taskSpecs) {
  const tasks = taskSpecs.map((spec, index) =>
    createTask(date, index, spec[0], spec[1], Math.floor(plannedMinutes / taskSpecs.length))
  );
  return {
    id: date.replace(/-/g, ""),
    date,
    phase,
    courses,
    plannedMinutes,
    tasks,
    recap: "",
    status: "notStarted",
    updatedAtUtc: new Date().toISOString(),
    appliedRequestIds: []
  };
}

function createMockState() {
  const days = [
    createDay("2026-09-15", "竞赛优先", "无固定课", 150, [
      ["english", "词汇50个/复习100个"],
      ["competition", "竞赛：读规则、定题、分工、建版本库"]
    ]),
    createDay("2026-09-16", "竞赛优先", "08:10-09:45 人工智能(主B204)；10:15-11:50 通信电子线路(主B208)；14:00-15:35 通信原理(主B207)", 60, [
      ["competition", "竞赛：模块实现/联调；通信/AI/数据结构当周回顾"]
    ]),
    createDay("2026-09-17", "竞赛优先", "19:00-20:35 无线传感网(主D3多媒体9)", 120, [
      ["mathematics", "函数、极限概念摸底：习题"],
      ["competition", "竞赛：风险清单/问题闭环"]
    ]),
    createDay("2026-09-18", "竞赛优先", "10:15-11:50 通信原理(主B207)；14:00-15:35 数据结构(主B101)", 90, [
      ["mathematics", "函数、极限概念摸底：错题复盘"],
      ["english", "词汇复习/长难句1句"]
    ]),
    createDay("2026-09-19", "竞赛优先", "无固定课", 270, [
      ["mathematics", "函数、极限概念摸底：90分钟专题"],
      ["english", "词汇+阅读/长难句"],
      ["competition", "竞赛长任务：读规则、定题、分工、建版本库"]
    ]),
    createDay("2026-09-20", "竞赛优先", "无固定课", 90, [
      ["signalSystems843", "暂缓，仅建立考纲目录：框架浏览30分钟"],
      ["coursework", "课程：通信/AI/数据结构当周回顾；周复盘与下周排程"]
    ]),
    createDay("2026-09-21", "竞赛优先", "08:10-09:45 通信电子线路(主B101)；14:00-15:35 人工智能(主B305)；15:50-17:25 数据结构(主B101)", 120, [
      ["mathematics", "极限运算法则与等价无穷小：概念+例题"],
      ["english", "词汇250；句子成分：30分钟"]
    ])
  ];

  days.forEach((day) => {
    day.tasks.sort((left, right) => categoryOrder.indexOf(left.category) - categoryOrder.indexOf(right.category));
  });

  return {
    schemaVersion: 1,
    days,
    selectedDate: "2026-09-15",
    settings: {
      mode: "mock",
      cloudEnvId: "",
      deviceToken: "",
      pairedDeviceName: "",
      wxPusherUid: "",
      reminders: {
        enabled: true,
        morningPlanTime: "07:30",
        startNudgeTime: "19:00",
        incompleteNudgeTime: "22:30",
        weeklyReviewTime: "21:30"
      }
    },
    pendingOperations: [],
    lastSyncedAtUtc: null
  };
}

module.exports = {
  createMockState
};
