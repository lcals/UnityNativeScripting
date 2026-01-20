using System.Collections.Generic;
using Bridge.Core;

sealed class WorldState
{
    private readonly Dictionary<ulong, Entity> _entities = new(capacity: 4);

    public void OnLog(BridgeLogLevel level, BridgeStringView message)
    {
        _ = level;
        _ = message;
    }

    public void OnSpawn(ulong entityId, ulong prefabHandle, BridgeTransform transform, uint flags)
    {
        _ = flags;
        _entities[entityId] = new Entity(prefabHandle, transform);
    }

    public void OnSetTransform(ulong entityId, uint mask, BridgeTransform transform)
    {
        if (_entities.TryGetValue(entityId, out var entity))
        {
            var tr = entity.Transform;
            if ((mask & 1u) != 0) tr.Position = transform.Position;
            if ((mask & 2u) != 0) tr.Rotation = transform.Rotation;
            if ((mask & 4u) != 0) tr.Scale = transform.Scale;
            _entities[entityId] = new Entity(entity.PrefabHandle, tr);
        }
    }

    public void OnDestroy(ulong entityId)
    {
        _entities.Remove(entityId);
    }

    private readonly struct Entity
    {
        public readonly ulong PrefabHandle;
        public readonly BridgeTransform Transform;

        public Entity(ulong prefabHandle, BridgeTransform transform)
        {
            PrefabHandle = prefabHandle;
            Transform = transform;
        }
    }
}
