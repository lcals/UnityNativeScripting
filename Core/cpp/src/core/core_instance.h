#pragma once

#include <bridge/bridge.h>
#include <bridge/runtime/core_app.h>

#include "command_stream.h"

#include <cstdint>
#include <memory>
#include <vector>

struct BridgeCore
{
	BridgeCoreConfig config{};
	uint64_t next_request_id = 1;

	bridge::CommandStream commands;
	std::vector<uint8_t> pending_call_bytes;

	std::unique_ptr<bridge::ICoreApp> app;
};

namespace bridge
{
	BridgeVersion GetVersion();

	BridgeCore* CreateCore(BridgeCoreConfig config);
	void DestroyCore(BridgeCore* core);

	void Tick(BridgeCore& core, float dt);

	BridgeResult GetCommandStream(
		const BridgeCore& core,
		const void** out_ptr,
		uint32_t* out_len);

	BridgeResult PushCallCore(BridgeCore& core, uint32_t funcId, const void* payload, uint32_t payloadSize);
}
