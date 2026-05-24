namespace SynthSharp.Core.Patterns;

/// <summary>Plays a <see cref="PatternSet"/>'s tracks in parallel, fanning events through a single handler.</summary>
public interface IPatternSetPlayer
{
    /// <summary>True while any track in the active set is playing.</summary>
    bool IsPlaying { get; }

    /// <summary>
    /// Plays all non-muted tracks in <paramref name="set"/> in parallel. If any track has Solo=true,
    /// only solo'd tracks play. Events from all tracks are routed through the same handler.
    /// </summary>
    /// <param name="set">The pattern set to play.</param>
    /// <param name="onEvent">Handler invoked once per event from any track, awaited inline.</param>
    /// <param name="cancellationToken">Cancels playback of all tracks.</param>
    /// <returns>A task that completes when all tracks finish or cancellation is requested.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="set"/> or <paramref name="onEvent"/> is null.</exception>
    Task PlayAsync(PatternSet set, Func<PatternEvent, Task> onEvent, CancellationToken cancellationToken = default);

    /// <summary>Cancels any in-flight <see cref="PlayAsync"/>.</summary>
    void Stop();
}
