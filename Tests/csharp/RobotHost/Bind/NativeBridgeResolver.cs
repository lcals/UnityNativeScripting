using System.Runtime.InteropServices;
using Bridge.Core;

static class NativeBridgeResolver
{
    public static void TryRegisterFromEnvOrDefault()
    {
        string? nativeDir = Environment.GetEnvironmentVariable("BRIDGE_NATIVE_DIR");
        nativeDir = string.IsNullOrWhiteSpace(nativeDir) ? Paths.FindDefaultNativeDir() : nativeDir;
        if (string.IsNullOrWhiteSpace(nativeDir))
            return;

        string libraryPath = Path.Combine(nativeDir, GetPlatformLibraryFileName("bridge_core"));
        if (!File.Exists(libraryPath))
            return;

        NativeLibrary.SetDllImportResolver(typeof(BridgeCore).Assembly, (name, _, _) =>
        {
            if (!string.Equals(name, "bridge_core", StringComparison.Ordinal))
                return IntPtr.Zero;

            return NativeLibrary.Load(libraryPath);
        });
    }

    private static string GetPlatformLibraryFileName(string baseName)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return baseName + ".dll";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "lib" + baseName + ".dylib";
        return "lib" + baseName + ".so";
    }
}

