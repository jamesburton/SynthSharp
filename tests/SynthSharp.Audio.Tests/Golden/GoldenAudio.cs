using Xunit.Sdk;

namespace SynthSharp.Audio.Tests.Golden;

/// <summary>
/// Helper for golden-file audio regression tests. Compares a freshly-rendered WAV byte stream
/// against a committed golden file using normalised PCM16 mean-absolute-error.
/// </summary>
public static class GoldenAudio
{
    private const string UpdateEnvVar = "SYNTHSHARP_UPDATE_GOLDEN";

    /// <summary>
    /// Asserts that <paramref name="actualWav"/> matches the golden at <paramref name="goldenRelativePath"/>
    /// within <paramref name="tolerancePerSample"/> mean-absolute-error per sample (samples normalised to [-1, 1]).
    /// </summary>
    /// <param name="actualWav">The freshly-rendered WAV bytes to compare.</param>
    /// <param name="goldenRelativePath">Path relative to the <c>Golden/</c> source directory.</param>
    /// <param name="tolerancePerSample">Maximum allowed MAE per sample before the assertion fails.</param>
    /// <remarks>
    /// When the SYNTHSHARP_UPDATE_GOLDEN environment variable is set to a truthy value,
    /// or when the golden file does not exist on disk, the actual WAV is written as the new golden
    /// and the assertion passes. This is the workflow for adding a new test or accepting an
    /// intentional DSP change.
    /// </remarks>
    public static void AssertMatchesGolden(
        byte[] actualWav,
        string goldenRelativePath,
        double tolerancePerSample = 0.005)
    {
        ArgumentNullException.ThrowIfNull(actualWav);
        ArgumentException.ThrowIfNullOrWhiteSpace(goldenRelativePath);

        var goldenFullPath = ResolveGoldenPath(goldenRelativePath);
        var updateRequested = IsUpdateMode();
        var goldenExists = File.Exists(goldenFullPath);

        if (updateRequested || !goldenExists)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(goldenFullPath)!);
            File.WriteAllBytes(goldenFullPath, actualWav);

            // We can't write to Console reliably from xunit, but Assert.True(true, "…") shows the message
            // in test output when collected.
            Assert.True(true, $"Wrote golden {goldenRelativePath} ({actualWav.Length} bytes).");
            return;
        }

        var expectedWav = File.ReadAllBytes(goldenFullPath);
        var (mae, firstDivergentSample, divergentCount) = CompareWavsNormalised(expectedWav, actualWav);

        if (mae > tolerancePerSample)
        {
            throw new XunitException(
                $"Golden mismatch for '{goldenRelativePath}': MAE={mae:F6} exceeds tolerance {tolerancePerSample:F6}. " +
                $"{divergentCount} samples differ. First divergent sample index: {firstDivergentSample}. " +
                $"To accept this change as the new golden, run with SYNTHSHARP_UPDATE_GOLDEN=true.");
        }
    }

    private static string ResolveGoldenPath(string relativePath)
    {
        // The compiled tests run from bin/Debug/net10.0/, but the goldens live in source under
        // tests/SynthSharp.Audio.Tests/Golden/. Walk up from the test DLL location to find the project dir.
        var testAssemblyDir = Path.GetDirectoryName(typeof(GoldenAudio).Assembly.Location)
            ?? throw new InvalidOperationException("Cannot resolve test assembly location.");

        // Walk up to find the project file then descend into Golden/.
        var dir = new DirectoryInfo(testAssemblyDir);
        while (dir is not null)
        {
            var projectFile = Path.Combine(dir.FullName, "SynthSharp.Audio.Tests.csproj");
            if (File.Exists(projectFile))
            {
                return Path.Combine(dir.FullName, "Golden", relativePath);
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate SynthSharp.Audio.Tests.csproj from '{testAssemblyDir}'.");
    }

    private static bool IsUpdateMode()
    {
        var v = Environment.GetEnvironmentVariable(UpdateEnvVar);
        if (string.IsNullOrEmpty(v)) return false;
        return v.Equals("1", StringComparison.Ordinal)
            || v.Equals("true", StringComparison.OrdinalIgnoreCase)
            || v.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private static (double Mae, int FirstDivergentSample, int DivergentCount) CompareWavsNormalised(byte[] expected, byte[] actual)
    {
        // Skip the 44-byte WAV header; compare int16 little-endian samples.
        const int headerLen = 44;
        var expectedSamples = (expected.Length - headerLen) / 2;
        var actualSamples = (actual.Length - headerLen) / 2;

        if (expectedSamples != actualSamples)
        {
            // Treat length mismatch as a total mismatch — surface a large MAE.
            return (1.0, 0, Math.Max(expectedSamples, actualSamples));
        }

        double totalAbs = 0;
        var firstDivergent = -1;
        var divergentCount = 0;

        for (var i = 0; i < expectedSamples; i++)
        {
            var e = BitConverter.ToInt16(expected, headerLen + (i * 2)) / 32768.0;
            var a = BitConverter.ToInt16(actual, headerLen + (i * 2)) / 32768.0;
            var d = Math.Abs(e - a);
            totalAbs += d;
            if (d > 1e-6)
            {
                if (firstDivergent < 0) firstDivergent = i;
                divergentCount++;
            }
        }

        return (totalAbs / expectedSamples, firstDivergent, divergentCount);
    }
}
