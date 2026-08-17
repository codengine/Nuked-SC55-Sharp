#!/usr/bin/env bash
set -euo pipefail

binary=$1
platform=$2
architecture=$3

if [[ $platform == linux ]]; then
    file "$binary" | grep -q "ELF 64-bit"
    if [[ $architecture == x64 ]]; then
        file "$binary" | grep -q "x86-64"
    else
        file "$binary" | grep -Eq "ARM aarch64|aarch64"
    fi

    mapfile -t exports < <(nm -D --defined-only "$binary" | awk '{print $3}' | grep '^nsc55_' | sort -u)
    expected=(nsc55_abi_version nsc55_clear_midi nsc55_create nsc55_destroy nsc55_queue_midi nsc55_render_f32 nsc55_render_s16 nsc55_reset_emulator nsc55_romset_supported nsc55_sample_rate)
    diff <(printf '%s\n' "${expected[@]}" | sort) <(printf '%s\n' "${exports[@]}")
    ! ldd "$binary" | grep -Eiq 'libstdc\+\+|libgcc|SDL|speex|rtmidi|clap'
    ! nm -C --defined-only "$binary" | grep -Eq 'LCD_FontRender|back_palette|lcd_font'

    versions=$(objdump -T "$binary" | grep -o 'GLIBC_[0-9][0-9.]*' | sed 's/GLIBC_//' | sort -Vu)
    while read -r version; do
        [[ -z $version ]] && continue
        oldest=$(printf '%s\n2.28\n' "$version" | sort -V | head -n 1)
        [[ $oldest == "$version" ]]
    done <<< "$versions"
elif [[ $platform == macos ]]; then
    file "$binary" | grep -q "Mach-O 64-bit"
    if [[ $architecture == x64 ]]; then
        file "$binary" | grep -q "x86_64"
    else
        file "$binary" | grep -q "arm64"
    fi

    nm -gU "$binary" | awk '{print $3}' | grep '^_nsc55_' | sed 's/^_//' | sort -u > /tmp/nsc55-exports.txt
    printf '%s\n' nsc55_abi_version nsc55_clear_midi nsc55_create nsc55_destroy nsc55_queue_midi nsc55_render_f32 nsc55_render_s16 nsc55_reset_emulator nsc55_romset_supported nsc55_sample_rate | sort > /tmp/nsc55-expected.txt
    diff /tmp/nsc55-expected.txt /tmp/nsc55-exports.txt
    ! nm -C --defined-only "$binary" | grep -Eq 'LCD_FontRender|back_palette|lcd_font'
    dependencies=$(otool -L "$binary" | tail -n +2)
    ! grep -Eiq 'SDL|speex|rtmidi|clap' <<< "$dependencies"
    unexpected=$(grep -Ev '^\s+/(usr/lib|System/Library)/' <<< "$dependencies" || true)
    [[ -z $unexpected ]]
else
    echo "Unsupported platform: $platform" >&2
    exit 2
fi

echo "Native binary verified: $binary"
