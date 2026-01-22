# 性能探索笔记（方便下次接着做）

本文件用于记录“做过但未必有效/已撤回”的探索性尝试，避免下次重复走弯路。

> 已验证确定提升的改动请写到：`Core/docs/PERF_OPTIMIZATIONS.md`

## 2026-01-22：仿 Puerts 的 IL2CPP InternalCall/vtable dispatch（未带来收益，已撤回）

**目标**
- 把 `BridgeAllCommandDispatcher.DispatchFastUnchecked` 的 “解析 + 分发” 下沉到 native，通过 IL2CPP 内部 API（InternalCalls + vtable slot）直接调用 Host 的虚函数，期望减少 IL2CPP 生成代码的热点开销。

**实现思路（POC）**
- 在 native（源码插件）里注册一个 `InternalCall`：
  - `Bridge.Core.Unity.Il2cppDispatch::DispatchFastUnchecked(IntPtr streamPtr, UInt32 streamLen, Object host)`
- 启动时做一次初始化：
  - 找到 `Bridge.Bindings.BridgeAllHostApiBase`，用 `il2cpp_class_get_method_from_name` 取到各虚函数的 `slot`。
  - 分发时用 `host->klass->vtable[slot]` 取到 `VirtualInvokeData`，然后直接调用 `methodPtr`。
- command 解析沿用现有 `BridgeCmdCallHost` 格式，payload struct 复用生成的 `Tests/cpp/generated/*.generated.h`（`HostFuncId` + `HostArgs_*`）。

**结论**
- 在当前测试场景（Unity IL2CPP Source，命令解析与 Host dispatch 已经是 IL2CPP 生成的 C++），这个方向几乎不赚，甚至可能因为额外的 InternalCall/初始化/不可内联等因素略亏。
- 后续如要继续从 “Host dispatch” 榨性能，优先考虑减少“每帧/每 bot”的 managed 热点循环（例如把更大粒度的批处理下沉），而不是单纯把 dispatch 逻辑从 C# 迁到另一套 native dispatch。

