namespace SynthSharp.Core.Patterns;

/// <summary>Records timed <see cref="PatternEvent"/>s into a target <see cref="PatternClip"/>.</summary>
public interface IPatternRecorder
{
    /// <summary>True while recording is active.</summary>
    bool IsRecording { get; }

    /// <summary>
    /// Starts recording into <paramref name="target"/>. Resets the internal time origin.
    /// If recording is already active, the existing session is replaced and the new session
    /// targets <paramref name="target"/>.
    /// </summary>
    /// <param name="target">Clip that will receive recorded events.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="target"/> is null.</exception>
    void Start(PatternClip target);

    /// <summary>
    /// Appends an event for <paramref name="padId"/> at the current elapsed time.
    /// No-op when <see cref="IsRecording"/> is false.
    /// </summary>
    void Record(string padId, float velocity = 1.0f);

    /// <summary>
    /// Stops recording and sets the target clip's <see cref="PatternClip.LengthMs"/>
    /// to the elapsed time at stop. No-op when <see cref="IsRecording"/> is false.
    /// </summary>
    void Stop();
}
