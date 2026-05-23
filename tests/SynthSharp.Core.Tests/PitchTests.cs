using SynthSharp.Core.Music;

namespace SynthSharp.Core.Tests;

public class PitchTests
{
    [Fact]
    public void ToFrequencyHz_MapsA4Correctly()
    {
        var hz = Pitch.ToFrequencyHz(69);
        Assert.Equal(440d, hz, 6);
    }

    [Fact]
    public void TryParseNote_ParsesCSharp4()
    {
        var ok = Pitch.TryParseNote("C#4", out var midi);
        Assert.True(ok);
        Assert.Equal(61, midi);
    }

    [Fact]
    public void TryResolveFrequency_AcceptsFrequencyOrNote()
    {
        Assert.True(Pitch.TryResolveFrequency("523.25", out var hz1));
        Assert.InRange(hz1, 523.2, 523.3);

        Assert.True(Pitch.TryResolveFrequency("C5", out var hz2));
        Assert.InRange(hz2, 523.2, 523.3);
    }

    // ---------------------------------------------------------------------------
    // ToMidiNote tests
    // ---------------------------------------------------------------------------

    [Fact]
    public void ToMidiNote_440Hz_Returns69()
    {
        Assert.Equal(69, Pitch.ToMidiNote(440.0));
    }

    [Fact]
    public void ToMidiNote_220Hz_Returns57()
    {
        Assert.Equal(57, Pitch.ToMidiNote(220.0));
    }

    [Fact]
    public void ToMidiNote_RoundsToNearestSemitone()
    {
        // 261.63 Hz is very close to C4 (MIDI 60, exact 261.626 Hz).
        Assert.Equal(60, Pitch.ToMidiNote(261.63));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-100.0)]
    public void ToMidiNote_NonPositive_ReturnsNegativeOne(double hz)
    {
        Assert.Equal(-1, Pitch.ToMidiNote(hz));
    }

    // ---------------------------------------------------------------------------
    // ToNoteName tests
    // ---------------------------------------------------------------------------

    [Fact]
    public void ToNoteName_69_ReturnsA4()
    {
        Assert.Equal("A4", Pitch.ToNoteName(69));
    }

    [Fact]
    public void ToNoteName_60_ReturnsC4()
    {
        Assert.Equal("C4", Pitch.ToNoteName(60));
    }

    [Fact]
    public void ToNoteName_61_ReturnsCSharp4()
    {
        Assert.Equal("C#4", Pitch.ToNoteName(61));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(128)]
    public void ToNoteName_OutOfRange_Throws(int midiNote)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Pitch.ToNoteName(midiNote));
    }

    // ---------------------------------------------------------------------------
    // Round-trip: MIDI → Hz → MIDI → note name
    // ---------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(PianoRangeMidiNotes))]
    public void RoundTrip_NoteName_Frequency_MidiNote(int midi)
    {
        var hz = Pitch.ToFrequencyHz(midi);
        var roundTrippedMidi = Pitch.ToMidiNote(hz);
        var noteName = Pitch.ToNoteName(roundTrippedMidi);

        // Round-tripped MIDI must equal original.
        Assert.Equal(midi, roundTrippedMidi);

        // Note name must match the expected name for this MIDI note.
        var expected = Pitch.ToNoteName(midi);
        Assert.Equal(expected, noteName);
    }

    /// <summary>Provides all MIDI notes in the standard piano range [21, 108] as test data.</summary>
    public static IEnumerable<object[]> PianoRangeMidiNotes()
    {
        for (var midi = 21; midi <= 108; midi++)
        {
            yield return new object[] { midi };
        }
    }
}
