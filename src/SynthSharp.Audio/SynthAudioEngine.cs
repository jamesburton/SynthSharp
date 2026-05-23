using SynthSharp.Core.Layout;
using SynthSharp.Core.Audio;

namespace SynthSharp.Audio;

/// <summary>Synthesizer audio engine — manages voice lifecycle, polyphony, and playback.</summary>
public sealed class SynthAudioEngine : ISynthAudioEngine
{
    private readonly object _gate = new();
    private readonly IAudioPlaybackBackend _playbackBackend;
    private readonly int _maxPolyphony;
    private readonly Dictionary<string, ActiveVoice> _activeVoices = new(StringComparer.OrdinalIgnoreCase);
    private long _voiceSequence;

    private static readonly TimeSpan MaxSustainDuration = TimeSpan.FromSeconds(10);
    private const string PreviewVoiceId = "__preview";

    /// <summary>Initializes a new engine with the given backend and polyphony cap.</summary>
    public SynthAudioEngine(IAudioPlaybackBackend playbackBackend, int maxPolyphony = 1)
    {
        _playbackBackend = playbackBackend;
        _maxPolyphony = Math.Max(1, maxPolyphony);
    }

    /// <summary>Starts a sustained note for the given voice, stopping any prior note on the same voice ID.</summary>
    public Task NoteOnAsync(string voiceId, PadAssignment assignment, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(voiceId))
        {
            throw new ArgumentException("Voice identifier is required.", nameof(voiceId));
        }

        lock (_gate)
        {
            StopVoiceNoLock(voiceId);
            EnsureVoiceCapacityNoLock();

            var voiceCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var sustainEnvelope = assignment.Envelope with { ReleaseSeconds = 0d };
            var voice = new ActiveVoice(
                VoiceId: voiceId,
                Assignment: assignment,
                CancellationSource: voiceCts,
                SequenceNumber: ++_voiceSequence);

            // Register before firing PlaySustainAsync so the cleanup continuation
            // can find the voice in the dictionary even in synchronous-completion edge cases.
            _activeVoices[voiceId] = voice;
            _ = PlaySustainAsync(voice, sustainEnvelope);
        }

        return Task.CompletedTask;
    }

    /// <summary>Ends a sustained note and plays the release tail if the envelope has a non-zero release.</summary>
    public void NoteOff(string voiceId)
    {
        if (string.IsNullOrWhiteSpace(voiceId))
        {
            return;
        }

        ActiveVoice? voice = null;
        lock (_gate)
        {
            if (_activeVoices.Remove(voiceId, out var activeVoice))
            {
                activeVoice.CancellationSource.Cancel();
                activeVoice.CancellationSource.Dispose();
                voice = activeVoice;
            }
        }

        if (voice is null || voice.Assignment.Envelope.ReleaseSeconds <= 0d)
        {
            return;
        }

        _ = PlayReleaseTailAsync(voice.Assignment);
    }

    /// <summary>Cancels and removes all active voices immediately.</summary>
    public void NoteOffAll()
    {
        lock (_gate)
        {
            foreach (var voice in _activeVoices.Values)
            {
                voice.CancellationSource.Cancel();
                voice.CancellationSource.Dispose();
            }

            _activeVoices.Clear();
        }
    }

    /// <summary>Previews a pad by starting a note, waiting the requested duration, then stopping it.</summary>
    public async Task PlayPadAsync(PadAssignment assignment, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        await NoteOnAsync(PreviewVoiceId, assignment, cancellationToken);

        try
        {
            await Task.Delay(duration, cancellationToken);
        }
        finally
        {
            NoteOff(PreviewVoiceId);
        }
    }

    private void EnsureVoiceCapacityNoLock()
    {
        while (_activeVoices.Count >= _maxPolyphony)
        {
            var oldestVoiceId = _activeVoices
                .OrderBy(x => x.Value.SequenceNumber)
                .First()
                .Key;
            StopVoiceNoLock(oldestVoiceId);
        }
    }

    private void StopVoiceNoLock(string voiceId)
    {
        if (!_activeVoices.Remove(voiceId, out var existingVoice))
        {
            return;
        }

        existingVoice.CancellationSource.Cancel();
        existingVoice.CancellationSource.Dispose();
    }

    private async Task PlaySustainAsync(ActiveVoice voice, Envelope sustainEnvelope)
    {
        try
        {
            using var stream = WavToneRenderer.RenderMonoPcm16(
                voice.Assignment.Waveform,
                voice.Assignment.FrequencyHz,
                MaxSustainDuration,
                sustainEnvelope);
            await _playbackBackend.PlayAsync(stream, voice.CancellationSource.Token);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            // Free the polyphony slot when sustain ends naturally (10s cap) without NoteOff.
            // The sequence-number / reference check guards against two races:
            //   1. NoteOff already removed + disposed this voice before we get here — ReferenceEquals
            //      returns false, so we skip the double-dispose.
            //   2. A re-trigger of the same voiceId started a new ActiveVoice instance and registered it
            //      under the same key — ReferenceEquals returns false, so we leave the new voice alone.
            lock (_gate)
            {
                if (_activeVoices.TryGetValue(voice.VoiceId, out var current) && ReferenceEquals(current, voice))
                {
                    _activeVoices.Remove(voice.VoiceId);
                    voice.CancellationSource.Dispose();
                }
            }
        }
    }

    private async Task PlayReleaseTailAsync(PadAssignment assignment)
    {
        var releaseDuration = TimeSpan.FromSeconds(Math.Max(0.02d, assignment.Envelope.ReleaseSeconds));
        var releaseEnvelope = new Envelope(
            AttackSeconds: 0d,
            DecaySeconds: 0d,
            SustainLevel: 1d,
            ReleaseSeconds: assignment.Envelope.ReleaseSeconds);

        using var stream = WavToneRenderer.RenderMonoPcm16(
            assignment.Waveform,
            assignment.FrequencyHz,
            releaseDuration,
            releaseEnvelope);
        await _playbackBackend.PlayAsync(stream);
    }

    private sealed record ActiveVoice(
        string VoiceId,
        PadAssignment Assignment,
        CancellationTokenSource CancellationSource,
        long SequenceNumber);
}
