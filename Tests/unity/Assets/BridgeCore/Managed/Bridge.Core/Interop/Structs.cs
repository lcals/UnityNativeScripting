using System;
using System.Runtime.InteropServices;

namespace Bridge.Core
{
    public enum BridgeResult : int
    {
        Ok = 0,
        Error = 1,
        InvalidArgument = 2
    }

    public enum BridgeMode : uint
    {
        Game = 0,
        Robot = 1
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct BridgeVersion
    {
        public readonly uint Major;
        public readonly uint Minor;
        public readonly uint Patch;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BridgeCoreConfig
    {
        public ulong Seed;
        public uint Mode;
        public uint Reserved0;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct BridgeStringView
    {
        public readonly ulong Ptr;
        public readonly uint Len;
        public readonly uint Reserved0;

        public string ToManagedString()
        {
            if (Ptr == 0 || Len == 0)
                return string.Empty;

            // netstandard2.1+ 支持按长度读取 UTF-8，避免额外分配 byte[]。
            return Marshal.PtrToStringUTF8(new IntPtr(unchecked((long)Ptr)), (int)Len) ?? string.Empty;
        }
    }

    public enum BridgeLogLevel : uint
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3
    }

    public enum BridgeAssetType : uint
    {
        Unknown = 0,
        Prefab = 1
    }

    public enum BridgeAssetStatus : uint
    {
        Ok = 0,
        NotFound = 1,
        Error = 2
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BridgeVec3
    {
        public float X;
        public float Y;
        public float Z;
        public float Reserved0;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BridgeQuat
    {
        public float X;
        public float Y;
        public float Z;
        public float W;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BridgeTransform
    {
        public BridgeVec3 Position;
        public BridgeQuat Rotation;
        public BridgeVec3 Scale;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BridgeCommandHeader
    {
        public ushort Type;
        public ushort Size;
        public uint Reserved0;
    }

    public enum BridgeCommandType : ushort
    {
        None = 0,
        CallHost = 1
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BridgeCmdCallHost
    {
        public BridgeCommandHeader Header;
        public uint FuncId;
        public uint PayloadSize;
    }
}
