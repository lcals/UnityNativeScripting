using System.Diagnostics;
using Bridge.Bindings;
using Bridge.Core;

static class Program
{
    private readonly struct RunResult
    {
        public readonly double ElapsedSeconds;
        public readonly long AllocatedBytes;

        public readonly ulong TotalCommands;
        public readonly ulong TotalAssetRequests;
        public readonly ulong TotalLogs;
        public readonly ulong TotalSpawns;
        public readonly ulong TotalTransforms;
        public readonly ulong TotalDestroys;

        public RunResult(
            double elapsedSeconds,
            long allocatedBytes,
            ulong totalCommands,
            ulong totalAssetRequests,
            ulong totalLogs,
            ulong totalSpawns,
            ulong totalTransforms,
            ulong totalDestroys)
        {
            ElapsedSeconds = elapsedSeconds;
            AllocatedBytes = allocatedBytes;
            TotalCommands = totalCommands;
            TotalAssetRequests = totalAssetRequests;
            TotalLogs = totalLogs;
            TotalSpawns = totalSpawns;
            TotalTransforms = totalTransforms;
            TotalDestroys = totalDestroys;
        }
    }

    private static int Main(string[] args)
    {
        int bots = Args.ReadInt(args, 0, 1000);
        int frames = Args.ReadInt(args, 1, 300);
        float dt = Args.ReadFloat(args, 2, 1.0f / 60.0f);

        string assetsRoot = Paths.FindDefaultAssetsRoot();
        string hostMode = "full";

        int idx = 3;
        if (args.Length > idx && !args[idx].StartsWith("--", StringComparison.Ordinal))
        {
            assetsRoot = Args.ReadString(args, idx, assetsRoot);
            idx++;
        }

        if (args.Length > idx && !args[idx].StartsWith("--", StringComparison.Ordinal))
        {
            hostMode = Args.ReadString(args, idx, hostMode);
            idx++;
        }

        assetsRoot = FindOption(args, "--assets") ??
                     FindOption(args, "--assetsRoot") ??
                     assetsRoot;

        hostMode = FindOption(args, "--host") ?? hostMode;

        NativeBridgeResolver.TryRegisterFromEnvOrDefault();

        bool nullHost = string.Equals(hostMode, "null", StringComparison.OrdinalIgnoreCase);

        Console.WriteLine($"RobotHost: bots={bots} frames={frames} dt={dt}");
        Console.WriteLine($"assetsRoot: {assetsRoot}");
        Console.WriteLine($"hostMode: {(nullHost ? "null" : "full")}");

        var r = Run(bots, frames, dt, assetsRoot, nullHost: nullHost);
        PrintRun("all", r);

        return 0;
    }

    private static string? FindOption(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }

    private static RunResult Run(int bots, int frames, float dt, string assetsRoot, bool nullHost)
    {
        var assetProvider = new FileAssetProvider(assetsRoot);
        _ = assetProvider.TryGetHandle("Main/Prefabs/Bot", out _);

        var cores = new BridgeCore[bots];

        if (nullHost)
        {
            var hosts = new RobotNullHostApi[bots];
            for (int i = 0; i < bots; i++)
            {
                var core = new BridgeCore(seed: (ulong)(i + 1), robotMode: true);
                cores[i] = core;
                hosts[i] = new RobotNullHostApi(core, assetProvider);
            }

            long allocBefore = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();

            try
            {
                for (int frame = 0; frame < frames; frame++)
                {
                    for (int i = 0; i < cores.Length; i++)
                    {
                        var core = cores[i];
                        var stream = core.TickAndGetCommandStream(dt);
                        BridgeAllCommandDispatcher.Dispatch(stream, hosts[i]);
                    }
                }
            }
            finally
            {
                sw.Stop();
                for (int i = 0; i < cores.Length; i++)
                    cores[i].Dispose();
            }

            long allocAfter = GC.GetAllocatedBytesForCurrentThread();

            ulong totalCommands = 0;
            ulong totalAssetRequests = 0;
            ulong totalLogs = 0;
            ulong totalSpawns = 0;
            ulong totalTransforms = 0;
            ulong totalDestroys = 0;

            for (int i = 0; i < hosts.Length; i++)
            {
                totalCommands += hosts[i].Commands;
                totalAssetRequests += hosts[i].AssetRequests;
                totalLogs += hosts[i].Logs;
                totalSpawns += hosts[i].Spawns;
                totalTransforms += hosts[i].Transforms;
                totalDestroys += hosts[i].Destroys;
            }

            return new RunResult(
                elapsedSeconds: sw.Elapsed.TotalSeconds,
                allocatedBytes: allocAfter - allocBefore,
                totalCommands: totalCommands,
                totalAssetRequests: totalAssetRequests,
                totalLogs: totalLogs,
                totalSpawns: totalSpawns,
                totalTransforms: totalTransforms,
                totalDestroys: totalDestroys);
        }
        else
        {
            var hosts = new RobotHostApi[bots];
            for (int i = 0; i < bots; i++)
            {
                var core = new BridgeCore(seed: (ulong)(i + 1), robotMode: true);
                cores[i] = core;

                var world = new WorldState();
                hosts[i] = new RobotHostApi(core, world, assetProvider);
            }

            long allocBefore = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();

            try
            {
                for (int frame = 0; frame < frames; frame++)
                {
                    for (int i = 0; i < cores.Length; i++)
                    {
                        var core = cores[i];
                        var stream = core.TickAndGetCommandStream(dt);
                        BridgeAllCommandDispatcher.Dispatch(stream, hosts[i]);
                    }
                }
            }
            finally
            {
                sw.Stop();
                for (int i = 0; i < cores.Length; i++)
                    cores[i].Dispose();
            }

            long allocAfter = GC.GetAllocatedBytesForCurrentThread();

            ulong totalCommands = 0;
            ulong totalAssetRequests = 0;
            ulong totalLogs = 0;
            ulong totalSpawns = 0;
            ulong totalTransforms = 0;
            ulong totalDestroys = 0;

            for (int i = 0; i < hosts.Length; i++)
            {
                totalCommands += hosts[i].Commands;
                totalAssetRequests += hosts[i].AssetRequests;
                totalLogs += hosts[i].Logs;
                totalSpawns += hosts[i].Spawns;
                totalTransforms += hosts[i].Transforms;
                totalDestroys += hosts[i].Destroys;
            }

            return new RunResult(
                elapsedSeconds: sw.Elapsed.TotalSeconds,
                allocatedBytes: allocAfter - allocBefore,
                totalCommands: totalCommands,
                totalAssetRequests: totalAssetRequests,
                totalLogs: totalLogs,
                totalSpawns: totalSpawns,
                totalTransforms: totalTransforms,
                totalDestroys: totalDestroys);
        }
    }

    private static void PrintRun(string label, RunResult r)
    {
        Console.WriteLine($"[{label}] elapsed: {r.ElapsedSeconds:F3} s");
        Console.WriteLine($"[{label}] allocated (thread): {r.AllocatedBytes} bytes");
        Console.WriteLine($"[{label}] total commands handled: {r.TotalCommands}");
        Console.WriteLine($"[{label}] commands/sec: {r.TotalCommands / Math.Max(1e-9, r.ElapsedSeconds):F0}");
        Console.WriteLine($"[{label}] total asset requests: {r.TotalAssetRequests}");
        Console.WriteLine($"[{label}] total logs: {r.TotalLogs}");
        Console.WriteLine($"[{label}] total spawns: {r.TotalSpawns}");
        Console.WriteLine($"[{label}] total transforms: {r.TotalTransforms}");
        Console.WriteLine($"[{label}] total destroys: {r.TotalDestroys}");
    }
}
