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

## 性能测试（Unity Performance Test Framework）

已在工程内加入 `com.unity.test-framework.performance`，并提供 EditMode 性能用例：

- `BridgeDemoGame.Tests.BridgeDispatchPerformanceTests.TickAndDispatch_OneFrame`

命令行运行（Windows / PowerShell）：

```powershell
& "C:\\Program Files\\Unity\\Hub\\Editor\\6000.0.40f1\\Editor\\Unity.exe" `
  -batchmode -nographics -quit `
  -projectPath "D:\\UGit\\UnityNativeScripting\\Tests\\unity" `
  -runTests -testPlatform EditMode `
  -testResults "D:\\UGit\\UnityNativeScripting\\build\\unity-editmode-test-results.xml" `
  -perfTestResults "D:\\UGit\\UnityNativeScripting\\build\\unity-editmode-perf-results.json" `
  -logFile "D:\\UGit\\UnityNativeScripting\\build\\unity-editmode-test.log"
```

注意：

- `-testPlatform` 在 Unity 6 下建议使用 `EditMode` / `PlayMode`（大小写匹配）。
- 如果 `Tests/unity` 工程已在 Unity Editor 中打开，命令行跑测试会被锁定；请先关闭该工程的 Editor 实例。
