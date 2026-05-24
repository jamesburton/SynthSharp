namespace SynthSharp.Input;

/// <summary>A MIDI note-on or note-off event with normalised velocity.</summary>
/// <param name="MidiNote">MIDI note number (0-127). 60 is middle C.</param>
/// <param name="Velocity">Normalised velocity in [0.0, 1.0]; 0 indicates a note-off coming through as note-on with zero velocity.</param>
public sealed record MidiNoteEvent(int MidiNote, float Velocity);
