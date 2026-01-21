using System;
using System.Diagnostics;
using Bridge.Bindings;
using Bridge.Core;
using DemoAsset.Bindings;
using DemoEntity.Bindings;
using DemoLog.Bindings;
using NUnit.Framework;
using Debug = UnityEngine.Debug;

namespace BridgeDemoGame.Tests
{
    public sealed class BridgeSourceModeThroughputTests
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
        public void TickManyAndDispatch_Throughput_1Bot()
        {
            RunThroughput(bots: 1);
        }

        [Test]
        public void TickManyAndDispatch_Throughput_1kBots()
        {
            RunThroughput(bots: 1000);
        }

        [Test]
        public void TickManyAndDispatch_Throughput_10kBots()
        {
            RunThroughput(bots: 10000);
        }

        private static void RunThroughput(int bots)
        {
            if (bots <= 0)
                throw new ArgumentOutOfRangeException(nameof(bots));

            const int warmupFrames = 60;
            const int measureFrames = 300;
            const float dt = 1.0f / 60.0f;

            BridgeCore.PrepareTickManyCache(bots);

            Debug.Log("BridgeSourceModeThroughputTests: start bots=" + bots);

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
                RunFrames(cores, hosts, streams, warmupFrames, dt, out _);

                bool allocSupported = false;
                long allocBefore = 0;
#if !ENABLE_IL2CPP || UNITY_EDITOR
                allocBefore = TryGetAllocatedBytesForCurrentThread(out allocSupported);
#endif
                var sw = Stopwatch.StartNew();
                RunFrames(cores, hosts, streams, measureFrames, dt, out ulong totalBytes);
                sw.Stop();

                long allocAfter = 0;
#if !ENABLE_IL2CPP || UNITY_EDITOR
                allocAfter = allocSupported ? TryGetAllocatedBytesForCurrentThread(out _) : 0;
#endif

                double seconds = Math.Max(1e-9, sw.Elapsed.TotalSeconds);
                double ticks = (double)bots * measureFrames;
                double ticksPerSecond = ticks / seconds;
                double mibPerSecond = (totalBytes / (1024.0 * 1024.0)) / seconds;
                long allocBytes = allocSupported ? (allocAfter - allocBefore) : -1;

                Debug.Log(string.Format(
                    "##bridgeperf: mode=il2cpp_source ticks_per_sec={0:0.00} mib_per_sec={1:0.00} bots={2} frames={3} elapsed_ms={4:0.00} alloc_bytes={5} total_bytes={6}",
                    ticksPerSecond,
                    mibPerSecond,
                    bots,
                    measureFrames,
                    sw.Elapsed.TotalMilliseconds,
                    allocBytes,
                    totalBytes));

                Debug.Log("BridgeSourceModeThroughputTests: done bots=" + bots);
            }
            finally
            {
                for (int i = 0; i < bots; i++)
                    cores[i].Dispose();
            }
        }

        private static void RunFrames(
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

        private static long TryGetAllocatedBytesForCurrentThread(out bool supported)
        {
            try
            {
                supported = true;
                return GC.GetAllocatedBytesForCurrentThread();
            }
            catch
            {
                supported = false;
                return 0;
            }
        }
    }
}
