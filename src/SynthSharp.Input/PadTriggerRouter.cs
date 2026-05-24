using SynthSharp.Core.Layout;

namespace SynthSharp.Input;

public sealed class PadTriggerRouter
{
    private readonly Dictionary<string, string> _padByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, string> _padByMidiNote = new();

    public PadTriggerRouter(IEnumerable<PadAssignment> pads)
    {
        Rebuild(pads);
    }

    public bool TryResolvePad(string key, out string padId)
    {
        return _padByKey.TryGetValue(key, out padId!);
    }

    /// <summary>Resolves a MIDI note number to the bound pad ID.</summary>
    /// <param name="midiNote">MIDI note number (0-127).</param>
    /// <param name="padId">When this method returns true, set to the pad ID; otherwise default.</param>
    /// <returns>True when a pad has <see cref="PadAssignment.MidiNote"/> equal to <paramref name="midiNote"/>.</returns>
    public bool TryResolvePadByMidiNote(int midiNote, out string padId)
    {
        return _padByMidiNote.TryGetValue(midiNote, out padId!);
    }

    public void Rebuild(IEnumerable<PadAssignment> pads)
    {
        _padByKey.Clear();
        _padByMidiNote.Clear();
        foreach (var pad in pads)
        {
            if (!string.IsNullOrWhiteSpace(pad.KeyBinding))
            {
                _padByKey[pad.KeyBinding.Trim()] = pad.PadId;
            }

            if (pad.MidiNote.HasValue)
            {
                _padByMidiNote[pad.MidiNote.Value] = pad.PadId;
            }
        }
    }
}
