static class Paths
{
    public static string FindDefaultAssetsRoot()
    {
        string repoRoot = FindRepoRoot();
        return Path.Combine(repoRoot, "Tests", "assets");
    }

    public static string FindDefaultNativeDir()
    {
        string repoRoot = FindRepoRoot();
        var candidates = new[]
        {
            Path.Combine(repoRoot, "build", "bin", "Release"),
            Path.Combine(repoRoot, "build", "bin", "Debug"),
            Path.Combine(repoRoot, "build", "bin"),
        };

        foreach (string candidate in candidates)
        {
            if (Directory.Exists(candidate))
                return candidate;
        }

        return string.Empty;
    }

    private static string FindRepoRoot()
    {
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 12; i++)
        {
            if (File.Exists(Path.Combine(dir, "CMakeLists.txt")) &&
                Directory.Exists(Path.Combine(dir, "Core")))
                return dir;

            string? parent = Directory.GetParent(dir)?.FullName;
            if (string.IsNullOrWhiteSpace(parent))
                break;
            dir = parent;
        }

        return Directory.GetCurrentDirectory();
    }
}

