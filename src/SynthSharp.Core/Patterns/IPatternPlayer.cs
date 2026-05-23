namespace SynthSharp.Core.Patterns;

/// <summary>Plays a <see cref="PatternClip"/> back, invoking a caller-supplied handler per event.</summary>
public interface IPatternPlayer
{
    /// <summary>True while a clip is currently playing.</summary>
    bool IsPlaying { get; }

    /// <summary>
    /// Plays <paramref name="clip"/>. For each event (sorted by <see cref="PatternEvent.TimeOffsetMs"/>),
    /// waits until that offset relative to start then awaits <paramref name="onEvent"/>.
    /// When <paramref name="clip"/>.LengthMs is positive and greater than the last event's offset,
    /// the task hangs the remainder before returning (gives a stable clip-length cadence for looping callers).
    /// </summary>
    /// <param name="clip">Clip to play.</param>
    /// <param name="onEvent">Handler invoked once per event, awaited inline.</param>
    /// <param name="cancellationToken">Cancels playback. The task completes (not faults) on cancel.</param>
    /// <returns>A task that completes when the clip finishes or cancellation is requested.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="clip"/> or <paramref name="onEvent"/> is null.</exception>
    Task PlayAsync(PatternClip clip, Func<PatternEvent, Task> onEvent, CancellationToken cancellationToken = default);

    /// <summary>Cancels any in-flight <see cref="PlayAsync"/>.</summary>
    void Stop();
}
