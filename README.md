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
| tsUtc | runId | tag | git | robothost_null cmd/s | robot_runner cmd/s | il2cpp_source 1k ticks/s | il2cpp_source 10k ticks/s |
|---|---|---|---|---:|---:|---:|---:|
<!-- PERF_TABLE_END -->

