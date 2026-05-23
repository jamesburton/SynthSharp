using Plugin.Maui.Audio;
using SynthSharp.Audio;

namespace SynthSharp.App;

public sealed class MauiAudioPlaybackBackend : IAudioPlaybackBackend
{
    private readonly IAudioManager _audioManager;

    public MauiAudioPlaybackBackend(IAudioManager audioManager)
    {
        _audioManager = audioManager;
    }

    public async Task PlayAsync(Stream pcmWaveStream, CancellationToken cancellationToken = default)
    {
        pcmWaveStream.Position = 0;
        using var player = _audioManager.CreatePlayer(pcmWaveStream);
        try
        {
            player.Play();

            // Poll until the audio finishes or cancellation is requested.
            // We catch OperationCanceledException locally so the cancellation path
            // is explicit at this layer rather than relying on an upstream catch.
            try
            {
                while (player.IsPlaying && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(10, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Cancellation requested mid-delay; fall through to stop the player.
            }
        }
        finally
        {
            player.Stop();
        }
    }
}
