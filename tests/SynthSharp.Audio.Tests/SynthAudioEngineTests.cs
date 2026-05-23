using SynthSharp.Core.Audio;
using SynthSharp.Core.Layout;

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
            // Capture stream length immediately — stream is disposed by the caller after this returns.
            var invocation = new Invocation(pcmWaveStream.Length, cancellationToken);
            Invocations.Add(invocation);

            // Honour cancellation: cancel the TCS so awaiting code unblocks.
            using var registration = cancellationToken.Register(
                () => invocation.CompletionSource.TrySetResult());

            await invocation.CompletionSource.Task;
        }

        public sealed class Invocation
        {
            public Invocation(long streamLength, CancellationToken token)
            {
                StreamLength = streamLength;
                Token = token;
            }

            public long StreamLength { get; }
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

        // Wait for the cleanup continuation to run and free the slot.
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!v1Invocation.Token.IsCancellationRequested && DateTime.UtcNow < deadline)
        {
            // Slot is freed when PlaySustainAsync finally block runs.
            // We probe by waiting a moment; the reference-equality guard in the engine
            // is what prevents double-dispose.
            await Task.Delay(10);
        }

        // Give the engine a brief moment to execute the finally block.
        await Task.Delay(30);

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
}
