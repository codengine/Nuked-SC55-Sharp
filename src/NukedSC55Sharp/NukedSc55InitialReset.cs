namespace NukedSC55Sharp;

/// <summary>Selects the MIDI reset sent before startup settling.</summary>
public enum NukedSc55InitialReset
{
    /// <summary>Uses the model-specific reference reset behavior.</summary>
    Automatic,
    /// <summary>Leaves the emulated hardware in its natural power-on mode.</summary>
    None,
    /// <summary>Sends the standard General MIDI reset sequence.</summary>
    GeneralMidi,
    /// <summary>Sends the Roland General Standard reset sequence.</summary>
    GeneralStandard,
}
