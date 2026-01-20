using System;

namespace Bridge.Core
{
    /// <summary>
    /// 原生 <c>BridgeCore</c> 的托管封装（Host 侧入口）。
    /// </summary>
    public sealed class BridgeCore : IDisposable
    {
        private IntPtr _handle;

        public BridgeCore(ulong seed = 1, bool robotMode = false)
        {
            var cfg = new BridgeCoreConfig
            {
                Seed = seed,
                Mode = (uint)(robotMode ? BridgeMode.Robot : BridgeMode.Game)
            };

            _handle = BridgeNative.BridgeCore_Create(cfg);
            if (_handle == IntPtr.Zero)
                throw new InvalidOperationException("BridgeCore_Create returned null");
        }

        /// <summary>
        /// 推进 Core 一帧（或一个逻辑 tick）。
        /// </summary>
        public void Tick(float dt)
        {
            ThrowIfDisposed();
            BridgeNative.BridgeCore_Tick(_handle, dt);
        }

        /// <summary>
        /// 推进 Core 一帧，并直接返回本帧生成的命令字节流（减少一次 P/Invoke）。
        /// </summary>
        public CommandStream TickAndGetCommandStream(float dt)
        {
            ThrowIfDisposed();
            var result = BridgeNative.BridgeCore_TickAndGetCommandStream(_handle, dt, out var ptr, out var len);
            if (result != BridgeResult.Ok || ptr == IntPtr.Zero || len == 0)
                return CommandStream.Empty;

            return new CommandStream(ptr, len);
        }

        public static unsafe void TickManyAndGetCommandStreams(BridgeCore[] cores, float dt, CommandStream[] streams)
        {
            if (cores == null)
                throw new ArgumentNullException(nameof(cores));
            if (streams == null)
                throw new ArgumentNullException(nameof(streams));
            if (streams.Length < cores.Length)
                throw new ArgumentException("streams.Length must be >= cores.Length", nameof(streams));

            int count = cores.Length;
            if (count == 0)
                return;

            IntPtr* corePtrs = stackalloc IntPtr[count];
            for (int i = 0; i < count; i++)
            {
                BridgeCore core = cores[i] ?? throw new ArgumentNullException(nameof(cores), $"cores[{i}] is null");
                core.ThrowIfDisposed();
                corePtrs[i] = core._handle;
            }

            IntPtr* outPtrs = stackalloc IntPtr[count];
            uint* outLens = stackalloc uint[count];

            var result = BridgeNative.BridgeCore_TickManyAndGetCommandStreams(corePtrs, (uint)count, dt, outPtrs, outLens);
            if (result != BridgeResult.Ok)
                throw new InvalidOperationException($"BridgeCore_TickManyAndGetCommandStreams failed: {result}");

            for (int i = 0; i < count; i++)
            {
                IntPtr ptr = outPtrs[i];
                uint len = outLens[i];
                streams[i] = (ptr == IntPtr.Zero || len == 0) ? CommandStream.Empty : new CommandStream(ptr, len);
            }
        }

        /// <summary>
        /// 获取最近一次 <see cref="Tick"/> 生成的命令字节流（command stream）。
        /// </summary>
        /// <remarks>
        /// 返回指针由原生侧持有，只保证在下一次 <see cref="Tick"/>（或 <see cref="Dispose"/>）前有效。
        /// </remarks>
        public CommandStream GetCommandStream()
        {
            ThrowIfDisposed();
            var result = BridgeNative.BridgeCore_GetCommandStream(_handle, out var ptr, out var len);
            if (result != BridgeResult.Ok || ptr == IntPtr.Zero || len == 0)
                return CommandStream.Empty;

            return new CommandStream(ptr, len);
        }

        public void PushCallCore(uint funcId)
        {
            ThrowIfDisposed();
            BridgeNative.BridgeCore_PushCallCore(_handle, funcId, IntPtr.Zero, 0);
        }

        public unsafe void PushCallCore<T>(uint funcId, T payload) where T : unmanaged
        {
            ThrowIfDisposed();
            BridgeNative.BridgeCore_PushCallCore(_handle, funcId, (IntPtr)(&payload), (uint)sizeof(T));
        }

        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
                BridgeNative.BridgeCore_Destroy(_handle);
                _handle = IntPtr.Zero;
            }
            GC.SuppressFinalize(this);
        }

        private void ThrowIfDisposed()
        {
            if (_handle == IntPtr.Zero)
                throw new ObjectDisposedException(nameof(BridgeCore));
        }
    }
}
