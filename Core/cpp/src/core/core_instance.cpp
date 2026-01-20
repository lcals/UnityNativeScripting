#include "core_instance.h"

#include <bridge/runtime/core_context.h>
#include <bridge/runtime/game_entry.h>

#include <algorithm>
#include <cassert>
#include <cstring>
#include <string>

namespace
{
	struct PendingCallHeader
	{
		uint32_t func_id = 0;
		uint32_t payload_size = 0;
	};

	static uint32_t Align8(uint32_t x)
	{
		return (x + 7u) & ~7u;
	}
}

namespace bridge
{
	CoreContext::CoreContext(BridgeCore& core)
		: core_(core)
	{
	}

	const BridgeCoreConfig& CoreContext::Config() const
	{
		return core_.config;
	}

	uint64_t CoreContext::AllocRequestId()
	{
		return core_.next_request_id++;
	}

	BridgeStringView CoreContext::StoreUtf8(std::string utf8)
	{
		return core_.commands.StoreUtf8(std::move(utf8));
	}

	void CoreContext::CallHost(uint32_t funcId, const void* payload, uint32_t payloadSize)
	{
		if (payloadSize > 0 && !payload)
		{
			return;
		}

		const uint32_t totalSize = static_cast<uint32_t>(sizeof(BridgeCmdCallHost)) + payloadSize;
		const uint32_t alignedTotal = Align8(totalSize);
		if (alignedTotal > UINT16_MAX)
		{
			return;
		}

		BridgeCmdCallHost cmd{};
		cmd.header.type = BRIDGE_CMD_CALL_HOST;
		cmd.header.size = static_cast<uint16_t>(alignedTotal);
		cmd.func_id = funcId;
		cmd.payload_size = payloadSize;

		core_.commands.PushBytes(&cmd, sizeof(cmd));
		core_.commands.PushBytes(payload, payloadSize);
		core_.commands.PushZeroBytes(alignedTotal - static_cast<uint32_t>(sizeof(cmd)) - payloadSize);
	}

	BridgeTransform CoreContext::IdentityTransform()
	{
		BridgeTransform tr{};
		tr.position = BridgeVec3{0, 0, 0, 0};
		tr.rotation = BridgeQuat{0, 0, 0, 1};
		tr.scale = BridgeVec3{1, 1, 1, 0};
		return tr;
	}

	BridgeVersion GetVersion()
	{
		return BridgeVersion{
			BRIDGE_VERSION_MAJOR,
			BRIDGE_VERSION_MINOR,
			BRIDGE_VERSION_PATCH};
	}

	BridgeCore* CreateCore(BridgeCoreConfig config)
	{
		auto* core = new BridgeCore();
		core->config = config;
		core->commands.Reserve(/*commandBytesCapacity*/ 1024, /*stringCountCapacity*/ 32);
		core->pending_call_bytes.reserve(256);
		core->app = CreateGameApp();
		if (!core->app)
		{
			delete core;
			return nullptr;
		}
		return core;
	}

	void DestroyCore(BridgeCore* core)
	{
		delete core;
	}

	void Tick(BridgeCore& core, float dt)
	{
		// Per-frame command buffer. Data pointers become invalid after Clear().
		core.commands.Clear();

		CoreContext ctx(core);

		// 先分发 Host->Core 调用，再跑本帧逻辑。
		const uint8_t* cur = core.pending_call_bytes.data();
		size_t remaining = core.pending_call_bytes.size();
		while (cur && remaining >= sizeof(PendingCallHeader))
		{
			PendingCallHeader hdr{};
			std::memcpy(&hdr, cur, sizeof(hdr));
			cur += sizeof(hdr);
			remaining -= sizeof(hdr);

			if (hdr.payload_size > remaining)
			{
				break;
			}

			const void* payload = cur;
			cur += hdr.payload_size;
			remaining -= hdr.payload_size;

			const uint32_t pad = Align8(hdr.payload_size) - hdr.payload_size;
			if (pad > remaining)
			{
				break;
			}
			cur += pad;
			remaining -= pad;

			core.app->OnCallCore(ctx, hdr.func_id, payload, hdr.payload_size);
		}
		core.pending_call_bytes.clear();

		core.app->Tick(ctx, std::max(0.0f, dt));
	}

	BridgeResult GetCommandStream(
		const BridgeCore& core,
		const void** out_ptr,
		uint32_t* out_len)
	{
		if (!out_ptr || !out_len)
		{
			return BRIDGE_INVALID_ARGUMENT;
		}

		*out_ptr = core.commands.Data();
		*out_len = core.commands.Size();
		return BRIDGE_OK;
	}

	BridgeResult PushCallCore(BridgeCore& core, uint32_t funcId, const void* payload, uint32_t payloadSize)
	{
		if (payloadSize > 0 && !payload)
		{
			return BRIDGE_INVALID_ARGUMENT;
		}

		PendingCallHeader hdr{};
		hdr.func_id = funcId;
		hdr.payload_size = payloadSize;

		const size_t oldSize = core.pending_call_bytes.size();
		const uint32_t alignedPayload = Align8(payloadSize);
		core.pending_call_bytes.resize(oldSize + sizeof(hdr) + alignedPayload);

		std::memcpy(core.pending_call_bytes.data() + oldSize, &hdr, sizeof(hdr));
		if (payloadSize > 0)
		{
			std::memcpy(core.pending_call_bytes.data() + oldSize + sizeof(hdr), payload, payloadSize);
		}
		if (alignedPayload > payloadSize)
		{
			std::memset(core.pending_call_bytes.data() + oldSize + sizeof(hdr) + payloadSize, 0, alignedPayload - payloadSize);
		}
		return BRIDGE_OK;
	}
}
