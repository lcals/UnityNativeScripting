using Bridge.Core;
using DemoAsset.Bindings;

sealed class RobotNullHostApi : IRobotHostApi
{
    private readonly BridgeCore _core;
    private readonly FileAssetProvider _assets;

    public ulong Commands { get; private set; }
    public ulong AssetRequests { get; private set; }
    public ulong Logs { get; private set; }
    public ulong Spawns { get; private set; }
    public ulong Transforms { get; private set; }
    public ulong Destroys { get; private set; }

    public RobotNullHostApi(BridgeCore core, FileAssetProvider assets)
    {
        _core = core;
        _assets = assets;
    }

    public void Log(BridgeLogLevel level, string message)
    {
        _ = level;
        _ = message;
        Commands++;
        Logs++;
    }

    public void LoadAsset(ulong requestId, BridgeAssetType assetType, string assetKey)
    {
        _ = assetType;

        Commands++;
        AssetRequests++;

        if (_assets.TryGetHandle(assetKey, out ulong handle))
            _core.AssetLoaded(requestId, handle, BridgeAssetStatus.Ok);
        else
            _core.AssetLoaded(requestId, 0, BridgeAssetStatus.NotFound);
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
