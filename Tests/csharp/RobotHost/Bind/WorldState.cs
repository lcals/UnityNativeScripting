using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Bridge.Core;

sealed class WorldState
{
    private bool _hasSingleEntity;
    private ulong _singleEntityId;
    private Entity _singleEntity;

    private Dictionary<ulong, Entity>? _entities;

    public void OnLog(BridgeLogLevel level, BridgeStringView message)
    {
        _ = level;
        _ = message;
    }

    public void OnSpawn(ulong entityId, ulong prefabHandle, in BridgeTransform transform, uint flags)
    {
        _ = flags;

        if (_entities == null)
        {
            if (!_hasSingleEntity || entityId == _singleEntityId)
            {
                _hasSingleEntity = true;
                _singleEntityId = entityId;
                _singleEntity.PrefabHandle = prefabHandle;
                _singleEntity.Transform = transform;
                return;
            }

            EnsureEntities();
        }

        _entities![entityId] = new Entity(prefabHandle, transform);
    }

    public void OnSetTransform(ulong entityId, uint mask, in BridgeTransform transform)
    {
        if (_entities == null)
        {
            if (!_hasSingleEntity || entityId != _singleEntityId)
                return;

            var tr = _singleEntity.Transform;
            if ((mask & 1u) != 0) tr.Position = transform.Position;
            if ((mask & 2u) != 0) tr.Rotation = transform.Rotation;
            if ((mask & 4u) != 0) tr.Scale = transform.Scale;
            _singleEntity.Transform = tr;
            return;
        }

        ref Entity entity = ref CollectionsMarshal.GetValueRefOrNullRef(_entities, entityId);
        if (Unsafe.IsNullRef(ref entity))
            return;

        var tr2 = entity.Transform;
        if ((mask & 1u) != 0) tr2.Position = transform.Position;
        if ((mask & 2u) != 0) tr2.Rotation = transform.Rotation;
        if ((mask & 4u) != 0) tr2.Scale = transform.Scale;
        entity.Transform = tr2;
    }

    public void OnSetPosition(ulong entityId, BridgeVec3 position)
    {
        if (_entities == null)
        {
            if (!_hasSingleEntity || entityId != _singleEntityId)
                return;

            var tr = _singleEntity.Transform;
            tr.Position = position;
            _singleEntity.Transform = tr;
            return;
        }

        ref Entity entity = ref CollectionsMarshal.GetValueRefOrNullRef(_entities, entityId);
        if (Unsafe.IsNullRef(ref entity))
            return;

        var tr2 = entity.Transform;
        tr2.Position = position;
        entity.Transform = tr2;
    }

    public void OnDestroy(ulong entityId)
    {
        if (_entities == null)
        {
            if (_hasSingleEntity && entityId == _singleEntityId)
                _hasSingleEntity = false;
            return;
        }

        _entities.Remove(entityId);
    }

    private void EnsureEntities()
    {
        if (_entities != null)
            return;

        _entities = new Dictionary<ulong, Entity>(capacity: 4);
        _entities[_singleEntityId] = _singleEntity;
        _hasSingleEntity = false;
    }

    private struct Entity
    {
        public ulong PrefabHandle;
        public BridgeTransform Transform;

        public Entity(ulong prefabHandle, BridgeTransform transform)
        {
            PrefabHandle = prefabHandle;
            Transform = transform;
        }
    }
}
