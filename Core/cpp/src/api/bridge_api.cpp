#include <bridge/bridge.h>

#include "../core/core_instance.h"

//------------------------------------------------------------------------------
// C ABI 实现（绑定层）
//
// 这个文件要尽量保持“朴素”：只做参数校验 + 转发到内部 C++ Runtime。
// 业务/玩法逻辑应放在业务层或测试工程（例如 Tests/cpp/demo_game）。
//------------------------------------------------------------------------------

BridgeVersion BRIDGE_CALL Bridge_GetVersion(void)
{
	return bridge::GetVersion();
}

BridgeCore* BRIDGE_CALL BridgeCore_Create(BridgeCoreConfig config)
{
	return bridge::CreateCore(config);
}

void BRIDGE_CALL BridgeCore_Destroy(BridgeCore* core)
{
	bridge::DestroyCore(core);
}

void BRIDGE_CALL BridgeCore_Tick(BridgeCore* core, float dt)
{
	if (!core)
	{
		return;
	}
	bridge::Tick(*core, dt);
}

BridgeResult BRIDGE_CALL BridgeCore_GetCommandStream(
	const BridgeCore* core,
	const void** out_ptr,
	uint32_t* out_len)
{
	if (!core)
	{
		return BRIDGE_INVALID_ARGUMENT;
	}
	return bridge::GetCommandStream(*core, out_ptr, out_len);
}

BridgeResult BRIDGE_CALL BridgeCore_PushCallCore(
	BridgeCore* core,
	uint32_t func_id,
	const void* payload,
	uint32_t payload_size)
{
	if (!core)
	{
		return BRIDGE_INVALID_ARGUMENT;
	}
	return bridge::PushCallCore(*core, func_id, payload, payload_size);
}
