using NukedSC55Sharp;

var directory = Directory.CreateTempSubdirectory("NukedSC55Sharp-Package-");
try
{
    var options = new NukedSc55Options(directory.FullName, NukedSc55RomSet.Mk2V101);
    try
    {
        using var synth = new NukedSc55(options);
        return 1;
    }
    catch (NukedSc55Exception exception) when (exception.Code == NukedSc55ErrorCode.RomSetIncomplete &&
                                                exception.Message.Contains("mk2-v1.01", StringComparison.Ordinal))
    {
        return 0;
    }
}
finally
{
    directory.Delete(true);
}
