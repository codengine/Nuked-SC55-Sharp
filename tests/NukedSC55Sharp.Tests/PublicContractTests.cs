namespace NukedSC55Sharp.Tests;

/// <summary>Proves the managed configuration, complete ROM-set enum, and native error contract.</summary>
public sealed class PublicContractTests
{
    /// <summary>Proves caller options retain their required selection and documented defaults.</summary>
    [Fact]
    public void OptionsExposeDocumentedDefaults()
    {
        var options = new NukedSc55Options("roms", NukedSc55RomSet.Mk1V121);

        Assert.Equal("roms", options.RomDirectory);
        Assert.Equal(NukedSc55RomSet.Mk1V121, options.RomSet);
        Assert.False(options.EnableOversampling);
        Assert.Equal(NukedSc55InitialReset.Automatic, options.InitialReset);
        Assert.Null(options.NvramPath);
    }

    /// <summary>Proves every standard and patched upstream ROM set has a stable managed value in registry order.</summary>
    [Fact]
    public void RomSetEnumContainsCompletePinnedRegistry()
    {
        NukedSc55RomSet[] expected =
        [
            NukedSc55RomSet.Mk2V101,
            NukedSc55RomSet.Sc155Mk2V101,
            NukedSc55RomSet.StV101,
            NukedSc55RomSet.Mk1V100,
            NukedSc55RomSet.Mk1V110,
            NukedSc55RomSet.Mk1V120,
            NukedSc55RomSet.Mk1V121,
            NukedSc55RomSet.Mk1V200,
            NukedSc55RomSet.Cm300V110,
            NukedSc55RomSet.Cm300V120,
            NukedSc55RomSet.Cm300V130,
            NukedSc55RomSet.Jv880V100,
            NukedSc55RomSet.Jv880V101,
            NukedSc55RomSet.Scb55V200,
            NukedSc55RomSet.Rlp3237V201,
            NukedSc55RomSet.Sc155Rev1,
            NukedSc55RomSet.Mk2CtfStrictSc55DrumSc55V121,
            NukedSc55RomSet.Mk2CtfStrictSc55DrumSc55V200,
            NukedSc55RomSet.Mk2CtfSc55DrumSc55V121,
            NukedSc55RomSet.Mk2CtfSc55DrumSc55V200,
            NukedSc55RomSet.Mk2CtfMk2DrumSc55V121,
            NukedSc55RomSet.Mk2CtfMk2DrumSc55V200,
            NukedSc55RomSet.Sc155Mk2CtfStrictSc55DrumSc55V121,
            NukedSc55RomSet.Sc155Mk2CtfStrictSc55DrumSc55V200,
            NukedSc55RomSet.Sc155Mk2CtfSc55DrumSc55V121,
            NukedSc55RomSet.Sc155Mk2CtfSc55DrumSc55V200,
            NukedSc55RomSet.Sc155Mk2CtfMk2DrumSc55V121,
            NukedSc55RomSet.Sc155Mk2CtfMk2DrumSc55V200
        ];

        Assert.Equal(expected, Enum.GetValues<NukedSc55RomSet>());
    }

    /// <summary>Proves each managed value reaches the exact native identifier registered at the pinned commit.</summary>
    [Fact]
    public void EveryRomSetMapsToRecognizedNativeIdentifier()
    {
        var identifiers = new[]
        {
            "mk2-v1.01", "sc155mk2-v1.01", "st-v1.01", "mk1-v1.00", "mk1-v1.10", "mk1-v1.20",
            "mk1-v1.21", "mk1-v2.00", "cm300-v1.10", "cm300-v1.20", "cm300-v1.30", "jv880-v1.0.0",
            "jv880-v1.0.1", "scb55-v2.00", "rlp3237-v2.01", "sc155-rev1",
            "mk2-ctf-strict-sc55-drum-sc55-v1.21", "mk2-ctf-strict-sc55-drum-sc55-v2.00",
            "mk2-ctf-sc55-drum-sc55-v1.21", "mk2-ctf-sc55-drum-sc55-v2.00",
            "mk2-ctf-mk2-drum-sc55-v1.21", "mk2-ctf-mk2-drum-sc55-v2.00",
            "sc155mk2-ctf-strict-sc55-drum-sc55-v1.21", "sc155mk2-ctf-strict-sc55-drum-sc55-v2.00",
            "sc155mk2-ctf-sc55-drum-sc55-v1.21", "sc155mk2-ctf-sc55-drum-sc55-v2.00",
            "sc155mk2-ctf-mk2-drum-sc55-v1.21", "sc155mk2-ctf-mk2-drum-sc55-v2.00"
        };
        var directory = Directory.CreateTempSubdirectory("NukedSC55Sharp-");
        try
        {
            var romSets = Enum.GetValues<NukedSc55RomSet>();
            for (var index = 0; index < romSets.Length; index++)
            {
                var options = new NukedSc55Options(directory.FullName, romSets[index]);
                var exception = Assert.Throws<NukedSc55Exception>(() => new NukedSc55(options));
                Assert.Equal(NukedSc55ErrorCode.RomSetIncomplete, exception.Code);
                Assert.Contains(identifiers[index], exception.Message, StringComparison.Ordinal);
            }
        }
        finally
        {
            directory.Delete(true);
        }
    }

    /// <summary>Proves persistent NVRAM is rejected before native creation for non-JV-880 hardware.</summary>
    [Fact]
    public void NvramPathRequiresJv880RomSet()
    {
        var directory = Directory.CreateTempSubdirectory("NukedSC55Sharp-");
        try
        {
            var options = new NukedSc55Options(directory.FullName, NukedSc55RomSet.Mk1V121)
            {
                NvramPath = Path.Combine(directory.FullName, "state.bin")
            };

            Assert.Throws<ArgumentException>(() => new NukedSc55(options));
        }
        finally
        {
            directory.Delete(true);
        }
    }
}
