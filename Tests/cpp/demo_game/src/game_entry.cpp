#include <bridge/runtime/game_entry.h>

#include "demo_asset_app.h"

namespace bridge
{
	std::unique_ptr<ICoreApp> CreateGameApp()
	{
		return CreateDemoAssetApp();
	}
}

