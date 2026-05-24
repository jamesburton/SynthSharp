namespace SynthSharp.Core.Patterns;

/// <summary>A single track within a <see cref="PatternSet"/>, wrapping a <see cref="PatternClip"/> with mute / solo flags.</summary>
public sealed class PatternTrack
{
    /// <summary>Display name for the track.</summary>
    public string Name { get; set; } = "track";

    /// <summary>The clip whose events this track plays.</summary>
    public required PatternClip Clip { get; init; }

    /// <summary>When true, the track is silent during playback. Solo on any track overrides this for non-solo'd tracks.</summary>
    public bool Mute { get; set; }

    /// <summary>When true, only tracks with Solo == true play; all others are silent regardless of Mute.</summary>
    public bool Solo { get; set; }
}
