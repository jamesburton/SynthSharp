using SynthSharp.Core.Layout;

namespace SynthSharp.Audio;

public sealed class SynthAudioEngine : ISynthAudioEngine
{
    private readonly object _gate = new();
    private readonly IAudioPlaybackBackend _playbackBackend;

    private CancellationTokenSource? _activePlaybackCts;

    public SynthAudioEngine(IAudioPlaybackBackend playbackBackend)
    {
        _playbackBackend = playbackBackend;
    }

    public async Task PlayPadAsync(PadAssignment assignment, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        CancellationTokenSource playbackCts;

        lock (_gate)
        {
            _activePlaybackCts?.Cancel();
            _activePlaybackCts?.Dispose();
            _activePlaybackCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            playbackCts = _activePlaybackCts;
        }

        using var stream = WavToneRenderer.RenderMonoPcm16(
            assignment.Waveform,
            assignment.FrequencyHz,
            duration,
            assignment.Envelope);

        await _playbackBackend.PlayAsync(stream, playbackCts.Token);
    }
}
