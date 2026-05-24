namespace SynthSharp.Core.Patterns;

/// <summary>A collection of <see cref="PatternTrack"/>s that play in parallel.</summary>
public sealed class PatternSet
{
    private readonly List<PatternTrack> _tracks = new();

    /// <summary>Display name for the set.</summary>
    public string Name { get; set; } = "untitled set";

    /// <summary>The tracks in this set. Insertion order is preserved; playback order is parallel.</summary>
    public IReadOnlyList<PatternTrack> Tracks => _tracks;

    /// <summary>Appends a track.</summary>
    /// <param name="track">The track to add.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="track"/> is null.</exception>
    public void AddTrack(PatternTrack track)
    {
        ArgumentNullException.ThrowIfNull(track);
        _tracks.Add(track);
    }

    /// <summary>Removes a track by reference.</summary>
    /// <param name="track">The track to remove.</param>
    /// <returns>True if the track was present and removed; false otherwise.</returns>
    public bool RemoveTrack(PatternTrack track)
    {
        ArgumentNullException.ThrowIfNull(track);
        return _tracks.Remove(track);
    }

    /// <summary>Removes all tracks.</summary>
    public void Clear() => _tracks.Clear();
}
