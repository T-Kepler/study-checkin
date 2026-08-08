# Study Check-in Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a WeChat mini program, a Windows EXE, CloudBase functions, WxPusher reminders, and automatic Excel check-in updates for the existing study plan.

**Architecture:** A dependency-free .NET 6 core library drives a WPF desktop client and a local mock cloud store. A native WeChat mini program uses the same JSON contract. CloudBase functions provide production persistence, pairing, synchronization, and scheduled WxPusher notifications.

**Tech Stack:** .NET 6 WPF, C#, System.Text.Json, Excel COM automation, native WeChat Mini Program JavaScript/WXML/WXSS, Node.js CloudBase functions.

---

### Task 1: Shared domain and tests

**Files:**
- Create: `src/windows/StudyCheckin.Core/*`
- Create: `tests/StudyCheckin.Core.Tests/*`

1. Define plan task, daily record, timer state, sync envelope, settings, and reminder models.
2. Add a JSON-backed repository with atomic writes.
3. Add timer rules for one active task, resume, stop, and four-hour confirmation.
4. Add completion and reminder-decision services.
5. Run the console test harness and require all assertions to pass.

### Task 2: Windows desktop application

**Files:**
- Create: `src/windows/StudyCheckin.Desktop/*`

1. Build the Today workspace with task checkboxes, start/stop controls, actual minutes, progress, recap, and sync status.
2. Add settings for Excel path, cloud endpoint, pairing token, reminder times, and startup behavior.
3. Add local/mock sync, one-minute polling, pending writes, and structured logs.
4. Add Excel COM import/writeback with file-lock detection and STA execution.
5. Add tray behavior, local reminders, startup registration, and error notifications.
6. Build and launch the application for a screenshot-based visual check.

### Task 3: WeChat mini program

**Files:**
- Create: `src/miniprogram/*`

1. Add Today, History, Statistics, and Settings pages.
2. Add a cloud adapter and a local Mock adapter with identical actions.
3. Implement exclusive timer state, manual minutes, completion, recap, offline queue, and retry.
4. Implement device pairing and WxPusher UID settings.
5. Validate all JavaScript and JSON files with Node.js.

### Task 4: CloudBase and WxPusher

**Files:**
- Create: `cloudfunctions/studyApi/*`
- Create: `cloudfunctions/reminder/*`

1. Implement CloudBase collections and authenticated actions.
2. Implement idempotent check-ins and server-side timer calculation.
3. Implement six-digit pairing-code exchange for desktop tokens.
4. Implement scheduled reminder decisions and WxPusher API calls.
5. Add environment templates, indexes, and deployment configuration.
6. Run Node syntax checks and local unit-style handler tests.

### Task 5: Packaging and documentation

**Files:**
- Create: `README.md`
- Create: `docs/setup-wechat-cloudbase.md`
- Create: `docs/user-guide.md`
- Create: `dist/StudyCheckin.exe`

1. Publish a self-contained Windows x64 single-file EXE.
2. Verify the EXE starts and the local Mock flow persists data.
3. Verify Excel updates against a copied workbook.
4. Document personal mini-program registration, CloudBase deployment, WxPusher binding, pairing, and Excel configuration.
5. Run the full C# tests, Node checks, file-integrity checks, and final UI review.

