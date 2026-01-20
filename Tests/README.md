# Tests（机器人/验证）

本目录用于验证 “Core（C++）⇄ Host（C#）” 的最小闭环，以及后续压测（机器人模式）。

- `Tests/cpp/robot_runner`：C++ 机器人 runner（快速压测/跑通 ABI）。
- `Tests/csharp/RobotHost`：标准 .NET Host（读 `Tests/assets` 模拟资源模块，并通过 `PushCallCore` 回推 `AssetLoaded`）。
- `Tests/unity`：Unity Host 范例工程（Windows Editor 下演示 Copy-Then-Load，并用 Unity `Resources` API 实现 `LoadAsset`）。
- `Tests/assets`：测试资源（示例文件）。

运行方式见：`Core/docs/BUILD.md`。
