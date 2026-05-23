using SynthSharp.Audio;
using SynthSharp.Core.Audio;
using SynthSharp.Core.Layout;
using SynthSharp.Core.Music;
using SynthSharp.Core.Persistence;
using SynthSharp.Input;

namespace SynthSharp.App;

public partial class MainPage : ContentPage
{
    private const string KeyboardVoicePrefix = "key:";
    private const string PadVoicePrefix = "pad:";

    private readonly ISynthAudioEngine _audioEngine;
    private readonly IKeyboardInputSource _keyboardInputSource;
    private readonly PadTriggerRouter _padTriggerRouter;
    private readonly Dictionary<string, PadAssignment> _padsById;

    private KeyboardLayoutPreset _currentPreset;

    public MainPage(
        ISynthAudioEngine audioEngine,
        IKeyboardInputSource keyboardInputSource,
        PadTriggerRouter padTriggerRouter,
        KeyboardLayoutPreset preset)
    {
        _audioEngine = audioEngine;
        _keyboardInputSource = keyboardInputSource;
        _padTriggerRouter = padTriggerRouter;
        _currentPreset = preset;
        _padsById = _currentPreset.Pads.ToDictionary(x => x.PadId, StringComparer.OrdinalIgnoreCase);

        InitializeComponent();

        WaveformPicker.ItemsSource = Enum.GetNames<WaveformType>();
        PadPicker.ItemsSource = _currentPreset.Pads
            .OrderBy(x => x.RowIndex)
            .ThenBy(x => x.ColumnIndex)
            .Select(DisplayNameForPad)
            .ToList();

        _keyboardInputSource.KeyPressed += OnKeyboardInput;
        _keyboardInputSource.KeyReleased += OnKeyboardRelease;

        if (PadPicker.ItemsSource.Count > 0)
        {
            PadPicker.SelectedIndex = 0;
        }

        RebuildPadRows();
        SetStatus("Ready.");

        // Best-effort pre-warm the audio pipeline so the user's first note doesn't
        // pay Plugin.Maui.Audio's cold-start cost on Windows MediaPlayer.
        // Fire-and-forget on the thread pool — by the time the user looks at the UI
        // and presses a key, the warmup is well finished.
        _ = Task.Run(async () =>
        {
            try
            {
                await _audioEngine.WarmupAsync();
            }
            catch
            {
                // Silent failure — warmup is purely an optimisation.
            }
        });
    }

    protected override void OnAppearing()
    {
        _keyboardInputSource.Start();
        base.OnAppearing();
    }

    protected override void OnDisappearing()
    {
        _audioEngine.NoteOffAll();
        _keyboardInputSource.Stop();
        base.OnDisappearing();
    }

    private void OnPadSelectionChanged(object? sender, EventArgs e)
    {
        var pad = GetSelectedPad();
        if (pad is null)
        {
            return;
        }

        LabelEntry.Text = pad.Label;
        KeyBindingEntry.Text = pad.KeyBinding;
        PitchEntry.Text = pad.FrequencyHz.ToString("0.##");
        WaveformPicker.SelectedItem = pad.Waveform.ToString();
        AttackEntry.Text = pad.Envelope.AttackSeconds.ToString("0.###");
        DecayEntry.Text = pad.Envelope.DecaySeconds.ToString("0.###");
        SustainEntry.Text = pad.Envelope.SustainLevel.ToString("0.###");
        ReleaseEntry.Text = pad.Envelope.ReleaseSeconds.ToString("0.###");
        SampleFileLabel.Text = string.IsNullOrWhiteSpace(pad.SampleFileName) ? "(none)" : pad.SampleFileName;
        GainEntry.Text = pad.SampleGain.ToString("0.###");
    }

    private async void OnApplyPadClicked(object? sender, EventArgs e)
    {
        var pad = GetSelectedPad();
        if (pad is null)
        {
            SetStatus("Select a pad first.");
            return;
        }

        var key = (KeyBindingEntry.Text ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(key))
        {
            SetStatus("Key binding is required.");
            return;
        }

        if (!Pitch.TryResolveFrequency(PitchEntry.Text ?? string.Empty, out var frequency))
        {
            SetStatus("Pitch must be a note (e.g. C4) or frequency in Hz.");
            return;
        }

        if (!Enum.TryParse<WaveformType>(WaveformPicker.SelectedItem?.ToString(), ignoreCase: true, out var waveform))
        {
            SetStatus("Pick a waveform.");
            return;
        }

        if (!TryParseNonNegative(AttackEntry.Text, out var attackSeconds)
            || !TryParseNonNegative(DecayEntry.Text, out var decaySeconds)
            || !TryParseInRange(SustainEntry.Text, 0d, 1d, out var sustainLevel)
            || !TryParseNonNegative(ReleaseEntry.Text, out var releaseSeconds))
        {
            SetStatus("Envelope values are invalid. Attack/Decay/Release >= 0 and Sustain in 0..1.");
            return;
        }

        if (!TryParseInRange(GainEntry.Text, 0d, 2d, out var sampleGain))
        {
            SetStatus("Gain must be a number in 0..2.");
            return;
        }

        pad.KeyBinding = key;
        pad.Label = string.IsNullOrWhiteSpace(LabelEntry.Text) ? pad.PadId : LabelEntry.Text.Trim();
        pad.FrequencyHz = frequency;
        pad.Waveform = waveform;
        pad.Envelope = new Envelope(attackSeconds, decaySeconds, sustainLevel, releaseSeconds);
        pad.SampleGain = sampleGain;

        _padTriggerRouter.Rebuild(_currentPreset.Pads);
        RefreshPadPickerItems();
        RebuildPadRows();
        await PlayPadAsync(pad);
        SetStatus($"Updated {pad.PadId}.");
    }

    private async void OnLoadSampleClicked(object? sender, EventArgs e)
    {
        var pad = GetSelectedPad();
        if (pad is null)
        {
            SetStatus("Select a pad first.");
            return;
        }

        FileResult? picked;
        try
        {
            picked = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select a WAV file",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    [DevicePlatform.WinUI] = new[] { ".wav" },
                    [DevicePlatform.macOS] = new[] { "wav" },
                    [DevicePlatform.iOS] = new[] { "public.audio" },
                    [DevicePlatform.Android] = new[] { "audio/wav", "audio/x-wav" },
                }),
            });
        }
        catch (Exception ex)
        {
            SetStatus($"File picker failed: {ex.Message}");
            return;
        }

        if (picked is null)
        {
            return; // user cancelled
        }

        try
        {
            var samplesDir = GetSamplesDirectory();
            Directory.CreateDirectory(samplesDir);

            // Generate a unique filename so multiple imports of the same source filename don't collide.
            var safeName = Guid.NewGuid().ToString("N") + ".wav";
            var destPath = Path.Combine(samplesDir, safeName);

            using (var src = await picked.OpenReadAsync())
            using (var dst = File.Create(destPath))
            {
                await src.CopyToAsync(dst);
            }

            pad.SampleFileName = safeName;
            SampleFileLabel.Text = safeName;
            SetStatus($"Loaded sample '{picked.FileName}' onto pad {pad.PadId} (apply to commit).");
        }
        catch (Exception ex)
        {
            SetStatus($"Failed to import sample: {ex.Message}");
        }
    }

    private void OnClearSampleClicked(object? sender, EventArgs e)
    {
        var pad = GetSelectedPad();
        if (pad is null)
        {
            SetStatus("Select a pad first.");
            return;
        }

        pad.SampleFileName = null;
        SampleFileLabel.Text = "(none)";
        SetStatus($"Cleared sample on pad {pad.PadId} (apply to commit).");
    }

    /// <summary>Returns the directory where imported sample WAV files are stored.</summary>
    public static string GetSamplesDirectory()
    {
        return Path.Combine(FileSystem.Current.AppDataDirectory, "samples");
    }

    private async void OnPlaySelectedClicked(object? sender, EventArgs e)
    {
        var pad = GetSelectedPad();
        if (pad is null)
        {
            SetStatus("Select a pad first.");
            return;
        }

        await PlayPadAsync(pad);
    }

    private async void OnSavePresetClicked(object? sender, EventArgs e)
    {
        var path = GetPresetPath();
        var json = PresetJsonSerializer.Serialize(_currentPreset);
        await File.WriteAllTextAsync(path, json);
        SetStatus($"Preset saved to {path}");
    }

    private async void OnLoadPresetClicked(object? sender, EventArgs e)
    {
        var path = GetPresetPath();
        if (!File.Exists(path))
        {
            SetStatus($"No saved preset found at {path}");
            return;
        }

        var json = await File.ReadAllTextAsync(path);
        _currentPreset = PresetJsonSerializer.Deserialize(json);

        _padsById.Clear();
        foreach (var pad in _currentPreset.Pads)
        {
            _padsById.Add(pad.PadId, pad);
        }

        _padTriggerRouter.Rebuild(_currentPreset.Pads);
        RefreshPadPickerItems();
        RebuildPadRows();
        SetStatus("Preset loaded.");
    }

    private void OnKeyboardInput(object? sender, string keyToken)
    {
        if (!_padTriggerRouter.TryResolvePad(keyToken, out var padId) || !_padsById.TryGetValue(padId, out var pad))
        {
            return;
        }

        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            SelectPad(padId);
            await StartPadVoiceAsync(pad, ToKeyboardVoiceId(keyToken));
        });
    }

    private void OnKeyboardRelease(object? sender, string keyToken)
    {
        _ = MainThread.InvokeOnMainThreadAsync(() =>
        {
            _audioEngine.NoteOff(ToKeyboardVoiceId(keyToken));
            return Task.CompletedTask;
        });
    }

    private async Task StartPadVoiceAsync(PadAssignment pad, string voiceId)
    {
        try
        {
            await _audioEngine.NoteOnAsync(voiceId, pad);
            SetStatus($"Playing {pad.Label} ({pad.FrequencyHz:0.##} Hz, {pad.Waveform}).");
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task PlayPadAsync(PadAssignment pad)
    {
        try
        {
            await _audioEngine.PlayPadAsync(pad, TimeSpan.FromMilliseconds(350));
            SetStatus($"Played {pad.Label} ({pad.FrequencyHz:0.##} Hz, {pad.Waveform}).");
        }
        catch (OperationCanceledException)
        {
        }
    }

    private PadAssignment? GetSelectedPad()
    {
        if (PadPicker.SelectedIndex < 0 || PadPicker.SelectedIndex >= _currentPreset.Pads.Count)
        {
            return null;
        }

        return _currentPreset.Pads
            .OrderBy(x => x.RowIndex)
            .ThenBy(x => x.ColumnIndex)
            .ElementAt(PadPicker.SelectedIndex);
    }

    private void RefreshPadPickerItems()
    {
        var selectedPadId = GetSelectedPad()?.PadId;
        PadPicker.ItemsSource = _currentPreset.Pads
            .OrderBy(x => x.RowIndex)
            .ThenBy(x => x.ColumnIndex)
            .Select(DisplayNameForPad)
            .ToList();

        if (!string.IsNullOrWhiteSpace(selectedPadId))
        {
            SelectPad(selectedPadId);
        }
    }

    private void SelectPad(string padId)
    {
        var orderedPads = _currentPreset.Pads.OrderBy(x => x.RowIndex).ThenBy(x => x.ColumnIndex).ToList();
        var index = orderedPads.FindIndex(x => x.PadId.Equals(padId, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            PadPicker.SelectedIndex = index;
        }
    }

    private void RebuildPadRows()
    {
        PadRowsLayout.Children.Clear();

        foreach (var group in _currentPreset.Pads.GroupBy(x => x.RowIndex).OrderBy(x => x.Key))
        {
            var rowLabel = new Label
            {
                Text = $"Row {group.Key + 1}: {group.First().Role}",
                FontAttributes = FontAttributes.Bold,
            };

            var row = new HorizontalStackLayout { Spacing = 6 };
            foreach (var pad in group.OrderBy(x => x.ColumnIndex))
            {
                var button = new Button
                {
                    BindingContext = pad.PadId,
                    FontSize = 12,
                    Padding = new Thickness(8, 6),
                    Text = $"{pad.Label}\n[{pad.KeyBinding}]",
                    WidthRequest = 82,
                    HeightRequest = 56,
                    LineBreakMode = LineBreakMode.WordWrap,
                };

                button.Pressed += async (_, _) =>
                {
                    SelectPad(pad.PadId);
                    await StartPadVoiceAsync(pad, ToPadVoiceId(pad.PadId));
                };
                button.Released += (_, _) => _audioEngine.NoteOff(ToPadVoiceId(pad.PadId));

                row.Children.Add(button);
            }

            PadRowsLayout.Children.Add(rowLabel);
            PadRowsLayout.Children.Add(new ScrollView
            {
                Orientation = ScrollOrientation.Horizontal,
                Content = row,
            });
        }
    }

    private static string DisplayNameForPad(PadAssignment pad)
    {
        return $"{pad.PadId} [{pad.KeyBinding}] {pad.Label}";
    }

    private static string GetPresetPath()
    {
        return Path.Combine(FileSystem.Current.AppDataDirectory, "synthsharp-preset.json");
    }

    private static bool TryParseNonNegative(string? text, out double value)
    {
        return double.TryParse(text, out value) && value >= 0d;
    }

    private static bool TryParseInRange(string? text, double min, double max, out double value)
    {
        return double.TryParse(text, out value) && value >= min && value <= max;
    }

    private static string ToKeyboardVoiceId(string keyToken) => $"{KeyboardVoicePrefix}{keyToken}";

    private static string ToPadVoiceId(string padId) => $"{PadVoicePrefix}{padId}";

    private void SetStatus(string message)
    {
        StatusLabel.Text = message;
    }
}
