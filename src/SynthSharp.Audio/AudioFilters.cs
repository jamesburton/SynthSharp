using NWaves.Filters.BiQuad;
using SynthSharp.Core.Audio;

namespace SynthSharp.Audio;

/// <summary>Internal helper that converts <see cref="FilterSettings"/> to a configured NWaves BiQuad filter.</summary>
internal static class AudioFilters
{
    /// <summary>
    /// Returns a freshly-constructed NWaves BiQuad filter for the given settings, or <see langword="null"/>
    /// when filtering is disabled.
    /// </summary>
    /// <param name="settings">The filter configuration to apply.</param>
    /// <param name="sampleRate">The sample rate in Hz; used to normalise the cutoff frequency.</param>
    /// <returns>
    /// A configured <see cref="BiQuadFilter"/> instance, or <see langword="null"/> when
    /// <paramref name="settings"/> has <see cref="FilterType.None"/> or <paramref name="sampleRate"/> is not positive.
    /// </returns>
    public static BiQuadFilter? Create(FilterSettings settings, int sampleRate)
    {
        if (settings.Type == FilterType.None || sampleRate <= 0)
        {
            return null;
        }

        // NWaves BiQuad constructors expect a normalised frequency in the range (0, 0.5]:
        // normalisedFreq = cutoffHz / sampleRate.
        var normalisedFreq = settings.CutoffHz / sampleRate;

        return settings.Type switch
        {
            FilterType.LowPass => new LowPassFilter(normalisedFreq, settings.Resonance),
            FilterType.HighPass => new HighPassFilter(normalisedFreq, settings.Resonance),
            FilterType.BandPass => new BandPassFilter(normalisedFreq, settings.Resonance),
            _ => null,
        };
    }
}
