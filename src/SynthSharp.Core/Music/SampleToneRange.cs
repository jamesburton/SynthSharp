namespace SynthSharp.Core.Music;

/// <summary>Describes the recommended playable semitone range around an imported sample's detected pitch.</summary>
/// <param name="CenterNote">Textual note name of the detected fundamental (e.g., "A4", "C#3").</param>
/// <param name="CenterPitchHz">Detected fundamental frequency in Hz.</param>
/// <param name="CenterMidiNote">MIDI note number of the detected fundamental (0-127).</param>
/// <param name="LowSemitone">Offset of the lowest playable semitone relative to <see cref="CenterMidiNote"/>; non-positive.</param>
/// <param name="HighSemitone">Offset of the highest playable semitone relative to <see cref="CenterMidiNote"/>; non-negative.</param>
public sealed record SampleToneRange(
    string CenterNote,
    float CenterPitchHz,
    int CenterMidiNote,
    int LowSemitone,
    int HighSemitone);
