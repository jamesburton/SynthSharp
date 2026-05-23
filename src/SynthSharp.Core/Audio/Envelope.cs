namespace SynthSharp.Core.Audio;

public readonly record struct Envelope(
    double AttackSeconds,
    double DecaySeconds,
    double SustainLevel,
    double ReleaseSeconds)
{
    public static Envelope Default { get; } = new(0.01, 0.05, 0.80, 0.10);
}
