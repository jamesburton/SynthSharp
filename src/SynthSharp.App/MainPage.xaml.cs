using SynthSharp.Audio;
using SynthSharp.Core.Audio;
using SynthSharp.Core.Layout;
using SynthSharp.Core.Music;
using SynthSharp.Core.Persistence;
using SynthSharp.Input;

namespace SynthSharp.App;

public partial class MainPage : ContentPage
{
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

        if (PadPicker.ItemsSource.Count > 0)
        {
            PadPicker.SelectedIndex = 0;
        }

        RebuildPadRows();
        SetStatus("Ready.");
    }

    protected override void OnAppearing()
    {
        _keyboardInputSource.Start();
        base.OnAppearing();
    }

    protected override void OnDisappearing()
    {
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

        pad.KeyBinding = key;
        pad.Label = string.IsNullOrWhiteSpace(LabelEntry.Text) ? pad.PadId : LabelEntry.Text.Trim();
        pad.FrequencyHz = frequency;
        pad.Waveform = waveform;

        _padTriggerRouter.Rebuild(_currentPreset.Pads);
        RefreshPadPickerItems();
        RebuildPadRows();
        await PlayPadAsync(pad);
        SetStatus($"Updated {pad.PadId}.");
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

    private async void OnPadButtonClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.BindingContext is not string padId || !_padsById.TryGetValue(padId, out var pad))
        {
            return;
        }

        SelectPad(pad.PadId);
        await PlayPadAsync(pad);
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
            await PlayPadAsync(pad);
        });
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
            // Monophonic playback cancels previous note when a new note starts.
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
                row.Children.Add(new Button
                {
                    BindingContext = pad.PadId,
                    FontSize = 12,
                    Padding = new Thickness(8, 6),
                    Text = $"{pad.Label}\n[{pad.KeyBinding}]",
                    WidthRequest = 82,
                    HeightRequest = 56,
                    LineBreakMode = LineBreakMode.WordWrap,
                    Command = new Command(async () => await PlayPadAsync(pad)),
                });
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

    private void SetStatus(string message)
    {
        StatusLabel.Text = message;
    }
}
