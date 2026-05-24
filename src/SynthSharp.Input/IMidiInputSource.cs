namespace SynthSharp.Input;

/// <summary>Source of MIDI note events from a hardware or virtual MIDI input device.</summary>
public interface IMidiInputSource
{
    /// <summary>True while the source is actively listening to a device.</summary>
    bool IsRunning { get; }

    /// <summary>Returns the MIDI input devices currently available on the host.</summary>
    IReadOnlyList<MidiDeviceInfo> GetAvailableDevices();

    /// <summary>Starts listening to <paramref name="device"/>. Stops any previously-running session.</summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="device"/> is null.</exception>
    void Start(MidiDeviceInfo device);

    /// <summary>Stops listening and releases the underlying device handle.</summary>
    void Stop();

    /// <summary>Raised when a note-on (with velocity &gt; 0) arrives from the device.</summary>
    event EventHandler<MidiNoteEvent>? NoteOn;

    /// <summary>Raised when a note-off (or note-on with velocity 0) arrives from the device.</summary>
    event EventHandler<MidiNoteEvent>? NoteOff;
}
