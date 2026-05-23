using SynthSharp.Core.Layout;

namespace SynthSharp.Core.Tests;

public class DefaultPresetFactoryTests
{
    [Fact]
    public void CreateFourRowDefault_CreatesExpectedRows()
    {
        var preset = DefaultPresetFactory.CreateFourRowDefault();

        Assert.Equal(4, preset.Pads.Select(x => x.RowIndex).Distinct().Count());
        Assert.Contains(preset.Pads, x => x.KeyBinding == "1");
        Assert.Contains(preset.Pads, x => x.KeyBinding == "Q");
        Assert.Contains(preset.Pads, x => x.KeyBinding == "A");
        Assert.Contains(preset.Pads, x => x.KeyBinding == "Z");
    }
}
