using SynthSharp.Core.Audio;
using SynthSharp.Core.Layout;

namespace SynthSharp.Core.Tests;

public class DefaultPresetFactoryTests
{
    [Fact]
    public void CreateFourRowDefault_CreatesExpectedRows()
    {
        var preset = DefaultPresetFactory.CreateFourRowDefault();

        Assert.Equal(4, preset.Pads.Select(x => x.RowIndex).Distinct().Count());
        Assert.Contains(preset.Pads, x => x.KeyBinding == "1");
        Assert.Contains(preset.Pads, x => x.KeyBinding == "Q");
        Assert.Contains(preset.Pads, x => x.KeyBinding == "A");
        Assert.Contains(preset.Pads, x => x.KeyBinding == "Z");
    }

    [Fact]
    public void PercussionRow_UsesNoiseWaveform()
    {
        var preset = DefaultPresetFactory.CreateFourRowDefault();

        var percussionPads = preset.Pads.Where(p => p.RowIndex == 2).ToList();

        Assert.NotEmpty(percussionPads);
        Assert.All(percussionPads, p => Assert.Equal(WaveformType.Noise, p.Waveform));
    }

    [Fact]
    public void PercussionRow_UsesSnappyEnvelope()
    {
        var preset = DefaultPresetFactory.CreateFourRowDefault();

        var percussionPads = preset.Pads.Where(p => p.RowIndex == 2).ToList();

        Assert.All(percussionPads, p =>
        {
            Assert.Equal(0d, p.Envelope.AttackSeconds);
            Assert.Equal(0d, p.Envelope.SustainLevel);
            Assert.Equal(0d, p.Envelope.ReleaseSeconds);
            Assert.InRange(p.Envelope.DecaySeconds, 0.05d, 0.30d);
        });
    }

    [Fact]
    public void MelodicRows_KeepDefaultEnvelope()
    {
        var preset = DefaultPresetFactory.CreateFourRowDefault();

        var melodicPads = preset.Pads.Where(p => p.RowIndex is 0 or 1).ToList();

        Assert.All(melodicPads, p => Assert.Equal(Envelope.Default, p.Envelope));
    }
}
