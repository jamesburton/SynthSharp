using SynthSharp.Core.Audio;
using SynthSharp.Core.Layout;
using SynthSharp.Core.Persistence;

namespace SynthSharp.Core.Tests;

public sealed class PresetJsonRoundtripLfoTests
{
    [Fact]
    public void RoundTrips_AmplitudeLfo()
    {
        var preset = DefaultPresetFactory.CreateFourRowDefault();
        preset.Pads[0].Lfo = new LfoSettings(LfoTarget.Amplitude, 3.5, 0.7);

        var json = PresetJsonSerializer.Serialize(preset);
        var round = PresetJsonSerializer.Deserialize(json);

        Assert.Equal(LfoTarget.Amplitude, round.Pads[0].Lfo.Target);
        Assert.Equal(3.5, round.Pads[0].Lfo.RateHz);
        Assert.Equal(0.7, round.Pads[0].Lfo.Depth);
    }

    [Fact]
    public void RoundTrips_DefaultOff()
    {
        var preset = DefaultPresetFactory.CreateFourRowDefault();
        var json = PresetJsonSerializer.Serialize(preset);
        var round = PresetJsonSerializer.Deserialize(json);
        Assert.Equal(LfoTarget.None, round.Pads[0].Lfo.Target);
    }
}
