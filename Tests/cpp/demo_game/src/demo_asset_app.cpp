#include "demo_asset_app.h"

#include <bridge/runtime/core_context.h>

#include <demo_asset_bindings.generated.h>
#include <demo_entity_bindings.generated.h>
#include <demo_log_bindings.generated.h>

#include <string>

namespace bridge
{
	namespace
	{
		// 最小示例 App（用于验证数据流）：
		// - 请求一个 Prefab 资源
		// - 资源加载完成后 Spawn 一个实体
		// - 每帧更新 Transform
		class DemoAssetApp final : public ICoreApp
		{
		public:
			void Tick(CoreContext& ctx, float dt) override
			{
				if (!startup_asset_requested_)
				{
					startup_asset_requested_ = true;
					startup_request_id_ = ctx.AllocRequestId();
					demo_log::Log(ctx, BRIDGE_LOG_INFO, "Requesting startup prefab asset");
					demo_asset::LoadAsset(ctx, startup_request_id_, BRIDGE_ASSET_PREFAB, "Main/Prefabs/Bot");
				}

				if (startup_asset_ready_ && !entity_spawned_)
				{
					entity_spawned_ = true;
					demo_entity::SpawnEntity(ctx, entity_id_, startup_asset_handle_, CoreContext::IdentityTransform(), /*flags*/ 0);
				}

				if (entity_spawned_)
				{
					t_ += dt;
					BridgeTransform tr = CoreContext::IdentityTransform();
					tr.position.x = t_;
					demo_entity::SetTransform(ctx, entity_id_, /*mask*/ 1u, tr);
				}
			}

			void OnCallCore(CoreContext& ctx, uint32_t funcId, const void* payload, uint32_t payloadSize) override
			{
				if (funcId != static_cast<uint32_t>(demo_asset::CoreFuncId::AssetLoaded) ||
				    payloadSize != sizeof(demo_asset::CoreArgs_AssetLoaded) ||
				    !payload)
				{
					return;
				}

				const auto& evt = *reinterpret_cast<const demo_asset::CoreArgs_AssetLoaded*>(payload);

				if (evt.requestId != startup_request_id_)
				{
					return;
				}
				if (evt.status != BRIDGE_ASSET_STATUS_OK)
				{
					demo_log::Log(ctx, BRIDGE_LOG_ERROR, "Startup asset failed to load");
					return;
				}

				startup_asset_ready_ = true;
				startup_asset_handle_ = evt.handle;
				demo_log::Log(ctx, BRIDGE_LOG_INFO, "Startup asset loaded");
			}

		private:
			uint64_t startup_request_id_ = 0;
			bool startup_asset_requested_ = false;
			bool startup_asset_ready_ = false;
			uint64_t startup_asset_handle_ = 0;

			bool entity_spawned_ = false;
			uint64_t entity_id_ = 1;

			float t_ = 0.0f;
		};
	}

	std::unique_ptr<ICoreApp> CreateDemoAssetApp()
	{
		return std::make_unique<DemoAssetApp>();
	}
}
