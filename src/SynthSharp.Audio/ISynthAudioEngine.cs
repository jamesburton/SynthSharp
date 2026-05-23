using SynthSharp.Core.Layout;

namespace SynthSharp.Audio;

/// <summary>Contract for the synthesizer audio engine managing voice lifecycle and playback.</summary>
public interface ISynthAudioEngine
{
    /// <summary>Starts a sustained note for the given voice, stopping any prior note on the same voice ID.</summary>
    /// <param name="voiceId">Unique identifier for the voice slot; case-insensitive.</param>
    /// <param name="assignment">Pad assignment describing waveform, frequency, and envelope.</param>
    /// <param name="cancellationToken">Optional token; cancellation stops the sustained note immediately.</param>
    /// <returns>A completed <see cref="Task"/> — playback runs fire-and-forget on the backend.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="voiceId"/> is null, empty, or whitespace.</exception>
    Task NoteOnAsync(string voiceId, PadAssignment assignment, CancellationToken cancellationToken = default);

    /// <summary>Ends a sustained note and plays the release tail if the envelope has a non-zero release.</summary>
    /// <param name="voiceId">The voice identifier passed to <see cref="NoteOnAsync"/>; no-op if not found.</param>
    void NoteOff(string voiceId);

    /// <summary>Cancels and removes all active voices immediately.</summary>
    void NoteOffAll();

    /// <summary>Previews a pad by starting a note, waiting the requested duration, then stopping it.</summary>
    /// <param name="assignment">Pad assignment describing waveform, frequency, and envelope.</param>
    /// <param name="duration">How long to hold the note before stopping it.</param>
    /// <param name="cancellationToken">Optional token; cancellation stops the preview immediately.</param>
    /// <returns>A <see cref="Task"/> that completes once the note has been stopped.</returns>
    Task PlayPadAsync(PadAssignment assignment, TimeSpan duration, CancellationToken cancellationToken = default);
}
