# Core/Host Bridge 设计（v0.2）

## 目标

将“业务/数据/规则”尽可能下沉到 C++ Core；引擎只作为渲染壳与必要能力提供者（Host）。

首期先跑通 **标准 .NET Host**，随后替换为 Unity Host（不改变 Core 架构）。

## 核心原则

1) **Core 不依赖引擎**：不包含 UnityEngine，不使用反射，不假设资源系统细节。  
2) **Host 来适配 Core**：现有模块按需要包一层实现 Host；Core API 固定且最小。  
3) **IL2CPP 友好**：避免 native→managed 回调（AOT/裁剪复杂），改用轮询 command stream。  
4) **接口可生成**：跨语言接口用 C++ 宏定义（标记“谁实现”），由生成器产出两侧代码。  
5) **机器人模式优先**：Core 可在 Headless Host 下运行，支持一进程成千上百实例压测。

## 数据边界

跨边界只允许：

- blittable struct（固定布局，可内存拷贝）
- 句柄/ID（`uint64`）
- 字符串：UTF-8 `ptr+len`（只在 command stream 有效期内可读）

禁止跨边界传递 Unity 对象、托管对象、List/Dictionary 等。

## 数据流

### Core → Host（命令）

Core 每帧生成一个 **command stream**（字节序列），Host 拉取并执行。

v0.2 中 Core→Host 的命令统一为：

- `BridgeCmdCallHost { func_id, payload_size, payload... }`

具体有哪些“Host API”（例如 `LoadAsset` / `SpawnEntity` / `SetTransform` / `Log`）由业务层通过宏文件定义并生成代码。

### Host → Core（事件）

Host 处理命令后以“调用 Core API”的方式回推（无需事件结构体一条条手写）：

- `BridgeCore_PushCallCore(core, func_id, payload, payload_size)`
- （后续）InputFrame / NetPacket / Lifecycle / UIEvent … 都是同一种机制

这样新增跨语言接口只需要改宏定义并重新生成，不需要改 Core 的稳定 ABI。

## 资源加载（以 Unity AB 为例）

1) Core 通过生成的 Host API 发起 `LoadAsset(assetKey, requestId, type)`（写入 command stream）  
2) Unity Host 收到后通过 AB 系统异步加载  
3) 加载完成后 Host 调用 Core API：`AssetLoaded(requestId, handle, status)`（内部用 `BridgeCore_PushCallCore` 实现）  
4) Core 收到回调后继续输出后续 Host API（例如 `SpawnEntity(prefabHandle=handle)`）

Core 只关心 `assetKey` 与 `handle`，不关心 AB 细节。

## 机器人模式

两种运行方式：

1) **Headless Host + 多实例 Core**（推荐压测方式）
   - Host 不执行渲染命令（Spawn/Transform 直接丢弃或只做统计）
   - 资源加载可立即回执（或模拟延迟）
   - 一进程 N 个 CoreInstance，单线程 tick（或分线程分区）

2) Unity 内机器人
   - Unity Host 执行命令，适合功能验证，不适合千人压测

## 版本与兼容

- Native：C++20，CMake 构建，导出 C ABI（`cdecl`）
- Managed：C# `netstandard2.1`（Unity 兼容）

稳定 ABI（`Core/cpp/include/bridge/bridge.h`）必须保持 layout 稳定。

业务接口（func_id 与 payload 结构）通过宏文件定义并由生成器产出：

- 定义：`Tests/defs/*.def`（建议一个 `.def` 对应一个模块/子系统）
- 生成（C++）：`Tests/cpp/<module>/generated/<cpp_ns>_bindings.generated.h`
- 生成（C# Host）：`Tests/csharp/RobotHost/Generated/<Module>.*.g.cs`
- 生成（Unity Host）：`Tests/unity/Assets/BridgeDemoGame/Generated/<Module>.*.g.cs`

另外：生成器使用“模块名 + 函数名”计算稳定的 `func_id`（哈希），并在生成期检测冲突，避免模块拆分后 ID 因顺序变化而漂移。

## 分发策略与性能

Host 侧拿到 Core 输出的 `CommandStream` 后，需要把 `CallHost(func_id, payload...)` 分发到各模块的 Host API 实现。

为性能与 Unity/IL2CPP 友好，本仓库只保留一种分发策略（不依赖 native→managed 回调）：

- **单次扫描聚合分发（all）**
  - 生成 `Bridge.Bindings.BridgeAllCommandDispatcher.Dispatch(stream, host)`
  - 单 pass 扫描，并按 `func_id` 分发到各模块 Host API
  - 模块化的边界仍然体现在：`I<Module>HostApi` / `*.Structs.g.cs` / `*.CoreCalls.g.cs`

为降低 C# / Unity 开销：

- 分发器使用 `unsafe` + `sizeof(T)` + 指针解引用读取 payload，避免 `Marshal.PtrToStructure` 的反射与分配。
- Host→Core 的 `PushCallCore<T>(payload)` 使用 `unmanaged` 泛型直接传栈上数据指针，避免 `AllocHGlobal`。

### 基准结果（示例）

环境：Windows，Release，bots=1000，frames=300，dt=1/60。

- C#（`--host null`）
  - `all`：约 14.95M cmd/s，分配 ~208KB
- C#（`--host full`）
  - `all`：约 8.64M cmd/s，分配 ~488KB
- C++ 解析 baseline（仅解析 command stream）：约 40.49M cmd/s
- Unity（EditMode / Performance Test Framework）
  - `TickAndDispatch_OneFrame(1)`：Avg ~0.01 ms
  - `TickAndDispatch_OneFrame(1000)`：Avg ~0.27 ms

对应命令：

```powershell
dotnet run --project Tests/csharp/RobotHost/RobotHost.csproj -c Release -- 1000 300 0.0166667 --host null
dotnet run --project Tests/csharp/RobotHost/RobotHost.csproj -c Release -- 1000 300 0.0166667 --host full
.\build\bin\Release\bridge_robot_runner.exe 1000 300 0.0166667
& "C:\\Program Files\\Unity\\Hub\\Editor\\6000.0.40f1\\Editor\\Unity.exe" -runTests -batchmode -nographics -projectPath "D:\\UGit\\UnityNativeScripting\\Tests\\unity" -testPlatform EditMode -testResults "D:\\UGit\\UnityNativeScripting\\build\\unity-editmode-test-results.xml" -perfTestResults "D:\\UGit\\UnityNativeScripting\\build\\unity-editmode-perf-results.json" -logFile "D:\\UGit\\UnityNativeScripting\\build\\unity-editmode-test.log"
```
