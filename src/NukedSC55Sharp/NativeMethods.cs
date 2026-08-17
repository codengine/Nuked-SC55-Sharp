using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace NukedSC55Sharp;

/// <summary>Mirrors status values returned by native ABI version 1.</summary>
internal enum NativeStatus
{
    /// <summary>The native operation completed.</summary>
    Success,
    /// <summary>The native call received an invalid argument.</summary>
    InvalidArgument,
    /// <summary>The requested ROM-set identifier is unknown.</summary>
    RomSetNotFound,
    /// <summary>The selected ROM set is incomplete or ambiguous.</summary>
    RomSetIncomplete,
    /// <summary>Identified ROM data could not be loaded.</summary>
    RomLoadFailed,
    /// <summary>Native emulator allocation or initialization failed.</summary>
    InitializationFailed,
    /// <summary>Native PCM rendering failed.</summary>
    RenderingFailed,
    /// <summary>An unexpected failure was contained by the C ABI.</summary>
    InternalError,
}

/// <summary>Mirrors MIDI reset values accepted by native ABI version 1.</summary>
internal enum NativeReset
{
    /// <summary>Does not send a reset sequence.</summary>
    None,
    /// <summary>Sends the General MIDI reset sequence.</summary>
    GeneralMidi,
    /// <summary>Sends the Roland General Standard reset sequence.</summary>
    GeneralStandard,
}

/// <summary>Declares the stable C ABI exported by the packaged native library.</summary>
internal static unsafe partial class NativeMethods
{
    private const string LibraryName = "NukedSC55Sharp.Native";

#if NETSTANDARD2_1
    /// <summary>Reads the native ABI version before creating an instance.</summary>
    [DllImport(LibraryName, EntryPoint = "nsc55_abi_version", CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint GetAbiVersion();

    /// <summary>Creates one native emulator and loads its exact ROM set.</summary>
    [DllImport(LibraryName, EntryPoint = "nsc55_create", CallingConvention = CallingConvention.Cdecl)]
    internal static extern NativeStatus Create(byte* romDirectory,
        byte* romSet,
        int enableOversampling,
        NativeReset initialReset,
        ulong startupSteps,
        byte* nvramPath,
        out IntPtr handle,
        byte* error,
        UIntPtr errorCapacity);

    /// <summary>Destroys one native emulator and triggers its clean NVRAM save.</summary>
    [DllImport(LibraryName, EntryPoint = "nsc55_destroy", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Destroy(IntPtr handle);

    /// <summary>Reads the PCM rate selected by the loaded hardware and oversampling policy.</summary>
    [DllImport(LibraryName, EntryPoint = "nsc55_sample_rate", CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint GetSampleRate(SafeNukedSc55Handle handle);

    /// <summary>Restores one instance to its configured initial playable state.</summary>
    [DllImport(LibraryName, EntryPoint = "nsc55_reset_emulator", CallingConvention = CallingConvention.Cdecl)]
    internal static extern NativeStatus Reset(SafeNukedSc55Handle handle, byte* error, UIntPtr errorCapacity);

    /// <summary>Copies raw UART bytes into the next render block's ordered event queue.</summary>
    [DllImport(LibraryName, EntryPoint = "nsc55_queue_midi", CallingConvention = CallingConvention.Cdecl)]
    internal static extern NativeStatus QueueMidi(SafeNukedSc55Handle handle,
        byte* bytes,
        UIntPtr length,
        uint frameOffset,
        byte* error,
        UIntPtr errorCapacity);

    /// <summary>Cancels every MIDI event waiting for the next render call.</summary>
    [DllImport(LibraryName, EntryPoint = "nsc55_clear_midi", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ClearMidi(SafeNukedSc55Handle handle);

    /// <summary>Renders normalized interleaved floating-point stereo frames.</summary>
    [DllImport(LibraryName, EntryPoint = "nsc55_render_f32", CallingConvention = CallingConvention.Cdecl)]
    internal static extern NativeStatus RenderFloat(SafeNukedSc55Handle handle,
        float* destination,
        UIntPtr frameCount,
        byte* error,
        UIntPtr errorCapacity);

    /// <summary>Renders clamped interleaved signed-16 stereo frames.</summary>
    [DllImport(LibraryName, EntryPoint = "nsc55_render_s16", CallingConvention = CallingConvention.Cdecl)]
    internal static extern NativeStatus RenderInt16(SafeNukedSc55Handle handle,
        short* destination,
        UIntPtr frameCount,
        byte* error,
        UIntPtr errorCapacity);
#else
    /// <summary>Reads the native ABI version before creating an instance.</summary>
    [LibraryImport(LibraryName, EntryPoint = "nsc55_abi_version")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial uint GetAbiVersion();

    /// <summary>Creates one native emulator and loads its exact ROM set.</summary>
    [LibraryImport(LibraryName, EntryPoint = "nsc55_create")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial NativeStatus Create(byte* romDirectory,
        byte* romSet,
        int enableOversampling,
        NativeReset initialReset,
        ulong startupSteps,
        byte* nvramPath,
        out IntPtr handle,
        byte* error,
        UIntPtr errorCapacity);

    /// <summary>Destroys one native emulator and triggers its clean NVRAM save.</summary>
    [LibraryImport(LibraryName, EntryPoint = "nsc55_destroy")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial void Destroy(IntPtr handle);

    /// <summary>Reads the PCM rate selected by the loaded hardware and oversampling policy.</summary>
    [LibraryImport(LibraryName, EntryPoint = "nsc55_sample_rate")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial uint GetSampleRate(SafeNukedSc55Handle handle);

    /// <summary>Restores one instance to its configured initial playable state.</summary>
    [LibraryImport(LibraryName, EntryPoint = "nsc55_reset_emulator")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial NativeStatus Reset(SafeNukedSc55Handle handle, byte* error, UIntPtr errorCapacity);

    /// <summary>Copies raw UART bytes into the next render block's ordered event queue.</summary>
    [LibraryImport(LibraryName, EntryPoint = "nsc55_queue_midi")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial NativeStatus QueueMidi(SafeNukedSc55Handle handle,
        byte* bytes,
        UIntPtr length,
        uint frameOffset,
        byte* error,
        UIntPtr errorCapacity);

    /// <summary>Cancels every MIDI event waiting for the next render call.</summary>
    [LibraryImport(LibraryName, EntryPoint = "nsc55_clear_midi")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial void ClearMidi(SafeNukedSc55Handle handle);

    /// <summary>Renders normalized interleaved floating-point stereo frames.</summary>
    [LibraryImport(LibraryName, EntryPoint = "nsc55_render_f32")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial NativeStatus RenderFloat(SafeNukedSc55Handle handle,
        float* destination,
        UIntPtr frameCount,
        byte* error,
        UIntPtr errorCapacity);

    /// <summary>Renders clamped interleaved signed-16 stereo frames.</summary>
    [LibraryImport(LibraryName, EntryPoint = "nsc55_render_s16")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial NativeStatus RenderInt16(SafeNukedSc55Handle handle,
        short* destination,
        UIntPtr frameCount,
        byte* error,
        UIntPtr errorCapacity);
#endif
}

/// <summary>Owns one opaque native emulator handle and guarantees eventual destruction.</summary>
internal sealed class SafeNukedSc55Handle : SafeHandle
{
    /// <summary>Wraps a successfully created native emulator handle.</summary>
    /// <param name="handle">Nonzero handle returned by native ABI version 1.</param>
    internal SafeNukedSc55Handle(IntPtr handle)
        : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }

    /// <summary>Gets whether the native handle is absent.</summary>
    public override bool IsInvalid => handle == IntPtr.Zero;

    /// <summary>Destroys native state; the C ABI guarantees this operation cannot throw.</summary>
    protected override bool ReleaseHandle()
    {
        NativeMethods.Destroy(handle);
        return true;
    }
}
