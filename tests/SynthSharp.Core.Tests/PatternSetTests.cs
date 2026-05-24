using SynthSharp.Core.Patterns;

namespace SynthSharp.Core.Tests;

/// <summary>Unit tests for <see cref="PatternSet"/>, <see cref="PatternTrack"/>, and <see cref="DefaultPatternSetPlayer"/>.</summary>
public sealed class PatternSetTests
{
    private static PatternClip ClipWithEvents(params (string padId, long ms)[] events)
    {
        var clip = new PatternClip();
        foreach (var (padId, ms) in events)
        {
            clip.AddEvent(new PatternEvent(padId, ms));
        }
        return clip;
    }

    [Fact]
    public void AddTrack_AppendsToList()
    {
        var set = new PatternSet();
        var track = new PatternTrack { Clip = new PatternClip(), Name = "drums" };

        set.AddTrack(track);

        Assert.Single(set.Tracks);
        Assert.Same(track, set.Tracks[0]);
    }

    [Fact]
    public void RemoveTrack_RemovesByReference()
    {
        var set = new PatternSet();
        var track = new PatternTrack { Clip = new PatternClip() };
        set.AddTrack(track);

        var removed = set.RemoveTrack(track);

        Assert.True(removed);
        Assert.Empty(set.Tracks);
    }

    [Fact]
    public void RemoveTrack_NotPresent_ReturnsFalse()
    {
        var set = new PatternSet();
        Assert.False(set.RemoveTrack(new PatternTrack { Clip = new PatternClip() }));
    }

    [Fact]
    public void Clear_RemovesAllTracks()
    {
        var set = new PatternSet();
        set.AddTrack(new PatternTrack { Clip = new PatternClip() });
        set.AddTrack(new PatternTrack { Clip = new PatternClip() });

        set.Clear();

        Assert.Empty(set.Tracks);
    }

    [Fact]
    public async Task Player_PlaysAllNonMutedTracksInParallel()
    {
        var set = new PatternSet();
        set.AddTrack(new PatternTrack { Name = "a", Clip = ClipWithEvents(("a-pad", 0), ("a-pad", 50)) });
        set.AddTrack(new PatternTrack { Name = "b", Clip = ClipWithEvents(("b-pad", 0), ("b-pad", 50)) });

        var player = new DefaultPatternSetPlayer();
        var fired = new List<string>();

        await player.PlayAsync(set, ev =>
        {
            fired.Add(ev.PadId);
            return Task.CompletedTask;
        });

        // Both tracks emit two events; order across tracks is unspecified but counts must match.
        Assert.Equal(2, fired.Count(p => p == "a-pad"));
        Assert.Equal(2, fired.Count(p => p == "b-pad"));
    }

    [Fact]
    public async Task Player_MutedTrack_IsSilent()
    {
        var set = new PatternSet();
        set.AddTrack(new PatternTrack { Name = "audible", Clip = ClipWithEvents(("a", 0), ("a", 50)) });
        set.AddTrack(new PatternTrack { Name = "muted", Mute = true, Clip = ClipWithEvents(("m", 0), ("m", 50)) });

        var player = new DefaultPatternSetPlayer();
        var fired = new List<string>();

        await player.PlayAsync(set, ev =>
        {
            fired.Add(ev.PadId);
            return Task.CompletedTask;
        });

        Assert.Contains("a", fired);
        Assert.DoesNotContain("m", fired);
    }

    [Fact]
    public async Task Player_SoloTrack_SuppressesNonSoloTracks()
    {
        var set = new PatternSet();
        set.AddTrack(new PatternTrack { Name = "normal", Clip = ClipWithEvents(("n", 0)) });
        set.AddTrack(new PatternTrack { Name = "solo", Solo = true, Clip = ClipWithEvents(("s", 0)) });

        var player = new DefaultPatternSetPlayer();
        var fired = new List<string>();

        await player.PlayAsync(set, ev =>
        {
            fired.Add(ev.PadId);
            return Task.CompletedTask;
        });

        Assert.Contains("s", fired);
        Assert.DoesNotContain("n", fired);
    }

    [Fact]
    public async Task Player_SoloOverridesMute()
    {
        // A muted-and-solo'd track still plays (Solo wins over Mute).
        var set = new PatternSet();
        set.AddTrack(new PatternTrack { Name = "regular", Clip = ClipWithEvents(("r", 0)) });
        set.AddTrack(new PatternTrack { Name = "muted-solo", Mute = true, Solo = true, Clip = ClipWithEvents(("ms", 0)) });

        var player = new DefaultPatternSetPlayer();
        var fired = new List<string>();

        await player.PlayAsync(set, ev =>
        {
            fired.Add(ev.PadId);
            return Task.CompletedTask;
        });

        Assert.Contains("ms", fired);
        Assert.DoesNotContain("r", fired);
    }

    [Fact]
    public async Task Player_EmptySet_ReturnsImmediately()
    {
        var set = new PatternSet();
        var player = new DefaultPatternSetPlayer();
        await player.PlayAsync(set, _ => Task.CompletedTask);
        // No exception, no fired events; just returns.
    }

    [Fact]
    public async Task Player_AllTracksMuted_ReturnsImmediately()
    {
        var set = new PatternSet();
        set.AddTrack(new PatternTrack { Mute = true, Clip = ClipWithEvents(("a", 0)) });
        set.AddTrack(new PatternTrack { Mute = true, Clip = ClipWithEvents(("b", 0)) });

        var player = new DefaultPatternSetPlayer();
        var fired = new List<string>();

        await player.PlayAsync(set, ev => { fired.Add(ev.PadId); return Task.CompletedTask; });

        Assert.Empty(fired);
    }

    [Fact]
    public async Task Player_Cancellation_StopsAllTracks()
    {
        var set = new PatternSet();
        set.AddTrack(new PatternTrack { Clip = ClipWithEvents(("a", 0), ("a", 5000)) });
        set.AddTrack(new PatternTrack { Clip = ClipWithEvents(("b", 0), ("b", 5000)) });

        var player = new DefaultPatternSetPlayer();
        using var cts = new CancellationTokenSource();
        var fired = new List<string>();

        var playTask = player.PlayAsync(set, ev =>
        {
            lock (fired) fired.Add(ev.PadId);
            return Task.CompletedTask;
        }, cts.Token);

        await Task.Delay(100);
        cts.Cancel();
        await playTask;

        // Both tracks should have fired their immediate events but not the 5000-ms ones.
        Assert.DoesNotContain("a", fired.Skip(2)); // no late "a" event after cancellation
        Assert.False(player.IsPlaying);
    }

    [Fact]
    public async Task Player_StopCancelsActivePlay()
    {
        var set = new PatternSet();
        var clip = new PatternClip { LengthMs = 10_000 };
        clip.AddEvent(new PatternEvent("a", 0));
        set.AddTrack(new PatternTrack { Clip = clip });

        var player = new DefaultPatternSetPlayer();
        var playTask = player.PlayAsync(set, _ => Task.CompletedTask);

        await Task.Delay(50);
        player.Stop();
        await playTask;

        Assert.False(player.IsPlaying);
    }

    [Fact]
    public async Task Player_NullSet_Throws()
    {
        var player = new DefaultPatternSetPlayer();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => player.PlayAsync(null!, _ => Task.CompletedTask));
    }

    [Fact]
    public async Task Player_NullCallback_Throws()
    {
        var player = new DefaultPatternSetPlayer();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => player.PlayAsync(new PatternSet(), null!));
    }

    [Fact]
    public void Constructor_NullFactory_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new DefaultPatternSetPlayer(null!));
    }
}
