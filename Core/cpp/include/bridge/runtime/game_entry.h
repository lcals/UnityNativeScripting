#pragma once

#include <bridge/runtime/core_app.h>

#include <memory>

namespace bridge
{
	// 业务层入口：由最终链接产物（业务库/插件）提供实现。
	// Runtime 在 BridgeCore_Create 时调用该函数创建 ICoreApp。
	std::unique_ptr<ICoreApp> CreateGameApp();
}

