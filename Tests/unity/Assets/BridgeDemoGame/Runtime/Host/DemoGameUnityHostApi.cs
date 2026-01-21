using System.Collections.Generic;
using Bridge.Bindings;
using Bridge.Core;
using DemoAsset.Bindings;
using DemoEntity.Bindings;
using DemoLog.Bindings;
using UnityEngine;

namespace BridgeDemoGame
{
    public sealed partial class DemoGameUnityHostApi : BridgeAllHostApiBase
    {
        private readonly BridgeCore _core;
        private readonly DemoGameUnityAssetService _assets;
        private readonly bool _enableRendering;

        private readonly Dictionary<ulong, GameObject> _entities = new Dictionary<ulong, GameObject>();

        public ulong Commands { get; private set; }
        public ulong AssetRequests { get; private set; }
        public ulong Logs { get; private set; }
        public ulong Spawns { get; private set; }
        public ulong Transforms { get; private set; }
        public ulong Destroys { get; private set; }

        public DemoGameUnityHostApi(BridgeCore core, DemoGameUnityAssetService assets, bool enableRendering)
        {
            _core = core;
            _assets = assets;
            _enableRendering = enableRendering;
        }

        private static void ApplyTransform(Transform t, in BridgeTransform transform, uint mask)
        {
            if ((mask & 1u) != 0)
                t.position = new Vector3(transform.Position.X, transform.Position.Y, transform.Position.Z);

            if ((mask & 2u) != 0)
                t.rotation = new Quaternion(transform.Rotation.X, transform.Rotation.Y, transform.Rotation.Z, transform.Rotation.W);

            if ((mask & 4u) != 0)
                t.localScale = new Vector3(transform.Scale.X, transform.Scale.Y, transform.Scale.Z);
        }
    }
}
