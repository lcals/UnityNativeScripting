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
        private IntPtr[] _coreHandles = Array.Empty<IntPtr>();
        private DemoGameUnityHostApi[] _hosts = Array.Empty<DemoGameUnityHostApi>();
        private CommandStream[] _streams = Array.Empty<CommandStream>();
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
            BridgeCore.PrepareTickManyCache(bots);
            _cores = new BridgeCore[bots];
            _coreHandles = new IntPtr[bots];
            _hosts = new DemoGameUnityHostApi[bots];
            _streams = new CommandStream[bots];

            bool render = EnableRendering && bots <= MaxBotsWithRendering;
            for (int i = 0; i < bots; i++)
            {
                var core = new BridgeCore(seed: (ulong)(i + 1), robotMode: RobotMode);
                _cores[i] = core;
                _coreHandles[i] = core.UnsafeHandle;
                _hosts[i] = new DemoGameUnityHostApi(core, _assets, render);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            BridgeCore.TickManyAndGetCommandStreams(_coreHandles, dt, _streams);
            for (int i = 0; i < _cores.Length; i++)
            {
#if ENABLE_IL2CPP
                BridgeAllCommandDispatcher.DispatchFastUnchecked(_streams[i], _hosts[i]);
#else
                BridgeAllCommandDispatcher.Dispatch(_streams[i], _hosts[i]);
#endif
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _cores.Length; i++)
            {
                try { _cores[i].Dispose(); } catch { }
            }
            _cores = Array.Empty<BridgeCore>();
            _coreHandles = Array.Empty<IntPtr>();
            _hosts = Array.Empty<DemoGameUnityHostApi>();
            _streams = Array.Empty<CommandStream>();
        }
    }
}
