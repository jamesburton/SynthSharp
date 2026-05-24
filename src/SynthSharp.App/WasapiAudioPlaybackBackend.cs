#if WINDOWS
using NAudio.CoreAudioApi;
using NAudio.Wave;
using SynthSharp.Audio;

namespace SynthSharp.App;

/// <summary>
/// Low-latency Windows <see cref="IAudioPlaybackBackend"/> backed by NAudio's WASAPI shared-mode output.
/// Typical startup latency &lt; 30 ms versus the 100–200 ms warmup of <see cref="MauiAudioPlaybackBackend"/>.
/// </summary>
public sealed class WasapiAudioPlaybackBackend : IAudioPlaybackBackend
{
    // Latency target for WasapiOut in shared mode. 50 ms is safe across consumer audio devices;
    // dropping to 10 ms can cause underruns on busy systems.
    private const int LatencyMilliseconds = 50;

    /// <inheritdoc/>
    public async Task PlayAsync(Stream pcmWaveStream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pcmWaveStream);

        pcmWaveStream.Position = 0;

        // WaveFileReader will dispose the underlying stream when it is itself disposed.
        // The caller supplies a fresh MemoryStream per call, so single-use ownership here is correct.
        using var reader = new WaveFileReader(pcmWaveStream);
        using var output = new WasapiOut(AudioClientShareMode.Shared, LatencyMilliseconds);

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnStopped(object? sender, StoppedEventArgs e) => completion.TrySetResult();
        output.PlaybackStopped += OnStopped;

        try
        {
            output.Init(reader);

            using var cancelReg = cancellationToken.Register(() =>
            {
                try
                {
                    output.Stop();
                }
                catch
                {
                    // Best-effort stop on cancellation.
                }

                completion.TrySetResult();
            });

            output.Play();
            await completion.Task.ConfigureAwait(false);
        }
        finally
        {
            output.PlaybackStopped -= OnStopped;
        }
    }
}
#endif
