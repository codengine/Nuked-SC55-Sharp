namespace NukedSC55Sharp;

/// <summary>Classifies failures reported by the managed/native SC-55 boundary.</summary>
public enum NukedSc55ErrorCode
{
    /// <summary>The native binary could not be found or loaded for the current runtime.</summary>
    NativeLibraryUnavailable,
    /// <summary>The native binary does not implement the managed ABI version.</summary>
    NativeAbiMismatch,
    /// <summary>The native core does not recognize the requested ROM-set identifier.</summary>
    RomSetNotFound,
    /// <summary>The ROM directory does not contain every file required by the selected set.</summary>
    RomSetIncomplete,
    /// <summary>One or more identified ROM files could not be loaded.</summary>
    RomLoadFailed,
    /// <summary>The native emulator could not initialize its state.</summary>
    InitializationFailed,
    /// <summary>The native emulator failed while producing PCM frames.</summary>
    RenderingFailed,
    /// <summary>The native boundary reported a failure outside the more specific categories.</summary>
    NativeFailure,
}

/// <summary>Reports a native loading, ROM, initialization, or rendering failure with a stable category.</summary>
public sealed class NukedSc55Exception : Exception
{
    /// <summary>Creates an exception from one translated native failure.</summary>
    /// <param name="code">Stable managed failure category.</param>
    /// <param name="message">Diagnostic supplied by the native layer.</param>
    /// <param name="innerException">Platform exception that prevented native loading, when applicable.</param>
    internal NukedSc55Exception(NukedSc55ErrorCode code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    /// <summary>Gets the stable category suitable for caller error handling.</summary>
    public NukedSc55ErrorCode Code { get; }
}
