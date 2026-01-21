using Bridge.Core;
using DemoAsset.Bindings;

namespace BridgeDemoGame
{
    public sealed partial class DemoGameUnityHostApi
    {
        public override void LoadAsset(ulong requestId, BridgeAssetType assetType, BridgeStringView assetKey)
        {
            Commands++;
            AssetRequests++;

            _assets.RequestLoad(_core, requestId, assetType, assetKey);
        }
    }
}
