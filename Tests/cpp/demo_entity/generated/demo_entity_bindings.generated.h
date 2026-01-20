#pragma once

#include <bridge/bridge.h>
#include <bridge/runtime/core_context.h>

#include <cstdint>
#include <string>
#include <string_view>

namespace demo_entity
{
	enum class HostFuncId : uint32_t
	{
		SpawnEntity = 0xBCAA331Du,
		SetTransform = 0x20DA0B6Fu,
		DestroyEntity = 0xC7C1C59Cu,
	};

	enum class CoreFuncId : uint32_t
	{
	};

	struct HostArgs_SpawnEntity
	{
		uint64_t entityId;
		uint64_t prefabHandle;
		BridgeTransform transform;
		uint32_t flags;
	};

	struct HostArgs_SetTransform
	{
		uint64_t entityId;
		uint32_t mask;
		BridgeTransform transform;
	};

	struct HostArgs_DestroyEntity
	{
		uint64_t entityId;
	};

	// Core -> Host 调用（写入 command stream）
	inline void SpawnEntity(bridge::CoreContext& ctx, uint64_t entityId, uint64_t prefabHandle, BridgeTransform transform, uint32_t flags)
	{
		HostArgs_SpawnEntity a{};
		a.entityId = entityId;
		a.prefabHandle = prefabHandle;
		a.transform = transform;
		a.flags = flags;
		ctx.CallHost(static_cast<uint32_t>(HostFuncId::SpawnEntity), &a, static_cast<uint32_t>(sizeof(a)));
	}

	inline void SetTransform(bridge::CoreContext& ctx, uint64_t entityId, uint32_t mask, BridgeTransform transform)
	{
		HostArgs_SetTransform a{};
		a.entityId = entityId;
		a.mask = mask;
		a.transform = transform;
		ctx.CallHost(static_cast<uint32_t>(HostFuncId::SetTransform), &a, static_cast<uint32_t>(sizeof(a)));
	}

	inline void DestroyEntity(bridge::CoreContext& ctx, uint64_t entityId)
	{
		HostArgs_DestroyEntity a{};
		a.entityId = entityId;
		ctx.CallHost(static_cast<uint32_t>(HostFuncId::DestroyEntity), &a, static_cast<uint32_t>(sizeof(a)));
	}

} // namespace demo_entity
