using SynthSharp.Core.Audio;
using SynthSharp.Core.Layout;
using SynthSharp.Core.Persistence;

namespace SynthSharp.Audio.Tests;

/// <summary>Unit tests for <see cref="SynthAudioEngine"/> voice lifecycle and playback behaviour.</summary>
public sealed class SynthAudioEngineTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    /// <summary>Builds a minimal <see cref="PadAssignment"/> suitable for tests.</summary>
    private static PadAssignment MakePad(double releaseSeconds = 0.10) => new()
    {
        PadId = "test-pad",
        RowIndex = 0,
        ColumnIndex = 0,
        Role = RowRole.MelodicA,
        KeyBinding = "A",
        Label = "A",
        Waveform = WaveformType.Sine,
        FrequencyHz = 440d,
        Envelope = new Envelope(
            AttackSeconds: 0.01,
            DecaySeconds: 0.05,
            SustainLevel: 0.80,
            ReleaseSeconds: releaseSeconds),
    };

    // ---------------------------------------------------------------------------
    // Fake backend
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Test double for <see cref="IAudioPlaybackBackend"/> that records every PlayAsync invocation
    /// and keeps each call suspended until the test signals completion or the token is cancelled.
    /// </summary>
    private sealed class FakeAudioPlaybackBackend : IAudioPlaybackBackend
    {
        public List<Invocation> Invocations { get; } = new();

        public async Task PlayAsync(Stream pcmWaveStream, CancellationToken cancellationToken = default)
        {
            // Capture stream bytes immediately (before any await) — stream is disposed by the caller once PlayAsync returns.
            using var ms = new MemoryStream();
            pcmWaveStream.Position = 0;
            pcmWaveStream.CopyTo(ms);
            var invocation = new Invocation(ms.ToArray(), cancellationToken);
            Invocations.Add(invocation);

            // Honour cancellation: cancel the TCS so awaiting code unblocks.
            using var registration = cancellationToken.Register(
                () => invocation.CompletionSource.TrySetResult());

            await invocation.CompletionSource.Task;
        }

        public sealed class Invocation
        {
            public Invocation(byte[] payload, CancellationToken token)
            {
                Payload = payload;
                Token = token;
            }

            /// <summary>Raw WAV bytes captured from the stream at PlayAsync entry.</summary>
            public byte[] Payload { get; }

            /// <summary>Number of bytes in the captured stream (convenience wrapper over <see cref="Payload"/>).</summary>
            public long StreamLength => Payload.Length;

            public CancellationToken Token { get; }
            public TaskCompletionSource CompletionSource { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>True when the token passed to PlayAsync was cancelled.</summary>
            public bool WasCancelled => Token.IsCancellationRequested;

            /// <summary>Signal natural completion of this invocation (simulates audio finishing).</summary>
            public void SignalComplete() => CompletionSource.TrySetResult();
        }
    }

    // ---------------------------------------------------------------------------
    // Tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task NoteOnAsync_StartsPlayback()
    {
        var backend = new FakeAudioPlaybackBackend();
        var engine = new SynthAudioEngine(backend, maxPolyphony: 1);

        await engine.NoteOnAsync("v1", MakePad());

        Assert.Single(backend.Invocations);
        Assert.True(backend.Invocations[0].StreamLength > 0);
    }

    [Fact]
    public async Task NoteOff_CancelsActivePlayback()
    {
        var backend = new FakeAudioPlaybackBackend();

        // Use release=0 so NoteOff does not trigger a second PlayAsync call.
        var engine = new SynthAudioEngine(backend, maxPolyphony: 1);

        await engine.NoteOnAsync("v1", MakePad(releaseSeconds: 0d));
        engine.NoteOff("v1");

        // Give the cancellation token registration time to propagate.
        await Task.Delay(20);

        Assert.Single(backend.Invocations);
        Assert.True(backend.Invocations[0].WasCancelled);
    }

    [Fact]
    public async Task NoteOff_WithReleaseGreaterThanZero_PlaysReleaseTail()
    {
        var backend = new FakeAudioPlaybackBackend();
        var engine = new SynthAudioEngine(backend, maxPolyphony: 1);

        await engine.NoteOnAsync("v1", MakePad(releaseSeconds: 0.10));
        engine.NoteOff("v1");

        // Wait for the release tail PlayAsync to be invoked.
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (backend.Invocations.Count < 2 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.Equal(2, backend.Invocations.Count);

        // First invocation (sustain) was cancelled by NoteOff.
        Assert.True(backend.Invocations[0].WasCancelled);

        // Second invocation (release tail) received no cancellation token, so it must not be cancelled.
        Assert.False(backend.Invocations[1].WasCancelled);

        // Let the release tail finish so the test doesn't hang.
        backend.Invocations[1].SignalComplete();
    }

    [Fact]
    public async Task NoteOff_WithReleaseEqualToZero_DoesNotPlayTail()
    {
        var backend = new FakeAudioPlaybackBackend();
        var engine = new SynthAudioEngine(backend, maxPolyphony: 1);

        await engine.NoteOnAsync("v1", MakePad(releaseSeconds: 0d));
        engine.NoteOff("v1");

        // Give async fire-and-forget tasks time to start.
        await Task.Delay(50);

        // Only one PlayAsync call — no release tail.
        Assert.Single(backend.Invocations);
    }

    [Fact]
    public async Task Polyphony_Cap_EvictsOldestBySequenceNumber()
    {
        var backend = new FakeAudioPlaybackBackend();
        var engine = new SynthAudioEngine(backend, maxPolyphony: 2);

        // Start two voices.
        await engine.NoteOnAsync("v1", MakePad(releaseSeconds: 0d));
        await engine.NoteOnAsync("v2", MakePad(releaseSeconds: 0d));

        // Starting a third must evict v1 (oldest by sequence number).
        await engine.NoteOnAsync("v3", MakePad(releaseSeconds: 0d));

        await Task.Delay(20);

        // Three PlayAsync calls started.
        Assert.Equal(3, backend.Invocations.Count);

        // The first invocation (v1) must have been cancelled.
        Assert.True(backend.Invocations[0].WasCancelled);

        // v2 and v3 are still alive.
        Assert.False(backend.Invocations[1].WasCancelled);
        Assert.False(backend.Invocations[2].WasCancelled);
    }

    [Fact]
    public async Task NoteOffAll_CancelsAndClearsAllVoices()
    {
        var backend = new FakeAudioPlaybackBackend();
        var engine = new SynthAudioEngine(backend, maxPolyphony: 3);

        await engine.NoteOnAsync("v1", MakePad(releaseSeconds: 0d));
        await engine.NoteOnAsync("v2", MakePad(releaseSeconds: 0d));
        await engine.NoteOnAsync("v3", MakePad(releaseSeconds: 0d));

        engine.NoteOffAll();

        await Task.Delay(30);

        // All three sustain invocations must be cancelled.
        Assert.Equal(3, backend.Invocations.Count);
        Assert.All(backend.Invocations, inv => Assert.True(inv.WasCancelled));

        // Calling NoteOff for any voice after NoteOffAll is a no-op — no new invocations.
        engine.NoteOff("v1");
        await Task.Delay(20);

        Assert.Equal(3, backend.Invocations.Count);
    }

    [Fact]
    public async Task NaturalSustainExpiry_FreesVoiceSlot()
    {
        var backend = new FakeAudioPlaybackBackend();

        // Single-voice polyphony so a new NoteOn requires an eviction if the slot is occupied.
        var engine = new SynthAudioEngine(backend, maxPolyphony: 1);

        await engine.NoteOnAsync("v1", MakePad(releaseSeconds: 0d));

        var v1Invocation = backend.Invocations[0];

        // Simulate natural sustain expiry (no NoteOff — the audio stream simply ends).
        v1Invocation.SignalComplete();

        // Give the engine's finally block a moment to run on the continuation queued by SignalComplete().
        await Task.Delay(50);

        // Start a brand-new voice with a different ID — should claim the freed slot, NOT evict v1.
        await engine.NoteOnAsync("v2", MakePad(releaseSeconds: 0d));

        // Two PlayAsync calls total.
        Assert.Equal(2, backend.Invocations.Count);

        // v1 completed naturally — its token must NOT have been cancelled by an eviction.
        Assert.False(v1Invocation.WasCancelled,
            "v1 sustain token should not be cancelled — it completed naturally, not evicted.");
    }

    [Fact]
    public async Task ReNoteOn_SameVoiceId_StopsPreviousVoice()
    {
        var backend = new FakeAudioPlaybackBackend();
        var engine = new SynthAudioEngine(backend, maxPolyphony: 2);

        await engine.NoteOnAsync("v1", MakePad(releaseSeconds: 0d));
        var first = backend.Invocations[0];

        // Re-trigger the same voiceId.
        await engine.NoteOnAsync("v1", MakePad(releaseSeconds: 0d));

        await Task.Delay(20);

        // Two PlayAsync calls.
        Assert.Equal(2, backend.Invocations.Count);

        // The first invocation must have been cancelled by the re-trigger.
        Assert.True(first.WasCancelled);

        // The second invocation is still live.
        Assert.False(backend.Invocations[1].WasCancelled);
    }

    [Fact]
    public void NoteOff_OnUnregisteredVoiceId_IsNoOp()
    {
        var backend = new FakeAudioPlaybackBackend();
        var engine = new SynthAudioEngine(backend, maxPolyphony: 1);

        // No NoteOn has been called — NoteOff on an unknown voice must not throw
        // and must not invoke the backend.
        engine.NoteOff("never-registered");

        Assert.Empty(backend.Invocations);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task NoteOnAsync_WithMissingVoiceId_ThrowsArgumentException(string? voiceId)
    {
        var backend = new FakeAudioPlaybackBackend();
        var engine = new SynthAudioEngine(backend, maxPolyphony: 1);

        await Assert.ThrowsAsync<ArgumentException>(
            () => engine.NoteOnAsync(voiceId!, MakePad()));

        Assert.Empty(backend.Invocations);
    }

    [Fact]
    public async Task Polyphony_EvictedVoice_DoesNotPlayReleaseTail()
    {
        var backend = new FakeAudioPlaybackBackend();
        var engine = new SynthAudioEngine(backend, maxPolyphony: 1);

        // Voice with a non-zero release — if NoteOff were called we'd expect a tail.
        await engine.NoteOnAsync("v1", MakePad(releaseSeconds: 0.10));

        // Eviction via a second NoteOn should NOT play a release tail —
        // voice stealing is a hard cut, not a graceful release.
        await engine.NoteOnAsync("v2", MakePad(releaseSeconds: 0.10));

        await Task.Delay(50);

        // Two PlayAsync calls total (v1 sustain + v2 sustain). No third for a release tail.
        Assert.Equal(2, backend.Invocations.Count);
        Assert.True(backend.Invocations[0].WasCancelled);
        Assert.False(backend.Invocations[1].WasCancelled);
    }

    [Fact]
    public async Task PlayPadAsync_StillWorksEndToEnd()
    {
        var backend = new FakeAudioPlaybackBackend();
        var engine = new SynthAudioEngine(backend, maxPolyphony: 1);

        // PlayPadAsync: NoteOn, waits duration, NoteOff.
        var playTask = engine.PlayPadAsync(MakePad(releaseSeconds: 0d), duration: TimeSpan.FromMilliseconds(30));

        // Wait for the sustain PlayAsync to be registered.
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (backend.Invocations.Count == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(5);
        }

        // Allow the pad delay to elapse and PlayPadAsync to call NoteOff.
        await playTask;

        // Backend was called at least once (sustain); voice was then cancelled by NoteOff.
        Assert.True(backend.Invocations.Count >= 1);
        Assert.True(backend.Invocations[0].WasCancelled);
    }

    [Fact]
    public async Task WarmupAsync_InvokesBackendOnce_AndCompletes()
    {
        var backend = new FakeAudioPlaybackBackend();
        var engine = new SynthAudioEngine(backend, maxPolyphony: 1);

        var warmupTask = engine.WarmupAsync();

        // Wait for the backend invocation to be registered, then complete it.
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (backend.Invocations.Count == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(5);
        }

        Assert.Single(backend.Invocations);
        backend.Invocations[0].SignalComplete();

        await warmupTask;
    }

    [Fact]
    public async Task WarmupAsync_SwallowsBackendExceptions()
    {
        var backend = new ThrowingBackend();
        var engine = new SynthAudioEngine(backend, maxPolyphony: 1);

        // Must not throw — warmup is best-effort.
        await engine.WarmupAsync();
    }

    private sealed class ThrowingBackend : IAudioPlaybackBackend
    {
        public Task PlayAsync(Stream pcmWaveStream, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("simulated backend failure");
    }

    // ---------------------------------------------------------------------------
    // Per-pad polyphony tests
    // ---------------------------------------------------------------------------

    /// <summary>
    /// A pad with MaxPolyphony=1 should cancel the previous voice on the same pad before starting a new one,
    /// leaving only the most-recently-triggered voice active.
    /// </summary>
    [Fact]
    public async Task NoteOnAsync_PerPadPolyphony_ReplacesOnSamePad()
    {
        var backend = new FakeAudioPlaybackBackend();
        var engine = new SynthAudioEngine(backend, maxPolyphony: 10);

        var pad = new PadAssignment
        {
            PadId = "kick",
            RowIndex = 0,
            ColumnIndex = 0,
            Role = RowRole.MelodicA,
            KeyBinding = "A",
            Label = "Kick",
            Waveform = WaveformType.Sine,
            FrequencyHz = 60d,
            MaxPolyphony = 1,
        };

        await engine.NoteOnAsync("v1", pad);
        await engine.NoteOnAsync("v2", pad);
        await engine.NoteOnAsync("v3", pad);

        await Task.Delay(30);

        // Three backend invocations total.
        Assert.Equal(3, backend.Invocations.Count);

        // First two were evicted by per-pad cap.
        Assert.True(backend.Invocations[0].WasCancelled);
        Assert.True(backend.Invocations[1].WasCancelled);

        // Third is still active.
        Assert.False(backend.Invocations[2].WasCancelled);
    }

    /// <summary>
    /// Per-pad eviction on pad A must not cancel voices belonging to pad B.
    /// </summary>
    [Fact]
    public async Task NoteOnAsync_PerPadPolyphony_DoesNotEvictOtherPads()
    {
        var backend = new FakeAudioPlaybackBackend();
        var engine = new SynthAudioEngine(backend, maxPolyphony: 10);

        var padA = new PadAssignment
        {
            PadId = "pad-a",
            RowIndex = 0,
            ColumnIndex = 0,
            Role = RowRole.MelodicA,
            KeyBinding = "A",
            Label = "A",
            Waveform = WaveformType.Sine,
            FrequencyHz = 440d,
            MaxPolyphony = 1,
        };

        var padB = new PadAssignment
        {
            PadId = "pad-b",
            RowIndex = 0,
            ColumnIndex = 1,
            Role = RowRole.MelodicA,
            KeyBinding = "B",
            Label = "B",
            Waveform = WaveformType.Sine,
            FrequencyHz = 880d,
            MaxPolyphony = 0,
        };

        // A1 then B1 (different pads), then A2 (re-triggers pad A — should evict A1 only).
        await engine.NoteOnAsync("v-a1", padA);
        await engine.NoteOnAsync("v-b1", padB);
        await engine.NoteOnAsync("v-a2", padA);

        await Task.Delay(30);

        Assert.Equal(3, backend.Invocations.Count);

        // A1 (invocation[0]) cancelled by per-pad eviction.
        Assert.True(backend.Invocations[0].WasCancelled);

        // B1 (invocation[1]) must still be active — pad B was not touched.
        Assert.False(backend.Invocations[1].WasCancelled);

        // A2 (invocation[2]) still active.
        Assert.False(backend.Invocations[2].WasCancelled);
    }

    /// <summary>
    /// A pad with MaxPolyphony=3 should allow up to 3 simultaneous voices; the 4th triggers eviction of the oldest.
    /// </summary>
    [Fact]
    public async Task NoteOnAsync_PerPadPolyphony_AllowsUpToLimit()
    {
        var backend = new FakeAudioPlaybackBackend();
        var engine = new SynthAudioEngine(backend, maxPolyphony: 10);

        var pad = new PadAssignment
        {
            PadId = "chord-pad",
            RowIndex = 0,
            ColumnIndex = 0,
            Role = RowRole.MelodicA,
            KeyBinding = "A",
            Label = "Chord",
            Waveform = WaveformType.Sine,
            FrequencyHz = 440d,
            MaxPolyphony = 3,
        };

        await engine.NoteOnAsync("v1", pad);
        await engine.NoteOnAsync("v2", pad);
        await engine.NoteOnAsync("v3", pad);
        await engine.NoteOnAsync("v4", pad); // triggers per-pad eviction of v1

        await Task.Delay(30);

        Assert.Equal(4, backend.Invocations.Count);

        // First voice evicted when v4 was added.
        Assert.True(backend.Invocations[0].WasCancelled);

        // v2, v3, v4 are still active.
        Assert.False(backend.Invocations[1].WasCancelled);
        Assert.False(backend.Invocations[2].WasCancelled);
        Assert.False(backend.Invocations[3].WasCancelled);
    }

    /// <summary>
    /// The engine-wide cap must still evict voices even when the per-pad cap would allow more.
    /// </summary>
    [Fact]
    public async Task NoteOnAsync_EngineWideCap_StillApplies()
    {
        var backend = new FakeAudioPlaybackBackend();
        var engine = new SynthAudioEngine(backend, maxPolyphony: 4);

        var pad = new PadAssignment
        {
            PadId = "synth-pad",
            RowIndex = 0,
            ColumnIndex = 0,
            Role = RowRole.MelodicA,
            KeyBinding = "A",
            Label = "Synth",
            Waveform = WaveformType.Sine,
            FrequencyHz = 440d,
            MaxPolyphony = 10,
        };

        await engine.NoteOnAsync("v1", pad);
        await engine.NoteOnAsync("v2", pad);
        await engine.NoteOnAsync("v3", pad);
        await engine.NoteOnAsync("v4", pad);
        await engine.NoteOnAsync("v5", pad); // engine-wide eviction of v1

        await Task.Delay(30);

        Assert.Equal(5, backend.Invocations.Count);

        // First voice evicted by engine-wide cap.
        Assert.True(backend.Invocations[0].WasCancelled);

        // 4 remaining voices are active.
        Assert.False(backend.Invocations[1].WasCancelled);
        Assert.False(backend.Invocations[2].WasCancelled);
        Assert.False(backend.Invocations[3].WasCancelled);
        Assert.False(backend.Invocations[4].WasCancelled);
    }

    /// <summary>
    /// MaxPolyphony=0 must fall back to engine-wide eviction only, preserving existing behaviour.
    /// </summary>
    [Fact]
    public async Task NoteOnAsync_MaxPolyphony0_FallsBackToEngineWide()
    {
        var backend = new FakeAudioPlaybackBackend();
        var engine = new SynthAudioEngine(backend, maxPolyphony: 2);

        var pad = MakePad(releaseSeconds: 0d);

        await engine.NoteOnAsync("v1", pad);
        await engine.NoteOnAsync("v2", pad);
        await engine.NoteOnAsync("v3", pad); // engine-wide eviction of v1

        await Task.Delay(30);

        Assert.Equal(3, backend.Invocations.Count);

        // First evicted by engine-wide cap.
        Assert.True(backend.Invocations[0].WasCancelled);

        // v2 and v3 still active.
        Assert.False(backend.Invocations[1].WasCancelled);
        Assert.False(backend.Invocations[2].WasCancelled);
    }

    // ---------------------------------------------------------------------------
    // Sample playback tests
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Writes a mono WAV file with <paramref name="frameCount"/> frames of constant <paramref name="amplitude"/>
    /// into a fresh temp directory. Returns the directory path and file name.
    /// </summary>
    private static (string Dir, string FileName) WriteTestSampleWav(
        int frameCount = 4410,
        int sampleRate = 44100,
        double amplitude = 0.5)
    {
        var dir = Path.Combine(Path.GetTempPath(), "synthsharp-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        var channel = new float[frameCount];
        for (var i = 0; i < frameCount; i++)
        {
            channel[i] = (float)amplitude;
        }

        var metadata = new SampleMetadata(
            Name: "test",
            ChannelCount: 1,
            SampleRateHz: sampleRate,
            FrameCount: frameCount,
            Duration: TimeSpan.FromSeconds((double)frameCount / sampleRate),
            SourceBitsPerSample: 16,
            SourcePath: null,
            ImportedAt: DateTimeOffset.UtcNow);
        var sample = new Sample(metadata, new[] { channel });

        var fileName = "test.wav";
        var fullPath = Path.Combine(dir, fileName);
        using var fs = File.Create(fullPath);
        new WavSampleExporter().Export(sample, fs);

        return (dir, fileName);
    }

    [Fact]
    public async Task NoteOnAsync_PadWithSampleFileName_LoadsAndPlaysSample()
    {
        var (dir, fileName) = WriteTestSampleWav();
        try
        {
            var backend = new FakeAudioPlaybackBackend();
            var engine = new SynthAudioEngine(
                backend,
                sampleImporter: new WavSampleImporter(),
                sampleExporter: new WavSampleExporter(),
                samplesDirectory: dir);

            var pad = MakePad();
            pad.SampleFileName = fileName;

            await engine.NoteOnAsync("v1", pad);

            Assert.Single(backend.Invocations);
            Assert.True(backend.Invocations[0].StreamLength > 0);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task NoteOnAsync_PadWithoutSampleFileName_StillSynthesises()
    {
        var backend = new FakeAudioPlaybackBackend();
        var engine = new SynthAudioEngine(backend, maxPolyphony: 1);

        // SampleFileName is null — should fall through to the synth path.
        await engine.NoteOnAsync("v1", MakePad());

        Assert.Single(backend.Invocations);
        Assert.True(backend.Invocations[0].StreamLength > 0);
    }

    [Fact]
    public async Task NoteOnAsync_PadWithSampleFileName_NoSampleSupport_Throws()
    {
        var backend = new FakeAudioPlaybackBackend();

        // 2-arg constructor — no sample importer/exporter configured.
        var engine = new SynthAudioEngine(backend, maxPolyphony: 1);

        var pad = MakePad();
        pad.SampleFileName = "some-sample.wav";

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.NoteOnAsync("v1", pad));
    }

    [Fact]
    public async Task NoteOnAsync_PadWithSampleFileName_FileMissing_Throws()
    {
        var dir = Path.Combine(Path.GetTempPath(), "synthsharp-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var backend = new FakeAudioPlaybackBackend();
            var engine = new SynthAudioEngine(
                backend,
                sampleImporter: new WavSampleImporter(),
                sampleExporter: new WavSampleExporter(),
                samplesDirectory: dir);

            var pad = MakePad();
            pad.SampleFileName = "nonexistent.wav";

            await Assert.ThrowsAsync<FileNotFoundException>(
                () => engine.NoteOnAsync("v1", pad));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ---------------------------------------------------------------------------
    // Velocity tests
    // ---------------------------------------------------------------------------

    /// <summary>Explicit velocity=1.0 must produce a stream byte-identical to the no-velocity overload.</summary>
    [Fact]
    public async Task NoteOnAsync_Velocity_1_0_MatchesNoVelocityOverload()
    {
        var backend1 = new FakeAudioPlaybackBackend();
        var engine1 = new SynthAudioEngine(backend1, maxPolyphony: 1);
        await engine1.NoteOnAsync("v1", MakePad(releaseSeconds: 0d));

        var backend2 = new FakeAudioPlaybackBackend();
        var engine2 = new SynthAudioEngine(backend2, maxPolyphony: 1);
        await engine2.NoteOnAsync("v1", MakePad(releaseSeconds: 0d), velocity: 1.0f);

        Assert.Single(backend1.Invocations);
        Assert.Single(backend2.Invocations);

        var bytes1 = backend1.Invocations[0].Payload;
        var bytes2 = backend2.Invocations[0].Payload;

        Assert.Equal(bytes1.Length, bytes2.Length);
        Assert.Equal(bytes1, bytes2);
    }

    /// <summary>Velocity=0.0 must produce a stream whose PCM samples are all zero (silence).</summary>
    [Fact]
    public async Task NoteOnAsync_Velocity_0_0_ProducesSilentOutput()
    {
        var backend = new FakeAudioPlaybackBackend();
        var engine = new SynthAudioEngine(backend, maxPolyphony: 1);

        await engine.NoteOnAsync("v1", MakePad(releaseSeconds: 0d), velocity: 0.0f);

        Assert.Single(backend.Invocations);

        var payload = backend.Invocations[0].Payload;
        // Skip 44-byte WAV header; every PCM16 sample must be zero.
        const int headerLen = 44;
        for (var i = headerLen; i < payload.Length; i++)
        {
            Assert.Equal(0, payload[i]);
        }
    }

    /// <summary>Velocity=0.5 must produce peak amplitude approximately half that of velocity=1.0 (within ±1 PCM unit).</summary>
    [Fact]
    public async Task NoteOnAsync_Velocity_0_5_HalvesPeakAmplitude()
    {
        // Square wave with flat envelope gives deterministic peak amplitude regardless of phase.
        var squarePad = new PadAssignment
        {
            PadId = "test-pad",
            RowIndex = 0,
            ColumnIndex = 0,
            Role = SynthSharp.Core.Layout.RowRole.MelodicA,
            KeyBinding = "A",
            Label = "A",
            Waveform = WaveformType.Square,
            FrequencyHz = 440d,
            Envelope = new Envelope(AttackSeconds: 0, DecaySeconds: 0, SustainLevel: 1.0, ReleaseSeconds: 0),
        };

        var backend1 = new FakeAudioPlaybackBackend();
        var engine1 = new SynthAudioEngine(backend1, maxPolyphony: 1);
        await engine1.NoteOnAsync("v1", squarePad, velocity: 1.0f);

        var backend2 = new FakeAudioPlaybackBackend();
        var engine2 = new SynthAudioEngine(backend2, maxPolyphony: 1);
        await engine2.NoteOnAsync("v1", squarePad, velocity: 0.5f);

        const int headerLen = 44;
        short PeakAmplitude(byte[] wav)
        {
            short max = 0;
            for (var i = headerLen; i + 1 < wav.Length; i += 2)
            {
                var sample = (short)Math.Abs(BitConverter.ToInt16(wav, i));
                if (sample > max) max = sample;
            }
            return max;
        }

        var peak1 = PeakAmplitude(backend1.Invocations[0].Payload);
        var peak2 = PeakAmplitude(backend2.Invocations[0].Payload);

        // 0.5-velocity peak should be approximately half the 1.0-velocity peak (within ±1 PCM unit).
        Assert.InRange(peak2, (peak1 / 2) - 1, (peak1 / 2) + 1);
    }

    /// <summary>Velocity outside [0, 1] must be clamped: -0.5 → silence, 2.0 → same as 1.0.</summary>
    [Fact]
    public async Task NoteOnAsync_Velocity_OutOfRange_IsClamped()
    {
        // velocity = -0.5 must produce silence (clamped to 0).
        var backendNeg = new FakeAudioPlaybackBackend();
        var engineNeg = new SynthAudioEngine(backendNeg, maxPolyphony: 1);
        await engineNeg.NoteOnAsync("v1", MakePad(releaseSeconds: 0d), velocity: -0.5f);

        const int headerLen = 44;
        var negPayload = backendNeg.Invocations[0].Payload;
        for (var i = headerLen; i < negPayload.Length; i++)
        {
            Assert.Equal(0, negPayload[i]);
        }

        // velocity = 2.0 must produce same bytes as velocity = 1.0 (clamped to 1.0).
        var backend1 = new FakeAudioPlaybackBackend();
        var engine1 = new SynthAudioEngine(backend1, maxPolyphony: 1);
        await engine1.NoteOnAsync("v1", MakePad(releaseSeconds: 0d), velocity: 1.0f);

        var backend2 = new FakeAudioPlaybackBackend();
        var engine2 = new SynthAudioEngine(backend2, maxPolyphony: 1);
        await engine2.NoteOnAsync("v1", MakePad(releaseSeconds: 0d), velocity: 2.0f);

        Assert.Equal(backend1.Invocations[0].Payload, backend2.Invocations[0].Payload);
    }
}
