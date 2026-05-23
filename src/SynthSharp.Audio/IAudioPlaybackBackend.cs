namespace SynthSharp.Audio;

public interface IAudioPlaybackBackend
{
    Task PlayAsync(Stream pcmWaveStream, CancellationToken cancellationToken = default);
}
