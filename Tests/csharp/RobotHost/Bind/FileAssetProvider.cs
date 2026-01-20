using System.Collections.Generic;

sealed class FileAssetProvider
{
    private readonly string _root;
    private readonly Dictionary<string, ulong> _handleCache = new(StringComparer.Ordinal);

    public FileAssetProvider(string root)
    {
        _root = root;
    }

    public bool TryGetHandle(string assetKey, out ulong handle)
    {
        if (_handleCache.TryGetValue(assetKey, out handle))
            return true;

        string? path = ResolvePath(assetKey);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            handle = 0;
            return false;
        }

        byte[] bytes = File.ReadAllBytes(path);
        handle = Fnv1a64(bytes);
        if (handle == 0)
            handle = 1;

        _handleCache[assetKey] = handle;
        return true;
    }

    private string? ResolvePath(string assetKey)
    {
        if (string.IsNullOrWhiteSpace(assetKey))
            return null;

        string rel = assetKey.Replace('\\', '/').TrimStart('/');
        string direct = Path.Combine(_root, rel);
        if (File.Exists(direct))
            return direct;

        foreach (string ext in new[] { ".bytes", ".bin", ".txt" })
        {
            string candidate = direct + ext;
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static ulong Fnv1a64(byte[] bytes)
    {
        const ulong offset = 1469598103934665603ul;
        const ulong prime = 1099511628211ul;

        ulong hash = offset;
        foreach (byte b in bytes)
        {
            hash ^= b;
            hash *= prime;
        }
        return hash;
    }
}

