using SynthSharp.Core.Audio;
using SynthSharp.Core.Music;

namespace SynthSharp.Core.Layout;

public static class DefaultPresetFactory
{
    private static readonly string[][] DefaultKeys =
    [
        ["1", "2", "3", "4", "5", "6", "7", "8", "9", "0"],
        ["Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P"],
        ["A", "S", "D", "F", "G", "H", "J", "K", "L", "M"],
        ["Z", "X", "C", "V", "B", "N", "Space", "LeftShift", "RightShift", "Tab"],
    ];

    private static readonly RowRole[] RowRoles =
    [
        RowRole.MelodicA,
        RowRole.MelodicB,
        RowRole.Percussion,
        RowRole.MixedSample,
    ];

    public static KeyboardLayoutPreset CreateFourRowDefault()
    {
        var pads = new List<PadAssignment>();
        var rootMidiByRow = new[] { 48, 60, 36, 42 };

        for (var row = 0; row < DefaultKeys.Length; row++)
        {
            for (var col = 0; col < DefaultKeys[row].Length; col++)
            {
                var midi = rootMidiByRow[row] + col;
                var frequency = Pitch.ToFrequencyHz(midi);
                var waveform = row switch
                {
                    0 => WaveformType.Sawtooth,
                    1 => WaveformType.Square,
                    2 => WaveformType.Triangle,
                    _ => WaveformType.Sine,
                };

                pads.Add(new PadAssignment
                {
                    PadId = $"R{row + 1}C{col + 1}",
                    RowIndex = row,
                    ColumnIndex = col,
                    Role = RowRoles[row],
                    KeyBinding = DefaultKeys[row][col],
                    Label = $"R{row + 1}-{col + 1}",
                    Waveform = waveform,
                    FrequencyHz = frequency,
                });
            }
        }

        return new KeyboardLayoutPreset
        {
            Name = "Default 4-row layout",
            Pads = pads,
        };
    }
}
