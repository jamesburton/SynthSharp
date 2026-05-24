namespace SynthSharp.Core.Audio;

/// <summary>Pure-math helper for evaluating a sine LFO at a point in time.</summary>
public static class Lfo
{
    /// <summary>Returns sin(2π · rate · time) · depth.</summary>
    /// <param name="rateHz">LFO frequency in Hz.</param>
    /// <param name="timeSeconds">Time since the LFO started, in seconds.</param>
    /// <param name="depth">Output scaling; result is in [-depth, +depth].</param>
    public static double EvaluateSine(double rateHz, double timeSeconds, double depth)
    {
        return Math.Sin(2d * Math.PI * rateHz * timeSeconds) * depth;
    }
}
