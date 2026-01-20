add_executable(bridge_robot_runner
  main.cpp
)

target_link_libraries(bridge_robot_runner PRIVATE bridge_core)
target_compile_features(bridge_robot_runner PRIVATE cxx_std_20)

target_include_directories(bridge_robot_runner PRIVATE
  ${CMAKE_SOURCE_DIR}/Tests/cpp/demo_asset/generated
)

if (MSVC)
  target_compile_options(bridge_robot_runner PRIVATE /W4 /permissive- /utf-8)
else()
  target_compile_options(bridge_robot_runner PRIVATE -Wall -Wextra -Wpedantic)
endif()

set_target_properties(bridge_robot_runner PROPERTIES
  RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin/$<CONFIG>"
)

add_test(
  NAME bridge_robot_runner_smoke
  COMMAND $<TARGET_FILE:bridge_robot_runner> 10 5 0.0166667
)
set_tests_properties(bridge_robot_runner_smoke PROPERTIES
  WORKING_DIRECTORY $<TARGET_FILE_DIR:bridge_robot_runner>
)
