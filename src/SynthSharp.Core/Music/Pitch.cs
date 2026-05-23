using System.Globalization;
using System.Text.RegularExpressions;

namespace SynthSharp.Core.Music;

public static partial class Pitch
{
    private const int A4Midi = 69;
    private const double A4Frequency = 440d;

    [GeneratedRegex(@"^([A-Ga-g])([#b]?)(-?\d+)$", RegexOptions.Compiled)]
    private static partial Regex NoteRegex();

    public static double ToFrequencyHz(int midiNote)
    {
        return A4Frequency * Math.Pow(2d, (midiNote - A4Midi) / 12d);
    }

    public static bool TryParseNote(string input, out int midiNote)
    {
        midiNote = 0;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var match = NoteRegex().Match(input.Trim());
        if (!match.Success)
        {
            return false;
        }

        var letter = char.ToUpperInvariant(match.Groups[1].Value[0]);
        var accidental = match.Groups[2].Value;
        var octave = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);

        var baseSemitone = letter switch
        {
            'C' => 0,
            'D' => 2,
            'E' => 4,
            'F' => 5,
            'G' => 7,
            'A' => 9,
            'B' => 11,
            _ => -1,
        };

        if (baseSemitone < 0)
        {
            return false;
        }

        if (accidental == "#")
        {
            baseSemitone += 1;
        }
        else if (accidental == "b")
        {
            baseSemitone -= 1;
        }

        midiNote = ((octave + 1) * 12) + baseSemitone;
        return midiNote is >= 0 and <= 127;
    }

    public static bool TryResolveFrequency(string input, out double frequencyHz)
    {
        frequencyHz = 0;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        if (double.TryParse(input.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedHz)
            && parsedHz is > 0 and <= 22050)
        {
            frequencyHz = parsedHz;
            return true;
        }

        if (!TryParseNote(input, out var midiNote))
        {
            return false;
        }

        frequencyHz = ToFrequencyHz(midiNote);
        return true;
    }
}
