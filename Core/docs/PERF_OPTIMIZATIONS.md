# 性能优化记录（仅记录已验证提升）

本文件用于固化“已经验证能提升性能”的改动点与复现方式，避免反复踩坑。

## 记录规范（只记“确定提升”的）

- 每条优化至少复验 2 次（避免噪声），并且核心指标有提升才写入本文件。
- 每条记录必须能复现：写清楚 Unity 版本、`RunPerf` 参数、对比基线（tag/runId/commit）。
- 性能数字以 `README.md` 的性能摘要表为准；本文件只记录“做了什么 + 为什么有效 + 怎么复现”。

## 如何跑性能

仓库统一用 `Tools/RunPerf.ps1`：

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/RunPerf.ps1 -UnityVersion 6000.0.40f1 -UnityIl2cppRepeat 3 -Tag "<your_tag>"
```

- 每次 run 的完整原始数据：`build/perf_history.jsonl`（不建议提交，只用于本机追溯/回归分析）
- 单次 run 产物：`build/perf_runs/<runId>/`
- Unity IL2CPP Source 模式产物：`build/perf_runs/<runId>/ruttt_il2cpp_source/.../il2cppOutput/`

建议固定参数（bots/frames/dt）后再对比；如果只做快速 smoke，可加 `-NoBuild`。

## 如何用 IL2CPP 输出定位热点

1. 跑一次 IL2CPP Source 模式（RunPerf 默认会跑）。
2. 打开 `build/perf_runs/<runId>/ruttt_il2cpp_source/.../il2cppOutput/`
3. 常用搜索：
   - `VirtualActionInvoker`：通常意味着虚调用/接口调用在热点路径上
   - `SetAt(`：通常意味着数组写入走了额外的边界检查/写屏障路径

## 已验证有效的优化点

### 1) IL2CPP 下批量 Tick 写回 streams 的数组写入优化

**引入**
- git：`23e79d6`

**现象（IL2CPP 输出）**
- `CommandStream[]` 逐个 `streams[i] = ...` 会生成 `SetAt(...)` 路径，额外开销明显。

**改动**
- 在 `TickManyAndGetCommandStreams` 写回阶段，改为 `fixed (CommandStream* dst = streams)`，用 `dst[i] = ...` 直接写入。

**位置**
- `Core/csharp/Bridge.Core/BridgeCore.cs`
- `Packages/com.unitynativescripting.bridgecore/Runtime/Bridge.Core/BridgeCore.cs`

**效果**
- IL2CPP 输出中数组写回从 `SetAt(...)` 变为通过 `GetAddressAt(0)` 获取底层指针后直接赋值。

### 2) DispatchFast 空流判断内联（减少一次属性调用）

**引入**
- git：`23e79d6`

**现象（IL2CPP 输出）**
- `stream.IsEmpty` 会变成一次属性调用（哪怕很小也会落在热点）。

**改动**
- `DispatchFast` 入口判断改为 `host == null || stream.Ptr == IntPtr.Zero || stream.Length == 0`。

**位置**
- 生成器：`Core/Tools/BridgeGen/Program.cs`
- 生成物：
  - `Tests/csharp/RobotHost/Generated/Bridge.AllCommandDispatcher.g.cs`
  - `Tests/unity/Assets/BridgeDemoGame/Generated/Bridge.AllCommandDispatcher.g.cs`

### 3) IL2CPP 下遍历 `CommandStream[]`：用 `fixed` 读指针避免 `GetAt(...)`

**引入**
- git：`2772303`

**现象（IL2CPP 输出）**
- `streams[i]`（struct 数组取值）会生成 `GetAt(...)` 路径，并带边界检查与 struct 拷贝，在 10k bots 下会被放大。

**改动**
- 在批量帧循环里对 `CommandStream[] streams` 做一次 `fixed (CommandStream* streamsPtr = streams)`，用 `streamsPtr[i]` 读取。

**位置**
- `Tests/unity/Assets/BridgeDemoGame/PlayModeTests/BridgeSourceModeThroughputTests.cs`
- `Tests/unity/Assets/BridgeDemoGame/PlayModeTests/BridgeDemoGame.PlayModeTests.asmdef`（开启 `allowUnsafeCode`）

**效果**
- IL2CPP 输出中 `streams[i]` 从 `GetAt(...)` 变为通过 `GetAddressAt(0)` 获取底层指针后直接索引读取（减少边界检查/拷贝开销）。

### 4) 用更小的 Host 命令替代 “每帧全量 Transform”

**引入**
- git：`d5fa6a1`
- 验证：Unity `6000.0.40f1`
  - tag=`post_d5fa6a1_setposition`；runId=`20260121_202154`
  - tag=`post_d5fa6a1_setposition_rerun`；runId=`20260121_202436`
- 对比基线：tag=`post_2772303_r5`；runId=`20260121_193135`

**现象**
- Demo 业务每帧只更新 position.x，但仍通过 `SetTransform(entityId, mask=1, BridgeTransform)` 发送全量 `BridgeTransform`（payload 64B，单 tick stream=80B）。
- 在 10k bots 下，这会放大跨边界拷贝与 Host 侧解析成本。

**改动**
- 为 `DemoEntity` 增加更小 payload 的 Host API：`SetPosition(entityId, BridgeVec3 position)`。
- Demo 业务侧（C++）每帧改为发 `SetPosition`，把每 tick stream 从 80B 降到 40B。
- RobotHost/Unity Host 增加对应实现（计入 `Transforms` 统计；Unity 侧直接设置 `Transform.position`）。

**位置**
- 定义：`Tests/defs/demo_entity_api.def`
- 生成器：`Core/Tools/BridgeGen/Program.cs`（补齐 `BridgeVec3/BridgeQuat` 类型映射）
- Demo 业务：`Tests/cpp/demo_game/src/demo_asset_app.cpp`
- RobotHost：`Tests/csharp/RobotHost/Bind/RobotHostApi.cs`、`Tests/csharp/RobotHost/Bind/WorldState.cs`
- Unity Host：`Tests/unity/Assets/BridgeDemoGame/Runtime/Host/DemoGameUnityHostApi.Entity.cs`

**效果（本机）**
- Unity IL2CPP Source（repeat=5）
  - 1k ticks/s：约 `50.34M` → `55.52M`
  - 10k ticks/s：约 `31.06M` → `33.67M`
- `total_bytes` 约减半（10k*300：`240,000,000` → `120,000,000`），解析与拷贝成本显著下降。

## 优化流程建议（只在提升时记录/提交）

1. 做一次“小步”改动（只动一个热点点位）。
2. 跑 `Tools/RunPerf.ps1` 并打 `-Tag`。
3. 对比 `README.md` 的摘要表（或直接看 `build/perf_history.jsonl`）确认核心指标提升：
   - Unity IL2CPP Source：`il2cpp_source 1k ticks/s`、`il2cpp_source 10k ticks/s`
   - C++ runner：`robot_runner ticks/s`（原生解析+tick 的基线）
4. 如果回退：回滚代码与 README 变更，不提交。
5. 如果提升：再跑一次复验（减少噪声），然后提交代码 + 记录。

## 新增记录模板

复制这一段追加到“已验证有效的优化点”下面：

```markdown
### N) <标题（一句话说明改动）>

**引入**
- git：<commit_sha>
- 验证：Unity <version>；tag=<tag>；runId=<runId>（复验 2 次）
- 对比：tag=<baseline_tag>；runId=<baseline_runId>

**现象（IL2CPP 输出）**
- <你在 il2cppOutput 里看到的热点/函数名/调用路径>

**改动**
- <做了什么>

**位置**
- <文件路径 / 生成器路径 / 生成物路径>

**效果**
- <为什么有效（减少虚调用/减少数组写屏障/减少分配/减少拷贝等）>
```
