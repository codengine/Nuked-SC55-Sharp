# Nuked-SC55-Sharp

Nuked-SC55-Sharp provides managed .NET bindings and packaged native binaries
for [Nuked-SC55](https://github.com/jcmoyer/Nuked-SC55). It accepts raw MIDI
bytes and renders native-rate interleaved stereo PCM without owning an audio
device, mixer, thread, resampler, or visual LCD renderer.

> [!IMPORTANT]
> The native component uses Nuked-SC55's MAME-derived license. It may not be
> sold or used in a commercial product or activity. See [LICENSE](LICENSE).

## Supported runtimes

The managed assembly targets `netstandard2.1`, `net8.0`, and `net10.0`. The
single package contains native assets for:

- Windows x64 and ARM64
- Linux glibc x64 and ARM64
- macOS x64 and ARM64

ROM images are not distributed. The loader identifies files by SHA-256 content,
so their filenames do not select the emulated revision.

## Basic use

```csharp
using NukedSC55Sharp;

var options = new NukedSc55Options("path/to/roms", NukedSc55RomSet.Mk1V121);
using var sc55 = new NukedSc55(options);

const int frames = 512;
var pcm = new float[frames * NukedSc55.ChannelCount];

sc55.QueueMidi(stackalloc byte[] { 0x90, 60, 100 }, frameOffset: 0);
sc55.QueueMidi(stackalloc byte[] { 0x80, 60, 0 }, frameOffset: 400);
sc55.Render(pcm);

// Resample sc55.SampleRate to your mixer rate, then mix or play pcm.
```

`QueueMidi` copies raw UART bytes. A system-exclusive message must include its
complete wire representation, including `F0` and `F7`. Frame offsets are
relative to the next `Render` call and equal offsets retain enqueue order.

Use `Render(Span<float>)` for normalized floating-point samples or
`Render(Span<short>)` for clamped signed 16-bit samples. Each destination is
interleaved left/right stereo and must contain an even number of samples.

Instances are independent but not thread-safe. The caller must serialize all
operations on each instance.

## Rates and startup

The default output is the native 32,000 or 33,103 Hz rate. Set
`EnableOversampling` before creating the instance to use the core's 64,000 or
66,207 Hz mode. `SampleRate` reports the exact selected rate.

`InitialReset = Automatic` applies the model-specific reference behavior.
Construction and `Reset()` perform model-specific startup settling before
returning, so these operations can take noticeable time.

## JV-880 NVRAM

Set `NvramPath` only with `Jv880V100` or `Jv880V101`. Nuked-SC55 loads that file
during initialization and saves it when the instance is cleanly disposed.

## Exact ROM sets

`NukedSc55RomSet` exposes every exact standard and CTF-patched set registered by
the pinned Nuked-SC55 core. The CTF variants are deprecated upstream patches but
remain first-class values in version 0.7.0 of this package.
