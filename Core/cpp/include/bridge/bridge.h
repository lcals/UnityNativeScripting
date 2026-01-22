#pragma once

#include <stddef.h>
#include <stdint.h>

#if defined(_WIN32)
  #define BRIDGE_CALL __cdecl
  #if defined(BRIDGE_BUILD_DLL)
    #define BRIDGE_API __declspec(dllexport)
  #else
    #define BRIDGE_API __declspec(dllimport)
  #endif
#else
  #define BRIDGE_CALL
  #define BRIDGE_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

//------------------------------------------------------------------------------
// 概览
//------------------------------------------------------------------------------
//
// 本文件定义 Core（C++）与 Host（C# / Unity 等）之间的“稳定 C ABI”。
//
// 目标：
// - Core 尽可能只包含业务/数据/规则
// - Host（引擎壳）提供渲染/资源/输入等能力
//
// 关键约束：
// - Unity/IL2CPP 友好：避免 native->managed 回调
// - 轮询式数据流：
//   - Core -> Host：每帧产出 command stream（字节流）
//   - Host -> Core：通过 BridgeCore_PushCallCore 推送“调用 Core API”的事件
//
//------------------------------------------------------------------------------
// Version
//------------------------------------------------------------------------------

#define BRIDGE_VERSION_MAJOR 0
#define BRIDGE_VERSION_MINOR 2
#define BRIDGE_VERSION_PATCH 0

typedef struct BridgeVersion
{
  uint32_t major;
  uint32_t minor;
  uint32_t patch;
} BridgeVersion;

BRIDGE_API BridgeVersion BRIDGE_CALL Bridge_GetVersion(void);

//------------------------------------------------------------------------------
// Core types
//------------------------------------------------------------------------------

typedef struct BridgeCore BridgeCore;

typedef enum BridgeResult
{
  BRIDGE_OK = 0,
  BRIDGE_ERROR = 1,
  BRIDGE_INVALID_ARGUMENT = 2
} BridgeResult;

typedef enum BridgeMode : uint32_t
{
  BRIDGE_MODE_GAME = 0,
  BRIDGE_MODE_ROBOT = 1
} BridgeMode;

typedef struct BridgeCoreConfig
{
  uint64_t seed;
  uint32_t mode; // BridgeMode
  // 预留字段（用于未来 ABI 扩展），必须为 0。
  uint32_t reserved0;
} BridgeCoreConfig;

BRIDGE_API BridgeCore* BRIDGE_CALL BridgeCore_Create(BridgeCoreConfig config);
BRIDGE_API void BRIDGE_CALL BridgeCore_Destroy(BridgeCore* core);

//------------------------------------------------------------------------------
// Common blittable structs
//------------------------------------------------------------------------------

typedef struct BridgeStringView
{
  // 指针值（UTF-8 字节），非 0 结尾。
  uint64_t ptr;
  // 字节长度。
  uint32_t len;
  // 预留字段（用于未来 ABI 扩展），必须为 0。
  uint32_t reserved0;
} BridgeStringView;

typedef struct BridgeVec3
{
  float x;
  float y;
  float z;
  float reserved0;
} BridgeVec3;

typedef struct BridgeQuat
{
  float x;
  float y;
  float z;
  float w;
} BridgeQuat;

typedef struct BridgeTransform
{
  BridgeVec3 position;
  BridgeQuat rotation;
  BridgeVec3 scale;
} BridgeTransform;

//------------------------------------------------------------------------------
// Common enums（可按需扩展/替换）
//------------------------------------------------------------------------------

typedef enum BridgeLogLevel : uint32_t
{
  BRIDGE_LOG_DEBUG = 0,
  BRIDGE_LOG_INFO = 1,
  BRIDGE_LOG_WARN = 2,
  BRIDGE_LOG_ERROR = 3
} BridgeLogLevel;

typedef enum BridgeAssetType : uint32_t
{
  BRIDGE_ASSET_UNKNOWN = 0,
  BRIDGE_ASSET_PREFAB = 1
} BridgeAssetType;

typedef enum BridgeAssetStatus : uint32_t
{
  BRIDGE_ASSET_STATUS_OK = 0,
  BRIDGE_ASSET_STATUS_NOT_FOUND = 1,
  BRIDGE_ASSET_STATUS_ERROR = 2
} BridgeAssetStatus;

//------------------------------------------------------------------------------
// Commands (Core -> Host)
//------------------------------------------------------------------------------

typedef enum BridgeCommandType : uint16_t
{
  BRIDGE_CMD_NONE = 0,
  // 通用 Host 调用：func_id + payload（由代码生成决定 payload 结构）
  BRIDGE_CMD_CALL_HOST = 1
} BridgeCommandType;

typedef struct BridgeCommandHeader
{
  uint16_t type; // BridgeCommandType
  // 命令总大小（包含 header+后续数据），必须 8 字节对齐
  uint16_t size;
} BridgeCommandHeader;

// 通用 Host 调用命令头：
// - payload 紧跟其后，并按 8 字节补齐到 header.size
typedef struct BridgeCmdCallHost
{
  BridgeCommandHeader header;
  uint32_t func_id;
} BridgeCmdCallHost;

// Command stream view（Core -> Host）：
// - 仅包含 ptr+len，指针由 Core 持有。
// - 只保证在下一次 Tick（或 Destroy）前有效。
typedef struct BridgeCommandStream
{
  const void* ptr;
  uint32_t len;
  // 预留字段（用于未来 ABI 扩展），必须为 0。
  uint32_t reserved0;
} BridgeCommandStream;

BRIDGE_API void BRIDGE_CALL BridgeCore_Tick(BridgeCore* core, float dt);

// 组合调用：Tick + GetCommandStream（减少 Host 侧 P/Invoke 次数）。
// 等价于：
//   BridgeCore_Tick(core, dt);
//   BridgeCore_GetCommandStream(core, out_ptr, out_len);
BRIDGE_API BridgeResult BRIDGE_CALL BridgeCore_TickAndGetCommandStream(
  BridgeCore* core,
  float dt,
  const void** out_ptr,
  uint32_t* out_len);

// 批量 Tick + 获取 command streams（机器人/压测用）。
// - cores / out_streams 均为长度为 count 的数组指针。
// - 成功返回后：out_streams[i] 为 cores[i] 本帧的 stream。
BRIDGE_API BridgeResult BRIDGE_CALL BridgeCore_TickManyAndGetCommandStreams(
  BridgeCore** cores,
  uint32_t count,
  float dt,
  BridgeCommandStream* out_streams);

// 返回最近一次 BridgeCore_Tick 生成的 command stream（连续字节流）指针。
// 返回的内存由 Core 持有，只保证在下一次 BridgeCore_Tick（或 BridgeCore_Destroy）前有效。
BRIDGE_API BridgeResult BRIDGE_CALL BridgeCore_GetCommandStream(
  const BridgeCore* core,
  const void** out_ptr,
  uint32_t* out_len);

//------------------------------------------------------------------------------
// Calls (Host -> Core)
//------------------------------------------------------------------------------

// Host 调用 Core API（由代码生成决定 func_id 与 payload 结构）。
// - payload 指向 blittable 数据（可为 null，当 payload_size==0）
BRIDGE_API BridgeResult BRIDGE_CALL BridgeCore_PushCallCore(
  BridgeCore* core,
  uint32_t func_id,
  const void* payload,
  uint32_t payload_size);

#ifdef __cplusplus
} // extern "C"
#endif
