using SynthSharp.Core.Audio;

namespace SynthSharp.Core.Layout;

public sealed class PadAssignment
{
    public required string PadId { get; init; }

    public required int RowIndex { get; init; }

    public required int ColumnIndex { get; init; }

    public required RowRole Role { get; init; }

    public required string KeyBinding { get; set; }

    public required string Label { get; set; }

    public required WaveformType Waveform { get; set; }

    public required double FrequencyHz { get; set; }

    public Envelope Envelope { get; set; } = Envelope.Default;
}
