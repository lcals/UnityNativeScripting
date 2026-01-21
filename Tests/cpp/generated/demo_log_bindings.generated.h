#pragma once

#include <bridge/bridge.h>
#include <bridge/runtime/core_context.h>

#include <cstdint>
#include <string>
#include <string_view>

namespace demo_log
{
	enum class HostFuncId : uint32_t
	{
		Log = 0xDA3184A2u,
	};

	enum class CoreFuncId : uint32_t
	{
	};

	struct HostArgs_Log
	{
		BridgeLogLevel level;
		BridgeStringView message;
	};

	// Core -> Host 调用（写入 command stream）
	inline void Log(bridge::CoreContext& ctx, BridgeLogLevel level, std::string_view message)
	{
		HostArgs_Log a{};
		a.level = level;
		a.message = ctx.StoreUtf8(std::string(message));
		ctx.CallHost(static_cast<uint32_t>(HostFuncId::Log), &a, static_cast<uint32_t>(sizeof(a)));
	}

} // namespace demo_log
