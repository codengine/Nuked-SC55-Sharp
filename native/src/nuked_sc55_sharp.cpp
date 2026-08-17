#include "nuked_sc55_sharp.h"

#include "audio.h"
#include "emu.h"
#include "pcm.h"
#include "rom_loader.h"
#include "standard_romsets.h"

#include <algorithm>
#include <cstring>
#include <exception>
#include <filesystem>
#include <memory>
#include <span>
#include <string>
#include <string_view>
#include <utility>
#include <vector>

namespace
{
constexpr uint32_t AbiVersion = 1;

struct MidiEvent
{
    uint32_t frame_offset;
    std::vector<uint8_t> bytes;
};

enum class SinkFormat
{
    None,
    Float32,
    Signed16,
};

bool IsSupportedRomset(std::string_view requested)
{
    const auto definitions = GetStandardRomsetDefinitions();
    return std::any_of(definitions.begin(), definitions.end(), [requested](const RomsetDefinition& definition) {
        return requested == definition.name;
    });
}

common::LoadRomsetError DisambiguateLoadError(common::LoadRomsetError error,
                                               std::string_view requested,
                                               common::LoadRomsetResult& result)
{
    if (error != common::LoadRomsetError::InvalidRomsetName || !IsSupportedRomset(requested))
    {
        return error;
    }

    if (result.registries.romsets.GetRomsetFamily(requested, result.romset))
    {
        IsCompleteRomset(result.romset_info, result.romset, &result.completion);
    }

    return common::LoadRomsetError::IncompleteRomset;
}

void WriteError(char* destination, size_t capacity, std::string_view message)
{
    if (destination == nullptr || capacity == 0)
    {
        return;
    }

    const size_t length = std::min(capacity - 1, message.size());
    std::memcpy(destination, message.data(), length);
    destination[length] = '\0';
}

std::string LoadErrorMessage(common::LoadRomsetError error,
                             const common::LoadRomsetResult& result,
                             std::string_view requested)
{
    std::string message = ToCString(error);
    message += ": ";
    message += requested;

    if (error == common::LoadRomsetError::IncompleteRomset)
    {
        message += "; missing";
        for (size_t index = 0; index < ROMLOCATION_COUNT; ++index)
        {
            if (result.completion[index] == RomCompletionStatus::Missing)
            {
                message += " ";
                message += ToCString(static_cast<RomLocation>(index));
            }
        }
    }

    return message;
}

std::filesystem::path Utf8Path(const char* value)
{
    const auto* begin = reinterpret_cast<const char8_t*>(value);
    return std::filesystem::path(std::u8string(begin, begin + std::strlen(value)));
}

nsc55_status MapLoadError(common::LoadRomsetError error)
{
    switch (error)
    {
    case common::LoadRomsetError::InvalidRomsetName:
        return NSC55_STATUS_ROMSET_NOT_FOUND;
    case common::LoadRomsetError::RomLoadFailed:
        return NSC55_STATUS_ROM_LOAD_FAILED;
    case common::LoadRomsetError::DetectionFailed:
    case common::LoadRomsetError::NoCompleteRomsets:
    case common::LoadRomsetError::IncompleteRomset:
    case common::LoadRomsetError::AmbiguousRomset:
        return NSC55_STATUS_ROMSET_INCOMPLETE;
    }

    return NSC55_STATUS_INTERNAL_ERROR;
}

EMU_SystemReset MapReset(nsc55_reset reset)
{
    switch (reset)
    {
    case NSC55_RESET_NONE:
        return EMU_SystemReset::NONE;
    case NSC55_RESET_GENERAL_MIDI:
        return EMU_SystemReset::GM_RESET;
    case NSC55_RESET_GENERAL_STANDARD:
        return EMU_SystemReset::GS_RESET;
    }

    return EMU_SystemReset::NONE;
}
} // namespace

struct nsc55_handle
{
    Emulator emulator;
    common::LoadRomsetResult roms;
    std::vector<MidiEvent> events;
    nsc55_reset initial_reset = NSC55_RESET_NONE;
    uint64_t startup_steps = 0;
    bool enable_oversampling = false;
    SinkFormat sink_format = SinkFormat::None;
    void* sink = nullptr;
    size_t sink_frame = 0;

    static void ReceiveSample(void* userdata, const AudioFrame<int32_t>& source)
    {
        auto& handle = *static_cast<nsc55_handle*>(userdata);
        switch (handle.sink_format)
        {
        case SinkFormat::Float32: {
            AudioFrame<float> normalized;
            Normalize(source, normalized);
            auto* destination = static_cast<float*>(handle.sink) + handle.sink_frame * 2;
            destination[0] = normalized.left;
            destination[1] = normalized.right;
            ++handle.sink_frame;
            break;
        }
        case SinkFormat::Signed16: {
            AudioFrame<int16_t> normalized;
            Normalize(source, normalized);
            auto* destination = static_cast<int16_t*>(handle.sink) + handle.sink_frame * 2;
            destination[0] = normalized.left;
            destination[1] = normalized.right;
            ++handle.sink_frame;
            break;
        }
        case SinkFormat::None:
            break;
        }
    }

    void Reset()
    {
        events.clear();
        sink_format = SinkFormat::None;
        sink = nullptr;
        sink_frame = 0;
        emulator.Reset();
        emulator.GetPCM().enable_oversampling = enable_oversampling;
        emulator.PostSystemReset(MapReset(initial_reset));
        for (uint64_t step = 0; step < startup_steps; ++step)
        {
            emulator.Step();
        }
    }

    template <typename Sample>
    void Render(Sample* destination, size_t frame_count, SinkFormat format)
    {
        std::stable_sort(events.begin(), events.end(), [](const MidiEvent& left, const MidiEvent& right) {
            return left.frame_offset < right.frame_offset;
        });

        sink_format = format;
        sink = destination;
        sink_frame = 0;
        size_t event_index = 0;
        while (sink_frame < frame_count)
        {
            while (event_index < events.size() && events[event_index].frame_offset == sink_frame)
            {
                emulator.PostMIDI(events[event_index].bytes);
                ++event_index;
            }

            const size_t target_frame = sink_frame + 1;
            while (sink_frame < target_frame)
            {
                emulator.Step();
            }
        }

        sink_format = SinkFormat::None;
        sink = nullptr;
        events.clear();
    }
};

extern "C" NSC55_API uint32_t nsc55_abi_version(void)
{
    return AbiVersion;
}

extern "C" NSC55_API int32_t nsc55_romset_supported(const char* romset_utf8)
{
    if (romset_utf8 == nullptr)
    {
        return 0;
    }

    return IsSupportedRomset(romset_utf8) ? 1 : 0;
}

extern "C" NSC55_API nsc55_status nsc55_create(const char* rom_directory_utf8,
                                                const char* romset_utf8,
                                                int32_t enable_oversampling,
                                                nsc55_reset initial_reset,
                                                uint64_t startup_steps,
                                                const char* nvram_path_utf8,
                                                nsc55_handle** output,
                                                char* error_utf8,
                                                size_t error_capacity)
{
    if (rom_directory_utf8 == nullptr || romset_utf8 == nullptr || output == nullptr ||
        (initial_reset != NSC55_RESET_NONE && initial_reset != NSC55_RESET_GENERAL_MIDI &&
         initial_reset != NSC55_RESET_GENERAL_STANDARD))
    {
        WriteError(error_utf8, error_capacity, "Invalid native creation arguments.");
        return NSC55_STATUS_INVALID_ARGUMENT;
    }

    *output = nullptr;
    try
    {
        auto handle = std::make_unique<nsc55_handle>();
        handle->initial_reset = initial_reset;
        handle->startup_steps = startup_steps;
        handle->enable_oversampling = enable_oversampling != 0;

        EMU_Options options;
        if (nvram_path_utf8 != nullptr && *nvram_path_utf8 != '\0')
        {
            options.nvram_filename = Utf8Path(nvram_path_utf8);
        }

        if (!handle->emulator.Init(options))
        {
            WriteError(error_utf8, error_capacity, "Nuked-SC55 could not allocate emulator state.");
            return NSC55_STATUS_INITIALIZATION_FAILED;
        }

        const common::RomOverrides overrides{};
        auto load_error = common::LoadRomset(Utf8Path(rom_directory_utf8),
                                             romset_utf8,
                                             common::RomLoader::Hashing,
                                             overrides,
                                             handle->roms);
        load_error = DisambiguateLoadError(load_error, romset_utf8, handle->roms);
        if (load_error != common::LoadRomsetError{})
        {
            WriteError(error_utf8, error_capacity, LoadErrorMessage(load_error, handle->roms, romset_utf8));
            return MapLoadError(load_error);
        }

        if (!handle->emulator.LoadRoms(handle->roms.romset, handle->roms.romset_info))
        {
            WriteError(error_utf8, error_capacity, "Nuked-SC55 rejected the loaded ROM data.");
            return NSC55_STATUS_ROM_LOAD_FAILED;
        }

        handle->emulator.SetSampleCallback(&nsc55_handle::ReceiveSample, handle.get());
        handle->Reset();
        *output = handle.release();
        WriteError(error_utf8, error_capacity, {});
        return NSC55_STATUS_SUCCESS;
    }
    catch (const std::exception& exception)
    {
        WriteError(error_utf8, error_capacity, exception.what());
        return NSC55_STATUS_INTERNAL_ERROR;
    }
    catch (...)
    {
        WriteError(error_utf8, error_capacity, "Unknown native initialization failure.");
        return NSC55_STATUS_INTERNAL_ERROR;
    }
}

extern "C" NSC55_API void nsc55_destroy(nsc55_handle* handle)
{
    try
    {
        delete handle;
    }
    catch (...)
    {
    }
}

extern "C" NSC55_API uint32_t nsc55_sample_rate(const nsc55_handle* handle)
{
    return handle == nullptr
        ? 0
        : PCM_GetOutputFrequency(const_cast<nsc55_handle*>(handle)->emulator.GetPCM());
}

extern "C" NSC55_API nsc55_status nsc55_reset_emulator(nsc55_handle* handle,
                                                        char* error_utf8,
                                                        size_t error_capacity)
{
    if (handle == nullptr)
    {
        WriteError(error_utf8, error_capacity, "The emulator handle is null.");
        return NSC55_STATUS_INVALID_ARGUMENT;
    }

    try
    {
        handle->Reset();
        WriteError(error_utf8, error_capacity, {});
        return NSC55_STATUS_SUCCESS;
    }
    catch (const std::exception& exception)
    {
        WriteError(error_utf8, error_capacity, exception.what());
        return NSC55_STATUS_INTERNAL_ERROR;
    }
    catch (...)
    {
        WriteError(error_utf8, error_capacity, "Unknown native reset failure.");
        return NSC55_STATUS_INTERNAL_ERROR;
    }
}

extern "C" NSC55_API nsc55_status nsc55_queue_midi(nsc55_handle* handle,
                                                    const uint8_t* bytes,
                                                    size_t length,
                                                    uint32_t frame_offset,
                                                    char* error_utf8,
                                                    size_t error_capacity)
{
    if (handle == nullptr || bytes == nullptr || length == 0)
    {
        WriteError(error_utf8, error_capacity, "MIDI data must contain at least one byte.");
        return NSC55_STATUS_INVALID_ARGUMENT;
    }

    try
    {
        handle->events.push_back({frame_offset, std::vector<uint8_t>(bytes, bytes + length)});
        WriteError(error_utf8, error_capacity, {});
        return NSC55_STATUS_SUCCESS;
    }
    catch (const std::exception& exception)
    {
        WriteError(error_utf8, error_capacity, exception.what());
        return NSC55_STATUS_INTERNAL_ERROR;
    }
    catch (...)
    {
        WriteError(error_utf8, error_capacity, "Unknown native MIDI queue failure.");
        return NSC55_STATUS_INTERNAL_ERROR;
    }
}

extern "C" NSC55_API void nsc55_clear_midi(nsc55_handle* handle)
{
    if (handle != nullptr)
    {
        handle->events.clear();
    }
}

namespace
{
template <typename Sample>
nsc55_status Render(nsc55_handle* handle,
                    Sample* destination,
                    size_t frame_count,
                    SinkFormat format,
                    char* error_utf8,
                    size_t error_capacity)
{
    if (handle == nullptr || (destination == nullptr && frame_count != 0))
    {
        WriteError(error_utf8, error_capacity, "Invalid native render arguments.");
        return NSC55_STATUS_INVALID_ARGUMENT;
    }

    const auto invalid_event = std::find_if(handle->events.begin(), handle->events.end(), [frame_count](const MidiEvent& event) {
        return event.frame_offset >= frame_count;
    });
    if (invalid_event != handle->events.end())
    {
        WriteError(error_utf8, error_capacity, "A MIDI frame offset lies outside the render block.");
        return NSC55_STATUS_INVALID_ARGUMENT;
    }

    if (frame_count == 0)
    {
        WriteError(error_utf8, error_capacity, {});
        return NSC55_STATUS_SUCCESS;
    }

    try
    {
        handle->Render(destination, frame_count, format);
        WriteError(error_utf8, error_capacity, {});
        return NSC55_STATUS_SUCCESS;
    }
    catch (const std::exception& exception)
    {
        handle->sink_format = SinkFormat::None;
        handle->sink = nullptr;
        WriteError(error_utf8, error_capacity, exception.what());
        return NSC55_STATUS_RENDERING_FAILED;
    }
    catch (...)
    {
        handle->sink_format = SinkFormat::None;
        handle->sink = nullptr;
        WriteError(error_utf8, error_capacity, "Unknown native rendering failure.");
        return NSC55_STATUS_RENDERING_FAILED;
    }
}
} // namespace

extern "C" NSC55_API nsc55_status nsc55_render_f32(nsc55_handle* handle,
                                                    float* interleaved_stereo,
                                                    size_t frame_count,
                                                    char* error_utf8,
                                                    size_t error_capacity)
{
    return Render(handle, interleaved_stereo, frame_count, SinkFormat::Float32, error_utf8, error_capacity);
}

extern "C" NSC55_API nsc55_status nsc55_render_s16(nsc55_handle* handle,
                                                    int16_t* interleaved_stereo,
                                                    size_t frame_count,
                                                    char* error_utf8,
                                                    size_t error_capacity)
{
    return Render(handle, interleaved_stereo, frame_count, SinkFormat::Signed16, error_utf8, error_capacity);
}
