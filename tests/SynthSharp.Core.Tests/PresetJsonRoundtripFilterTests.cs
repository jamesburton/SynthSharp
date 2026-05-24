using SynthSharp.Core.Audio;
using SynthSharp.Core.Layout;
using SynthSharp.Core.Persistence;

namespace SynthSharp.Core.Tests;

/// <summary>Verifies that <see cref="FilterSettings"/> survives a JSON serialisation round-trip via <see cref="PresetJsonSerializer"/>.</summary>
public sealed class PresetJsonRoundtripFilterTests
{
    [Fact]
    public void RoundTrips_LowPassFilter()
    {
        var preset = DefaultPresetFactory.CreateFourRowDefault();
        preset.Pads[0].Filter = new FilterSettings(FilterType.LowPass, 800d, 1.2d);

        var json = PresetJsonSerializer.Serialize(preset);
        var round = PresetJsonSerializer.Deserialize(json);

        Assert.Equal(FilterType.LowPass, round.Pads[0].Filter.Type);
        Assert.Equal(800d, round.Pads[0].Filter.CutoffHz);
        Assert.Equal(1.2d, round.Pads[0].Filter.Resonance);
    }

    [Fact]
    public void RoundTrips_DefaultOff()
    {
        var preset = DefaultPresetFactory.CreateFourRowDefault();
        var json = PresetJsonSerializer.Serialize(preset);
        var round = PresetJsonSerializer.Deserialize(json);
        Assert.Equal(FilterType.None, round.Pads[0].Filter.Type);
    }

    [Fact]
    public void RoundTrips_HighPassFilter()
    {
        var preset = DefaultPresetFactory.CreateFourRowDefault();
        preset.Pads[0].Filter = new FilterSettings(FilterType.HighPass, 2000d, 0.5d);

        var json = PresetJsonSerializer.Serialize(preset);
        var round = PresetJsonSerializer.Deserialize(json);

        Assert.Equal(FilterType.HighPass, round.Pads[0].Filter.Type);
        Assert.Equal(2000d, round.Pads[0].Filter.CutoffHz);
        Assert.Equal(0.5d, round.Pads[0].Filter.Resonance);
    }

    [Fact]
    public void RoundTrips_BandPassFilter()
    {
        var preset = DefaultPresetFactory.CreateFourRowDefault();
        preset.Pads[0].Filter = new FilterSettings(FilterType.BandPass, 1500d, 2.0d);

        var json = PresetJsonSerializer.Serialize(preset);
        var round = PresetJsonSerializer.Deserialize(json);

        Assert.Equal(FilterType.BandPass, round.Pads[0].Filter.Type);
        Assert.Equal(1500d, round.Pads[0].Filter.CutoffHz);
        Assert.Equal(2.0d, round.Pads[0].Filter.Resonance);
    }
}
