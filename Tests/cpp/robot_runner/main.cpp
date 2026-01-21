#include <bridge/bridge.h>

#include <demo_asset_bindings.generated.h>

#include <chrono>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <string>
#include <vector>

namespace
{
  struct CommandCursor
  {
    const uint8_t* p = nullptr;
    const uint8_t* end = nullptr;
  };

  static bool Next(CommandCursor& cur, const BridgeCommandHeader*& outHeader)
  {
    if (!cur.p || cur.p >= cur.end)
    {
      return false;
    }
    if (static_cast<size_t>(cur.end - cur.p) < sizeof(BridgeCommandHeader))
    {
      return false;
    }
    const auto* header = reinterpret_cast<const BridgeCommandHeader*>(cur.p);
    if (header->size < sizeof(BridgeCommandHeader) ||
        static_cast<size_t>(cur.end - cur.p) < header->size)
    {
      return false;
    }
    outHeader = header;
    cur.p += header->size;
    return true;
  }

  static std::string ReadUtf8(BridgeStringView view)
  {
    const char* p = reinterpret_cast<const char*>(static_cast<uintptr_t>(view.ptr));
    if (!p || view.len == 0)
    {
      return std::string();
    }
    return std::string(p, p + view.len);
  }

  static uint64_t FakeHandleFromKey(const std::string& key)
  {
    // Simple FNV-1a 64-bit
    uint64_t hash = 1469598103934665603ull;
    for (unsigned char c : key)
    {
      hash ^= static_cast<uint64_t>(c);
      hash *= 1099511628211ull;
    }
    return hash ? hash : 1ull;
  }
}

int main(int argc, char** argv)
{
  int bots = 1000;
  int frames = 300;
  float dt = 1.0f / 60.0f;

  if (argc >= 2) bots = std::atoi(argv[1]);
  if (argc >= 3) frames = std::atoi(argv[2]);
  if (argc >= 4) dt = static_cast<float>(std::atof(argv[3]));

  std::printf("robot_runner: bots=%d frames=%d dt=%f\n", bots, frames, dt);

  std::vector<BridgeCore*> cores;
  cores.reserve(static_cast<size_t>(bots));

  for (int i = 0; i < bots; ++i)
  {
    BridgeCoreConfig cfg{};
    cfg.seed = static_cast<uint64_t>(i + 1);
    cfg.mode = BRIDGE_MODE_ROBOT;
    cores.push_back(BridgeCore_Create(cfg));
  }

  const auto start = std::chrono::high_resolution_clock::now();

  uint64_t totalCommands = 0;
  uint64_t totalAssetRequests = 0;

  for (int frame = 0; frame < frames; ++frame)
  {
    for (BridgeCore* core : cores)
    {
      BridgeCore_Tick(core, dt);

      const void* bytes = nullptr;
      uint32_t len = 0;
      BridgeCore_GetCommandStream(core, &bytes, &len);

      CommandCursor cur{};
      cur.p = reinterpret_cast<const uint8_t*>(bytes);
      cur.end = cur.p ? (cur.p + len) : nullptr;

      uint64_t commandsThisCore = 0;

      const BridgeCommandHeader* header = nullptr;
      while (Next(cur, header))
      {
        ++commandsThisCore;
        if (header->type == BRIDGE_CMD_CALL_HOST &&
            header->size >= sizeof(BridgeCmdCallHost))
        {
          const auto* cmd = reinterpret_cast<const BridgeCmdCallHost*>(header);
          const uint32_t payload_bytes = static_cast<uint32_t>(header->size) - static_cast<uint32_t>(sizeof(BridgeCmdCallHost));
          if (cmd->func_id == static_cast<uint32_t>(demo_asset::HostFuncId::LoadAsset) &&
              payload_bytes >= sizeof(demo_asset::HostArgs_LoadAsset))
          {
            ++totalAssetRequests;
            const uint8_t* payload = reinterpret_cast<const uint8_t*>(cmd) + sizeof(BridgeCmdCallHost);
            const auto* args = reinterpret_cast<const demo_asset::HostArgs_LoadAsset*>(payload);

            std::string key = ReadUtf8(args->assetKey);
            uint64_t handle = FakeHandleFromKey(key);

            demo_asset::CoreArgs_AssetLoaded evt{};
            evt.requestId = args->requestId;
            evt.handle = handle;
            evt.status = BRIDGE_ASSET_STATUS_OK;

            BridgeCore_PushCallCore(core,
              static_cast<uint32_t>(demo_asset::CoreFuncId::AssetLoaded),
              &evt,
              static_cast<uint32_t>(sizeof(evt)));
          }
        }
      }

      totalCommands += commandsThisCore;
    }
  }

  const auto end = std::chrono::high_resolution_clock::now();
  const std::chrono::duration<double> elapsed = end - start;

  std::printf("elapsed: %.3f s\n", elapsed.count());
  std::printf("total commands parsed: %llu\n", static_cast<unsigned long long>(totalCommands));
  if (elapsed.count() > 0.0)
    std::printf("commands/sec: %.0f\n", static_cast<double>(totalCommands) / elapsed.count());
  std::printf("total asset requests: %llu\n", static_cast<unsigned long long>(totalAssetRequests));
  std::printf("ticks: %llu\n",
    static_cast<unsigned long long>(static_cast<uint64_t>(bots) * static_cast<uint64_t>(frames)));

  for (BridgeCore* core : cores)
  {
    BridgeCore_Destroy(core);
  }

  return 0;
}
