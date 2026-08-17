#include "nuked_sc55_sharp.h"

#include <array>
#include <cassert>
#include <chrono>
#include <cstdint>
#include <filesystem>
#include <string>

int main()
{
    assert(nsc55_abi_version() == 1);

    constexpr std::array romsets = {
        "mk2-v1.01",
        "sc155mk2-v1.01",
        "st-v1.01",
        "mk1-v1.00",
        "mk1-v1.10",
        "mk1-v1.20",
        "mk1-v1.21",
        "mk1-v2.00",
        "cm300-v1.10",
        "cm300-v1.20",
        "cm300-v1.30",
        "jv880-v1.0.0",
        "jv880-v1.0.1",
        "scb55-v2.00",
        "rlp3237-v2.01",
        "sc155-rev1",
        "mk2-ctf-strict-sc55-drum-sc55-v1.21",
        "mk2-ctf-strict-sc55-drum-sc55-v2.00",
        "mk2-ctf-sc55-drum-sc55-v1.21",
        "mk2-ctf-sc55-drum-sc55-v2.00",
        "mk2-ctf-mk2-drum-sc55-v1.21",
        "mk2-ctf-mk2-drum-sc55-v2.00",
        "sc155mk2-ctf-strict-sc55-drum-sc55-v1.21",
        "sc155mk2-ctf-strict-sc55-drum-sc55-v2.00",
        "sc155mk2-ctf-sc55-drum-sc55-v1.21",
        "sc155mk2-ctf-sc55-drum-sc55-v2.00",
        "sc155mk2-ctf-mk2-drum-sc55-v1.21",
        "sc155mk2-ctf-mk2-drum-sc55-v2.00",
    };
    for (const char* romset : romsets)
    {
        assert(nsc55_romset_supported(romset) == 1);
    }
    assert(nsc55_romset_supported("not-a-romset") == 0);
    assert(nsc55_romset_supported(nullptr) == 0);

    nsc55_handle*          handle = nullptr;
    std::array<char, 1024> error{};
    nsc55_status status = nsc55_create("directory-that-does-not-exist",
                                       "mk1-v1.00",
                                       0,
                                       NSC55_RESET_GENERAL_STANDARD,
                                       0,
                                       nullptr,
                                       &handle,
                                       error.data(),
                                       error.size());
    assert(status == NSC55_STATUS_ROMSET_INCOMPLETE);
    assert(handle == nullptr);
    assert(std::string(error.data()).find("Failed to detect romsets: mk1-v1.00") != std::string::npos);

    const auto unique = std::chrono::steady_clock::now().time_since_epoch().count();
    const auto empty_directory = std::filesystem::temp_directory_path() /
                                 ("NukedSC55Sharp-native-contracts-" + std::to_string(unique));
    assert(std::filesystem::create_directory(empty_directory));

    const std::string empty_directory_utf8 = empty_directory.string();
    error.fill(0);
    status = nsc55_create(empty_directory_utf8.c_str(),
                          "mk1-v1.00",
                          0,
                          NSC55_RESET_GENERAL_STANDARD,
                          0,
                          nullptr,
                          &handle,
                          error.data(),
                          error.size());
    assert(status == NSC55_STATUS_ROMSET_INCOMPLETE);
    assert(handle == nullptr);
    assert(std::string(error.data()).find("Requested romset is incomplete: mk1-v1.00; missing") !=
           std::string::npos);

    error.fill(0);
    status = nsc55_create(empty_directory_utf8.c_str(),
                          "not-a-romset",
                          0,
                          NSC55_RESET_NONE,
                          0,
                          nullptr,
                          &handle,
                          error.data(),
                          error.size());
    assert(status == NSC55_STATUS_ROMSET_NOT_FOUND);
    assert(handle == nullptr);
    assert(std::string(error.data()).find("Invalid romset name: not-a-romset") != std::string::npos);

    error.fill(0);
    status = nsc55_create(nullptr,
                          "mk1-v1.00",
                          0,
                          NSC55_RESET_NONE,
                          0,
                          nullptr,
                          &handle,
                          error.data(),
                          error.size());
    assert(status == NSC55_STATUS_INVALID_ARGUMENT);
    assert(handle == nullptr);
    assert(std::string(error.data()) == "Invalid native creation arguments.");
    assert(std::filesystem::remove(empty_directory));
    return 0;
}
