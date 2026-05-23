using SynthSharp.Core.Layout;

namespace SynthSharp.Audio;

public interface ISynthAudioEngine
{
    Task PlayPadAsync(PadAssignment assignment, TimeSpan duration, CancellationToken cancellationToken = default);
}
