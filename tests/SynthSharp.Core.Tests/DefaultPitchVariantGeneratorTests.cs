using SynthSharp.Core.Audio;
using SynthSharp.Core.Music;

namespace SynthSharp.Core.Tests;

public sealed class DefaultPitchVariantGeneratorTests
{
    private static Sample MakeSample()
    {
        var metadata = new SampleMetadata(
            Name: "test",
            ChannelCount: 1,
            SampleRateHz: 44100,
            FrameCount: 100,
            Duration: TimeSpan.FromMilliseconds(100d * 1000d / 44100d),
            SourceBitsPerSample: 32,
            SourcePath: null,
            ImportedAt: DateTimeOffset.UtcNow);

        return new Sample(metadata, new[] { new float[100] });
    }

    private sealed class IdentityPitchShifter : IPitchShifter
    {
        public int ShiftCallCount { get; private set; }

        public List<int> ReceivedSemitones { get; } = new();

        public Sample Shift(Sample source, int semitones)
        {
            ShiftCallCount++;
            ReceivedSemitones.Add(semitones);
            return source;
        }
    }

    [Fact]
    public void Generate_A4WithPlusMinusTwelve_Produces25Variants()
    {
        var source = MakeSample();
        var range = new SampleToneRange("A4", 440f, 69, -12, 12);
        var shifter = new IdentityPitchShifter();
        var generator = new DefaultPitchVariantGenerator();

        var variants = generator.Generate(source, range, shifter);

        Assert.Equal(25, variants.Count);
        Assert.Equal(25, shifter.ShiftCallCount);
    }

    [Fact]
    public void Generate_CentreVariantIsAtOffsetZero()
    {
        var source = MakeSample();
        var range = new SampleToneRange("A4", 440f, 69, -12, 12);
        var generator = new DefaultPitchVariantGenerator();

        var variants = generator.Generate(source, range, new IdentityPitchShifter());

        var centre = variants.Single(v => v.SemitoneOffset == 0);
        Assert.Equal("A4", centre.Note);
        Assert.Equal(69, centre.MidiNote);
    }

    [Fact]
    public void Generate_EachVariantMidiNoteEqualsCenterPlusOffset()
    {
        var source = MakeSample();
        var range = new SampleToneRange("C4", 261.63f, 60, -5, 5);
        var generator = new DefaultPitchVariantGenerator();

        var variants = generator.Generate(source, range, new IdentityPitchShifter());

        foreach (var v in variants)
        {
            Assert.Equal(60 + v.SemitoneOffset, v.MidiNote);
        }
    }

    [Fact]
    public void Generate_PositiveOnlyRange_ProducesCorrectCount()
    {
        var source = MakeSample();
        var range = new SampleToneRange("A4", 440f, 69, 0, 5);
        var generator = new DefaultPitchVariantGenerator();

        var variants = generator.Generate(source, range, new IdentityPitchShifter());

        Assert.Equal(6, variants.Count);
        Assert.Equal(0, variants[0].SemitoneOffset);
        Assert.Equal(5, variants[^1].SemitoneOffset);
    }

    [Fact]
    public void Generate_VariantsOrderedAscendingByOffset()
    {
        var source = MakeSample();
        var range = new SampleToneRange("A4", 440f, 69, -3, 3);
        var generator = new DefaultPitchVariantGenerator();

        var variants = generator.Generate(source, range, new IdentityPitchShifter());

        for (var i = 1; i < variants.Count; i++)
        {
            Assert.True(variants[i].SemitoneOffset > variants[i - 1].SemitoneOffset);
        }
    }

    [Fact]
    public void Generate_PassesSemitoneOffsetToShifter()
    {
        var source = MakeSample();
        var range = new SampleToneRange("A4", 440f, 69, -2, 2);
        var generator = new DefaultPitchVariantGenerator();
        var shifter = new IdentityPitchShifter();

        generator.Generate(source, range, shifter);

        Assert.Equal(new[] { -2, -1, 0, 1, 2 }, shifter.ReceivedSemitones);
    }

    [Fact]
    public void Generate_NullSource_Throws()
    {
        var generator = new DefaultPitchVariantGenerator();
        var range = new SampleToneRange("A4", 440f, 69, -1, 1);

        Assert.Throws<ArgumentNullException>(
            () => generator.Generate(null!, range, new IdentityPitchShifter()));
    }

    [Fact]
    public void Generate_NullRange_Throws()
    {
        var generator = new DefaultPitchVariantGenerator();

        Assert.Throws<ArgumentNullException>(
            () => generator.Generate(MakeSample(), null!, new IdentityPitchShifter()));
    }

    [Fact]
    public void Generate_NullShifter_Throws()
    {
        var generator = new DefaultPitchVariantGenerator();
        var range = new SampleToneRange("A4", 440f, 69, -1, 1);

        Assert.Throws<ArgumentNullException>(
            () => generator.Generate(MakeSample(), range, null!));
    }

    [Fact]
    public void Generate_HighBelowLow_Throws()
    {
        var generator = new DefaultPitchVariantGenerator();
        var range = new SampleToneRange("A4", 440f, 69, 5, -5);

        Assert.Throws<ArgumentException>(
            () => generator.Generate(MakeSample(), range, new IdentityPitchShifter()));
    }

    [Fact]
    public void Generate_CancellationBetweenIterations_Throws()
    {
        var source = MakeSample();
        var range = new SampleToneRange("A4", 440f, 69, -12, 12);
        var generator = new DefaultPitchVariantGenerator();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => generator.Generate(source, range, new IdentityPitchShifter(), cts.Token));
    }
}
