using Bridge.Core;
using DemoEntity.Bindings;
using UnityEngine;

namespace BridgeDemoGame
{
    public sealed partial class DemoGameUnityHostApi : IDemoEntityHostApi
    {
        public void SpawnEntity(ulong entityId, ulong prefabHandle, BridgeTransform transform, uint flags)
        {
            _ = prefabHandle;
            _ = flags;

            Commands++;
            Spawns++;

            if (!_enableRendering)
                return;

            if (_entities.TryGetValue(entityId, out GameObject existing) && existing != null)
            {
                Object.Destroy(existing);
                _entities.Remove(entityId);
            }

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Entity_" + entityId;
            ApplyTransform(go.transform, transform, mask: 0x7u);
            _entities[entityId] = go;
        }

        public void SetTransform(ulong entityId, uint mask, BridgeTransform transform)
        {
            Commands++;
            Transforms++;

            if (!_enableRendering)
                return;

            if (_entities.TryGetValue(entityId, out GameObject go) && go != null)
                ApplyTransform(go.transform, transform, mask);
        }

        public void DestroyEntity(ulong entityId)
        {
            Commands++;
            Destroys++;

            if (!_enableRendering)
                return;

            if (_entities.TryGetValue(entityId, out GameObject go))
            {
                if (go != null)
                    Object.Destroy(go);
                _entities.Remove(entityId);
            }
        }
    }
}