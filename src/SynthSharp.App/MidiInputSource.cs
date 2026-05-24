#if WINDOWS
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using SynthSharp.Input;

namespace SynthSharp.App;

/// <summary>Windows MIDI input source backed by DryWetMIDI. Activates only when running on the Windows TFM.</summary>
public sealed class MidiInputSource : IMidiInputSource, IDisposable
{
    private InputDevice? _device;

    /// <inheritdoc/>
    public bool IsRunning => _device is not null && _device.IsListeningForEvents;

    /// <inheritdoc/>
    public event EventHandler<MidiNoteEvent>? NoteOn;

    /// <inheritdoc/>
    public event EventHandler<MidiNoteEvent>? NoteOff;

    /// <inheritdoc/>
    public IReadOnlyList<MidiDeviceInfo> GetAvailableDevices()
    {
        var devices = InputDevice.GetAll();
        var result = new List<MidiDeviceInfo>(devices.Count);
        foreach (var device in devices)
        {
            result.Add(new MidiDeviceInfo(device.Name, device.Name));

            // InputDevice is IDisposable; dispose each enumerated instance after reading its name
            // to release the underlying native handle.
            device.Dispose();
        }

        return result;
    }

    /// <inheritdoc/>
    public void Start(MidiDeviceInfo device)
    {
        ArgumentNullException.ThrowIfNull(device);
        Stop();

        var inputDevice = InputDevice.GetByName(device.Name);
        inputDevice.EventReceived += OnEventReceived;
        inputDevice.StartEventsListening();
        _device = inputDevice;
    }

    /// <inheritdoc/>
    public void Stop()
    {
        if (_device is null)
        {
            return;
        }

        try
        {
            _device.StopEventsListening();
        }
        catch
        {
            // Best-effort stop.
        }

        _device.EventReceived -= OnEventReceived;
        _device.Dispose();
        _device = null;
    }

    /// <inheritdoc/>
    public void Dispose() => Stop();

    private void OnEventReceived(object? sender, MidiEventReceivedEventArgs args)
    {
        switch (args.Event)
        {
            case NoteOnEvent noteOn when noteOn.Velocity == 0:
                // MIDI spec: note-on with velocity 0 is equivalent to note-off.
                NoteOff?.Invoke(this, new MidiNoteEvent(noteOn.NoteNumber, 0f));
                break;
            case NoteOnEvent noteOn:
                NoteOn?.Invoke(this, new MidiNoteEvent(noteOn.NoteNumber, noteOn.Velocity / 127f));
                break;
            case NoteOffEvent noteOff:
                NoteOff?.Invoke(this, new MidiNoteEvent(noteOff.NoteNumber, noteOff.Velocity / 127f));
                break;
        }
    }
}
#endif
