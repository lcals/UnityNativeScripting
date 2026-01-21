# Unity（Windows）IL2CPP：源码插件（C++ Source Plugin）编译与 il2cppOutput 解读

本文记录 Unity Windows 平台在 **IL2CPP Player** 下，把 C++ 以“源码插件”的方式编进 `GameAssembly.dll` 的实际流程，以及如何用 `il2cppOutput` 目录反查/定位问题与优化点。

> 结论先说：源码被编进 `GameAssembly.dll` ≠ P/Invoke 一定不会去找 DLL。是否走“内部直连”取决于 IL2CPP 生成代码中的 P/Invoke 解析路径。

## 1. 参与目录（按流水线顺序）

### 1.1 仓库内 C++ 源码（来源）

- `Core/cpp/include/bridge/*.h`：稳定 C ABI（例如 `bridge.h`）
- `Core/cpp/src/**`：Core 运行时实现
- `Tests/cpp/generated/*.generated.h`：业务宏定义生成的绑定头
- `Tests/cpp/demo_game/src/**`：示例业务（用于测试/演示）

### 1.2 Unity 工程内“源码插件”（同步目标）

通过 `Packages/com.unitynativescripting.bridgecore/Editor/BridgeCoreNativeSourceSync.cs` 同步到：

- `Tests/unity/Assets/Plugins/x86_64/BridgeCoreSource/`

同步时会做两件事：

1) **扁平化（flatten）文件路径**：避免 Unity 构建阶段再次扁平化后 include 路径失效。  
2) **改写 include**：把 `<bridge/...>` 与相对路径 include 改成同目录 `\"xxx.h\"`。

### 1.3 Bee 构建产物（真正被编译的输入目录）

Unity Player 构建时，Bee 会把源码插件与 IL2CPP 生成的 C++ 汇总到类似：

- `Tests/unity/Library/Bee/artifacts/WinPlayerBuildProgram/il2cppOutput/cpp/`

你可以在 `Tests/unity/Library/Bee/Player*.dag.json` 里看到：

- 源码插件文件（例如 `Assets/Plugins/x86_64/BridgeCoreSource/bridge_api.cpp`）被复制/参与编译
- IL2CPP 生成的 `Bridge.Core.cpp` 等被编译
- 最终链接产物为 `GameAssembly.dll`

### 1.4 build 输出中的 il2cppOutput（便于你分析的“镜像”）

Player 构建完成后，Unity 会把“将要编译/已生成”的 C++ 文件复制一份到：

- `build/unity_il2cpp/<Product>_BackUpThisFolder_ButDontShipItWithYourGame/il2cppOutput/`

这个目录是你要看的重点：

- 既包含 IL2CPP 从 C# 翻译出的 `*.cpp`（例如 `Bridge.Core.cpp`、`BridgeDemoGame.Runtime.cpp`）
- 也包含我们同步进来的源码插件 `*.cpp/*.h`（例如 `bridge_api.cpp`、`core_instance.cpp`）

因此它非常适合用来做：

- 定位 P/Invoke 是否仍在动态加载 DLL
- 观察 IL2CPP 对托管代码生成的 C++ 形态（分配、异常路径、数组访问等）
- 在“最终编译前”就做针对性优化分析

## 2. 如何确认“已经按源码编译进 GameAssembly”

满足任一即可：

1) 在 `build/.../il2cppOutput/` 里能看到 `bridge_api.cpp`、`core_instance.cpp` 等源码插件文件。  
2) 在 `Tests/unity/Library/Bee/Player*.dag.json` 搜索 `BridgeCoreSource`，能看到这些文件作为编译输入。  

## 3. 关键点：P/Invoke 到底走哪条路

即使 C++ 源码已经编进 `GameAssembly.dll`，如果 IL2CPP 生成的 P/Invoke wrapper 仍然走：

- `il2cpp_codegen_resolve_pinvoke("bridge_core", "BridgeCore_Create", ...)`

那么运行时依然会尝试加载 `bridge_core.dll`（或等价动态库），缺失就会出现：

- `DllNotFoundException: Unable to load DLL 'bridge_core'`

你可以直接在：

- `build/.../il2cppOutput/Bridge.Core.cpp`

里搜索 `il2cpp_codegen_resolve_pinvoke` 来确认。

同时你会看到 IL2CPP 生成代码里存在一个“内部直连”的分支：

- `FORCE_PINVOKE_INTERNAL` 或 `FORCE_PINVOKE_<lib>_INTERNAL`

当宏生效时，wrapper 会改为直接调用同名 C 函数符号（由链接器在 `GameAssembly.dll` 内解析），不再动态加载 DLL。

## 4. 接下来我们要做什么（和测试的关系）

后续所有性能/吞吐测试会统一采用 **源码插件模式（IL2CPP）**：

1) 构建前同步 C++ 源码到 `Assets/Plugins/x86_64/BridgeCoreSource`  
2) 构建 IL2CPP Player（含 RuntimeUnitTestToolkit 的测试 Runner）  
3) 运行 Player 执行测试并输出结果/性能日志  

同时需要把 `Bridge.Core` 的 P/Invoke 解析路径切换到“内部直连”，否则 Player 运行期仍会因找不到 `bridge_core.dll` 而失败。

