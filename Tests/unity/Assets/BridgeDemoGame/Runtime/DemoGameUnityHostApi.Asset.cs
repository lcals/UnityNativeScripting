using Bridge.Core;
using DemoAsset.Bindings;

namespace BridgeDemoGame
{
    public sealed partial class DemoGameUnityHostApi : IDemoAssetHostApi
    {
        public void LoadAsset(ulong requestId, BridgeAssetType assetType, string assetKey)
        {
            Commands++;
            AssetRequests++;

            _assets.RequestLoad(_core, requestId, assetType, assetKey);
        }
    }
}