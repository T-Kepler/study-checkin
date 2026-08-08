# 微信小程序 Mock 使用指南

## 当前能力

本地 Mock 版不需要公众号、服务器或正式 AppID，已包含：

- 今日任务、日期切换、课程信息和完成率。
- 单任务互斥计时、手动分钟、四小时确认和任务打卡。
- 当日复盘、临时任务、历史筛选和学习统计。
- 离线操作队列、同步重试、6 位设备配对入口。
- CloudBase 环境 ID、WxPusher UID 和提醒时间设置。

Mock 版中的 CloudBase、配对和 WxPusher 仅验证界面与数据流程，不会连接真实云端或发送微信消息。

## 导入微信开发者工具

1. 安装微信开发者工具稳定版。
2. 选择“导入项目”。
3. 项目目录选择 `D:\.日常\大三上\study-checkin\src\miniprogram`。
4. 没有正式 AppID 时选择测试号或游客模式。
5. 项目名称填写“自律台”，后端服务选择“不使用云服务”。
6. 导入后点击“编译”。

仓库的 `project.config.json` 使用 `touristappid`，正式注册后在开发者工具中换成自己的 AppID。个人配置会写入已忽略的 `project.private.config.json`，不会提交到 Git。

## Mock 验收流程

1. 打开“今日”，确认显示 2026 年 9 月 15 日计划。
2. 点击第一项任务“开始”，确认按钮变为“停止”且计时递增。
3. 停止计时，修改实际分钟并完成任务。
4. 填写当日复盘并保存。
5. 在“历史”打开任意日期，确认会跳回“今日”。
6. 在“统计”切换近 7 天、近 30 天和全部。
7. 在“设置”输入任意 6 位数字，确认 Mock 配对成功。
8. 点击“重置 Mock 数据”可恢复首次打开状态。

## 数据位置

Mock 数据保存在微信小程序本地缓存：

```text
study-checkin:state:v1
study-checkin:mock-cloud:v1
```

重新编译不会自动清空数据。需要回到初始计划时，从设置页重置，或在开发者工具中清除缓存。

## 自动验证

在项目根目录运行：

```powershell
node tests\miniprogram\domain.test.js
node tests\miniprogram\service.test.js
node scripts\validate-miniprogram.js
```

验证覆盖计时互斥、时长计算、四小时保护、暂停任务、手动补录、离线队列、同步恢复、配对、JS/JSON/WXML/WXSS 和标签栏图片。
