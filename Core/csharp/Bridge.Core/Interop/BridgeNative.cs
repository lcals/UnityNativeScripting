using System;
using System.Runtime.InteropServices;

namespace Bridge.Core
{
    internal static class BridgeNative
    {
        private const string LibraryName = "bridge_core";

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern BridgeVersion Bridge_GetVersion();

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr BridgeCore_Create(BridgeCoreConfig config);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void BridgeCore_Destroy(IntPtr core);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void BridgeCore_Tick(IntPtr core, float dt);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern BridgeResult BridgeCore_TickAndGetCommandStream(
            IntPtr core,
            float dt,
            out IntPtr ptr,
            out uint len);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern BridgeResult BridgeCore_GetCommandStream(
            IntPtr core,
            out IntPtr ptr,
            out uint len);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern BridgeResult BridgeCore_PushCallCore(
            IntPtr core,
            uint funcId,
            IntPtr payload,
            uint payloadSize);
    }
}
