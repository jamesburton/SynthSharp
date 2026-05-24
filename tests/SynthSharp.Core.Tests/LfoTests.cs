using SynthSharp.Core.Audio;

namespace SynthSharp.Core.Tests;

public sealed class LfoTests
{
    [Fact]
    public void EvaluateSine_AtTimeZero_IsZero()
    {
        Assert.InRange(Lfo.EvaluateSine(rateHz: 1, timeSeconds: 0, depth: 1), -1e-9, 1e-9);
    }

    [Fact]
    public void EvaluateSine_AtQuarterPeriod_IsPositiveDepth()
    {
        Assert.InRange(Lfo.EvaluateSine(rateHz: 1, timeSeconds: 0.25, depth: 1), 0.9999, 1.0001);
    }

    [Fact]
    public void EvaluateSine_AtHalfPeriod_IsZero()
    {
        Assert.InRange(Lfo.EvaluateSine(rateHz: 1, timeSeconds: 0.5, depth: 1), -1e-9, 1e-9);
    }

    [Fact]
    public void EvaluateSine_AtThreeQuarterPeriod_IsNegativeDepth()
    {
        Assert.InRange(Lfo.EvaluateSine(rateHz: 1, timeSeconds: 0.75, depth: 1), -1.0001, -0.9999);
    }

    [Fact]
    public void EvaluateSine_DepthScalesOutput()
    {
        Assert.InRange(Lfo.EvaluateSine(rateHz: 1, timeSeconds: 0.25, depth: 0.5), 0.4999, 0.5001);
    }

    [Fact]
    public void LfoSettings_Off_HasTargetNone()
    {
        Assert.Equal(LfoTarget.None, LfoSettings.Off.Target);
    }
}
