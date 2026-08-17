cmake_minimum_required(VERSION 3.25)
project(NukedSC55SharpNative VERSION 0.7.0 LANGUAGES C CXX)

option(NSC55_BUILD_TESTS "Build native contract tests" OFF)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
set(CMAKE_C_VISIBILITY_PRESET hidden)
set(CMAKE_CXX_VISIBILITY_PRESET hidden)
set(CMAKE_VISIBILITY_INLINES_HIDDEN ON)

if(MSVC)
    set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
endif()

set(NUKED_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/../external/Nuked-SC55")

add_library(NukedSC55Sharp.Native SHARED
    include/nuked_sc55_sharp.h
    src/headless_lcd.cpp
    src/nuked_sc55_sharp.cpp
    "${NUKED_ROOT}/src/backend/diagnostics.cpp"
    "${NUKED_ROOT}/src/backend/emu.cpp"
    "${NUKED_ROOT}/src/backend/file_hashing.cpp"
    "${NUKED_ROOT}/src/backend/file_io.cpp"
    "${NUKED_ROOT}/src/backend/mcu_interrupt.cpp"
    "${NUKED_ROOT}/src/backend/mcu_opcodes.cpp"
    "${NUKED_ROOT}/src/backend/mcu_timer.cpp"
    "${NUKED_ROOT}/src/backend/mcu.cpp"
    "${NUKED_ROOT}/src/backend/pcm.cpp"
    "${NUKED_ROOT}/src/backend/rom_io.cpp"
    "${NUKED_ROOT}/src/backend/rom.cpp"
    "${NUKED_ROOT}/src/backend/standard_romsets.cpp"
    "${NUKED_ROOT}/src/backend/submcu.cpp"
    "${NUKED_ROOT}/src/backend/sha/sha224-256.c"
    "${NUKED_ROOT}/src/common/path_util.cpp"
    "${NUKED_ROOT}/src/common/rom_loader.cpp")

target_include_directories(NukedSC55Sharp.Native
    PUBLIC "${CMAKE_CURRENT_SOURCE_DIR}/include"
    PRIVATE
        "${NUKED_ROOT}/src/backend"
        "${NUKED_ROOT}/src/common")
target_compile_definitions(NukedSC55Sharp.Native PRIVATE NUKED_SC55_SHARP_NATIVE_BUILD)
set_target_properties(NukedSC55Sharp.Native PROPERTIES OUTPUT_NAME "NukedSC55Sharp.Native")

if(MSVC)
    target_compile_options(NukedSC55Sharp.Native PRIVATE /W4 /permissive- /utf-8)
else()
    target_compile_options(NukedSC55Sharp.Native PRIVATE -Wall -Wextra -Wpedantic)
    if(NOT APPLE)
        target_link_options(NukedSC55Sharp.Native PRIVATE -static-libstdc++ -static-libgcc -Wl,--exclude-libs,ALL)
    endif()
endif()

install(TARGETS NukedSC55Sharp.Native
    RUNTIME DESTINATION .
    LIBRARY DESTINATION .)

if(NSC55_BUILD_TESTS)
    enable_testing()
    add_executable(NukedSC55Sharp.Native.Tests tests/native_contract_tests.cpp)
    target_link_libraries(NukedSC55Sharp.Native.Tests PRIVATE NukedSC55Sharp.Native)
    add_test(NAME native-contracts COMMAND NukedSC55Sharp.Native.Tests)
endif()
