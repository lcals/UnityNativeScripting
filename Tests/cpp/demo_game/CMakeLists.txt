cmake_minimum_required(VERSION 3.20)

project(bridge_demo_game LANGUAGES C CXX)

add_library(bridge_demo_game STATIC
  src/demo_asset_app.cpp
  src/game_entry.cpp
)

target_link_libraries(bridge_demo_game PUBLIC bridge_runtime)
target_compile_features(bridge_demo_game PUBLIC cxx_std_20)

target_include_directories(bridge_demo_game PRIVATE
  ${CMAKE_SOURCE_DIR}/Tests/cpp/demo_asset/generated
  ${CMAKE_SOURCE_DIR}/Tests/cpp/demo_entity/generated
  ${CMAKE_SOURCE_DIR}/Tests/cpp/demo_log/generated
)

if (MSVC)
  target_compile_options(bridge_demo_game PRIVATE /W4 /permissive- /utf-8)
else()
  target_compile_options(bridge_demo_game PRIVATE -Wall -Wextra -Wpedantic)
endif()

add_library(bridge_core SHARED
  ${CMAKE_SOURCE_DIR}/Core/cpp/src/api/bridge_api.cpp
)

target_link_libraries(bridge_core PRIVATE bridge_demo_game PUBLIC bridge_runtime)
target_compile_features(bridge_core PUBLIC cxx_std_20)

if (MSVC)
  target_compile_options(bridge_core PRIVATE /W4 /permissive- /utf-8)
else()
  target_compile_options(bridge_core PRIVATE -Wall -Wextra -Wpedantic)
endif()

target_compile_definitions(bridge_core PRIVATE BRIDGE_BUILD_DLL=1)

set_target_properties(bridge_core PROPERTIES
  OUTPUT_NAME "bridge_core"
  RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin/$<CONFIG>"
  LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin/$<CONFIG>"
  ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/lib/$<CONFIG>"
)
