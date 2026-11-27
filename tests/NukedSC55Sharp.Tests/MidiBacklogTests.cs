using System.Globalization;

namespace NukedSC55Sharp.Tests;

/// <summary>
/// Proves that Reset starts from power-on state.
/// Runs on the caller's non-redistributable ROMs: Mk1V121 receives through the main MCU, Mk2V101 through the sub-MCU.
/// </summary>
public sealed class MidiBacklogTests
{
    // A held organ note measures 0.033-0.051 RMS on both sets; the threshold leaves a wide margin.
    private const double SoundingLevel = 0.010;

    private const int WindowsPerSecond = 10;

    private static readonly byte[] OrganOnChannel0 = [0xC0, 16];
    private static readonly byte[] NoteOn = [0x90, 60, 100];
    private static readonly byte[] TwoByteMessage = [0xCF, 0];

    /// <summary>
    /// After a sounding note and bytes left in the ring, Reset renders exactly what a freshly created instance renders.
    /// </summary>
    [Theory]
    [InlineData(NukedSc55RomSet.Mk1V121, NukedSc55InitialReset.Automatic)]
    [InlineData(NukedSc55RomSet.Mk1V121, NukedSc55InitialReset.None)]
    [InlineData(NukedSc55RomSet.Mk2V101, NukedSc55InitialReset.Automatic)]
    [InlineData(NukedSc55RomSet.Mk2V101, NukedSc55InitialReset.None)]
    public void ResetBehavesLikeFreshInstance(NukedSc55RomSet romSet, NukedSc55InitialReset initialReset)
    {
        byte[] sequence = [.. OrganOnChannel0, .. NoteOn];
        using var fresh = CreateOrSkip(romSet, initialReset);
        fresh.QueueMidi(sequence);
        var expected = Render(fresh, WindowsPerSecond);
        Assert.True(Max(Levels(expected), 0) >= SoundingLevel,
            $"The fresh instance did not sound: {Format(Levels(expected))}");

        using var synth = CreateOrSkip(romSet, initialReset);
        synth.QueueMidi([.. OrganOnChannel0, 0x90, 64, 100]);
        Render(synth, 3);
        synth.QueueMidi([.. Filler(2730), .. TwoByteMessage, 0x90, 67, 100]);
        Render(synth, 1);
        synth.Reset();
        synth.QueueMidi(sequence);
        var actual = Render(synth, WindowsPerSecond);

        Assert.True(expected.AsSpan().SequenceEqual(actual),
            $"After Reset the output differs from a fresh instance. Fresh: {Format(Levels(expected))}; " +
            $"after Reset: {Format(Levels(actual))}");
    }

    private static NukedSc55 CreateOrSkip(NukedSc55RomSet romSet,
        NukedSc55InitialReset initialReset = NukedSc55InitialReset.Automatic)
    {
        var romDirectory = Environment.GetEnvironmentVariable("NUKED_SC55_ROM_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(romDirectory), "NUKED_SC55_ROM_DIR is not set.");
        try
        {
            return new NukedSc55(new NukedSc55Options(romDirectory!, romSet) { InitialReset = initialReset });
        }
        catch (NukedSc55Exception exception) when (exception.Code == NukedSc55ErrorCode.RomSetIncomplete)
        {
            Assert.Skip($"{romSet} is not available in {romDirectory}.");
            throw;
        }
    }

    /// <summary>Renders one call per 100 ms window and returns the interleaved stereo PCM of all windows.</summary>
    private static float[] Render(NukedSc55 synth, int windows)
    {
        var window = new float[synth.SampleRate / WindowsPerSecond * NukedSc55.ChannelCount];
        var pcm = new float[window.Length * windows];
        for (var index = 0; index < windows; index++)
        {
            synth.Render(window);
            window.CopyTo(pcm, index * window.Length);
        }

        return pcm;
    }

    /// <summary>Returns the RMS level of each of <paramref name="windows" /> equal windows with its DC offset removed.</summary>
    private static double[] Levels(float[] pcm, int windows = WindowsPerSecond)
    {
        var length = pcm.Length / windows;
        var levels = new double[windows];
        for (var window = 0; window < windows; window++)
        {
            var samples = pcm.AsSpan(window * length, length).ToArray();
            var mean = samples.Average();
            levels[window] = Math.Sqrt(samples.Sum(sample => (sample - mean) * (sample - mean)) / length);
        }

        return levels;
    }

    /// <summary>Note-Offs of a key that never sounds, three bytes each.</summary>
    private static byte[] Filler(int noteOffs) =>
        [.. Enumerable.Repeat<byte[]>([0x80, 0, 0], noteOffs).SelectMany(bytes => bytes)];

    private static double Max(double[] levels, int start, int end = int.MaxValue) =>
        levels[start..Math.Min(end, levels.Length)].Max();

    private static string Format(double[] levels) =>
        string.Join(" ", levels.Select(level => level.ToString("0.0000", CultureInfo.InvariantCulture)));
}
