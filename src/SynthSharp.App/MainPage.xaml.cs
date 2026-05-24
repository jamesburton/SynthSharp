using SynthSharp.Audio;
using SynthSharp.Core.Audio;
using SynthSharp.Core.Layout;
using SynthSharp.Core.Music;
using SynthSharp.Core.Patterns;
using SynthSharp.Core.Persistence;
using SynthSharp.Input;

namespace SynthSharp.App;

public partial class MainPage : ContentPage
{
    private const string KeyboardVoicePrefix = "key:";
    private const string MidiVoicePrefix = "midi:";
    private const string PadVoicePrefix = "pad:";
    private const string PatternVoicePrefix = "pattern:";

    // Fixed hold-then-release for pattern playback since PatternEvent does not currently
    // carry a duration. Long enough to sound on sustained voices, short enough not to
    // overlap typical step patterns at 120 BPM (125 ms per sixteenth).
    private static readonly TimeSpan PatternNoteHoldDuration = TimeSpan.FromMilliseconds(180);

    private readonly ISynthAudioEngine _audioEngine;
    private readonly IKeyboardInputSource _keyboardInputSource;
    private readonly IMidiInputSource _midiInputSource;
    private readonly PadTriggerRouter _padTriggerRouter;
    private readonly Dictionary<string, PadAssignment> _padsById;
    private readonly IPatternRecorder _patternRecorder;
    private readonly IPatternSetPlayer _patternSetPlayer;
    private readonly PatternSet _patternSet;

    // Parallel list backing MidiDevicePicker; preserves stable MidiDeviceInfo objects
    // so selection by index is safe even when two devices share the same display name.
    private IReadOnlyList<MidiDeviceInfo> _midiDevices = Array.Empty<MidiDeviceInfo>();

    private KeyboardLayoutPreset _currentPreset;
    private PatternTrack _selectedTrack;

    public MainPage(
        ISynthAudioEngine audioEngine,
        IKeyboardInputSource keyboardInputSource,
        IMidiInputSource midiInputSource,
        PadTriggerRouter padTriggerRouter,
        KeyboardLayoutPreset preset,
        IPatternRecorder patternRecorder,
        IPatternSetPlayer patternSetPlayer,
        PatternSet patternSet)
    {
        _audioEngine = audioEngine;
        _keyboardInputSource = keyboardInputSource;
        _midiInputSource = midiInputSource;
        _padTriggerRouter = padTriggerRouter;
        _currentPreset = preset;
        _padsById = _currentPreset.Pads.ToDictionary(x => x.PadId, StringComparer.OrdinalIgnoreCase);
        _patternRecorder = patternRecorder;
        _patternSetPlayer = patternSetPlayer;
        _patternSet = patternSet;

        // Ensure the set always has at least one track so recording has a target.
        if (_patternSet.Tracks.Count == 0)
        {
            _patternSet.AddTrack(new PatternTrack { Name = "Track 1", Clip = new PatternClip { Name = "Track 1" } });
        }
        _selectedTrack = _patternSet.Tracks[0];

        InitializeComponent();

        WaveformPicker.ItemsSource = Enum.GetNames<WaveformType>();
        FilterTypePicker.ItemsSource = Enum.GetNames<FilterType>();
        LfoTargetPicker.ItemsSource = Enum.GetNames<LfoTarget>();
        PadPicker.ItemsSource = _currentPreset.Pads
            .OrderBy(x => x.RowIndex)
            .ThenBy(x => x.ColumnIndex)
            .Select(DisplayNameForPad)
            .ToList();

        _keyboardInputSource.KeyPressed += OnKeyboardInput;
        _keyboardInputSource.KeyReleased += OnKeyboardRelease;

        _midiInputSource.NoteOn += OnMidiNoteOn;
        _midiInputSource.NoteOff += OnMidiNoteOff;

        if (PadPicker.ItemsSource.Count > 0)
        {
            PadPicker.SelectedIndex = 0;
        }

        RebuildPadRows();
        RebuildTrackPicker();
        TrackPicker.SelectedIndex = 0;
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
        RefreshMidiDevices();
        base.OnAppearing();
    }

    protected override void OnDisappearing()
    {
        _audioEngine.NoteOffAll();
        _keyboardInputSource.Stop();
        _midiInputSource.Stop();
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

        FilterTypePicker.SelectedItem = pad.Filter.Type.ToString();
        FilterCutoffEntry.Text = pad.Filter.CutoffHz.ToString("0.##");
        FilterResonanceEntry.Text = pad.Filter.Resonance.ToString("0.###");

        LfoTargetPicker.SelectedItem = pad.Lfo.Target.ToString();
        LfoRateEntry.Text = pad.Lfo.RateHz.ToString("0.##");
        LfoDepthEntry.Text = pad.Lfo.Depth.ToString("0.###");

        LoopEnabledCheckBox.IsChecked = pad.SampleLoopEnabled;
        LoopStartEntry.Text = pad.SampleLoopStartFrame.ToString();
        LoopEndEntry.Text = pad.SampleLoopEndFrame.ToString();

        TrimStartEntry.Text = pad.SampleTrimStartFrame.ToString();
        TrimEndEntry.Text = pad.SampleTrimEndFrame.ToString();
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

        if (!Enum.TryParse<FilterType>(FilterTypePicker.SelectedItem?.ToString(), ignoreCase: true, out var filterType))
        {
            SetStatus("Pick a filter type.");
            return;
        }

        if (!TryParseInRange(FilterCutoffEntry.Text, 20d, 22050d, out var filterCutoff)
            || !TryParseInRange(FilterResonanceEntry.Text, 0.1d, 10d, out var filterResonance))
        {
            SetStatus("Filter cutoff must be 20..22050 Hz and Q must be 0.1..10.");
            return;
        }

        if (!Enum.TryParse<LfoTarget>(LfoTargetPicker.SelectedItem?.ToString(), ignoreCase: true, out var lfoTarget))
        {
            SetStatus("Pick an LFO target.");
            return;
        }

        if (!TryParseInRange(LfoRateEntry.Text, 0.01d, 50d, out var lfoRate)
            || !TryParseInRange(LfoDepthEntry.Text, 0d, 1d, out var lfoDepth))
        {
            SetStatus("LFO rate must be 0.01..50 Hz and depth must be 0..1.");
            return;
        }

        var loopEnabled = LoopEnabledCheckBox.IsChecked;
        if (!int.TryParse(LoopStartEntry.Text, out var loopStart) || loopStart < 0
            || !int.TryParse(LoopEndEntry.Text, out var loopEnd) || loopEnd < 0)
        {
            SetStatus("Loop start and end must be non-negative integers (frame counts).");
            return;
        }

        if (loopEnabled && loopEnd > 0 && loopEnd <= loopStart)
        {
            SetStatus("Loop end must be greater than loop start when looping is enabled.");
            return;
        }

        if (!int.TryParse(TrimStartEntry.Text, out var trimStart) || trimStart < 0
            || !int.TryParse(TrimEndEntry.Text, out var trimEnd) || trimEnd < 0)
        {
            SetStatus("Trim start and end must be non-negative integers (frame counts).");
            return;
        }

        if (trimEnd > 0 && trimEnd <= trimStart)
        {
            SetStatus("Trim end must be greater than trim start when non-zero.");
            return;
        }

        pad.KeyBinding = key;
        pad.Label = string.IsNullOrWhiteSpace(LabelEntry.Text) ? pad.PadId : LabelEntry.Text.Trim();
        pad.FrequencyHz = frequency;
        pad.Waveform = waveform;
        pad.Envelope = new Envelope(attackSeconds, decaySeconds, sustainLevel, releaseSeconds);
        pad.SampleGain = sampleGain;
        pad.Filter = new FilterSettings(filterType, filterCutoff, filterResonance);
        pad.Lfo = new LfoSettings(lfoTarget, lfoRate, lfoDepth);
        pad.SampleLoopEnabled = loopEnabled;
        pad.SampleLoopStartFrame = loopStart;
        pad.SampleLoopEndFrame = loopEnd;
        pad.SampleTrimStartFrame = trimStart;
        pad.SampleTrimEndFrame = trimEnd;

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

        // Capture into the pattern clip while recording — fires for keyboard input
        // and the same hook below covers on-screen pad presses.
        if (_patternRecorder.IsRecording)
        {
            _patternRecorder.Record(padId);
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

    // DryWetMIDI raises EventReceived on a background thread; all audio and UI
    // operations must be marshalled to the main thread.
    private void OnMidiNoteOn(object? sender, MidiNoteEvent e)
    {
        if (!_padTriggerRouter.TryResolvePadByMidiNote(e.MidiNote, out var padId)
            || !_padsById.TryGetValue(padId, out var pad))
        {
            return; // note not bound to any pad — silently drop
        }

        // Capture velocity from the MIDI event before dispatching to the main thread.
        var velocity = e.Velocity;
        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            SelectPad(padId);
            await StartPadVoiceAsync(pad, ToMidiVoiceId(e.MidiNote), velocity);
        });
    }

    private void OnMidiNoteOff(object? sender, MidiNoteEvent e)
    {
        _ = MainThread.InvokeOnMainThreadAsync(() =>
        {
            _audioEngine.NoteOff(ToMidiVoiceId(e.MidiNote));
            return Task.CompletedTask;
        });
    }

    private void RefreshMidiDevices()
    {
        _midiDevices = _midiInputSource.GetAvailableDevices();
        MidiDevicePicker.ItemsSource = _midiDevices.Select(d => d.Name).ToList();
        if (_midiDevices.Count > 0 && MidiDevicePicker.SelectedIndex < 0)
        {
            MidiDevicePicker.SelectedIndex = 0;
        }
    }

    private void OnRefreshMidiDevicesClicked(object? sender, EventArgs e)
    {
        RefreshMidiDevices();
        MidiStatusLabel.Text = _midiDevices.Count == 0 ? "No MIDI devices found." : $"{_midiDevices.Count} device(s) found.";
    }

    private void OnConnectMidiClicked(object? sender, EventArgs e)
    {
        var index = MidiDevicePicker.SelectedIndex;
        if (index < 0 || index >= _midiDevices.Count)
        {
            MidiStatusLabel.Text = "Select a MIDI device first.";
            return;
        }

        var device = _midiDevices[index];
        try
        {
            _midiInputSource.Start(device);
            MidiStatusLabel.Text = $"Connected: {device.Name}";
        }
        catch (Exception ex)
        {
            MidiStatusLabel.Text = $"Connect failed: {ex.Message}";
        }
    }

    private void OnDisconnectMidiClicked(object? sender, EventArgs e)
    {
        _midiInputSource.Stop();
        MidiStatusLabel.Text = "Not connected.";
    }

    private async Task StartPadVoiceAsync(PadAssignment pad, string voiceId, float velocity = 1.0f)
    {
        try
        {
            await _audioEngine.NoteOnAsync(voiceId, pad, velocity);
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
                    if (_patternRecorder.IsRecording)
                    {
                        _patternRecorder.Record(pad.PadId);
                    }
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

    private static string ToMidiVoiceId(int midiNote) => $"{MidiVoicePrefix}{midiNote}";

    private static string ToPadVoiceId(string padId) => $"{PadVoicePrefix}{padId}";

    private void SetStatus(string message)
    {
        StatusLabel.Text = message;
    }

    // ---------------------------------------------------------------------------
    // Pattern record / play handlers
    // ---------------------------------------------------------------------------

    private void OnRecordClicked(object? sender, EventArgs e)
    {
        if (_patternRecorder.IsRecording)
        {
            _patternRecorder.Stop();
            RecordButton.Text = "Record";
            SetStatus($"Stopped recording. {_selectedTrack.Name}: {_selectedTrack.Clip.Events.Count} events.");
            UpdatePatternStatus();
        }
        else
        {
            _selectedTrack.Clip.Clear();
            _patternRecorder.Start(_selectedTrack.Clip);
            RecordButton.Text = "Stop recording";
            UpdatePatternStatus();
            SetStatus($"Recording into {_selectedTrack.Name} — press keys or pad buttons to capture events.");
        }
    }

    private void OnPlayPatternClicked(object? sender, EventArgs e)
    {
        var totalEvents = _patternSet.Tracks.Sum(t => t.Clip.Events.Count);
        if (totalEvents == 0)
        {
            SetStatus("Pattern set is empty — record something first.");
            return;
        }

        if (_patternRecorder.IsRecording)
        {
            // Stop recording before playback so events don't interleave.
            _patternRecorder.Stop();
            RecordButton.Text = "Record";
        }

        SetStatus($"Playing set ({_patternSet.Tracks.Count} tracks, {totalEvents} events).");

        _ = Task.Run(async () =>
        {
            try
            {
                await _patternSetPlayer.PlayAsync(_patternSet, PlayPatternEventAsync);
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    SetStatus("Pattern set playback finished.");
                    return Task.CompletedTask;
                });
            }
            catch (Exception ex)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    SetStatus($"Pattern playback failed: {ex.Message}");
                    return Task.CompletedTask;
                });
            }
        });
    }

    private void OnStopPatternClicked(object? sender, EventArgs e)
    {
        _patternSetPlayer.Stop();
        SetStatus("Pattern playback stopped.");
    }

    private void OnClearPatternClicked(object? sender, EventArgs e)
    {
        if (_patternRecorder.IsRecording)
        {
            _patternRecorder.Stop();
            RecordButton.Text = "Record";
        }

        _selectedTrack.Clip.Clear();
        UpdatePatternStatus();
        SetStatus($"Cleared {_selectedTrack.Name}.");
    }

    private void OnAddTrackClicked(object? sender, EventArgs e)
    {
        var trackNumber = _patternSet.Tracks.Count + 1;
        var name = $"Track {trackNumber}";
        var newTrack = new PatternTrack { Name = name, Clip = new PatternClip { Name = name } };
        _patternSet.AddTrack(newTrack);
        RebuildTrackPicker();
        SelectTrack(newTrack);
        SetStatus($"Added {name}.");
    }

    private void OnRemoveTrackClicked(object? sender, EventArgs e)
    {
        if (_patternSet.Tracks.Count <= 1)
        {
            SetStatus("Can't remove the last track — clear it instead.");
            return;
        }

        if (_patternRecorder.IsRecording)
        {
            _patternRecorder.Stop();
            RecordButton.Text = "Record";
        }

        var removed = _patternSet.RemoveTrack(_selectedTrack);
        if (!removed)
        {
            return;
        }

        var newSelection = _patternSet.Tracks[0];
        RebuildTrackPicker();
        SelectTrack(newSelection);
        SetStatus($"Removed track; now editing {newSelection.Name}.");
    }

    private void OnTrackSelectionChanged(object? sender, EventArgs e)
    {
        if (TrackPicker.SelectedIndex < 0 || TrackPicker.SelectedIndex >= _patternSet.Tracks.Count)
        {
            return;
        }

        _selectedTrack = _patternSet.Tracks[TrackPicker.SelectedIndex];
        UpdatePatternStatus();
    }

    private void RebuildTrackPicker()
    {
        TrackPicker.ItemsSource = _patternSet.Tracks.Select(t => t.Name).ToList();
    }

    private void SelectTrack(PatternTrack track)
    {
        var index = -1;
        for (var i = 0; i < _patternSet.Tracks.Count; i++)
        {
            if (ReferenceEquals(_patternSet.Tracks[i], track))
            {
                index = i;
                break;
            }
        }

        if (index >= 0)
        {
            TrackPicker.SelectedIndex = index;
            _selectedTrack = track;
            UpdatePatternStatus();
        }
    }

    private Task PlayPatternEventAsync(PatternEvent ev)
    {
        if (!_padsById.TryGetValue(ev.PadId, out var pad))
        {
            return Task.CompletedTask;
        }

        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var voiceId = ToPatternVoiceId(ev.PadId);
            SelectPad(ev.PadId);
            try
            {
                // Use the velocity captured at record time so pattern playback preserves dynamics.
                await _audioEngine.NoteOnAsync(voiceId, pad, ev.Velocity);
                await Task.Delay(PatternNoteHoldDuration);
                _audioEngine.NoteOff(voiceId);
            }
            catch
            {
                // Best-effort playback inside the pattern loop — never abort the player on a single event failure.
            }
        });
    }

    private void UpdatePatternStatus()
    {
        var totalEvents = _patternSet.Tracks.Sum(t => t.Clip.Events.Count);
        PatternStatusLabel.Text = $"{_selectedTrack.Name}: {_selectedTrack.Clip.Events.Count} events ({_selectedTrack.Clip.LengthMs} ms). Set total: {totalEvents} events across {_patternSet.Tracks.Count} track(s).";
    }

    private static string ToPatternVoiceId(string padId) => $"{PatternVoicePrefix}{padId}";
}
