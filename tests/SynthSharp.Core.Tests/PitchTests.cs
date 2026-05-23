using SynthSharp.Core.Music;

namespace SynthSharp.Core.Tests;

public class PitchTests
{
    [Fact]
    public void ToFrequencyHz_MapsA4Correctly()
    {
        var hz = Pitch.ToFrequencyHz(69);
        Assert.Equal(440d, hz, 6);
    }

    [Fact]
    public void TryParseNote_ParsesCSharp4()
    {
        var ok = Pitch.TryParseNote("C#4", out var midi);
        Assert.True(ok);
        Assert.Equal(61, midi);
    }

    [Fact]
    public void TryResolveFrequency_AcceptsFrequencyOrNote()
    {
        Assert.True(Pitch.TryResolveFrequency("523.25", out var hz1));
        Assert.InRange(hz1, 523.2, 523.3);

        Assert.True(Pitch.TryResolveFrequency("C5", out var hz2));
        Assert.InRange(hz2, 523.2, 523.3);
    }
}
