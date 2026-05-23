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

    /// <summary>Converts a frequency in Hz to the nearest MIDI note number (0–127).</summary>
    /// <param name="frequencyHz">Frequency in Hz; must be positive.</param>
    /// <returns>The nearest MIDI note number, or -1 when <paramref name="frequencyHz"/> is non-positive or out of the MIDI range.</returns>
    public static int ToMidiNote(double frequencyHz)
    {
        if (frequencyHz <= 0)
        {
            return -1;
        }

        var midi = (int)Math.Round(A4Midi + (12d * Math.Log2(frequencyHz / A4Frequency)));
        return midi is >= 0 and <= 127 ? midi : -1;
    }

    /// <summary>Returns the frequency of the nearest MIDI semitone to <paramref name="frequencyHz"/>.</summary>
    /// <param name="frequencyHz">A positive frequency in Hz.</param>
    /// <returns>
    /// The frequency of the nearest MIDI note in Hz, or 0 if <paramref name="frequencyHz"/> is non-positive
    /// or falls outside the MIDI 0–127 range.
    /// </returns>
    public static double SnapToNearestSemitone(double frequencyHz)
    {
        var midi = ToMidiNote(frequencyHz);
        return midi < 0 ? 0d : ToFrequencyHz(midi);
    }

    /// <summary>Converts a MIDI note number to a textual note name using sharps (e.g., 69 → "A4", 61 → "C#4").</summary>
    /// <param name="midiNote">MIDI note number in [0, 127].</param>
    /// <returns>The note name with octave suffix.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="midiNote"/> is outside [0, 127].</exception>
    public static string ToNoteName(int midiNote)
    {
        if (midiNote is < 0 or > 127)
        {
            throw new ArgumentOutOfRangeException(nameof(midiNote), midiNote, "MIDI note must be in [0, 127].");
        }

        var names = new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        var octave = (midiNote / 12) - 1; /* MIDI 60 = C4 */
        var pitchClass = midiNote % 12;
        return $"{names[pitchClass]}{octave}";
    }
}
