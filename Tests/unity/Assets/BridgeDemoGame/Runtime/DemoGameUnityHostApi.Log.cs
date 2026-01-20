using Bridge.Core;
using DemoLog.Bindings;
using UnityEngine;

namespace BridgeDemoGame
{
    public sealed partial class DemoGameUnityHostApi: IDemoLogHostApi
    {
        public void Log(BridgeLogLevel level, string message)
        {
            Commands++;
            Logs++;

            switch (level)
            {
                case BridgeLogLevel.Debug:
                    Debug.Log(message);
                    break;
                case BridgeLogLevel.Info:
                    Debug.Log(message);
                    break;
                case BridgeLogLevel.Warn:
                    Debug.LogWarning(message);
                    break;
                default:
                    Debug.LogError(message);
                    break;
            }
        }
    }
}

