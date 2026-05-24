namespace SynthSharp.Core.Patterns;

/// <summary>
/// Default <see cref="IPatternSetPlayer"/> — wraps an <see cref="IPatternPlayer"/> per audible track
/// and runs them concurrently via <see cref="Task.WhenAll(IEnumerable{Task})"/>.
/// </summary>
public sealed class DefaultPatternSetPlayer : IPatternSetPlayer
{
    private readonly Func<IPatternPlayer> _playerFactory;
    private readonly object _gate = new();
    private CancellationTokenSource? _activeCts;

    /// <summary>Initialises a new set player that constructs a fresh <see cref="DefaultPatternPlayer"/> per track.</summary>
    public DefaultPatternSetPlayer()
        : this(() => new DefaultPatternPlayer())
    {
    }

    /// <summary>Initialises a new set player using <paramref name="playerFactory"/> to construct per-track players.</summary>
    /// <param name="playerFactory">Factory returning a fresh <see cref="IPatternPlayer"/> instance for each track.</param>
    public DefaultPatternSetPlayer(Func<IPatternPlayer> playerFactory)
    {
        ArgumentNullException.ThrowIfNull(playerFactory);
        _playerFactory = playerFactory;
    }

    /// <inheritdoc/>
    public bool IsPlaying
    {
        get
        {
            lock (_gate)
            {
                return _activeCts is not null;
            }
        }
    }

    /// <inheritdoc/>
    public async Task PlayAsync(PatternSet set, Func<PatternEvent, Task> onEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(onEvent);

        // Compute the audible set: if any track has Solo=true, only solo'd tracks play.
        // Otherwise, mute respects per-track Mute flag.
        var anySolo = set.Tracks.Any(t => t.Solo);
        var audibleTracks = anySolo
            ? set.Tracks.Where(t => t.Solo).ToList()
            : set.Tracks.Where(t => !t.Mute).ToList();

        if (audibleTracks.Count == 0)
        {
            return;
        }

        var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (_gate)
        {
            _activeCts?.Cancel();
            _activeCts?.Dispose();
            _activeCts = linked;
        }

        try
        {
            // Serialise the onEvent invocations across tracks so the caller doesn't have to
            // worry about concurrent UI / audio engine calls. Tracks' own timing remains parallel.
            var eventGate = new SemaphoreSlim(1, 1);

            async Task SerialisedOnEvent(PatternEvent ev)
            {
                await eventGate.WaitAsync(linked.Token).ConfigureAwait(false);
                try
                {
                    await onEvent(ev).ConfigureAwait(false);
                }
                finally
                {
                    eventGate.Release();
                }
            }

            var trackTasks = audibleTracks
                .Select(t => _playerFactory().PlayAsync(t.Clip, SerialisedOnEvent, linked.Token))
                .ToArray();

            await Task.WhenAll(trackTasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is the normal Stop() path; surface it as a clean return.
        }
        finally
        {
            lock (_gate)
            {
                if (_activeCts == linked)
                {
                    _activeCts = null;
                }
            }
            linked.Dispose();
        }
    }

    /// <inheritdoc/>
    public void Stop()
    {
        lock (_gate)
        {
            _activeCts?.Cancel();
        }
    }
}
