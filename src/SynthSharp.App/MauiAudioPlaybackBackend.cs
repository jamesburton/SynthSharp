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
        player.Play();

        while (player.IsPlaying && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(10, cancellationToken);
        }
    }
}
