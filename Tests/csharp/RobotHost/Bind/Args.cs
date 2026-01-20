static class Args
{
    public static int ReadInt(string[] args, int index, int fallback)
        => (index < args.Length && int.TryParse(args[index], out int value)) ? value : fallback;

    public static float ReadFloat(string[] args, int index, float fallback)
        => (index < args.Length && float.TryParse(args[index], out float value)) ? value : fallback;

    public static string ReadString(string[] args, int index, string fallback)
        => (index < args.Length && !string.IsNullOrWhiteSpace(args[index])) ? args[index] : fallback;
}

