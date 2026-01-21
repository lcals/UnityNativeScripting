#if !ENABLE_IL2CPP
using System;
using System.Diagnostics;
using Bridge.Bindings;
using Bridge.Core;
using DemoAsset.Bindings;
using DemoEntity.Bindings;
using DemoLog.Bindings;
using NUnit.Framework;
using Debug = UnityEngine.Debug;

namespace BridgeDemoGame.PlayModeTests
{
    public sealed class BridgeMonoGcTests
    {
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

            public void SpawnEntity(ulong entityId, ulong prefabHandle, in BridgeTransform transform, uint flags)
            {
                _ = entityId;
                _ = prefabHandle;
                _ = transform;
                _ = flags;
            }

            public void SetTransform(ulong entityId, uint mask, in BridgeTransform transform)
            {
                _ = entityId;
                _ = mask;
                _ = transform;
            }

            public void SetPosition(ulong entityId, BridgeVec3 position)
            {
                _ = entityId;
                _ = position;
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

        [Test]
        public void TickAndDispatch_NoGcAlloc_1Bot()
        {
            const int warmupFrames = 60;
            const int measureFrames = 600;
            const float dt = 1.0f / 60.0f;

            using (var core = new BridgeCore(seed: 1, robotMode: true))
            {
                var host = new NullHostApi(core);

                RunSingleCoreFrames(core, host, warmupFrames, dt, out _);

                CollectAndWait();
                long allocBefore = GC.GetAllocatedBytesForCurrentThread();

                var sw = Stopwatch.StartNew();
                RunSingleCoreFrames(core, host, measureFrames, dt, out ulong totalBytes);
                sw.Stop();

                long allocAfter = GC.GetAllocatedBytesForCurrentThread();
                long allocBytes = allocAfter - allocBefore;

                Debug.Log(string.Format(
                    "##bridgegc: mode=mono alloc_bytes={0} bots=1 frames={1} elapsed_ms={2:0.00} total_bytes={3}",
                    allocBytes,
                    measureFrames,
                    sw.Elapsed.TotalMilliseconds,
                    totalBytes));

                if (allocBytes != 0)
                    throw new Exception("GC allocated bytes != 0: " + allocBytes);
            }
        }

        [Test]
        public void TickManyAndDispatch_NoGcAlloc_10000Bots()
        {
            const int bots = 10000;
            const int warmupFrames = 10;
            const int measureFrames = 60;
            const float dt = 1.0f / 60.0f;

            BridgeCore.PrepareTickManyCache(bots);

            var cores = new BridgeCore[bots];
            var hosts = new NullHostApi[bots];
            var streams = new CommandStream[bots];

            for (int i = 0; i < bots; i++)
            {
                var core = new BridgeCore(seed: (ulong)(i + 1), robotMode: true);
                cores[i] = core;
                hosts[i] = new NullHostApi(core);
            }

            try
            {
                RunManyCoreFrames(cores, hosts, streams, warmupFrames, dt, out _);

                CollectAndWait();
                long allocBefore = GC.GetAllocatedBytesForCurrentThread();

                var sw = Stopwatch.StartNew();
                RunManyCoreFrames(cores, hosts, streams, measureFrames, dt, out ulong totalBytes);
                sw.Stop();

                long allocAfter = GC.GetAllocatedBytesForCurrentThread();
                long allocBytes = allocAfter - allocBefore;

                Debug.Log(string.Format(
                    "##bridgegc: mode=mono alloc_bytes={0} bots={1} frames={2} elapsed_ms={3:0.00} total_bytes={4}",
                    allocBytes,
                    bots,
                    measureFrames,
                    sw.Elapsed.TotalMilliseconds,
                    totalBytes));

                if (allocBytes != 0)
                    throw new Exception("GC allocated bytes != 0: " + allocBytes);
            }
            finally
            {
                for (int i = 0; i < bots; i++)
                    cores[i].Dispose();
            }
        }

        private static void RunSingleCoreFrames(BridgeCore core, NullHostApi host, int frames, float dt, out ulong totalBytes)
        {
            totalBytes = 0;
            for (int i = 0; i < frames; i++)
            {
                CommandStream stream = core.TickAndGetCommandStream(dt);
                totalBytes += stream.Length;
                BridgeAllCommandDispatcher.Dispatch(stream, host);
            }
        }

        private static void RunManyCoreFrames(
            BridgeCore[] cores,
            NullHostApi[] hosts,
            CommandStream[] streams,
            int frames,
            float dt,
            out ulong totalBytes)
        {
            totalBytes = 0;
            for (int frame = 0; frame < frames; frame++)
            {
                BridgeCore.TickManyAndGetCommandStreams(cores, dt, streams);
                for (int i = 0; i < cores.Length; i++)
                {
                    CommandStream stream = streams[i];
                    totalBytes += stream.Length;
                    BridgeAllCommandDispatcher.Dispatch(stream, hosts[i]);
                }
            }
        }

        private static void CollectAndWait()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
#endif
