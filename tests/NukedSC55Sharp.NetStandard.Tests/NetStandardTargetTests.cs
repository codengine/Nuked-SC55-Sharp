namespace NukedSC55Sharp.NetStandard.Tests;

/// <summary>Proves the forced consumer reference resolves the netstandard2.1 assembly.</summary>
public sealed class NetStandardTargetTests
{
    /// <summary>Exercises the contract through the project reference forced to the netstandard2.1 target.</summary>
    [Fact]
    public void ForcedNetStandardReferenceExposesManagedContract()
    {
        var options = new NukedSc55Options("roms", NukedSc55RomSet.Mk2V101);

        Assert.Equal(NukedSc55RomSet.Mk2V101, options.RomSet);
    }
}
