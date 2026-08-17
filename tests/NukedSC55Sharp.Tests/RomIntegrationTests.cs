using NukedSC55Sharp;

namespace NukedSC55Sharp.Tests;

/// <summary>Exercises real synthesis only when the caller supplies non-redistributable ROMs.</summary>
public sealed class RomIntegrationTests
{
    private static readonly byte[] GeneralMidiReset = [0xF0, 0x7E, 0x7F, 0x09, 0x01, 0xF7];
    private static readonly byte[] NoteOnWithRunningStatus = [0x90, 60, 100, 64, 96];
    private static readonly byte[] NoteOffWithRunningStatus = [0x80, 60, 0, 64, 0];

    /// <summary>Proves every ROM set available in the supplied directory satisfies the complete synthesis contract.</summary>
    [Fact]
    public void AvailableRomSetsSatisfySynthesisContract()
    {
        var romDirectory = Environment.GetEnvironmentVariable("NUKED_SC55_ROM_DIR");
        if (string.IsNullOrWhiteSpace(romDirectory))
        {
            return;
        }

        var availableCount = 0;
        foreach (var romSet in Enum.GetValues<NukedSc55RomSet>())
        {
            var options = new NukedSc55Options(romDirectory, romSet);
            NukedSc55 synth;
            try
            {
                synth = new NukedSc55(options);
            }
            catch (NukedSc55Exception exception) when (exception.Code == NukedSc55ErrorCode.RomSetIncomplete)
            {
                continue;
            }

            availableCount++;
            Assert.Equal(romSet, synth.RomSet);
            Assert.False(synth.OversamplingEnabled);
            Assert.True(synth.SampleRate is 32_000 or 33_103);

            var floatPcm = new float[synth.SampleRate * NukedSc55.ChannelCount];
            QueueAudibleSequence(synth);
            synth.Render(floatPcm);
            Assert.All(floatPcm, sample => Assert.True(float.IsFinite(sample)));
            Assert.Contains(floatPcm, sample => sample != 0f);

            var invalidDestination = Enumerable.Repeat(0.25f, 128).ToArray();
            synth.QueueMidi(NoteOnWithRunningStatus, frameOffset: 64);
            Assert.Throws<ArgumentOutOfRangeException>(() => synth.Render(invalidDestination));
            Assert.All(invalidDestination, sample => Assert.Equal(0.25f, sample));
            synth.ClearQueuedMidi();
            synth.Render(invalidDestination);

            synth.QueueMidi(NoteOnWithRunningStatus, frameOffset: int.MaxValue);
            synth.Reset();
            var resetFloatPcm = new float[floatPcm.Length];
            QueueAudibleSequence(synth);
            synth.Render(resetFloatPcm);
            Assert.All(resetFloatPcm, sample => Assert.True(float.IsFinite(sample)));
            Assert.Contains(resetFloatPcm, sample => sample != 0f);

            synth.Dispose();
            Assert.Throws<ObjectDisposedException>(() => synth.ClearQueuedMidi());
            Assert.Throws<ObjectDisposedException>(() => synth.Reset());

            using (var repeated = new NukedSc55(options))
            {
                var repeatedFloatPcm = new float[floatPcm.Length];
                QueueAudibleSequence(repeated);
                repeated.Render(repeatedFloatPcm);
                Assert.Equal(floatPcm, repeatedFloatPcm);
            }

            using (var shortSynth = new NukedSc55(options))
            {
                var shortPcm = new short[floatPcm.Length];
                QueueAudibleSequence(shortSynth);
                shortSynth.Render(shortPcm);
                Assert.Contains(shortPcm, sample => sample != 0);
                AssertEquivalentNormalization(floatPcm, shortPcm);
            }

            var oversamplingOptions = new NukedSc55Options(romDirectory, romSet)
            {
                EnableOversampling = true,
            };
            using (var oversampled = new NukedSc55(oversamplingOptions))
            {
                Assert.True(oversampled.OversamplingEnabled);
                Assert.Equal(synth.SampleRate == 32_000 ? 64_000 : 66_207, oversampled.SampleRate);
            }

            foreach (var reset in new[]
                     {
                         NukedSc55InitialReset.None,
                         NukedSc55InitialReset.GeneralMidi,
                         NukedSc55InitialReset.GeneralStandard,
                     })
            {
                var resetOptions = new NukedSc55Options(romDirectory, romSet) { InitialReset = reset };
                using var resetSynth = new NukedSc55(resetOptions);
                Assert.Equal(synth.SampleRate, resetSynth.SampleRate);
            }
        }

        Assert.True(availableCount > 0, $"No supported ROM set was detected in {romDirectory}.");
    }

    private static void QueueAudibleSequence(NukedSc55 synth)
    {
        synth.QueueMidi(GeneralMidiReset);
        synth.QueueMidi(NoteOnWithRunningStatus);
        synth.QueueMidi(NoteOffWithRunningStatus, synth.SampleRate / 2);
    }

    private static void AssertEquivalentNormalization(float[] floatPcm, short[] shortPcm)
    {
        Assert.Equal(floatPcm.Length, shortPcm.Length);
        for (var index = 0; index < floatPcm.Length; index++)
        {
            var nativeSample = (int)(floatPcm[index] * 536_870_912f);
            var expected = Math.Clamp(nativeSample >> 15, short.MinValue, short.MaxValue);
            Assert.InRange(Math.Abs(shortPcm[index] - expected), 0, 1);
        }
    }
}
