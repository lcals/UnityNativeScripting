# BridgeCore（Unity 侧）

## 作为 Unity Package 使用

此目录为标准 UPM 包（`com.unitynativescripting.bridgecore`）。在其他 Unity 工程中可通过 `Packages/manifest.json` 引用：

- Git（推荐）：`"com.unitynativescripting.bridgecore": "git+<repo>.git?path=/Packages/com.unitynativescripting.bridgecore"`
- 本地开发：`"com.unitynativescripting.bridgecore": "file:<abs-or-rel>/Packages/com.unitynativescripting.bridgecore"`

## 两种运行模式（推荐组合）

### 1) Windows Editor：Copy-Then-Load（热替换友好）

- 运行时从 `Library/BridgeNative/<buildId>/bridge_core.dll` 加载，避免锁定固定源文件（便于热替换/重编译覆盖）
- 源 DLL 默认从仓库构建输出 `build/bin/Release/bridge_core.dll` 查找（会自动向上寻找仓库根目录）
- 也可通过环境变量 `BRIDGE_CORE_DLL` 指定源 DLL 的绝对路径

Unity 菜单（Windows Editor）：

- `BridgeCore/Windows/Build bridge_core.dll (Release)`
- `BridgeCore/Windows/Build + Hot Reload (Release)`
- `BridgeCore/Windows/Reload bridge_core.dll (from build output)`

更多说明见：`Core/docs/UNITY_WIN_NATIVE_LOADING.md`。

### 2) Player：IL2CPP 源码插件（编进 GameAssembly.dll）

- 同步 C++ 源码到 `Assets/Plugins/x86_64/BridgeCoreSource`
- Unity IL2CPP Player Build 时会把这些 `*.cpp/*.h` 与 il2cppOutput 一起编译进 `GameAssembly.dll`

Unity 菜单（Windows Editor）：

- `BridgeCore/Windows/Sync C++ Sources (for IL2CPP source plugin)`

更多说明见：`Core/docs/UNITY_IL2CPP_SOURCE_BUILD.md`。

### 3) Player：Mono / DLL 插件（可选）

- 把构建产物 `bridge_core.dll` 同步到 `Assets/Plugins/BridgeCore/Win64/bridge_core.dll`
- 插件导入器会被配置为：**不在 Editor 自动加载**（避免锁定），仅用于 Player

Unity 菜单（Windows Editor）：

- `BridgeCore/Windows/Sync bridge_core.dll (for Player)`
- `BridgeCore/Windows/Configure Plugin Importer`
