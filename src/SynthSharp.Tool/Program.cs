using SynthSharp.Audio;
using SynthSharp.Core.Audio;
using SynthSharp.Core.Layout;
using SynthSharp.Core.Music;
using SynthSharp.Core.Persistence;

if (args.Length == 0 || HasFlag(args, "--help", "-h"))
{
    PrintHelp();
    return 0;
}

var command = args[0].Trim().ToLowerInvariant();
return command switch
{
    "tone" => RunTone(args.Skip(1).ToArray()),
    "preset" => RunPreset(args.Skip(1).ToArray()),
    _ => UnknownCommand(command),
};

static int RunTone(string[] args)
{
    var waveText = GetOption(args, "--wave", "-w") ?? "sine";
    var pitchText = GetOption(args, "--pitch", "-p") ?? "A4";
    var durationText = GetOption(args, "--duration-ms", "-d") ?? "350";
    var outputPath = GetOption(args, "--out", "-o") ?? "tone.wav";

    if (!TryParseWaveform(waveText, out var waveform))
    {
        Console.Error.WriteLine($"Unsupported waveform '{waveText}'. Use sine, square, sawtooth, or triangle.");
        return 2;
    }

    if (!Pitch.TryResolveFrequency(pitchText, out var frequencyHz))
    {
        Console.Error.WriteLine($"Unsupported pitch '{pitchText}'. Use note names (e.g., C4) or frequency in Hz.");
        return 2;
    }

    if (!int.TryParse(durationText, out var durationMs) || durationMs <= 0)
    {
        Console.Error.WriteLine($"Invalid duration '{durationText}'.");
        return 2;
    }

    using var stream = WavToneRenderer.RenderMonoPcm16(
        waveform,
        frequencyHz,
        TimeSpan.FromMilliseconds(durationMs),
        Envelope.Default);

    var fullPath = Path.GetFullPath(outputPath);
    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
    using var file = File.Create(fullPath);
    stream.CopyTo(file);

    Console.WriteLine($"Generated {waveform} tone at {frequencyHz:0.##}Hz -> {fullPath}");
    return 0;
}

static int RunPreset(string[] args)
{
    var outputPath = GetOption(args, "--out", "-o") ?? "default-preset.json";
    var preset = DefaultPresetFactory.CreateFourRowDefault();
    var json = PresetJsonSerializer.Serialize(preset);
    var fullPath = Path.GetFullPath(outputPath);
    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
    File.WriteAllText(fullPath, json);
    Console.WriteLine($"Wrote default preset -> {fullPath}");
    return 0;
}

static bool HasFlag(string[] args, params string[] flags)
{
    return args.Any(arg => flags.Any(flag => string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase)));
}

static string? GetOption(string[] args, params string[] names)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (!names.Any(name => string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)))
        {
            continue;
        }

        if (i + 1 < args.Length)
        {
            return args[i + 1];
        }
    }

    return null;
}

static bool TryParseWaveform(string value, out WaveformType waveform)
{
    waveform = value.Trim().ToLowerInvariant() switch
    {
        "sine" => WaveformType.Sine,
        "square" => WaveformType.Square,
        "saw" or "sawtooth" => WaveformType.Sawtooth,
        "triangle" => WaveformType.Triangle,
        _ => WaveformType.Sine,
    };

    return value.Trim().ToLowerInvariant() is "sine" or "square" or "saw" or "sawtooth" or "triangle";
}

static int UnknownCommand(string command)
{
    Console.Error.WriteLine($"Unknown command '{command}'.");
    PrintHelp();
    return 2;
}

static void PrintHelp()
{
    Console.WriteLine(
        """
        SynthSharp Tool v0.1.0

        Commands:
          synthsharp tone   --wave sine --pitch C4 --duration-ms 350 --out tone.wav
          synthsharp preset --out default-preset.json

        Notes:
          - `--pitch` accepts note names (A4, C#5) or explicit Hz values.
          - This tool package can be installed globally or run zero-install via `dnx` once published.
        """);
}
