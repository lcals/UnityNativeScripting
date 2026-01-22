using System;
using System.Runtime.InteropServices;

namespace Bridge.Core
{
    /// <summary>
    /// 原生侧返回的 command stream（指针 + 长度）。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct CommandStream
    {
        public readonly IntPtr Ptr;
        public readonly uint Length;
        private readonly uint _reserved0;

        public bool IsEmpty => Ptr == IntPtr.Zero || Length == 0;

        public static CommandStream Empty => new CommandStream(IntPtr.Zero, 0);

        internal CommandStream(IntPtr ptr, uint length)
        {
            Ptr = ptr;
            Length = length;
            _reserved0 = 0;
        }
    }
}
