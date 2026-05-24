using SynthSharp.Core.Layout;
using SynthSharp.Core.Audio;
using SynthSharp.Core.Persistence;

namespace SynthSharp.Audio;

/// <summary>Synthesizer audio engine — manages voice lifecycle, polyphony, and playback.</summary>
public sealed class SynthAudioEngine : ISynthAudioEngine
{
    private readonly object _gate = new();
    private readonly IAudioPlaybackBackend _playbackBackend;
    private readonly int _maxPolyphony;
    private readonly Dictionary<string, ActiveVoice> _activeVoices = new(StringComparer.OrdinalIgnoreCase);
    private long _voiceSequence;

    private readonly ISampleImporter? _sampleImporter;
    private readonly ISampleExporter? _sampleExporter;
    private readonly string? _samplesDirectory;

    private static readonly TimeSpan MaxSustainDuration = TimeSpan.FromSeconds(10);
    private const string PreviewVoiceId = "__preview";

    /// <summary>Initializes a new engine with the given backend and polyphony cap. Sample playback is not available with this constructor.</summary>
    /// <param name="playbackBackend">The audio playback backend used to render PCM streams.</param>
    /// <param name="maxPolyphony">Maximum simultaneous voices; oldest is evicted when the cap is reached.</param>
    public SynthAudioEngine(IAudioPlaybackBackend playbackBackend, int maxPolyphony = 1)
        : this(playbackBackend, sampleImporter: null, sampleExporter: null, samplesDirectory: null, maxPolyphony: maxPolyphony)
    {
    }

    /// <summary>
    /// Initializes a new engine with the given backend, sample support, and polyphony cap.
    /// </summary>
    /// <param name="playbackBackend">The audio playback backend used to render PCM streams.</param>
    /// <param name="sampleImporter">
    /// Importer used to decode WAV files from disk. When null, pads that reference a
    /// <see cref="PadAssignment.SampleFileName"/> will throw <see cref="InvalidOperationException"/> at play time.
    /// </param>
    /// <param name="sampleExporter">
    /// Exporter used to re-encode gain+envelope-shaped sample data as PCM16. When null, pads that reference a
    /// <see cref="PadAssignment.SampleFileName"/> will throw <see cref="InvalidOperationException"/> at play time.
    /// </param>
    /// <param name="samplesDirectory">
    /// Directory from which <see cref="PadAssignment.SampleFileName"/> values are resolved.
    /// When null, pads that reference a sample file will throw <see cref="InvalidOperationException"/> at play time.
    /// </param>
    /// <param name="maxPolyphony">Maximum simultaneous voices; oldest is evicted when the cap is reached.</param>
    public SynthAudioEngine(
        IAudioPlaybackBackend playbackBackend,
        ISampleImporter? sampleImporter,
        ISampleExporter? sampleExporter,
        string? samplesDirectory,
        int maxPolyphony = 1)
    {
        _playbackBackend = playbackBackend;
        _sampleImporter = sampleImporter;
        _sampleExporter = sampleExporter;
        _samplesDirectory = samplesDirectory;
        _maxPolyphony = Math.Max(1, maxPolyphony);
    }

    /// <summary>Starts a sustained note for the given voice, stopping any prior note on the same voice ID.</summary>
    /// <param name="voiceId">Unique identifier for the voice slot; case-insensitive.</param>
    /// <param name="assignment">Pad assignment describing waveform, frequency, and envelope.</param>
    /// <param name="cancellationToken">Optional token; cancellation stops the sustained note immediately.</param>
    /// <returns>A completed <see cref="Task"/> — playback runs fire-and-forget on the backend.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="voiceId"/> is null, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="assignment"/> references a sample file but the engine was constructed without sample support.
    /// </exception>
    /// <exception cref="FileNotFoundException">
    /// Thrown when <paramref name="assignment"/> references a sample file that does not exist in the configured samples directory.
    /// </exception>
    public Task NoteOnAsync(string voiceId, PadAssignment assignment, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(voiceId))
        {
            throw new ArgumentException("Voice identifier is required.", nameof(voiceId));
        }

        // Render outside the lock so concurrent NoteOn/NoteOff/NoteOffAll don't block
        // on PCM synthesis (~10s of audio = ~882KB per voice).
        MemoryStream stream;
        if (!string.IsNullOrWhiteSpace(assignment.SampleFileName))
        {
            stream = RenderSampleStream(assignment);
        }
        else
        {
            var sustainEnvelope = assignment.Envelope with { ReleaseSeconds = 0d };
            stream = WavToneRenderer.RenderMonoPcm16(
                assignment.Waveform, assignment.FrequencyHz, MaxSustainDuration, sustainEnvelope,
                filter: assignment.Filter, lfo: assignment.Lfo);
        }

        lock (_gate)
        {
            StopVoiceNoLock(voiceId);
            EnsureVoiceCapacityNoLock();

            var voiceCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var voice = new ActiveVoice(
                VoiceId: voiceId,
                Assignment: assignment,
                CancellationSource: voiceCts,
                SequenceNumber: ++_voiceSequence);

            // Register before firing PlaySustainAsync so the cleanup continuation
            // can find the voice in the dictionary even in synchronous-completion edge cases.
            _activeVoices[voiceId] = voice;
            _ = PlaySustainAsync(voice, stream);
        }

        return Task.CompletedTask;
    }

    /// <summary>Ends a sustained note and plays the release tail if the envelope has a non-zero release.</summary>
    /// <param name="voiceId">The voice identifier passed to <see cref="NoteOnAsync"/>; no-op if not found.</param>
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
    /// <param name="assignment">Pad assignment describing waveform, frequency, and envelope.</param>
    /// <param name="duration">How long to hold the note before calling <see cref="NoteOff"/>.</param>
    /// <param name="cancellationToken">Optional token; cancellation stops the preview immediately.</param>
    /// <returns>A <see cref="Task"/> that completes once the note has been stopped.</returns>
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

    /// <inheritdoc/>
    public async Task WarmupAsync(CancellationToken cancellationToken = default)
    {
        // Render a 20 ms silent burst (sustain level 0 across the whole sample) and play it
        // through the backend. The render is cheap (~1.8 KB of zeros) and the backend call
        // forces the platform-specific media pipeline to spin up — on Windows that's
        // Plugin.Maui.Audio creating the WinRT MediaPlayer + MediaSource for the first time,
        // which is the dominant latency source on the first user-triggered note.
        var silentEnvelope = new Envelope(
            AttackSeconds: 0d,
            DecaySeconds: 0d,
            SustainLevel: 0d,
            ReleaseSeconds: 0d);

        using var stream = WavToneRenderer.RenderMonoPcm16(
            WaveformType.Sine,
            frequencyHz: 100d,
            duration: TimeSpan.FromMilliseconds(20),
            envelope: silentEnvelope);

        try
        {
            await _playbackBackend.PlayAsync(stream, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Warmup cancellation is fine — caller can abort if they're shutting down early.
        }
        catch
        {
            // Best-effort warmup; swallow any backend startup failure so it doesn't crash the app.
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

    private async Task PlaySustainAsync(ActiveVoice voice, MemoryStream stream)
    {
        try
        {
            using (stream)
            {
                await _playbackBackend.PlayAsync(stream, voice.CancellationSource.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when NoteOff cancels the voice.
        }
        catch (Exception)
        {
            // Swallow any backend failure — this is a fire-and-forget audio path with no recovery path.
            // A future logging seam would go here.
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

    private MemoryStream RenderSampleStream(PadAssignment assignment)
    {
        if (_sampleImporter is null || _sampleExporter is null || string.IsNullOrWhiteSpace(_samplesDirectory))
        {
            throw new InvalidOperationException(
                $"Pad '{assignment.PadId}' references sample '{assignment.SampleFileName}' but the engine was constructed without sample support.");
        }

        var path = Path.Combine(_samplesDirectory, assignment.SampleFileName!);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Sample file '{assignment.SampleFileName}' not found in '{_samplesDirectory}'.", path);
        }

        using var fileStream = File.OpenRead(path);
        var sample = _sampleImporter.Import(fileStream, sourcePath: path);
        var sustainEnvelope = assignment.Envelope with { ReleaseSeconds = 0d };

        // Cap looped renders at MaxSustainDuration of frames so sustained loops match the
        // synth path's 10s budget. Non-loop renders pass maxOutputFrames=0 and the renderer
        // uses the source's natural length.
        var maxOutputFrames = assignment.SampleLoopEnabled
            ? (int)(sample.Metadata.SampleRateHz * MaxSustainDuration.TotalSeconds)
            : 0;

        return SampleRenderer.Render(
            sample,
            assignment.SampleGain,
            sustainEnvelope,
            _sampleExporter,
            filter: assignment.Filter,
            lfo: assignment.Lfo,
            loopEnabled: assignment.SampleLoopEnabled,
            loopStartFrame: assignment.SampleLoopStartFrame,
            loopEndFrame: assignment.SampleLoopEndFrame,
            maxOutputFrames: maxOutputFrames);
    }

    private async Task PlayReleaseTailAsync(PadAssignment assignment)
    {
        try
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
        catch
        {
            // Fire-and-forget release tail; nothing to do on failure.
        }
    }

    private sealed record ActiveVoice(
        string VoiceId,
        PadAssignment Assignment,
        CancellationTokenSource CancellationSource,
        long SequenceNumber);
}
