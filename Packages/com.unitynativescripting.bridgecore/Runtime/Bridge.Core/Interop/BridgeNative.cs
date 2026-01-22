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
        // Windows Editor 下使用 “Copy-Then-Load + GetProcAddress” 模式：
        // - 避免 Unity 自动加载/锁定固定 DLL 路径
        // - 支持重编译后覆盖源 DLL，再热重载
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
        private unsafe delegate BridgeResult BridgeCore_TickManyAndGetCommandStreamsDelegate(
            IntPtr* cores,
            uint count,
            float dt,
            CommandStream* outStreams);

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
        private static BridgeCore_TickManyAndGetCommandStreamsDelegate s_tickManyAndGetCommandStreams;
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
            s_tickManyAndGetCommandStreams = GetDelegate<BridgeCore_TickManyAndGetCommandStreamsDelegate>(module, "BridgeCore_TickManyAndGetCommandStreams");
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

        internal static unsafe BridgeResult BridgeCore_TickManyAndGetCommandStreams(
            IntPtr* cores,
            uint count,
            float dt,
            CommandStream* outStreams)
        {
            EnsureBound();
            return s_tickManyAndGetCommandStreams(cores, count, dt, outStreams);
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
#if ENABLE_IL2CPP && !UNITY_EDITOR
        // IL2CPP Player 下如果把 C++ 以“源码插件”编进 GameAssembly.dll，应使用 __Internal 走内部符号解析，避免运行时动态加载 bridge_core.dll。
        private const string LibraryName = "__Internal";
#else
        private const string LibraryName = "bridge_core";
#endif

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
        internal static extern unsafe BridgeResult BridgeCore_TickManyAndGetCommandStreams(
            IntPtr* cores,
            uint count,
            float dt,
            CommandStream* outStreams);

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
