#pragma once

#include <bridge/bridge.h>
#include <bridge/runtime/core_context.h>

#include <cstdint>
#include <string>
#include <string_view>

namespace demo_asset
{
	enum class HostFuncId : uint32_t
	{
		LoadAsset = 0x82A5E93Au,
	};

	enum class CoreFuncId : uint32_t
	{
		AssetLoaded = 0x2442BC8Au,
	};

	struct HostArgs_LoadAsset
	{
		uint64_t requestId;
		BridgeAssetType assetType;
		BridgeStringView assetKey;
	};

	struct CoreArgs_AssetLoaded
	{
		uint64_t requestId;
		uint64_t handle;
		BridgeAssetStatus status;
	};

	// Core -> Host 调用（写入 command stream）
	inline void LoadAsset(bridge::CoreContext& ctx, uint64_t requestId, BridgeAssetType assetType, std::string_view assetKey)
	{
		HostArgs_LoadAsset a{};
		a.requestId = requestId;
		a.assetType = assetType;
		a.assetKey = ctx.StoreUtf8(std::string(assetKey));
		ctx.CallHost(static_cast<uint32_t>(HostFuncId::LoadAsset), &a, static_cast<uint32_t>(sizeof(a)));
	}

} // namespace demo_asset
