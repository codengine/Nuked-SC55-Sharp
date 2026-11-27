using System.Globalization;

namespace NukedSC55Sharp.Tests;

/// <summary>
/// Proves that MIDI reaches the firmware complete, in order and no faster than a MIDI cable carries it, even when a
/// burst exceeds the emulated 8 KiB receive ring, and that Reset starts from power-on state.
/// Runs on the caller's non-redistributable ROMs: Mk1V121 receives through the main MCU, Mk2V101 through the sub-MCU.
/// </summary>
public sealed class MidiBacklogTests
{
    // A held organ note measures 0.033-0.051 RMS on both sets; silence measures 0 and a released note falls below
    // 0.001 within 0.6 s. The thresholds leave a wide margin on both sides.
    private const double SoundingLevel = 0.010;
    private const double SilentLevel = 0.002;

    // At 3125 bytes per second an 8192-byte burst takes 2.6 s and the longest burst, 14,432 bytes, 4.6 s.
    private const int WindowsPerSecond = 10;
    private const int RenderWindows = 6 * WindowsPerSecond;
    private const int LastSecond = RenderWindows - WindowsPerSecond;

    private static readonly byte[] OrganOnChannel0 = [0xC0, 16];
    private static readonly byte[] NoteOn = [0x90, 60, 100];
    private static readonly byte[] NoteOff = [0x80, 60, 0];
    private static readonly byte[] TwoByteMessage = [0xCF, 0];

    /// <summary>A Note-On that follows 8187 bytes, with 2 more after it, sounds once the bytes ahead of it have arrived.</summary>
    [Theory]
    [InlineData(NukedSc55RomSet.Mk1V121)]
    [InlineData(NukedSc55RomSet.Mk2V101)]
    public void NoteAfterBurstOfRingSizeSounds(NukedSc55RomSet romSet)
    {
        using var synth = CreateOrSkip(romSet);
        Prepare(synth, OrganOnChannel0);

        synth.QueueMidi([.. Filler(2729), .. NoteOn, .. TwoByteMessage]);
        var levels = RenderLevels(synth, RenderWindows);

        Assert.True(Max(levels, LastSecond) >= SoundingLevel,
            $"The Note-On inside an 8192-byte burst never sounded: {Format(levels)}");
    }

    /// <summary>A Note-On followed by 3000 bytes in the same frame sounds at once and keeps sounding.</summary>
    [Theory]
    [InlineData(NukedSc55RomSet.Mk1V121)]
    [InlineData(NukedSc55RomSet.Mk2V101)]
    public void NoteFollowedByDenseBurstSoundsAtOnce(NukedSc55RomSet romSet)
    {
        using var synth = CreateOrSkip(romSet);
        Prepare(synth, OrganOnChannel0);

        synth.QueueMidi([.. NoteOn, .. Filler(1000)]);
        var levels = RenderLevels(synth, RenderWindows);

        Assert.True(Max(levels, 0, 2) >= SoundingLevel,
            $"The Note-On ahead of a 3000-byte burst did not sound within 200 ms: {Format(levels)}");
        Assert.True(Max(levels, LastSecond) >= SoundingLevel,
            $"The Note-On ahead of a 3000-byte burst stopped sounding: {Format(levels)}");
    }

    /// <summary>A full controller reset followed by 8192 more bytes in the same frame still releases held notes.</summary>
    [Theory]
    [InlineData(NukedSc55RomSet.Mk1V121)]
    [InlineData(NukedSc55RomSet.Mk2V101)]
    public void ResetFollowedByRingSizeOfBytesReleasesHeldNotes(NukedSc55RomSet romSet)
    {
        using var synth = CreateOrSkip(romSet);
        byte[] heldNotes = [];
        foreach (var channel in new byte[] { 0, 7, 15 })
        {
            heldNotes = [.. heldNotes, (byte)(0xC0 | channel), 16, (byte)(0x90 | channel), 60, 100];
        }

        Prepare(synth, heldNotes);
        var heldLevels = RenderLevels(synth, WindowsPerSecond / 2);
        Assert.True(Max(heldLevels, 0) >= SoundingLevel, $"The held notes did not sound: {Format(heldLevels)}");

        synth.QueueMidi([.. ControllerReset(), .. Filler(2730), .. TwoByteMessage]);
        var levels = RenderLevels(synth, RenderWindows);

        Assert.True(Max(levels, LastSecond) < SilentLevel,
            $"Held notes still sound after a reset followed by 8192 bytes: {Format(levels)}");
    }

    /// <summary>Bytes still waiting for the ring enter ahead of the events queued for later render calls.</summary>
    [Theory]
    [InlineData(NukedSc55RomSet.Mk1V121)]
    [InlineData(NukedSc55RomSet.Mk2V101)]
    public void BacklogStaysAheadOfLaterRenderCalls(NukedSc55RomSet romSet)
    {
        using var synth = CreateOrSkip(romSet);
        Prepare(synth, OrganOnChannel0);

        synth.QueueMidi([.. Filler(2730), .. TwoByteMessage, .. NoteOn, .. Filler(1000)]);
        var levels = RenderLevels(synth, 1);
        synth.QueueMidi(NoteOff);
        levels = [.. levels, .. RenderLevels(synth, RenderWindows - 1)];

        Assert.True(Max(levels, 0, WindowsPerSecond / 2) < SilentLevel,
            $"The Note-On sounded before the 8192 bytes queued ahead of it could drain: {Format(levels)}");
        Assert.True(Max(levels, 0) >= SoundingLevel, $"The Note-On waiting behind the ring never sounded: {Format(levels)}");
        Assert.True(Max(levels, LastSecond) < SilentLevel,
            $"The note still sounds, so the later Note-Off overtook the waiting Note-On: {Format(levels)}");
    }

    /// <summary>Clearing queued MIDI cancels unrendered events but keeps bytes waiting from an earlier render call.</summary>
    [Theory]
    [InlineData(NukedSc55RomSet.Mk1V121)]
    [InlineData(NukedSc55RomSet.Mk2V101)]
    public void ClearQueuedMidiKeepsBytesWaitingForTheRing(NukedSc55RomSet romSet)
    {
        using var synth = CreateOrSkip(romSet);
        Prepare(synth, OrganOnChannel0);

        synth.QueueMidi([.. Filler(2730), .. TwoByteMessage, .. NoteOn]);
        var levels = RenderLevels(synth, 1);
        synth.QueueMidi(NoteOff);
        synth.ClearQueuedMidi();
        levels = [.. levels, .. RenderLevels(synth, RenderWindows - 1)];

        Assert.True(Max(levels, 0) >= SoundingLevel,
            $"The Note-On waiting for the ring never sounded after ClearQueuedMidi: {Format(levels)}");
        Assert.True(Max(levels, LastSecond) >= SoundingLevel,
            $"The note stopped, so the cleared Note-Off was still delivered: {Format(levels)}");
    }

    /// <summary>
    /// After a sounding note and bytes left in the ring and the backlog, Reset renders exactly what a freshly created
    /// instance renders.
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

    private static void Prepare(NukedSc55 synth, byte[] setup)
    {
        synth.QueueMidi(setup);
        Render(synth, WindowsPerSecond / 5);
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

    private static double[] RenderLevels(NukedSc55 synth, int windows) => Levels(Render(synth, windows), windows);

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

    /// <summary>
    /// The 6240-byte reset of the game that exposed the defect: per channel, a Note-Off for every key, sustain off and
    /// All Notes Off.
    /// </summary>
    private static byte[] ControllerReset()
    {
        var bytes = new List<byte>();
        for (var channel = 0; channel < 16; channel++)
        {
            for (var key = 0; key < 128; key++)
            {
                bytes.AddRange([(byte)(0x80 | channel), (byte)key, 0]);
            }

            bytes.AddRange([(byte)(0xB0 | channel), 64, 0, (byte)(0xB0 | channel), 123, 0]);
        }

        return [.. bytes];
    }

    private static double Max(double[] levels, int start, int end = int.MaxValue) =>
        levels[start..Math.Min(end, levels.Length)].Max();

    private static string Format(double[] levels) =>
        string.Join(" ", levels.Select(level => level.ToString("0.0000", CultureInfo.InvariantCulture)));
}
