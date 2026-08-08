# 自律台 Study Check-in

个人学习计划打卡系统。当前已完成 Windows 本地版，可从规划 Excel 读取每日任务，支持计时、手动补录、完成打卡、当日复盘、托盘提醒和 Excel 回写。

## 当前状态

- Windows WPF 客户端：可用
- Excel 导入与回写：可用，只更新 `每日执行_2026` 的 K、M、N 列
- 本地 Mock 同步与失败重试：可用
- 微信小程序、CloudBase、WxPusher：待接入

## 直接运行

本机发布文件位于：

```text
dist\StudyCheckin.exe
```

该 EXE 已包含 .NET 运行时，不需要另装 .NET。Excel 自动导入和回写需要电脑安装 Microsoft Excel。

详细操作见 [Windows 使用指南](docs/user-guide.md)。

## 开发与验证

```powershell
dotnet build StudyCheckin.sln --configuration Release
dotnet run --project tests\StudyCheckin.Core.Tests\StudyCheckin.Core.Tests.csproj --configuration Release
dotnet run --project tests\StudyCheckin.Desktop.IntegrationTests\StudyCheckin.Desktop.IntegrationTests.csproj --configuration Release -- "D:\.日常\大三上\规划\2028考研_竞赛_课程详细规划.xlsx"
powershell -ExecutionPolicy Bypass -File scripts\publish-windows.ps1
```

Excel 集成测试只操作临时副本，并检查原工作簿未被修改。

## 本地数据

运行数据保存在：

```text
%LOCALAPPDATA%\StudyCheckin
```

其中 `state.json` 是本地打卡状态，`mock-cloud.json` 是当前阶段的模拟云端数据，`settings.json` 保存 Excel 路径和启动设置。
