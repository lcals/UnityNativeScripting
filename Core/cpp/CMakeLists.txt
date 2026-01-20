cmake_minimum_required(VERSION 3.20)

project(bridge_runtime LANGUAGES C CXX)

add_library(bridge_runtime STATIC
  src/core/command_stream.cpp
  src/core/core_instance.cpp
)

target_include_directories(bridge_runtime
  PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}/include
)

target_compile_features(bridge_runtime PUBLIC cxx_std_20)

if (MSVC)
  target_compile_options(bridge_runtime PRIVATE /W4 /permissive- /utf-8)
else()
  target_compile_options(bridge_runtime PRIVATE -Wall -Wextra -Wpedantic)
endif()

