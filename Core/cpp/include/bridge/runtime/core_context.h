#pragma once

#include <bridge/bridge.h>

#include <cstdint>
#include <string>

struct BridgeCore;

namespace bridge
{
	// 业务层在 Tick/事件回调中使用的上下文对象（由 Runtime 创建并传入）。
	//
	// 该类型属于 C++ 侧“业务/Runtime 接口”，不属于对外 C ABI（bridge.h）。
	class CoreContext
	{
	public:
		explicit CoreContext(BridgeCore& core);

		const BridgeCoreConfig& Config() const;
		uint64_t AllocRequestId();

		BridgeStringView StoreUtf8(std::string utf8);

		// 向 Host 发起一次“函数调用”（具体 func_id 与 payload 结构由代码生成定义）。
		// payload 会被复制进 command stream，且 header.size 按 8 字节补齐。
		void CallHost(uint32_t funcId, const void* payload, uint32_t payloadSize);

		static BridgeTransform IdentityTransform();

	private:
		BridgeCore& core_;
	};
}
