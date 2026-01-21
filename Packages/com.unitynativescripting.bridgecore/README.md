# BridgeCore（Unity 侧）

## 作为 Unity Package 使用

此目录为标准 UPM 包（`com.unitynativescripting.bridgecore`）。在其他 Unity 工程中可通过 `Packages/manifest.json` 引用：

- Git（推荐）：`"com.unitynativescripting.bridgecore": "git+<repo>.git?path=/Packages/com.unitynativescripting.bridgecore"`
- 本地开发：`"com.unitynativescripting.bridgecore": "file:<abs-or-rel>/Packages/com.unitynativescripting.bridgecore"`

此目录仅提供 **Windows Editor** 下的原生 DLL 加载支持：

- 运行时从 `Library/BridgeNative/<buildId>/bridge_core.dll` 加载，避免锁定固定源文件
- 源 DLL 默认从仓库构建输出 `build/bin/Release/bridge_core.dll` 查找（会自动向上寻找仓库根目录）
- 也可通过环境变量 `BRIDGE_CORE_DLL` 指定源 DLL 的绝对路径

Unity 菜单：

- `BridgeCore/Windows/Build + Hot Reload (Release)`：触发 CMake 编译并重新加载

更多说明见：`Core/docs/UNITY_WIN_NATIVE_LOADING.md`。
