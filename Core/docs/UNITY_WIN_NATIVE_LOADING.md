# Unity（Windows）原生 DLL 加载方案（Editor 优先）

目标：在 Unity Editor（Windows）中使用 `bridge_core.dll` 跑起来，同时支持“重编译后覆盖 DLL”而不被 Windows 文件锁卡死；出包阶段再切换到更标准的插件/源码编译方案。

## 背景：为什么会被锁

Windows 下一个 DLL 被进程加载后，其文件会被锁定（不能覆盖/删除）。Unity Editor 在域重载、脚本重编译、迭代调试时经常需要替换原生 DLL，如果直接加载固定路径（例如 `Assets/Plugins/.../bridge_core.dll`），就会出现：

- C++ 重新编译输出无法覆盖（“正在被占用”）
- Unity 需要重启才能更新原生 DLL

## 设计原则

1) **Editor 与 Player 分开处理**：Editor 追求可热替换；Player 追求稳定部署。
2) **Editor 不直接锁定固定源文件**：实际加载前先复制到临时目录，再加载临时副本。
3) **保持 `DllImport("bridge_core")` 名称稳定**：避免每次生成不同的 import 名称导致 C# 代码变更。
4) **依赖 DLL 一起复制**：避免加载时从错误位置解析依赖。

## Editor（Windows）推荐方案：Copy-Then-Load

### 目录约定

- 作为“源 DLL”的路径（可被覆盖）：例如仓库构建产物 `../build/bin/Release/bridge_core.dll`
- 作为“实际加载”的临时目录（会被锁，但可换目录）：`<Project>/Library/BridgeNative/<buildId>/`

其中 `<buildId>` 可以用 `LastWriteTimeUtc + FileSize` 或 hash 组合，保证每次编译变化都会得到新目录，从而绕过锁定。

### 加载步骤（Editor 启动/脚本域重载时）

1) 从源目录拷贝：
   - `bridge_core.dll`
   - 同目录所有依赖 `.dll`（如果有）
2) 设置 DLL 搜索路径到临时目录：
   - Windows API：`SetDllDirectoryW(tempDir)`
3) 主动加载临时 DLL：
   - Windows API：`LoadLibraryW(tempDir\\bridge_core.dll)`

这样：

- 被锁的是 `Library/BridgeNative/<buildId>/bridge_core.dll`
- 源 DLL 仍可被 C++ 构建覆盖

### 与 Unity 插件导入的关系

为了避免 Unity Editor 自动加载并锁定 `Assets/Plugins` 下的 DLL，建议：

- 通过 `PluginImporter` 把 `bridge_core.dll` 标记为 **不兼容 Editor**（仅用于 Player 平台）
- Editor 只通过上述 Copy-Then-Load 路径加载

## Player（Windows）建议方案

两种选择：

1) **标准原生插件**：把 `bridge_core.dll` 放到 `Assets/Plugins/x86_64/`（或正确的平台目录），让 Unity 在 Player 中加载。
2) **源码编译进 Player**：如果你的构建系统支持把 C++ 直接编译链接进 Unity Player（例如自定义构建管线），则 Player 不需要单独的 DLL 文件。

Player 通常不需要“复制到临时目录”来绕开锁问题，因为运行时不会频繁替换 DLL。

## 与本仓库的落地

本仓库提供一组 Unity 脚本（Windows）用于：

- Editor：从“源 DLL”复制到 `Library/BridgeNative/<buildId>/` 并加载
- Editor：可选的“同步 DLL 到 Assets/Plugins（仅用于出包）”工具

对应实现位于 `Packages/com.unitynativescripting.bridgecore/`（不提交二进制 DLL）。
