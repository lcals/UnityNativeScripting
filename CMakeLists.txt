cmake_minimum_required(VERSION 3.20)

project(NativeBridge LANGUAGES C CXX)

option(BRIDGE_BUILD_TESTS "Build tests (robot runner, etc.)" ON)

add_subdirectory(Core/cpp)

if (BRIDGE_BUILD_TESTS)
  enable_testing()
  add_subdirectory(Tests/cpp)
endif()
