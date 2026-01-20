#pragma once

#include <bridge/runtime/core_app.h>

#include <memory>

namespace bridge
{
	std::unique_ptr<ICoreApp> CreateDemoAssetApp();
}
