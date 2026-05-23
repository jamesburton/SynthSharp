namespace SynthSharp.Core.Audio;

/// <summary>Immutable metadata describing an imported sample.</summary>
/// <param name="Name">Display name; typically derived from the source filename.</param>
/// <param name="ChannelCount">Number of audio channels (1 = mono, 2 = stereo).</param>
/// <param name="SampleRateHz">Sample rate in samples-per-second per channel (e.g., 44100, 48000).</param>
/// <param name="FrameCount">Total number of frames per channel.</param>
/// <param name="Duration">Total playback duration, derived from FrameCount / SampleRateHz.</param>
/// <param name="SourceBitsPerSample">Bit depth of the original encoded file (e.g., 16 for PCM16).</param>
/// <param name="SourcePath">Filesystem path of the source file, or null when imported from a stream.</param>
/// <param name="ImportedAt">UTC timestamp at which the sample was decoded into memory.</param>
public sealed record SampleMetadata(
    string Name,
    int ChannelCount,
    int SampleRateHz,
    int FrameCount,
    TimeSpan Duration,
    int SourceBitsPerSample,
    string? SourcePath,
    DateTimeOffset ImportedAt);
