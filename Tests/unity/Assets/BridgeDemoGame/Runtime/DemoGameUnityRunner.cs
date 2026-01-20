using System;
using Bridge.Bindings;
using Bridge.Core;
using UnityEngine;

namespace BridgeDemoGame
{
    public sealed class DemoGameUnityRunner : MonoBehaviour
    {
        [Header("Core")]
        public int Bots = 1;
        public bool RobotMode = false;

        [Header("Host")]
        public bool EnableRendering = true;
        public int MaxBotsWithRendering = 32;

        private BridgeCore[] _cores = Array.Empty<BridgeCore>();
        private DemoGameUnityHostApi[] _hosts = Array.Empty<DemoGameUnityHostApi>();
        private DemoGameUnityAssetService _assets;

        private void Awake()
        {
            _assets = GetComponent<DemoGameUnityAssetService>();
            if (_assets == null)
                _assets = gameObject.AddComponent<DemoGameUnityAssetService>();
        }

        private void Start()
        {
            int bots = Mathf.Max(1, Bots);
            _cores = new BridgeCore[bots];
            _hosts = new DemoGameUnityHostApi[bots];

            bool render = EnableRendering && bots <= MaxBotsWithRendering;
            for (int i = 0; i < bots; i++)
            {
                var core = new BridgeCore(seed: (ulong)(i + 1), robotMode: RobotMode);
                _cores[i] = core;
                _hosts[i] = new DemoGameUnityHostApi(core, _assets, render);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < _cores.Length; i++)
            {
                BridgeCore core = _cores[i];
                CommandStream stream = core.TickAndGetCommandStream(dt);
                BridgeAllCommandDispatcher.Dispatch(stream, _hosts[i]);
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _cores.Length; i++)
            {
                try { _cores[i].Dispose(); } catch { }
            }
            _cores = Array.Empty<BridgeCore>();
            _hosts = Array.Empty<DemoGameUnityHostApi>();
        }
    }
}
