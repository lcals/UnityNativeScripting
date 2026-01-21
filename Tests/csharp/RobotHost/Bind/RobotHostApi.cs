using Bridge.Core;
using DemoAsset.Bindings;

sealed class RobotHostApi : IRobotHostApi
{
    private readonly BridgeCore _core;
    private readonly WorldState _world;
    private readonly FileAssetProvider _assets;

    public ulong Commands { get; private set; }
    public ulong AssetRequests { get; private set; }
    public ulong Logs { get; private set; }
    public ulong Spawns { get; private set; }
    public ulong Transforms { get; private set; }
    public ulong Destroys { get; private set; }

    public RobotHostApi(BridgeCore core, WorldState world, FileAssetProvider assets)
    {
        _core = core;
        _world = world;
        _assets = assets;
    }

    public void Log(BridgeLogLevel level, BridgeStringView message)
    {
        Commands++;
        Logs++;
        _world.OnLog(level, message);
    }

    public void LoadAsset(ulong requestId, BridgeAssetType assetType, BridgeStringView assetKey)
    {
        _ = assetType;

        Commands++;
        AssetRequests++;

        if (_assets.TryGetHandle(assetKey, out ulong handle))
            _core.AssetLoaded(requestId, handle, BridgeAssetStatus.Ok);
        else
            _core.AssetLoaded(requestId, 0, BridgeAssetStatus.NotFound);
    }

    public void SpawnEntity(ulong entityId, ulong prefabHandle, in BridgeTransform transform, uint flags)
    {
        Commands++;
        Spawns++;
        _world.OnSpawn(entityId, prefabHandle, in transform, flags);
    }

    public void SetTransform(ulong entityId, uint mask, in BridgeTransform transform)
    {
        Commands++;
        Transforms++;
        _world.OnSetTransform(entityId, mask, in transform);
    }

    public void DestroyEntity(ulong entityId)
    {
        Commands++;
        Destroys++;
        _world.OnDestroy(entityId);
    }
}
