using Bridge.Core;
using DemoAsset.Bindings;

sealed class RobotNullHostApi : IRobotHostApi
{
    private readonly BridgeCore _core;

    public ulong Commands { get; private set; }
    public ulong AssetRequests { get; private set; }
    public ulong Logs { get; private set; }
    public ulong Spawns { get; private set; }
    public ulong Transforms { get; private set; }
    public ulong Destroys { get; private set; }

    public RobotNullHostApi(BridgeCore core, FileAssetProvider assets)
    {
        _core = core;
        _ = assets;
    }

    public void Log(BridgeLogLevel level, BridgeStringView message)
    {
        _ = level;
        _ = message;
        Commands++;
        Logs++;
    }

    public void LoadAsset(ulong requestId, BridgeAssetType assetType, BridgeStringView assetKey)
    {
        _ = assetType;

        Commands++;
        AssetRequests++;

        ulong handle = assetKey.Fnv1a64();
        if (handle == 0)
            handle = 1;
        _core.AssetLoaded(requestId, handle, BridgeAssetStatus.Ok);
    }

    public void SpawnEntity(ulong entityId, ulong prefabHandle, BridgeTransform transform, uint flags)
    {
        _ = entityId;
        _ = prefabHandle;
        _ = transform;
        _ = flags;
        Commands++;
        Spawns++;
    }

    public void SetTransform(ulong entityId, uint mask, BridgeTransform transform)
    {
        _ = entityId;
        _ = mask;
        _ = transform;
        Commands++;
        Transforms++;
    }

    public void DestroyEntity(ulong entityId)
    {
        _ = entityId;
        Commands++;
        Destroys++;
    }
}
