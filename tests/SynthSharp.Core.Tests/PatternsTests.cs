using System.Diagnostics;
using SynthSharp.Core.Patterns;

namespace SynthSharp.Core.Tests;

public sealed class PatternsTests
{
    // ----- PatternClip -----

    [Fact]
    public void AddEvent_NegativeTimeOffset_Throws()
    {
        var clip = new PatternClip();
        Assert.Throws<ArgumentException>(() => clip.AddEvent(new PatternEvent("pad", -1)));
    }

    [Fact]
    public void AddEvent_Null_Throws()
    {
        var clip = new PatternClip();
        Assert.Throws<ArgumentNullException>(() => clip.AddEvent(null!));
    }

    [Fact]
    public void Clear_RemovesEventsAndResetsLength()
    {
        var clip = new PatternClip { LengthMs = 5000 };
        clip.AddEvent(new PatternEvent("a", 0));
        clip.AddEvent(new PatternEvent("b", 100));

        clip.Clear();

        Assert.Empty(clip.Events);
        Assert.Equal(0L, clip.LengthMs);
    }

    // ----- DefaultPatternRecorder -----

    [Fact]
    public void Recorder_RecordsMonotonicTimestamps()
    {
        var clip = new PatternClip();
        var recorder = new DefaultPatternRecorder();
        recorder.Start(clip);

        recorder.Record("a");
        Thread.Sleep(40);
        recorder.Record("b");
        Thread.Sleep(40);
        recorder.Record("c");

        recorder.Stop();

        Assert.Equal(3, clip.Events.Count);
        Assert.True(clip.Events[0].TimeOffsetMs <= clip.Events[1].TimeOffsetMs);
        Assert.True(clip.Events[1].TimeOffsetMs <= clip.Events[2].TimeOffsetMs);
    }

    [Fact]
    public void Recorder_Stop_SetsLengthFromElapsed()
    {
        var clip = new PatternClip();
        var recorder = new DefaultPatternRecorder();
        recorder.Start(clip);

        Thread.Sleep(80);
        recorder.Stop();

        Assert.True(clip.LengthMs >= 60); // allow scheduling jitter on slow CI
    }

    [Fact]
    public void Recorder_RecordWhenNotRecording_IsNoOp()
    {
        var clip = new PatternClip();
        var recorder = new DefaultPatternRecorder();

        recorder.Record("pad-without-start");

        Assert.Empty(clip.Events);
    }

    [Fact]
    public void Recorder_Restart_TargetsNewClip()
    {
        var first = new PatternClip();
        var second = new PatternClip();
        var recorder = new DefaultPatternRecorder();

        recorder.Start(first);
        recorder.Record("a");
        recorder.Start(second);
        recorder.Record("b");
        recorder.Stop();

        Assert.Single(first.Events);
        Assert.Single(second.Events);
        Assert.Equal("a", first.Events[0].PadId);
        Assert.Equal("b", second.Events[0].PadId);
    }

    [Fact]
    public void Recorder_StartNull_Throws()
    {
        var recorder = new DefaultPatternRecorder();
        Assert.Throws<ArgumentNullException>(() => recorder.Start(null!));
    }

    // ----- DefaultPatternPlayer -----

    [Fact]
    public async Task Player_FiresEventsInChronologicalOrder_EvenWhenInputIsOutOfOrder()
    {
        var clip = new PatternClip();
        clip.AddEvent(new PatternEvent("late", 80));
        clip.AddEvent(new PatternEvent("early", 0));
        clip.AddEvent(new PatternEvent("mid", 40));

        var player = new DefaultPatternPlayer();
        var fired = new List<string>();
        await player.PlayAsync(clip, ev => { fired.Add(ev.PadId); return Task.CompletedTask; });

        Assert.Equal(new[] { "early", "mid", "late" }, fired);
    }

    [Fact]
    public async Task Player_RespectsTimingOfEvents()
    {
        var clip = new PatternClip();
        clip.AddEvent(new PatternEvent("a", 0));
        clip.AddEvent(new PatternEvent("b", 60));
        clip.AddEvent(new PatternEvent("c", 120));

        var player = new DefaultPatternPlayer();
        var timestamps = new List<long>();
        var sw = Stopwatch.StartNew();

        await player.PlayAsync(clip, ev => { timestamps.Add(sw.ElapsedMilliseconds); return Task.CompletedTask; });

        Assert.Equal(3, timestamps.Count);
        // Each timestamp should be within ±40ms of its target.
        Assert.InRange(timestamps[0], 0, 40);
        Assert.InRange(timestamps[1], 30, 110);
        Assert.InRange(timestamps[2], 80, 170);
    }

    [Fact]
    public async Task Player_HonoursCancellationBetweenEvents()
    {
        var clip = new PatternClip();
        clip.AddEvent(new PatternEvent("a", 0));
        clip.AddEvent(new PatternEvent("b", 500)); // wait long enough to cancel
        clip.AddEvent(new PatternEvent("c", 1000));

        var player = new DefaultPatternPlayer();
        using var cts = new CancellationTokenSource();
        var fired = new List<string>();

        var playTask = player.PlayAsync(clip, ev => { fired.Add(ev.PadId); return Task.CompletedTask; }, cts.Token);

        await Task.Delay(100);
        cts.Cancel();
        await playTask;

        Assert.Contains("a", fired);
        Assert.DoesNotContain("c", fired);
    }

    [Fact]
    public async Task Player_HoldsForRemainderOfLengthMs()
    {
        var clip = new PatternClip { LengthMs = 200 };
        clip.AddEvent(new PatternEvent("a", 0));

        var player = new DefaultPatternPlayer();
        var sw = Stopwatch.StartNew();

        await player.PlayAsync(clip, _ => Task.CompletedTask);

        Assert.InRange(sw.ElapsedMilliseconds, 180, 320); // ~200ms + jitter
    }

    [Fact]
    public async Task Player_LengthZero_ReturnsAtLastEvent()
    {
        var clip = new PatternClip { LengthMs = 0 };
        clip.AddEvent(new PatternEvent("a", 0));
        clip.AddEvent(new PatternEvent("b", 60));

        var player = new DefaultPatternPlayer();
        var sw = Stopwatch.StartNew();

        await player.PlayAsync(clip, _ => Task.CompletedTask);

        // Should return shortly after the last event, not hang.
        Assert.InRange(sw.ElapsedMilliseconds, 50, 200);
    }

    [Fact]
    public async Task Player_StopCancelsActivePlay()
    {
        var clip = new PatternClip { LengthMs = 10_000 };
        clip.AddEvent(new PatternEvent("a", 0));

        var player = new DefaultPatternPlayer();
        var playTask = player.PlayAsync(clip, _ => Task.CompletedTask);

        await Task.Delay(50);
        player.Stop();
        await playTask;

        Assert.False(player.IsPlaying);
    }

    [Fact]
    public async Task Player_NullClip_Throws()
    {
        var player = new DefaultPatternPlayer();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => player.PlayAsync(null!, _ => Task.CompletedTask));
    }

    [Fact]
    public async Task Player_NullCallback_Throws()
    {
        var player = new DefaultPatternPlayer();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => player.PlayAsync(new PatternClip(), null!));
    }
}
