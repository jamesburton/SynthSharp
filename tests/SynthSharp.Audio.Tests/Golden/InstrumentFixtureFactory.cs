using SynthSharp.Audio;
using SynthSharp.Core.Audio;

namespace SynthSharp.Audio.Tests.Golden;

/// <summary>
/// Generates and caches the instrument-like WAV fixture used by sample-aware perceptual goldens.
/// The fixture is a 200 ms plucked-sawtooth at 220 Hz with envelope + low-pass filter — synthetic
/// but close to a real plucked-string shape (transient, decay, harmonic content).
/// </summary>
public static class InstrumentFixtureFactory
{
    /// <summary>Filename (within Golden/Fixtures/) of the instrument fixture.</summary>
    public const string FixtureFileName = "synthetic_pluck_220hz_200ms.wav";

    private const double FrequencyHz = 220.0;
    private const int DurationMs = 200;

    /// <summary>
    /// Returns the absolute path to the fixture, regenerating it deterministically when missing.
    /// </summary>
    public static string EnsureFixturePath()
    {
        var path = ResolvePath();
        if (!File.Exists(path))
        {
            Generate(path);
        }

        return path;
    }

    /// <summary>Forces regeneration of the fixture from the baked-in parameters.</summary>
    public static void Regenerate()
    {
        Generate(ResolvePath());
    }

    private static void Generate(string path)
    {
        var envelope = new Envelope(
            AttackSeconds: 0.005,
            DecaySeconds: 0.150,
            SustainLevel: 0.0,
            ReleaseSeconds: 0.045);
        var filter = new FilterSettings(FilterType.LowPass, CutoffHz: 1500, Resonance: 0.707);

        using var stream = WavToneRenderer.RenderMonoPcm16(
            WaveformType.Sawtooth,
            FrequencyHz,
            TimeSpan.FromMilliseconds(DurationMs),
            envelope,
            filter: filter);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, stream.ToArray());
    }

    private static string ResolvePath()
    {
        // Mirror GoldenAudio's resolver: walk up from the test DLL location to find the project file.
        var testAssemblyDir = Path.GetDirectoryName(typeof(InstrumentFixtureFactory).Assembly.Location)
            ?? throw new InvalidOperationException("Cannot resolve test assembly location.");

        var dir = new DirectoryInfo(testAssemblyDir);
        while (dir is not null)
        {
            var projectFile = Path.Combine(dir.FullName, "SynthSharp.Audio.Tests.csproj");
            if (File.Exists(projectFile))
            {
                return Path.Combine(dir.FullName, "Golden", "Fixtures", FixtureFileName);
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate SynthSharp.Audio.Tests.csproj from '{testAssemblyDir}'.");
    }
}
