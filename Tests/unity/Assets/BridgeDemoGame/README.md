# DemoGame（Unity Host 范例）

目标：在 Unity 中复用与 `Tests/csharp/RobotHost` 相同的协议与 Core，但把资源读取改为 Unity API（`Resources.LoadAsync<TextAsset>`）。

## 运行步骤（Windows Editor）

1) 在仓库根目录构建原生库：

```powershell
cmake -S . -B build -DCMAKE_BUILD_TYPE=Release
cmake --build build --config Release
```

确保存在：`build/bin/Release/bridge_core.dll`

也可以直接在 Unity 菜单执行：

- `BridgeCore/Windows/Build + Hot Reload (Release)`

2) 用 Unity 打开工程：`Tests/unity`

3) 新建一个空场景，创建空 GameObject，挂载组件：

- `BridgeDemoGame.DemoGameUnityRunner`

保持默认参数即可（`Bots=1`）。

4) 点击 Play

预期效果：

- Core 会请求 `Main/Prefabs/Bot`（对应 `Assets/BridgeDemoGame/Resources/Main/Prefabs/Bot.bytes`）
- Host 收到 `LoadAsset` 后通过 `Resources.LoadAsync<TextAsset>` 读取并回推 `AssetLoaded`
- Core 随后输出 `SpawnEntity` / `SetTransform`，Unity 侧会生成一个 Capsule 并沿 X 轴移动

## 常用配置

- 环境变量 `BRIDGE_CORE_DLL`：可指定源 `bridge_core.dll` 的绝对路径（优先级最高）
- `DemoGameUnityRunner.Bots`：多实例（超过 `MaxBotsWithRendering` 会自动关闭渲染命令）

## 测试（RuntimeUnitTestToolkit）

工程已引入 `com.cysharp.runtimeunittesttoolkit`，用于在 **Player** 下运行 NUnit 测试（可用于 Mono / IL2CPP）。

我们按后端分两类：

- **IL2CPP Player（源码插件）**：只看吞吐/性能（`##bridgeperf: ...`）
- **Mono Player（DLL 插件）**：只看 GC 分配（`##bridgegc: ...`）

### IL2CPP（源码插件）跑测试

命令行（Windows / PowerShell）：

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\6000.0.40f1\Editor\Unity.exe"
$proj  = "D:\UGit\UnityNativeScripting\Tests\unity"
$out   = "D:\UGit\UnityNativeScripting\build\ruttt_il2cpp_source\test.exe"

& $unity -quit -batchmode -nographics `
  -projectPath $proj `
  -executeMethod Bridge.Core.Unity.Editor.BridgeCoreRuntimeUnitTestBuild.BuildUnitTest `
  /ScriptBackend IL2CPP /BuildTarget StandaloneWindows64 /buildPath $out `
  -logFile "D:\UGit\UnityNativeScripting\build\ruttt_il2cpp_source\build.log"

& $out -batchmode -nographics -logFile "D:\UGit\UnityNativeScripting\build\ruttt_il2cpp_source\run.log"
```

说明：

- `BuildUnitTest` 会在 IL2CPP 下自动执行 `BridgeCore/Windows/Sync C++ Sources`，把 C++ 源码同步到 `Assets/Plugins/x86_64/BridgeCoreSource`，并在 Player Build 时编进 `GameAssembly.dll`。
- 这些测试位于 `Assets/BridgeDemoGame/PlayModeTests/`（例如 `BridgeRuntimeSmokeTests` / `BridgeSourceModeThroughputTests`）。吞吐结果会输出：`##bridgeperf: mode=il2cpp_source ...`

### Mono（DLL 插件）跑测试（GC）

命令行（Windows / PowerShell）：

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\6000.0.40f1\Editor\Unity.exe"
$proj  = "D:\UGit\UnityNativeScripting\Tests\unity"
$out   = "D:\UGit\UnityNativeScripting\build\ruttt_mono\test.exe"

& $unity -quit -batchmode -nographics `
  -projectPath $proj `
  -executeMethod Bridge.Core.Unity.Editor.BridgeCoreRuntimeUnitTestBuild.BuildUnitTest `
  /ScriptBackend Mono2x /BuildTarget StandaloneWindows64 /buildPath $out `
  -logFile "D:\UGit\UnityNativeScripting\build\ruttt_mono\build.log"

Push-Location (Split-Path $out)
& .\test.exe -batchmode -nographics -logFile "D:\UGit\UnityNativeScripting\build\ruttt_mono\run.log"
Pop-Location
```

说明：

- `BuildUnitTest` 会在 Mono 下自动执行 `BridgeCore/Windows/Sync bridge_core.dll (for Player)`，把 `build/bin/Release/bridge_core.dll` 同步为 Unity 的 Player 插件。
- GC 测试位于 `Assets/BridgeDemoGame/PlayModeTests/BridgeMonoGcTests.cs`，结果会输出：`##bridgegc: mode=mono ...`

注意：

- Mono Player 运行时需要把工作目录设为 build 输出目录（包含 `MonoBleedingEdge/`），否则可能无法启动或无法输出日志。

注意：

- 如果 `build/unity_il2cpp/BridgeDemoGame.exe`（或 unit test player）仍在运行，重建可能会因为文件被映射而失败；请先结束进程再 build。
- 如果 `Tests/unity` 工程已在 Unity Editor 中打开，命令行 build 也可能被锁定；建议先关闭该工程的 Editor 实例。
