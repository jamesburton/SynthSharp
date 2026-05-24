using SynthSharp.Core.Audio;
using SynthSharp.Core.Layout;
using SynthSharp.Core.Persistence;

namespace SynthSharp.Core.Tests;

/// <summary>Verifies that <see cref="PadAssignment.SampleFileName"/> and <see cref="PadAssignment.SampleGain"/> survive a JSON serialisation round-trip.</summary>
public class PresetJsonRoundtripSampleTests
{
    [Fact]
    public void PresetJsonSerializer_RoundTripsSampleFileNameAndGain()
    {
        var preset = new KeyboardLayoutPreset
        {
            Name = "test-preset",
            Pads = new[]
            {
                new PadAssignment
                {
                    PadId = "pad-0",
                    RowIndex = 0,
                    ColumnIndex = 0,
                    Role = RowRole.MelodicA,
                    KeyBinding = "A",
                    Label = "A",
                    Waveform = WaveformType.Sine,
                    FrequencyHz = 440d,
                    SampleFileName = "my-sample.wav",
                    SampleGain = 0.5,
                },
            },
        };

        var json = PresetJsonSerializer.Serialize(preset);
        var deserialized = PresetJsonSerializer.Deserialize(json);

        var pad = deserialized.Pads[0];
        Assert.Equal("my-sample.wav", pad.SampleFileName);
        Assert.Equal(0.5, pad.SampleGain);
    }

    [Fact]
    public void PresetJsonSerializer_RoundTripsSampleLoopFields()
    {
        var preset = new KeyboardLayoutPreset
        {
            Name = "test-loop-preset",
            Pads = new[]
            {
                new PadAssignment
                {
                    PadId = "pad-0",
                    RowIndex = 0,
                    ColumnIndex = 0,
                    Role = RowRole.MelodicA,
                    KeyBinding = "A",
                    Label = "A",
                    Waveform = WaveformType.Sine,
                    FrequencyHz = 440d,
                    SampleFileName = "loopable.wav",
                    SampleGain = 0.8,
                    SampleLoopEnabled = true,
                    SampleLoopStartFrame = 12345,
                    SampleLoopEndFrame = 67890,
                },
            },
        };

        var json = PresetJsonSerializer.Serialize(preset);
        var deserialized = PresetJsonSerializer.Deserialize(json);

        var pad = deserialized.Pads[0];
        Assert.True(pad.SampleLoopEnabled);
        Assert.Equal(12345, pad.SampleLoopStartFrame);
        Assert.Equal(67890, pad.SampleLoopEndFrame);
    }

    [Fact]
    public void PresetJsonSerializer_DefaultLoopFields_RoundTrip()
    {
        var preset = new KeyboardLayoutPreset
        {
            Name = "default-loop-preset",
            Pads = new[]
            {
                new PadAssignment
                {
                    PadId = "pad-0",
                    RowIndex = 0,
                    ColumnIndex = 0,
                    Role = RowRole.MelodicA,
                    KeyBinding = "A",
                    Label = "A",
                    Waveform = WaveformType.Sine,
                    FrequencyHz = 440d,
                },
            },
        };

        var json = PresetJsonSerializer.Serialize(preset);
        var deserialized = PresetJsonSerializer.Deserialize(json);

        var pad = deserialized.Pads[0];
        Assert.False(pad.SampleLoopEnabled);
        Assert.Equal(0, pad.SampleLoopStartFrame);
        Assert.Equal(0, pad.SampleLoopEndFrame);
    }
}
