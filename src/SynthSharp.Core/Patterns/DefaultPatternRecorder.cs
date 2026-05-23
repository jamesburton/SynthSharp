using System.Diagnostics;

namespace SynthSharp.Core.Patterns;

/// <summary>Default <see cref="IPatternRecorder"/> using a <see cref="Stopwatch"/> as the time source.</summary>
public sealed class DefaultPatternRecorder : IPatternRecorder
{
    private readonly object _gate = new();
    private PatternClip? _target;
    private Stopwatch? _stopwatch;

    /// <inheritdoc/>
    public bool IsRecording
    {
        get
        {
            lock (_gate)
            {
                return _target is not null;
            }
        }
    }

    /// <inheritdoc/>
    public void Start(PatternClip target)
    {
        ArgumentNullException.ThrowIfNull(target);

        lock (_gate)
        {
            _target = target;
            _stopwatch = Stopwatch.StartNew();
        }
    }

    /// <inheritdoc/>
    public void Record(string padId, float velocity = 1.0f)
    {
        if (string.IsNullOrEmpty(padId))
        {
            return;
        }

        PatternClip? target;
        long elapsed;
        lock (_gate)
        {
            if (_target is null || _stopwatch is null)
            {
                return;
            }

            target = _target;
            elapsed = _stopwatch.ElapsedMilliseconds;
        }

        target.AddEvent(new PatternEvent(padId, elapsed, velocity));
    }

    /// <inheritdoc/>
    public void Stop()
    {
        lock (_gate)
        {
            if (_target is null || _stopwatch is null)
            {
                return;
            }

            _target.LengthMs = _stopwatch.ElapsedMilliseconds;
            _stopwatch.Stop();
            _stopwatch = null;
            _target = null;
        }
    }
}
