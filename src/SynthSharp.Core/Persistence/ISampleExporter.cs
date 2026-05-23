using SynthSharp.Core.Audio;

namespace SynthSharp.Core.Persistence;

/// <summary>Encodes an in-memory <see cref="Sample"/> to a binary destination stream.</summary>
public interface ISampleExporter
{
    /// <summary>Writes the sample to <paramref name="destination"/> in the exporter's format.</summary>
    /// <param name="sample">The sample to export.</param>
    /// <param name="destination">Writable stream to receive the encoded bytes.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sample"/> or <paramref name="destination"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the sample's metadata is not supported by this exporter.</exception>
    void Export(Sample sample, Stream destination);
}
