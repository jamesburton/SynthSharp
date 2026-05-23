using SynthSharp.Core.Audio;

namespace SynthSharp.Core.Music;

/// <summary>A pitch-shifted variant of a source sample, tagged with its offset and note name.</summary>
/// <param name="Sample">The pitch-shifted sample. For SemitoneOffset == 0, this is the original source.</param>
/// <param name="SemitoneOffset">Semitones relative to the source's detected centre pitch; negative = below, positive = above.</param>
/// <param name="Note">Textual note name of this variant (e.g., "A4", "C#5").</param>
/// <param name="MidiNote">MIDI note number for this variant (0–127).</param>
/// <param name="FrequencyHz">Target frequency in Hz for this variant.</param>
public sealed record SamplePitchVariant(
    Sample Sample,
    int SemitoneOffset,
    string Note,
    int MidiNote,
    float FrequencyHz);
