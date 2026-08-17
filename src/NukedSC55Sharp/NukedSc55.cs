using System.Runtime.InteropServices;
using System.Text;

namespace NukedSC55Sharp;

/// <summary>Owns one caller-serialized Nuked-SC55 instance that converts raw MIDI bytes to native-rate stereo PCM.</summary>
/// <remarks>The caller owns audio playback, mixing, resampling, and synchronization. Separate instances are independent.</remarks>
public sealed unsafe class NukedSc55 : IDisposable
{
    private const uint AbiVersion = 1;
    private const int ErrorCapacity = 4096;
    private readonly byte[] _errorBuffer = new byte[ErrorCapacity];
    private readonly SafeNukedSc55Handle _handle;
    private bool _disposed;
    private int _maximumQueuedFrameOffset = -1;

    /// <summary>Creates and settles one emulator after loading the exact selected ROM set by content hash.</summary>
    /// <param name="options">ROM, reset, rate, and optional JV-880 persistence settings to snapshot.</param>
    /// <exception cref="DirectoryNotFoundException">The ROM directory does not exist.</exception>
    /// <exception cref="ArgumentException">NVRAM was configured for non-JV-880 hardware.</exception>
    /// <exception cref="NukedSc55Exception">The native binary or selected ROM set cannot initialize.</exception>
    public NukedSc55(NukedSc55Options options)
    {
        options = options ?? throw new ArgumentNullException(nameof(options));

        var descriptor = RomSetCatalog.GetDescriptor(options.RomSet);
        if (!Directory.Exists(options.RomDirectory))
        {
            throw new DirectoryNotFoundException($"The ROM directory does not exist: {options.RomDirectory}");
        }

        if (options.NvramPath is not null && !descriptor.IsJv880)
        {
            throw new ArgumentException("NVRAM persistence is available only for JV-880 ROM sets.", nameof(options));
        }
        if (options.NvramPath is not null && string.IsNullOrWhiteSpace(options.NvramPath))
        {
            throw new ArgumentException("The NVRAM path cannot be blank.", nameof(options));
        }

        VerifyAbi();
        var romDirectory = ToNullTerminatedUtf8(options.RomDirectory);
        var romSet = ToNullTerminatedUtf8(descriptor.Identifier);
        var nvramPath = options.NvramPath is null ? null : ToNullTerminatedUtf8(options.NvramPath);
        var reset = RomSetCatalog.ResolveReset(options.InitialReset, descriptor);

        IntPtr nativeHandle;
        NativeStatus status;
        fixed (byte* romDirectoryPointer = romDirectory)
        fixed (byte* romSetPointer = romSet)
        fixed (byte* nvramPointer = nvramPath)
        fixed (byte* errorPointer = _errorBuffer)
        {
            status = NativeMethods.Create(romDirectoryPointer,
                romSetPointer,
                options.EnableOversampling ? 1 : 0,
                reset,
                descriptor.StartupSteps,
                nvramPointer,
                out nativeHandle,
                errorPointer,
                (UIntPtr)_errorBuffer.Length);
        }

        NativeCall.EnsureSuccess(status, _errorBuffer, "The native emulator could not be created.");
        _handle = new SafeNukedSc55Handle(nativeHandle);
        RomSet = options.RomSet;
        OversamplingEnabled = options.EnableOversampling;
        SampleRate = checked((int)NativeMethods.GetSampleRate(_handle));
    }

    /// <summary>Gets the fixed number of interleaved output channels.</summary>
    public const int ChannelCount = 2;

    /// <summary>Gets the exact ROM-set revision loaded by this instance.</summary>
    public NukedSc55RomSet RomSet { get; }

    /// <summary>Gets the native PCM rate selected by the hardware and oversampling setting.</summary>
    public int SampleRate { get; }

    /// <summary>Gets whether the instance emits the native core's doubled PCM rate.</summary>
    public bool OversamplingEnabled { get; }

    /// <summary>Copies raw MIDI UART bytes to an ordered offset within the next render call.</summary>
    /// <param name="bytes">One or more raw bytes; complete SysEx must include both <c>F0</c> and <c>F7</c>.</param>
    /// <param name="frameOffset">Zero-based native PCM frame offset in the next render block.</param>
    public void QueueMidi(ReadOnlySpan<byte> bytes, int frameOffset = 0)
    {
        ThrowIfDisposed();
        if (bytes.IsEmpty)
        {
            throw new ArgumentException("MIDI data must contain at least one byte.", nameof(bytes));
        }
        if (frameOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameOffset), frameOffset, "A MIDI frame offset cannot be negative.");
        }

        NativeStatus status;
        fixed (byte* bytesPointer = bytes)
        fixed (byte* errorPointer = _errorBuffer)
        {
            status = NativeMethods.QueueMidi(_handle,
                bytesPointer,
                (UIntPtr)bytes.Length,
                (uint)frameOffset,
                errorPointer,
                (UIntPtr)_errorBuffer.Length);
        }

        NativeCall.EnsureSuccess(status, _errorBuffer, "The native emulator could not queue MIDI data.");
        _maximumQueuedFrameOffset = Math.Max(_maximumQueuedFrameOffset, frameOffset);
    }

    /// <summary>Cancels every MIDI event waiting for the next render call.</summary>
    public void ClearQueuedMidi()
    {
        ThrowIfDisposed();
        NativeMethods.ClearMidi(_handle);
        _maximumQueuedFrameOffset = -1;
    }

    /// <summary>Renders normalized interleaved floating-point stereo at <see cref="SampleRate" />.</summary>
    /// <param name="interleavedStereo">Destination containing two samples per requested frame.</param>
    public void Render(Span<float> interleavedStereo)
    {
        var frameCount = ValidateRender(interleavedStereo.Length, nameof(interleavedStereo));
        NativeStatus status;
        fixed (float* destination = interleavedStereo)
        fixed (byte* errorPointer = _errorBuffer)
        {
            status = NativeMethods.RenderFloat(_handle,
                destination,
                (UIntPtr)frameCount,
                errorPointer,
                (UIntPtr)_errorBuffer.Length);
        }

        NativeCall.EnsureSuccess(status, _errorBuffer, "The native emulator could not render floating-point PCM.");
        _maximumQueuedFrameOffset = -1;
    }

    /// <summary>Renders clamped interleaved signed-16 stereo at <see cref="SampleRate" />.</summary>
    /// <param name="interleavedStereo">Destination containing two samples per requested frame.</param>
    public void Render(Span<short> interleavedStereo)
    {
        var frameCount = ValidateRender(interleavedStereo.Length, nameof(interleavedStereo));
        NativeStatus status;
        fixed (short* destination = interleavedStereo)
        fixed (byte* errorPointer = _errorBuffer)
        {
            status = NativeMethods.RenderInt16(_handle,
                destination,
                (UIntPtr)frameCount,
                errorPointer,
                (UIntPtr)_errorBuffer.Length);
        }

        NativeCall.EnsureSuccess(status, _errorBuffer, "The native emulator could not render signed-16 PCM.");
        _maximumQueuedFrameOffset = -1;
    }

    /// <summary>Clears queued MIDI and repeats the configured hardware reset and startup settling.</summary>
    public void Reset()
    {
        ThrowIfDisposed();
        NativeStatus status;
        fixed (byte* errorPointer = _errorBuffer)
        {
            status = NativeMethods.Reset(_handle, errorPointer, (UIntPtr)_errorBuffer.Length);
        }

        NativeCall.EnsureSuccess(status, _errorBuffer, "The native emulator could not reset.");
        _maximumQueuedFrameOffset = -1;
    }

    /// <summary>Releases native state and performs the upstream clean-disposal NVRAM save when configured.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _handle.Dispose();
        _disposed = true;
    }

    private int ValidateRender(int sampleCount, string parameterName)
    {
        ThrowIfDisposed();
        if ((sampleCount & 1) != 0)
        {
            throw new ArgumentException("An interleaved stereo destination must contain an even sample count.", parameterName);
        }

        var frameCount = sampleCount / ChannelCount;
        if (_maximumQueuedFrameOffset >= frameCount)
        {
            throw new ArgumentOutOfRangeException(parameterName,
                "A queued MIDI frame offset lies outside this render block.");
        }

        return frameCount;
    }

    private void ThrowIfDisposed()
    {
#if NETSTANDARD2_1
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(NukedSc55));
        }
#else
        ObjectDisposedException.ThrowIf(_disposed, this);
#endif
    }

    private static byte[] ToNullTerminatedUtf8(string value)
    {
        return Encoding.UTF8.GetBytes(value + '\0');
    }

    private static void VerifyAbi()
    {
        uint version;
        try
        {
            version = NativeMethods.GetAbiVersion();
        }
        catch (DllNotFoundException exception)
        {
            throw new NukedSc55Exception(NukedSc55ErrorCode.NativeLibraryUnavailable,
                "The NukedSC55Sharp native library is unavailable for this runtime.",
                exception);
        }
        catch (BadImageFormatException exception)
        {
            throw new NukedSc55Exception(NukedSc55ErrorCode.NativeLibraryUnavailable,
                "The NukedSC55Sharp native library has the wrong architecture.",
                exception);
        }
        catch (EntryPointNotFoundException exception)
        {
            throw new NukedSc55Exception(NukedSc55ErrorCode.NativeAbiMismatch,
                "The NukedSC55Sharp native library does not expose ABI version 1.",
                exception);
        }

        if (version != AbiVersion)
        {
            throw new NukedSc55Exception(NukedSc55ErrorCode.NativeAbiMismatch,
                $"The managed library requires native ABI {AbiVersion}, but found ABI {version}.");
        }
    }
}
