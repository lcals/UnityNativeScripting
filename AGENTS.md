# UnityNativeScripting（工作区）协作说明

当前仓库包含：

- `Core/`：全新 Core-first 运行时（C++20 + C# `netstandard2.1`）。
- `Core/Tools/`：代码生成等工具。
- `Tests/`：机器人/压测/示例业务 + 标准 Host（C++ + C#）。
- `Tests/unity/`：Unity Host 范例工程（Windows Editor 下 Copy-Then-Load）。

## 新系统目标

核心思想：把业务/数据/规则尽可能下沉到 C++ Core；引擎（Unity/自研）只做渲染壳与必须能力提供者（Host）。

关键约束：Unity/IL2CPP 友好，避免 native→managed 回调（AOT/裁剪复杂），改为轮询式数据流：

- Core → Host：每帧输出一段 command stream（字节流）。
- Host → Core：通过 C ABI 推送事件（例如资源加载完成）。

## 目录约定

- `Core/cpp/`：C++20 核心（导出稳定 C ABI）
- `Core/csharp/`：C# 封装（Unity 兼容）
- `Core/docs/`：设计/构建说明（中文）
- `Tests/cpp/demo_game/`：示例业务（引用 `bridge_runtime`，并产出最终 `bridge_core.dll` 供 Host 加载）
- `Tests/cpp/`：C++ 机器人 runner（用于压测/快速验证）
- `Tests/csharp/`：标准 .NET Host（读文件模拟资源模块），并包含绑定实现
- `Tests/unity/`：Unity Host 范例工程
- `Tests/assets/`：测试资源（示例文件）

## 构建

### C++（C++20）

```powershell
cmake -S . -B build -DCMAKE_BUILD_TYPE=Release
cmake --build build --config Release
```

### C#（`netstandard2.1`）

```powershell
dotnet build Core/csharp/Bridge.Core/Bridge.Core.csproj -c Release
dotnet build Tests/csharp/RobotHost/RobotHost.csproj -c Release
```

## 重要约束（ABI/绑定）

- 公共 C ABI 入口定义在 `Core/cpp/include/bridge/bridge.h`。
- C ABI 结构体变更必须同步更新：`Core/csharp/Bridge.Core/Interop/Structs.cs`。
- 业务侧接口通过宏文件定义并生成：
  - 定义：`Tests/defs/*.def`（建议一个 `.def` 对应一个业务模块/子系统）
  - 生成（C++）：`Tests/cpp/<module>/generated/<cpp_ns>_bindings.generated.h`
  - 生成（C# Host）：`Tests/csharp/RobotHost/Generated/<Module>.*.g.cs`
  - 生成（Unity Host）：`Tests/unity/Assets/BridgeDemoGame/Generated/<Module>.*.g.cs`
- 跨边界只传：blittable struct、ID/handle（`uint64`）、UTF-8 字符串视图（`ptr+len`，仅在当帧有效）。
