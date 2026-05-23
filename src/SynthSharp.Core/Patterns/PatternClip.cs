namespace SynthSharp.Core.Patterns;

/// <summary>A recorded sequence of <see cref="PatternEvent"/>s with tempo metadata.</summary>
public sealed class PatternClip
{
    private readonly List<PatternEvent> _events = new();

    /// <summary>Display name for the clip.</summary>
    public string Name { get; set; } = "untitled";

    /// <summary>Tempo in beats per minute. Informational metadata; does not affect playback timing.</summary>
    public int TempoBpm { get; set; } = 120;

    /// <summary>Steps per bar (grid resolution). Informational metadata for UI display.</summary>
    public int StepsPerBar { get; set; } = 16;

    /// <summary>
    /// Total clip length in milliseconds. 0 means "derive from the last event timestamp at playback time".
    /// Set by <see cref="IPatternRecorder.Stop"/> when recording finishes.
    /// </summary>
    public long LengthMs { get; set; }

    /// <summary>The events in this clip. Insertion order is preserved; the player sorts by TimeOffsetMs.</summary>
    public IReadOnlyList<PatternEvent> Events => _events;

    /// <summary>Appends an event.</summary>
    /// <param name="e">The event to add.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="e"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="e"/>.TimeOffsetMs is negative.</exception>
    public void AddEvent(PatternEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.TimeOffsetMs < 0)
        {
            throw new ArgumentException(
                $"TimeOffsetMs must be non-negative; got {e.TimeOffsetMs}.", nameof(e));
        }

        _events.Add(e);
    }

    /// <summary>Removes all events and resets <see cref="LengthMs"/> to 0.</summary>
    public void Clear()
    {
        _events.Clear();
        LengthMs = 0;
    }
}
