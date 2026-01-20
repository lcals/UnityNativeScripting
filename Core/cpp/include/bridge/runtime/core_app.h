#pragma once

#include <bridge/bridge.h>

namespace bridge
{
	class CoreContext;

	// 可插拔的业务/玩法层（由业务库实现）。
	//
	// 说明：
	// - Runtime 只负责：稳定 ABI、命令流缓冲、事件接收与分发时序
	// - ICoreApp 决定每帧输出哪些“Host 命令”（例如请求资源、创建实体等）
	struct ICoreApp
	{
		virtual ~ICoreApp() = default;
		virtual void Tick(CoreContext& ctx, float dt) = 0;
		virtual void OnCallCore(CoreContext& ctx, uint32_t funcId, const void* payload, uint32_t payloadSize) = 0;
	};
}
