namespace NukedSC55Sharp;

/// <summary>Holds the exact native identifier and proven startup policy for one managed ROM-set value.</summary>
internal readonly struct RomSetDescriptor
{
    /// <summary>Creates one immutable ROM-set mapping.</summary>
    /// <param name="identifier">Exact identifier registered by the pinned native core.</param>
    /// <param name="automaticReset">Reset selected when the caller requests automatic behavior.</param>
    /// <param name="startupSteps">Core steps discarded before audio is exposed.</param>
    /// <param name="isJv880">Whether the hardware accepts persistent NVRAM.</param>
    internal RomSetDescriptor(string identifier, NativeReset automaticReset, ulong startupSteps, bool isJv880 = false)
    {
        Identifier = identifier;
        AutomaticReset = automaticReset;
        StartupSteps = startupSteps;
        IsJv880 = isJv880;
    }

    /// <summary>Gets the exact identifier accepted by the pinned core.</summary>
    internal string Identifier { get; }

    /// <summary>Gets the reference-matched automatic reset.</summary>
    internal NativeReset AutomaticReset { get; }

    /// <summary>Gets the reference-matched startup settling length.</summary>
    internal ulong StartupSteps { get; }

    /// <summary>Gets whether this set belongs to the JV-880 family.</summary>
    internal bool IsJv880 { get; }
}

/// <summary>Maps the complete managed ROM-set contract to the pinned upstream registry.</summary>
internal static class RomSetCatalog
{
    private const ulong ShortMk1StartupSteps = 700_000;
    private const ulong Mk2StartupSteps = 9_500_000;
    private const ulong FullStartupSteps = 24_000_000;

    private static readonly RomSetDescriptor[] Descriptors =
    [
        new("mk2-v1.01", NativeReset.GeneralStandard, Mk2StartupSteps),
        new("sc155mk2-v1.01", NativeReset.None, FullStartupSteps),
        new("st-v1.01", NativeReset.None, FullStartupSteps),
        new("mk1-v1.00", NativeReset.GeneralStandard, ShortMk1StartupSteps),
        new("mk1-v1.10", NativeReset.GeneralStandard, ShortMk1StartupSteps),
        new("mk1-v1.20", NativeReset.GeneralStandard, ShortMk1StartupSteps),
        new("mk1-v1.21", NativeReset.GeneralStandard, ShortMk1StartupSteps),
        new("mk1-v2.00", NativeReset.GeneralStandard, ShortMk1StartupSteps),
        new("cm300-v1.10", NativeReset.None, FullStartupSteps),
        new("cm300-v1.20", NativeReset.None, FullStartupSteps),
        new("cm300-v1.30", NativeReset.None, FullStartupSteps),
        new("jv880-v1.0.0", NativeReset.None, FullStartupSteps, true),
        new("jv880-v1.0.1", NativeReset.None, FullStartupSteps, true),
        new("scb55-v2.00", NativeReset.None, FullStartupSteps),
        new("rlp3237-v2.01", NativeReset.None, FullStartupSteps),
        new("sc155-rev1", NativeReset.None, FullStartupSteps),
        new("mk2-ctf-strict-sc55-drum-sc55-v1.21", NativeReset.GeneralStandard, FullStartupSteps),
        new("mk2-ctf-strict-sc55-drum-sc55-v2.00", NativeReset.GeneralStandard, FullStartupSteps),
        new("mk2-ctf-sc55-drum-sc55-v1.21", NativeReset.GeneralStandard, FullStartupSteps),
        new("mk2-ctf-sc55-drum-sc55-v2.00", NativeReset.GeneralStandard, FullStartupSteps),
        new("mk2-ctf-mk2-drum-sc55-v1.21", NativeReset.GeneralStandard, FullStartupSteps),
        new("mk2-ctf-mk2-drum-sc55-v2.00", NativeReset.GeneralStandard, FullStartupSteps),
        new("sc155mk2-ctf-strict-sc55-drum-sc55-v1.21", NativeReset.None, FullStartupSteps),
        new("sc155mk2-ctf-strict-sc55-drum-sc55-v2.00", NativeReset.None, FullStartupSteps),
        new("sc155mk2-ctf-sc55-drum-sc55-v1.21", NativeReset.None, FullStartupSteps),
        new("sc155mk2-ctf-sc55-drum-sc55-v2.00", NativeReset.None, FullStartupSteps),
        new("sc155mk2-ctf-mk2-drum-sc55-v1.21", NativeReset.None, FullStartupSteps),
        new("sc155mk2-ctf-mk2-drum-sc55-v2.00", NativeReset.None, FullStartupSteps)
    ];

    /// <summary>Gets the descriptor for one defined public enum value.</summary>
    /// <param name="romSet">Public enum value selected by the caller.</param>
    /// <returns>The exact native mapping and startup policy.</returns>
    internal static RomSetDescriptor GetDescriptor(NukedSc55RomSet romSet)
    {
        var index = (int)romSet;
        return (uint)index >= (uint)Descriptors.Length
            ? throw new ArgumentOutOfRangeException(nameof(romSet), romSet, "The ROM set is not defined.")
            : Descriptors[index];
    }

    /// <summary>Resolves an explicit or automatic public reset to its native representation.</summary>
    /// <param name="reset">Caller-selected reset policy.</param>
    /// <param name="descriptor">ROM-set policy used for automatic selection.</param>
    /// <returns>The reset understood by the native ABI.</returns>
    internal static NativeReset ResolveReset(NukedSc55InitialReset reset, in RomSetDescriptor descriptor)
    {
        return reset switch
        {
            NukedSc55InitialReset.Automatic => descriptor.AutomaticReset,
            NukedSc55InitialReset.None => NativeReset.None,
            NukedSc55InitialReset.GeneralMidi => NativeReset.GeneralMidi,
            NukedSc55InitialReset.GeneralStandard => NativeReset.GeneralStandard,
            _ => throw new ArgumentOutOfRangeException(nameof(reset), reset, "The initial reset is not defined.")
        };
    }
}
