namespace SynthSharp.Core.Layout;

public sealed class KeyboardLayoutPreset
{
    public required string Name { get; init; }

    public required IReadOnlyList<PadAssignment> Pads { get; init; }
}
