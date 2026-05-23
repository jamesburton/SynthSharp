using SynthSharp.Core.Audio;

namespace SynthSharp.Core.Persistence;

/// <summary>Decodes a sample from a binary source stream into the in-memory <see cref="Sample"/> representation.</summary>
public interface ISampleImporter
{
    /// <summary>Reads the source stream, decodes its audio payload, and returns a <see cref="Sample"/>.</summary>
    /// <param name="source">Readable seekable stream positioned at the start of the encoded sample.</param>
    /// <param name="sourcePath">Optional original filesystem path; populates <see cref="SampleMetadata.SourcePath"/>.</param>
    /// <param name="name">Optional display name; when null, derived from <paramref name="sourcePath"/> or "imported-sample".</param>
    /// <returns>The decoded sample.</returns>
    /// <exception cref="InvalidDataException">Thrown when the stream does not contain a recognised, supported encoding.</exception>
    Sample Import(Stream source, string? sourcePath = null, string? name = null);
}
