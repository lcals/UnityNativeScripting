using System.Collections;
using Bridge.Bindings;
using Bridge.Core;
using Bridge.Core.Unity;
using DemoAsset.Bindings;
using DemoEntity.Bindings;
using DemoLog.Bindings;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace BridgeDemoGame.PlayModeTests
{
    public sealed class BridgeRuntimeSmokeTests
    {
        private sealed class NullHostApi : BridgeAllHostApiBase
        {
            private readonly BridgeCore _core;

            public NullHostApi(BridgeCore core)
            {
                _core = core;
            }

            public override void LoadAsset(ulong requestId, BridgeAssetType assetType, BridgeStringView assetKey)
            {
                _ = assetType;
                _ = assetKey;
                _core.AssetLoaded(requestId, handle: 1, BridgeAssetStatus.Ok);
            }

            public override void SpawnEntity(ulong entityId, ulong prefabHandle, in BridgeTransform transform, uint flags)
            {
                _ = entityId;
                _ = prefabHandle;
                _ = transform;
                _ = flags;
            }

            public override void SetTransform(ulong entityId, uint mask, in BridgeTransform transform)
            {
                _ = entityId;
                _ = mask;
                _ = transform;
            }

            public override void SetPosition(ulong entityId, BridgeVec3 position)
            {
                _ = entityId;
                _ = position;
            }

            public override void DestroyEntity(ulong entityId)
            {
                _ = entityId;
            }

            public override void Log(BridgeLogLevel level, BridgeStringView message)
            {
                _ = level;
                _ = message;
            }
        }

        [UnityTest]
        public IEnumerator TickAndDispatch_60Frames_NoException()
        {
#if UNITY_EDITOR_WIN
            BridgeCoreWinLoader.TryEnsureLoaded();
#endif
            using (var core = new BridgeCore(seed: 1, robotMode: true))
            {
                var host = new NullHostApi(core);
                for (int i = 0; i < 60; i++)
                {
                    var stream = core.TickAndGetCommandStream(1.0f / 60.0f);
#if ENABLE_IL2CPP
                    BridgeAllCommandDispatcher.DispatchFast(stream, host);
#else
                    BridgeAllCommandDispatcher.Dispatch(stream, host);
#endif
                    yield return null;
                }
            }
        }
    }
}
