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
}
