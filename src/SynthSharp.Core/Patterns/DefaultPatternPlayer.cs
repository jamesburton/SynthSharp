using System.Diagnostics;

namespace SynthSharp.Core.Patterns;

/// <summary>Default <see cref="IPatternPlayer"/> driven by a <see cref="Stopwatch"/> and <see cref="Task.Delay(int)"/>.</summary>
public sealed class DefaultPatternPlayer : IPatternPlayer
{
    private readonly object _gate = new();
    private CancellationTokenSource? _activeCts;

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
    public async Task PlayAsync(PatternClip clip, Func<PatternEvent, Task> onEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ArgumentNullException.ThrowIfNull(onEvent);

        var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (_gate)
        {
            // Cancel any prior play (Stop semantics also live here).
            _activeCts?.Cancel();
            _activeCts?.Dispose();
            _activeCts = linked;
        }

        try
        {
            var ordered = clip.Events.OrderBy(e => e.TimeOffsetMs).ToList();
            var stopwatch = Stopwatch.StartNew();

            foreach (var ev in ordered)
            {
                if (linked.IsCancellationRequested) return;

                var delay = ev.TimeOffsetMs - stopwatch.ElapsedMilliseconds;
                if (delay > 0)
                {
                    try
                    {
                        await Task.Delay((int)delay, linked.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }

                if (linked.IsCancellationRequested) return;

                try
                {
                    await onEvent(ev).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            // Hold for the remainder of LengthMs if set.
            if (clip.LengthMs > 0)
            {
                var remainder = clip.LengthMs - stopwatch.ElapsedMilliseconds;
                if (remainder > 0)
                {
                    try
                    {
                        await Task.Delay((int)remainder, linked.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
            }
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
