using System;
using System.Collections;
using System.Collections.Generic;
using Bridge.Core;
using DemoAsset.Bindings;
using UnityEngine;

namespace BridgeDemoGame
{
    public sealed class DemoGameUnityAssetService : MonoBehaviour
    {
        private readonly Dictionary<ulong, string> _assetKeyIntern = new Dictionary<ulong, string>();
        private readonly Dictionary<string, PendingAssetLoad> _pending = new Dictionary<string, PendingAssetLoad>(StringComparer.Ordinal);
        private readonly Dictionary<string, ulong> _assetKeyToHandle = new Dictionary<string, ulong>(StringComparer.Ordinal);
        private readonly Dictionary<ulong, TextAsset> _handleToAsset = new Dictionary<ulong, TextAsset>();

        public bool TryGetTextAsset(ulong handle, out TextAsset asset)
        {
            return _handleToAsset.TryGetValue(handle, out asset);
        }

        public void RequestLoad(BridgeCore core, ulong requestId, BridgeAssetType assetType, BridgeStringView assetKey)
        {
            if (core == null)
                return;

            if (assetType != BridgeAssetType.Prefab)
            {
                core.AssetLoaded(requestId, 0, BridgeAssetStatus.Error);
                return;
            }

            if (assetKey.Ptr == 0 || assetKey.Len == 0)
            {
                core.AssetLoaded(requestId, 0, BridgeAssetStatus.NotFound);
                return;
            }

            string key = InternKey(assetKey);
            if (_assetKeyToHandle.TryGetValue(key, out ulong cachedHandle) && cachedHandle != 0)
            {
                core.AssetLoaded(requestId, cachedHandle, BridgeAssetStatus.Ok);
                return;
            }

            if (_pending.TryGetValue(key, out PendingAssetLoad pending))
            {
                pending.Waiters.Add(new PendingRequest(core, requestId));
                return;
            }

            pending = new PendingAssetLoad(key);
            pending.Waiters.Add(new PendingRequest(core, requestId));
            _pending.Add(key, pending);
            StartCoroutine(LoadCoroutine(pending));
        }

        private string InternKey(BridgeStringView key)
        {
            ulong hash = key.Fnv1a64();
            if (hash != 0 && _assetKeyIntern.TryGetValue(hash, out string cached) && !string.IsNullOrEmpty(cached))
                return cached;

            string s = key.ToManagedString();
            if (hash != 0 && !string.IsNullOrEmpty(s))
                _assetKeyIntern[hash] = s;
            return s;
        }

        private IEnumerator LoadCoroutine(PendingAssetLoad pending)
        {
            ResourceRequest req = Resources.LoadAsync<TextAsset>(pending.AssetKey);
            yield return req;

            TextAsset textAsset = req.asset as TextAsset;
            if (textAsset == null)
            {
                Complete(pending, handle: 0, BridgeAssetStatus.NotFound);
                yield break;
            }

            ulong handle = Fnv1a64(textAsset.bytes);
            if (handle == 0)
                handle = 1;

            _assetKeyToHandle[pending.AssetKey] = handle;
            _handleToAsset[handle] = textAsset;

            Complete(pending, handle, BridgeAssetStatus.Ok);
        }

        private void Complete(PendingAssetLoad pending, ulong handle, BridgeAssetStatus status)
        {
            _pending.Remove(pending.AssetKey);

            for (int i = 0; i < pending.Waiters.Count; i++)
            {
                PendingRequest r = pending.Waiters[i];
                try
                {
                    r.Core.AssetLoaded(r.RequestId, handle, status);
                }
                catch
                {
                    // Core 已销毁或异常时忽略（示例工程不做强保证）。
                }
            }
        }

        private static ulong Fnv1a64(byte[] bytes)
        {
            const ulong offset = 1469598103934665603ul;
            const ulong prime = 1099511628211ul;

            ulong hash = offset;
            for (int i = 0; i < bytes.Length; i++)
            {
                hash ^= bytes[i];
                hash *= prime;
            }
            return hash;
        }

        private sealed class PendingAssetLoad
        {
            public readonly string AssetKey;
            public readonly List<PendingRequest> Waiters = new List<PendingRequest>();

            public PendingAssetLoad(string assetKey)
            {
                AssetKey = assetKey;
            }
        }

        private readonly struct PendingRequest
        {
            public readonly BridgeCore Core;
            public readonly ulong RequestId;

            public PendingRequest(BridgeCore core, ulong requestId)
            {
                Core = core;
                RequestId = requestId;
            }
        }
    }
}
