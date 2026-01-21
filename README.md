# UnityNativeScripting（Core-first 重构）

本仓库用于验证与迭代一套 **Core-first** 架构：

- 业务/数据/规则尽可能下沉到 **C++ Core**
- 引擎（Unity/自研）只作为渲染壳与必须能力提供者（Host）
- 为了 IL2CPP/AOT 友好，避免 native→managed 回调，改用 **轮询式数据流**
  - Core → Host：每帧输出 command stream（字节流）
  - Host → Core：通过稳定 C ABI 推送事件（`PushCallCore`）

## 目录

- `Core/`：运行时核心（C++20 + C# `netstandard2.1`）
- `Core/Tools/`：代码生成工具（绑定生成）
- `Tests/`：示例业务、机器人压测、标准 .NET Host
- `Tests/unity/`：Unity Host 范例工程（Windows Editor 下 Copy-Then-Load）

## 快速开始

### 1) 构建 C++（产出 `bridge_core.dll`）

```powershell
cmake -S . -B build -DCMAKE_BUILD_TYPE=Release
cmake --build build --config Release
```

### 2) 运行 C++ 机器人（可选）

```powershell
cd build/bin/Release
.\bridge_robot_runner.exe 1000 300 0.0166667
```

### 3) 运行标准 C# Host（RobotHost）

```powershell
dotnet run --project Tests/csharp/RobotHost/RobotHost.csproj -c Release -- 1000 300 0.0166667
```

如需用“空 Host”降低业务逻辑干扰（更接近桥接开销）：

```powershell
dotnet run --project Tests/csharp/RobotHost/RobotHost.csproj -c Release -- 1000 300 0.0166667 --host null
```

### 4) 生成绑定（扫描 `Tests/defs/*.def`）

```powershell
dotnet run --project Core/Tools/BridgeGen/BridgeGen.csproj -c Release --
```

如需把 C# 绑定输出到 Unity 工程（示例）：

```powershell
dotnet run --project Core/Tools/BridgeGen/BridgeGen.csproj -c Release -- --out-cs Tests/unity/Assets/BridgeDemoGame/Generated
```

## 文档

- 架构设计：`Core/docs/BRIDGE_DESIGN.md`
- 构建与运行：`Core/docs/BUILD.md`
- Unity Windows 原生库加载：`Core/docs/UNITY_WIN_NATIVE_LOADING.md`
- 性能优化记录：`Core/docs/PERF_OPTIMIZATIONS.md`

## 性能测试（带历史记录）

仓库提供一个 PowerShell 脚本用于跑机器人/Unity 性能用例，并把每次结果追加写入历史文件：

```powershell
powershell -ExecutionPolicy Bypass -File Tools/RunPerf.ps1 -Bots 1000 -Frames 3000 -Tag "baseline"
```

- 默认输出：`build/perf_history.jsonl`（每行一条 JSON 记录）
- 单次 run 的日志/产物：`build/perf_runs/<runId>/`
- 可选：`-NoUnity` / `-NoUnityEditMode` / `-NoUnityIl2cpp` / `-NoBuild`
- 可选：`-UnityVersion 6000.0.40f1` 或 `-UnityExe <path>` 用于指定 Unity 版本/路径

### 性能摘要（自动追加）

下表由 `Tools/RunPerf.ps1` 自动追加（更完整的数据仍以 `build/perf_history.jsonl` 为准）。

<!-- PERF_TABLE_START -->
| tsUtc | runId | tag | git | robothost_null cmd/s | robot_runner cmd/s | robot_runner ticks/s | il2cpp_source 1k ticks/s | il2cpp_source 10k ticks/s |
|---|---|---|---|---:|---:|---:|---:|---:|
| 2026-01-21T13:33:27.2531131Z | 20260121_213218 | post_4bfba43_shrink_header | 4bfba43 | 24956397 | 82791134 | 83333333.33 | 53676144.98 | 38640229.71 |
| 2026-01-21T13:19:57.4007236Z | 20260121_211854 | probe_9219989_stable_window | 9219989 | 24159430 | 80006607 | 78947368.42 | 49798159.76 | 34168195.22 |
| 2026-01-21T13:15:08.8643720Z | 20260121_211406 | post_912e0e5_unchecked_now | 326156a | 25574750 | 74101492 | 73170731.71 | 56039152.69 | 37078326.73 |
| 2026-01-21T13:13:38.1183595Z | 20260121_211235 | baseline_c484055_now | c484055 | 26166305 | 76538414 | 76923076.92 | 56114623.47 | 36469951.80 |
| 2026-01-21T13:11:18.7568374Z | 20260121_211030 | verify_dispatchfast_unchecked_noreadme_1 | 326156a | 26111954 | 79490078 | 78947368.42 | 56328507.86 | 33492085.82 |
| 2026-01-21T13:08:58.7206512Z | 20260121_210811 | post_912e0e5_dispatchfast_unchecked_rerun | 326156a | 24878322 | 77301476 | 76923076.92 | 56203982.99 | 34179309.21 |
| 2026-01-21T13:06:31.6271292Z | 20260121_210530 | post_912e0e5_dispatchfast_unchecked | 912e0e5 | 24472001 | 75259385 | 75000000.00 | 57012542.76 | 35951479.88 |
| 2026-01-21T12:53:22.0211694Z | 20260121_205232 | post_c484055_dispatchfast_generic | c484055 | 25094858 | 76026471 | 76923076.92 | 56037059.18 | 34641512.31 |
| 2026-01-21T12:22:32.4341938Z | 20260121_202154 | post_d5fa6a1_setposition | d5fa6a1 | 27401987 | 76187915 | 76923076.92 | 55518543.19 | 33672905.88 |
| 2026-01-21T12:08:02.0093094Z | 20260121_200706 | baseline_r11 | 0d3443a | 22894010 | 57608306 | 57692307.69 | 50491450.11 | 31203746.95 |
| 2026-01-21T11:32:08.5443073Z | 20260121_193135 | post_2772303_r5 | 0d3443a | 22939969 | 59002556 | 58823529.41 | 50338948.92 | 31063843.45 |
| 2026-01-21T11:30:02.2954247Z | 20260121_192929 | post_2772303 | 0d3443a | 24758375 | 54120785 | 54545454.55 | 50497399.38 | 30900438.79 |
| 2026-01-21T11:14:51.1784595Z | 20260121_191407 | baseline_payload_checks | 98be1d5 | 23288884 | 58962241 | 58823529.41 | 41732162.98 | 30506561.45 |
| 2026-01-21T11:04:08.5733790Z | 20260121_190339 | baseline_pair | 98be1d5 | 25024758 | 59328831 | 58823529.41 | 49724855.80 | 30624025.77 |
| 2026-01-21T11:00:11.1960690Z | 20260121_185933 | baseline_rerun2 | 98be1d5 | 24866432 | 60605938 | 60000000.00 | 49637645.19 | 30804679.85 |
| 2026-01-21T10:57:13.8541013Z | 20260121_185636 | baseline_rerun | 98be1d5 | 24181264 | 60877586 | 61224489.80 | 48282743.75 | 32490177.14 |
| 2026-01-21T09:46:22.1895560Z | 20260121_174537 | post_23e79d6 | 23e79d6 | 23447208 | 58632532 | 58823529.41 | 50646588.11 | 32289520.76 |
| 2026-01-21T09:02:21.8370259Z | 20260121_170122 | post_commit | 46c54af | 22331042 | 60740046 | 61224489.80 | 47947066.44 | 32652492.58 |
<!-- PERF_TABLE_END -->










