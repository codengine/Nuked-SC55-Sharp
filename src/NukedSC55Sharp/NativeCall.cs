using System.Text;

namespace NukedSC55Sharp;

/// <summary>Translates stable native status values and UTF-8 diagnostics into managed exceptions.</summary>
internal static class NativeCall
{
    /// <summary>Throws a categorized exception when a native operation did not succeed.</summary>
    /// <param name="status">Status returned by the native ABI.</param>
    /// <param name="errorBuffer">Null-terminated UTF-8 diagnostic buffer filled by the same call.</param>
    /// <param name="fallback">Message used when the native diagnostic is empty.</param>
    internal static void EnsureSuccess(NativeStatus status, byte[] errorBuffer, string fallback)
    {
        if (status == NativeStatus.Success)
        {
            return;
        }

        var terminator = Array.IndexOf(errorBuffer, (byte)0);
        var length = terminator < 0 ? errorBuffer.Length : terminator;
        var message = length == 0 ? fallback : Encoding.UTF8.GetString(errorBuffer, 0, length);
        throw new NukedSc55Exception(Map(status), message);
    }

    /// <summary>Maps one native ABI status to the stable public error taxonomy.</summary>
    /// <param name="status">Failed native status.</param>
    /// <returns>Corresponding managed error category.</returns>
    internal static NukedSc55ErrorCode Map(NativeStatus status)
    {
        return status switch
        {
            NativeStatus.RomSetNotFound => NukedSc55ErrorCode.RomSetNotFound,
            NativeStatus.RomSetIncomplete => NukedSc55ErrorCode.RomSetIncomplete,
            NativeStatus.RomLoadFailed => NukedSc55ErrorCode.RomLoadFailed,
            NativeStatus.InitializationFailed => NukedSc55ErrorCode.InitializationFailed,
            NativeStatus.RenderingFailed => NukedSc55ErrorCode.RenderingFailed,
            _ => NukedSc55ErrorCode.NativeFailure,
        };
    }
}
