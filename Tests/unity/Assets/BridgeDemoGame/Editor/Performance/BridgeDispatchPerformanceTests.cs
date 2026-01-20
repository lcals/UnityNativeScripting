using Bridge.Bindings;
using Bridge.Core;
using Bridge.Core.Unity;
using DemoAsset.Bindings;
using DemoEntity.Bindings;
using DemoLog.Bindings;
using NUnit.Framework;
using Unity.PerformanceTesting;

namespace BridgeDemoGame.Tests
{
    public sealed class BridgeDispatchPerformanceTests
    {
        private static readonly SampleGroup AllocatedBytes = new SampleGroup("GC.Alloc.Bytes", SampleUnit.Byte, false);

        private sealed class NullHostApi : IDemoAssetHostApi, IDemoEntityHostApi, IDemoLogHostApi
        {
            private readonly BridgeCore _core;

            public NullHostApi(BridgeCore core)
            {
                _core = core;
            }

            public void LoadAsset(ulong requestId, BridgeAssetType assetType, BridgeStringView assetKey)
            {
                _ = assetType;
                _ = assetKey;
                _core.AssetLoaded(requestId, handle: 1, BridgeAssetStatus.Ok);
            }

            public void SpawnEntity(ulong entityId, ulong prefabHandle, BridgeTransform transform, uint flags)
            {
                _ = entityId;
                _ = prefabHandle;
                _ = transform;
                _ = flags;
            }

            public void SetTransform(ulong entityId, uint mask, BridgeTransform transform)
            {
                _ = entityId;
                _ = mask;
                _ = transform;
            }

            public void DestroyEntity(ulong entityId)
            {
                _ = entityId;
            }

            public void Log(BridgeLogLevel level, BridgeStringView message)
            {
                _ = level;
                _ = message;
            }
        }

        [Test, Performance]
        [TestCase(1)]
        [TestCase(1000)]
        public void EmptyLoop(int bots)
        {
            long allocBefore = 0;

            Measure.Method(() =>
                {
                    for (int i = 0; i < bots; i++)
                    {
                    }
                })
                .SetUp(() => allocBefore = System.GC.GetAllocatedBytesForCurrentThread())
                .CleanUp(() =>
                {
                    long allocAfter = System.GC.GetAllocatedBytesForCurrentThread();
                    Measure.Custom(AllocatedBytes, allocAfter - allocBefore);
                })
                .WarmupCount(5)
                .MeasurementCount(30)
                .IterationsPerMeasurement(1)
                .Run();
        }

        [Test, Performance]
        [TestCase(1)]
        [TestCase(1000)]
        public void TickAndDispatch_OneFrame(int bots)
        {
#if !UNITY_EDITOR_WIN
            Assert.Ignore("当前性能测试仅支持 Windows Editor（依赖 bridge_core.dll 的 Win 加载路径）。");
#else
            BridgeCoreWinLoader.TryEnsureLoaded();

            var cores = new BridgeCore[bots];
            var hosts = new NullHostApi[bots];

            for (int i = 0; i < bots; i++)
            {
                var core = new BridgeCore(seed: (ulong)(i + 1), robotMode: true);
                cores[i] = core;
                hosts[i] = new NullHostApi(core);
            }

            const float dt = 1.0f / 60.0f;
            long allocBefore = 0;

            try
            {
                Measure.Method(() =>
                    {
                        for (int i = 0; i < bots; i++)
                        {
                            var core = cores[i];
                            var stream = core.TickAndGetCommandStream(dt);
                            BridgeAllCommandDispatcher.Dispatch(stream, hosts[i]);
                        }
                    })
                    .SetUp(() => allocBefore = System.GC.GetAllocatedBytesForCurrentThread())
                    .CleanUp(() =>
                    {
                        long allocAfter = System.GC.GetAllocatedBytesForCurrentThread();
                        Measure.Custom(AllocatedBytes, allocAfter - allocBefore);
                    })
                    .WarmupCount(5)
                    .MeasurementCount(30)
                    .IterationsPerMeasurement(1)
                    .Run();
            }
            finally
            {
                for (int i = 0; i < bots; i++)
                    cores[i].Dispose();
            }
#endif
        }
    }
}
