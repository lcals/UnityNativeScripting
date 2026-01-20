using System;
using System.Runtime.InteropServices;

#if UNITY_EDITOR_WIN
using Bridge.Core.Unity;
#endif

namespace Bridge.Core
{
    internal static class BridgeNative
    {
#if UNITY_EDITOR_WIN
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr libraryHandle, string symbolName);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate BridgeVersion Bridge_GetVersionDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr BridgeCore_CreateDelegate(BridgeCoreConfig config);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void BridgeCore_DestroyDelegate(IntPtr core);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void BridgeCore_TickDelegate(IntPtr core, float dt);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate BridgeResult BridgeCore_TickAndGetCommandStreamDelegate(IntPtr core, float dt, out IntPtr ptr, out uint len);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate BridgeResult BridgeCore_GetCommandStreamDelegate(IntPtr core, out IntPtr ptr, out uint len);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate BridgeResult BridgeCore_PushCallCoreDelegate(IntPtr core, uint funcId, IntPtr payload, uint payloadSize);

        private static IntPtr s_boundModule;
        private static Bridge_GetVersionDelegate s_getVersion;
        private static BridgeCore_CreateDelegate s_create;
        private static BridgeCore_DestroyDelegate s_destroy;
        private static BridgeCore_TickDelegate s_tick;
        private static BridgeCore_TickAndGetCommandStreamDelegate s_tickAndGetCommandStream;
        private static BridgeCore_GetCommandStreamDelegate s_getCommandStream;
        private static BridgeCore_PushCallCoreDelegate s_pushCallCore;

        private static void EnsureBound()
        {
            BridgeCoreWinLoader.TryEnsureLoaded();

            IntPtr module = BridgeCoreWinLoader.GetLoadedModuleHandle();
            if (module == IntPtr.Zero)
                throw new DllNotFoundException("bridge_core.dll 未加载（请先编译 build/bin/Release/bridge_core.dll）");

            if (module == s_boundModule)
                return;

            s_getVersion = GetDelegate<Bridge_GetVersionDelegate>(module, "Bridge_GetVersion");
            s_create = GetDelegate<BridgeCore_CreateDelegate>(module, "BridgeCore_Create");
            s_destroy = GetDelegate<BridgeCore_DestroyDelegate>(module, "BridgeCore_Destroy");
            s_tick = GetDelegate<BridgeCore_TickDelegate>(module, "BridgeCore_Tick");
            s_tickAndGetCommandStream = GetDelegate<BridgeCore_TickAndGetCommandStreamDelegate>(module, "BridgeCore_TickAndGetCommandStream");
            s_getCommandStream = GetDelegate<BridgeCore_GetCommandStreamDelegate>(module, "BridgeCore_GetCommandStream");
            s_pushCallCore = GetDelegate<BridgeCore_PushCallCoreDelegate>(module, "BridgeCore_PushCallCore");
            s_boundModule = module;
        }

        private static T GetDelegate<T>(IntPtr libraryHandle, string functionName) where T : class
        {
            IntPtr symbol = GetProcAddress(libraryHandle, functionName);
            if (symbol == IntPtr.Zero)
                throw new MissingMethodException("bridge_core.dll", functionName);

            return Marshal.GetDelegateForFunctionPointer(symbol, typeof(T)) as T;
        }

        internal static BridgeVersion Bridge_GetVersion()
        {
            EnsureBound();
            return s_getVersion();
        }

        internal static IntPtr BridgeCore_Create(BridgeCoreConfig config)
        {
            EnsureBound();
            return s_create(config);
        }

        internal static void BridgeCore_Destroy(IntPtr core)
        {
            EnsureBound();
            s_destroy(core);
        }

        internal static void BridgeCore_Tick(IntPtr core, float dt)
        {
            EnsureBound();
            s_tick(core, dt);
        }

        internal static BridgeResult BridgeCore_TickAndGetCommandStream(IntPtr core, float dt, out IntPtr ptr, out uint len)
        {
            EnsureBound();
            return s_tickAndGetCommandStream(core, dt, out ptr, out len);
        }

        internal static BridgeResult BridgeCore_GetCommandStream(IntPtr core, out IntPtr ptr, out uint len)
        {
            EnsureBound();
            return s_getCommandStream(core, out ptr, out len);
        }

        internal static BridgeResult BridgeCore_PushCallCore(IntPtr core, uint funcId, IntPtr payload, uint payloadSize)
        {
            EnsureBound();
            return s_pushCallCore(core, funcId, payload, payloadSize);
        }
#else
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
#endif
    }
}
