namespace SynthSharp.Input;

/// <summary>Identifies a MIDI input device available on the host.</summary>
/// <param name="Id">Platform-specific stable identifier; use for reconnecting across sessions.</param>
/// <param name="Name">Human-readable device name (e.g., "MPK Mini MK3").</param>
public sealed record MidiDeviceInfo(string Id, string Name);
