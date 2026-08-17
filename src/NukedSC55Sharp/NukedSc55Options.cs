namespace NukedSC55Sharp;

/// <summary>Configures ROM selection and hardware startup for one emulator instance.</summary>
public sealed class NukedSc55Options
{
    /// <summary>Creates options for one exact ROM set in a caller-owned ROM directory.</summary>
    /// <param name="romDirectory">Directory whose files are matched by their SHA-256 content.</param>
    /// <param name="romSet">Exact hardware and firmware revision to load.</param>
    public NukedSc55Options(string romDirectory, NukedSc55RomSet romSet)
    {
        if (string.IsNullOrWhiteSpace(romDirectory))
        {
            throw new ArgumentException("A ROM directory is required.", nameof(romDirectory));
        }

        RomDirectory = romDirectory;
        RomSet = romSet;
    }

    /// <summary>Gets the directory scanned for ROM content; filenames do not select the ROM set.</summary>
    public string RomDirectory { get; }

    /// <summary>Gets the exact hardware and firmware revision requested by the caller.</summary>
    public NukedSc55RomSet RomSet { get; }

    /// <summary>Gets or sets whether the native PCM engine emits its doubled output rate.</summary>
    public bool EnableOversampling { get; set; }

    /// <summary>Gets or sets the reset sent before startup settling.</summary>
    public NukedSc55InitialReset InitialReset { get; set; } = NukedSc55InitialReset.Automatic;

    /// <summary>Gets or sets the optional JV-880 NVRAM file loaded at startup and saved on clean disposal.</summary>
    public string? NvramPath { get; set; }
}
