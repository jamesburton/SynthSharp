using SynthSharp.Core.Music;

namespace SynthSharp.Core.Tests;

public sealed class DefaultToneRangeEstimatorTests
{
    private static PitchEstimate MakePitch(float hz, float confidence) =>
        new(hz, confidence, Array.Empty<float>());

    [Fact]
    public void Estimate_440Hz_FullConfidence_ReturnsA4CenterRangeMinusTwelveToPlusTwelve()
    {
        var estimator = new DefaultToneRangeEstimator();
        var pitch = MakePitch(440f, 0.9f);

        var range = estimator.Estimate(pitch);

        Assert.NotNull(range);
        Assert.Equal("A4", range.CenterNote);
        Assert.Equal(69, range.CenterMidiNote);
        Assert.Equal(440f, range.CenterPitchHz);
        Assert.Equal(-12, range.LowSemitone);
        Assert.Equal(12, range.HighSemitone);
    }

    [Fact]
    public void Estimate_220Hz_ReturnsA3Center()
    {
        var estimator = new DefaultToneRangeEstimator();
        var pitch = MakePitch(220f, 0.9f);

        var range = estimator.Estimate(pitch);

        Assert.NotNull(range);
        Assert.Equal("A3", range.CenterNote);
        Assert.Equal(57, range.CenterMidiNote);
    }

    [Fact]
    public void Estimate_ZeroFundamental_ReturnsNull()
    {
        var estimator = new DefaultToneRangeEstimator();
        var pitch = MakePitch(0f, 0f);

        var range = estimator.Estimate(pitch);

        Assert.Null(range);
    }

    [Fact]
    public void Estimate_NegativeFundamental_ReturnsNull()
    {
        var estimator = new DefaultToneRangeEstimator();
        var pitch = MakePitch(-100f, 0.9f);

        Assert.Null(estimator.Estimate(pitch));
    }

    [Fact]
    public void Estimate_BelowDefaultConfidenceThreshold_ReturnsNull()
    {
        var estimator = new DefaultToneRangeEstimator();
        var pitch = MakePitch(440f, 0.1f); // default MinConfidence is 0.3

        Assert.Null(estimator.Estimate(pitch));
    }

    [Fact]
    public void Estimate_AboveCustomConfidenceThreshold_ReturnsRange()
    {
        var estimator = new DefaultToneRangeEstimator();
        var pitch = MakePitch(440f, 0.5f);
        var options = new ToneRangeOptions(MinConfidence: 0.3f);

        var range = estimator.Estimate(pitch, options);

        Assert.NotNull(range);
    }

    [Fact]
    public void Estimate_CustomSemitoneRange_RespectedWhenNoClamping()
    {
        var estimator = new DefaultToneRangeEstimator();
        var pitch = MakePitch(440f, 0.9f);
        var options = new ToneRangeOptions(MaxSemitonesBelow: 6, MaxSemitonesAbove: 18);

        var range = estimator.Estimate(pitch, options);

        Assert.NotNull(range);
        Assert.Equal(-6, range.LowSemitone);
        Assert.Equal(18, range.HighSemitone);
    }

    [Fact]
    public void Estimate_LowExtreme_ClampsLowSemitoneToMidiZero()
    {
        // MIDI 21 = A0 = 27.5 Hz. With MaxSemitonesBelow=24, raw low would be -24 (= MIDI -3, illegal).
        // Expect clamped to -21 so center-low = 0 = lowest MIDI.
        var estimator = new DefaultToneRangeEstimator();
        var pitch = MakePitch(27.5f, 0.9f);
        var options = new ToneRangeOptions(MaxSemitonesBelow: 24, MaxSemitonesAbove: 12);

        var range = estimator.Estimate(pitch, options);

        Assert.NotNull(range);
        Assert.Equal(21, range.CenterMidiNote);
        Assert.Equal(-21, range.LowSemitone);
        Assert.Equal(12, range.HighSemitone);
    }

    [Fact]
    public void Estimate_HighExtreme_ClampsHighSemitoneToMidi127()
    {
        // MIDI 120 = C9 = 8372 Hz (above NWaves' practical range, but the estimator is independent).
        // Use ToFrequencyHz to derive the exact Hz so ToMidiNote round-trips to 120.
        var hz = (float)Pitch.ToFrequencyHz(120);
        var estimator = new DefaultToneRangeEstimator();
        var pitch = MakePitch(hz, 0.9f);
        var options = new ToneRangeOptions(MaxSemitonesBelow: 12, MaxSemitonesAbove: 24);

        var range = estimator.Estimate(pitch, options);

        Assert.NotNull(range);
        Assert.Equal(120, range.CenterMidiNote);
        Assert.Equal(-12, range.LowSemitone);
        Assert.Equal(7, range.HighSemitone); // 127 - 120
    }

    [Fact]
    public void Estimate_NullPitch_Throws()
    {
        var estimator = new DefaultToneRangeEstimator();

        Assert.Throws<ArgumentNullException>(() => estimator.Estimate(null!));
    }
}
