using Bridge.Core;
using DemoLog.Bindings;
using UnityEngine;

namespace BridgeDemoGame
{
    public sealed partial class DemoGameUnityHostApi
    {
        public override void Log(BridgeLogLevel level, BridgeStringView message)
        {
            Commands++;
            Logs++;

            if (!_enableRendering)
                return;

            string msg = message.ToManagedString();
            switch (level)
            {
                case BridgeLogLevel.Debug:
                    Debug.Log(msg);
                    break;
                case BridgeLogLevel.Info:
                    Debug.Log(msg);
                    break;
                case BridgeLogLevel.Warn:
                    Debug.LogWarning(msg);
                    break;
                default:
                    Debug.LogError(msg);
                    break;
            }
        }
    }
}
