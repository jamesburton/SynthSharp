using SynthSharp.Input;

namespace SynthSharp.App;

/// <summary>No-op <see cref="IMidiInputSource"/> for platforms where MIDI is not wired up.</summary>
public sealed class NullMidiInputSource : IMidiInputSource
{
    /// <inheritdoc/>
    public bool IsRunning => false;

    /// <inheritdoc/>
    public IReadOnlyList<MidiDeviceInfo> GetAvailableDevices() => Array.Empty<MidiDeviceInfo>();

    /// <inheritdoc/>
    public void Start(MidiDeviceInfo device) { }

    /// <inheritdoc/>
    public void Stop() { }

#pragma warning disable CS0067 // event never raised — intentional null implementation
    /// <inheritdoc/>
    public event EventHandler<MidiNoteEvent>? NoteOn;

    /// <inheritdoc/>
    public event EventHandler<MidiNoteEvent>? NoteOff;
#pragma warning restore CS0067
}
