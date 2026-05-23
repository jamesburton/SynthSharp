namespace SynthSharp.Core.Patterns;

/// <summary>A single triggered note inside a <see cref="PatternClip"/>.</summary>
/// <param name="PadId">Identifier of the pad that was triggered (matches <c>PadAssignment.PadId</c>).</param>
/// <param name="TimeOffsetMs">Milliseconds from the start of the clip when this event fires. Non-negative.</param>
/// <param name="Velocity">Linear velocity in [0.0, 1.0] (informational; engine wiring may ignore it for now).</param>
public sealed record PatternEvent(
    string PadId,
    long TimeOffsetMs,
    float Velocity = 1.0f);
