# 构建与运行

## C++（C++20）

在仓库根目录执行：

```powershell
cmake -S . -B build -DCMAKE_BUILD_TYPE=Release
cmake --build build --config Release
```

（Windows / VS 多配置）默认输出到：

- `build/bin/Release/bridge_core.dll`
- `build/bin/Release/bridge_robot_runner.exe`

### 运行 C++ 机器人（Windows）

```powershell
cd build/bin/Release
.\bridge_robot_runner.exe 1000 300 0.0166667
```

### 运行 CTest（可选）

```powershell
cd build
ctest -C Release
```

## C#（`netstandard2.1`，Unity 兼容）

构建托管封装（Core 侧）：

```powershell
dotnet build Core/csharp/Bridge.Core/Bridge.Core.csproj -c Release
```

构建示例 Host（包含生成的绑定代码与绑定实现）：

```powershell
dotnet build Tests/csharp/RobotHost/RobotHost.csproj -c Release
```

### 运行 C# 机器人 Host（读文件模拟资源模块）

默认资源根目录为：`Tests/assets`。

```powershell
# 先确保已编译出 build/bin/Release/bridge_core.dll
dotnet run --project Tests/csharp/RobotHost/RobotHost.csproj -c Release -- 1000 300 0.0166667
```

如需用“空 Host”降低业务逻辑干扰（更接近桥接开销）：

```powershell
dotnet run --project Tests/csharp/RobotHost/RobotHost.csproj -c Release -- 1000 300 0.0166667 --host null
```

如需手动指定原生库目录（包含 `bridge_core.dll`）：

```powershell
$env:BRIDGE_NATIVE_DIR="D:\\UGit\\UnityNativeScripting\\build\\bin\\Release"
dotnet run --project Tests/csharp/RobotHost/RobotHost.csproj -c Release -- 1000 300 0.0166667
```

## Unity（Windows Editor）

Unity Host 示例工程位于：`Tests/unity`。

- 原生 DLL 加载采用 “Copy-Then-Load”（避免锁定构建产物），见：`Core/docs/UNITY_WIN_NATIVE_LOADING.md`
- DemoGame 范例使用说明见：`Tests/unity/Assets/BridgeDemoGame/README.md`

## 代码生成（绑定）

示例业务接口定义在：`Tests/defs/*.def`（示例拆为多个模块，例如 `demo_asset_api.def` / `demo_entity_api.def` / `demo_log_api.def`）。

运行生成器（会覆盖生成文件）：

```powershell
dotnet run --project Core/Tools/BridgeGen/BridgeGen.csproj -c Release --
```

如需把 C# 绑定输出到 Unity 工程（示例）：

```powershell
dotnet run --project Core/Tools/BridgeGen/BridgeGen.csproj -c Release -- --out-cs Tests/unity/Assets/BridgeDemoGame/Generated
```
