using Plugin.Maui.Audio;
using SynthSharp.Audio;

namespace SynthSharp.App;

/// <summary>
/// <see cref="IAudioPlaybackBackend"/> backed by <see cref="Plugin.Maui.Audio.IAudioManager"/>.
/// </summary>
public sealed class MauiAudioPlaybackBackend : IAudioPlaybackBackend
{
    private static readonly TimeSpan StartupGracePeriod = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(10);

    private readonly IAudioManager _audioManager;

    /// <summary>Initializes a new backend wrapping the given Plugin.Maui.Audio audio manager.</summary>
    public MauiAudioPlaybackBackend(IAudioManager audioManager)
    {
        _audioManager = audioManager;
    }

    /// <summary>Plays the supplied PCM WAV stream synchronously to completion or cancellation.</summary>
    /// <remarks>
    /// Uses an <see cref="IAudioPlayer.IsPlaying"/> poll with a startup grace period rather
    /// than the <see cref="IAudioPlayer.PlaybackEnded"/> event. Two reasons:
    /// <list type="number">
    /// <item><description>
    /// On Windows, Plugin.Maui.Audio defines IsPlaying as <c>PlaybackState == MediaPlaybackState.Playing</c>.
    /// Immediately after <see cref="IAudioPlayer.Play"/> returns the state is still
    /// Opening or Buffering, so a naive poll exits before any audio flows. The startup
    /// grace period waits up to <see cref="StartupGracePeriod"/> for the state to
    /// transition to Playing before treating IsPlaying=false as "playback finished".
    /// </description></item>
    /// <item><description>
    /// <see cref="IAudioPlayer.PlaybackEnded"/> has been observed not to fire reliably for very
    /// short streams (release tails of a few tens of milliseconds) or when the underlying
    /// MediaPlayer fails to load the source. Polling guarantees this method always returns
    /// rather than hanging forever and leaking the player + stream + fire-and-forget Task.
    /// </description></item>
    /// </list>
    /// </remarks>
    public async Task PlayAsync(Stream pcmWaveStream, CancellationToken cancellationToken = default)
    {
        pcmWaveStream.Position = 0;
        using var player = _audioManager.CreatePlayer(pcmWaveStream);

        try
        {
            player.Play();

            // Phase 1: wait for state to transition to Playing (or grace period to expire).
            var startupDeadline = DateTime.UtcNow + StartupGracePeriod;
            while (!player.IsPlaying && DateTime.UtcNow < startupDeadline)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            // Phase 2: poll for completion or cancellation. If IsPlaying never became true
            // (e.g. MediaPlayer failed to load the source), this loop exits immediately,
            // the player is stopped in the finally block, and the method returns cleanly —
            // no hang.
            try
            {
                while (player.IsPlaying)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Cancellation mid-delay; fall through to the finally Stop.
            }
        }
        finally
        {
            try
            {
                player.Stop();
            }
            catch
            {
                // Plugin.Maui.Audio Stop is best-effort across platforms; swallow any
                // platform exception during teardown so we always release the player.
            }
        }
    }
}
