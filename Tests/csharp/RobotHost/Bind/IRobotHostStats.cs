interface IRobotHostStats
{
    ulong Commands { get; }
    ulong AssetRequests { get; }
    ulong Logs { get; }
    ulong Spawns { get; }
    ulong Transforms { get; }
    ulong Destroys { get; }
}

