using DemoAsset.Bindings;
using DemoEntity.Bindings;
using DemoLog.Bindings;

interface IRobotHostApi : IDemoLogHostApi, IDemoAssetHostApi, IDemoEntityHostApi, IRobotHostStats
{
}

