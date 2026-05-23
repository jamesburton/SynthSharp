using System.Text.Json;
using System.Text.Json.Serialization;
using SynthSharp.Core.Layout;

namespace SynthSharp.Core.Persistence;

public static class PresetJsonSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(KeyboardLayoutPreset preset)
    {
        return JsonSerializer.Serialize(preset, SerializerOptions);
    }

    public static KeyboardLayoutPreset Deserialize(string json)
    {
        var preset = JsonSerializer.Deserialize<KeyboardLayoutPreset>(json, SerializerOptions);
        return preset ?? throw new InvalidOperationException("Preset JSON payload did not deserialize.");
    }
}
