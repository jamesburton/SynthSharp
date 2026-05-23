using Plugin.Maui.Audio;
using SynthSharp.Audio;

namespace SynthSharp.App;

/// <summary>
/// <see cref="IAudioPlaybackBackend"/> backed by <see cref="Plugin.Maui.Audio.IAudioManager"/>.
/// </summary>
public sealed class MauiAudioPlaybackBackend : IAudioPlaybackBackend
{
    private readonly IAudioManager _audioManager;

    /// <summary>Initializes a new backend wrapping the given Plugin.Maui.Audio audio manager.</summary>
    public MauiAudioPlaybackBackend(IAudioManager audioManager)
    {
        _audioManager = audioManager;
    }

    /// <summary>Plays the supplied PCM WAV stream synchronously to completion or cancellation.</summary>
    /// <remarks>
    /// Uses the <see cref="IAudioPlayer.PlaybackEnded"/> event rather than polling
    /// <see cref="IAudioPlayer.IsPlaying"/>. On Windows, Plugin.Maui.Audio defines IsPlaying
    /// as <c>PlaybackState == MediaPlaybackState.Playing</c>, which is false for a window of
    /// time immediately after <see cref="IAudioPlayer.Play"/> returns (state is still Opening
    /// or Buffering). A naive <c>while (player.IsPlaying)</c> poll exits before any audio flows
    /// and the player gets disposed silent — the original cause of the v0.1.0 "silent app" bug.
    /// </remarks>
    public async Task PlayAsync(Stream pcmWaveStream, CancellationToken cancellationToken = default)
    {
        pcmWaveStream.Position = 0;
        using var player = _audioManager.CreatePlayer(pcmWaveStream);

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnPlaybackEnded(object? sender, EventArgs e) => completion.TrySetResult();
        player.PlaybackEnded += OnPlaybackEnded;

        try
        {
            // Register cancellation before Play so a fast cancel still routes through Stop.
            using var cancelReg = cancellationToken.Register(() =>
            {
                try
                {
                    player.Stop();
                }
                catch
                {
                    // Plugin.Maui.Audio Stop is best-effort across platforms; swallow any
                    // platform exception during cancel so we always complete the task.
                }

                completion.TrySetResult();
            });

            player.Play();
            await completion.Task.ConfigureAwait(false);
        }
        finally
        {
            player.PlaybackEnded -= OnPlaybackEnded;
        }
    }
}
