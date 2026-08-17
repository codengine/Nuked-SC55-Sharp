#pragma once

#include <stddef.h>
#include <stdint.h>

#if defined(_WIN32) && defined(NUKED_SC55_SHARP_NATIVE_BUILD)
#define NSC55_API __declspec(dllexport)
#elif defined(_WIN32)
#define NSC55_API __declspec(dllimport)
#elif defined(__GNUC__)
#define NSC55_API __attribute__((visibility("default")))
#else
#define NSC55_API
#endif

#ifdef __cplusplus
extern "C" {
#endif

typedef struct nsc55_handle nsc55_handle;

typedef enum nsc55_status {
    NSC55_STATUS_SUCCESS = 0,
    NSC55_STATUS_INVALID_ARGUMENT = 1,
    NSC55_STATUS_ROMSET_NOT_FOUND = 2,
    NSC55_STATUS_ROMSET_INCOMPLETE = 3,
    NSC55_STATUS_ROM_LOAD_FAILED = 4,
    NSC55_STATUS_INITIALIZATION_FAILED = 5,
    NSC55_STATUS_RENDERING_FAILED = 6,
    NSC55_STATUS_INTERNAL_ERROR = 7
} nsc55_status;

typedef enum nsc55_reset {
    NSC55_RESET_NONE = 0,
    NSC55_RESET_GENERAL_MIDI = 1,
    NSC55_RESET_GENERAL_STANDARD = 2
} nsc55_reset;

NSC55_API uint32_t nsc55_abi_version(void);
NSC55_API int32_t nsc55_romset_supported(const char* romset_utf8);
NSC55_API nsc55_status nsc55_create(const char* rom_directory_utf8,
                                    const char* romset_utf8,
                                    int32_t enable_oversampling,
                                    nsc55_reset initial_reset,
                                    uint64_t startup_steps,
                                    const char* nvram_path_utf8,
                                    nsc55_handle** output,
                                    char* error_utf8,
                                    size_t error_capacity);
NSC55_API void nsc55_destroy(nsc55_handle* handle);
NSC55_API uint32_t nsc55_sample_rate(const nsc55_handle* handle);
NSC55_API nsc55_status nsc55_reset_emulator(nsc55_handle* handle,
                                            char* error_utf8,
                                            size_t error_capacity);
NSC55_API nsc55_status nsc55_queue_midi(nsc55_handle* handle,
                                        const uint8_t* bytes,
                                        size_t length,
                                        uint32_t frame_offset,
                                        char* error_utf8,
                                        size_t error_capacity);
NSC55_API void nsc55_clear_midi(nsc55_handle* handle);
NSC55_API nsc55_status nsc55_render_f32(nsc55_handle* handle,
                                        float* interleaved_stereo,
                                        size_t frame_count,
                                        char* error_utf8,
                                        size_t error_capacity);
NSC55_API nsc55_status nsc55_render_s16(nsc55_handle* handle,
                                        int16_t* interleaved_stereo,
                                        size_t frame_count,
                                        char* error_utf8,
                                        size_t error_capacity);

#ifdef __cplusplus
}
#endif
