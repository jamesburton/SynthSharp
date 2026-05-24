# PLANS.md

## Completed baseline

- Greenfield solution scaffolded for .NET 10.
- Core synthesis/playback/input modular split implemented.
- MVP 4-row playable pad editor and key-triggered playback implemented.
- Preset JSON save/load added.
- Release automation for v0.1.0 added (`ci.yml`, `release.yml`).
- Companion CLI tool project added for NuGet global tool and `dnx` execution path.
- README refined with `dnx`-first quick start and explicit MAUI app run/download guidance.
- **Audio robustness (Phase 1):** note-off + sustained playback, optional polyphony via voice pool (default `maxPolyphony: 8`), per-pad ADSR envelope controls wired through the MAUI editor. Engine covered by unit tests for voice lifecycle, polyphony eviction, natural-expiry slot release, and re-trigger behavior.

### Known limitations (Phase 1)

- Sustained notes are capped at 10 seconds (`MaxSustainDuration`); holding a key longer than that produces silence until release.
- `PlayReleaseTailAsync` does not accept a cancellation token, so `NoteOffAll` cannot abort an in-flight release tail. Release tails are typically <1s so this is acceptable for MVP.
- `NoteOff` latency is bounded by `MauiAudioPlaybackBackend`'s 10 ms poll interval; up to ~10 ms of audio may continue after the key is released before the player is stopped.
- `MauiAudioPlaybackBackend` itself has no direct unit-test coverage; it is exercised end-to-end through `Plugin.Maui.Audio` at runtime. A dedicated test would require introducing an `IAudioPlayer` seam.
- Audible verification of sustain/release/polyphony behavior remains a user follow-up — automated tests cover engine semantics but not perceived audio quality.
- **Sample import/export foundation (Phase 2):** `Sample` + `SampleMetadata` types in `SynthSharp.Core.Audio` storing planar float32 channels in `[-1.0, 1.0]`; `ISampleImporter`/`ISampleExporter` abstractions in `SynthSharp.Core.Persistence`; `WavSampleImporter` and `WavSampleExporter` in `SynthSharp.Audio` for PCM16 mono/stereo WAV. Importer skips unknown RIFF chunks (`LIST`, `JUNK`, `bext`, fmt extensions) and rejects non-PCM, non-16-bit, or non-mono/stereo files with `InvalidDataException`. 30+ unit tests including round-trip fidelity within the 1/32768 quantisation tolerance.

### Known limitations (Phase 2)

- WAV import/export covers PCM16 mono and stereo only; 24-bit PCM and IEEE float32 WAV are explicitly deferred.
- No UI integration yet — there is no MAUI file picker, "Load sample" button, or pad-to-sample binding. Sample wiring through the app comes with the Sample lane evolution phase.
- The importer assumes the source stream is seekable (uses `Seek` to skip chunks). Non-seekable network streams would need a wrapping seekable buffer.
- No streaming-decode API: the entire sample is decoded into memory at import time. Long samples consume `frameCount × channelCount × 4` bytes.
- **Pitch detection + tone-range extraction (Phase 3):** `PitchEstimate`, `PitchDetectionOptions`, `IPitchDetector` in `SynthSharp.Core.Music`; `NWavesPitchDetector` in `SynthSharp.Audio` runs NWaves' YIN per windowed frame over a downmixed-to-mono `float[]` and aggregates valid frames by median with `ConfidenceScore = validFrames / totalFrames`. `SampleToneRange` + `ToneRangeOptions` + `IToneRangeEstimator` + `DefaultToneRangeEstimator` derive a recommended ±N-semitone playable range, clamped to MIDI [0, 127] and gated by a minimum confidence. `Pitch` static helpers extended with `ToMidiNote(double)` and `ToNoteName(int)`. 30+ unit tests including sine/sawtooth detection, silence, polarity-cancelled downmix, MIDI extremes clamping, and a guard against NWaves issue #88.

### Known limitations (Phase 3)

- **NWaves 0.9.6** is the underlying DSP library; last release **2021-10-06**. Research (see commit history) confirmed it is the only published MIT-licensed .NET NuGet that ships YIN pitch detection — NAudio, Math.NET, ManagedBass and other actively-maintained alternatives do not cover pitch detection, and BSD/LGPL/Aubio alternatives were ruled out on license or distribution grounds. The `IPitchDetector` abstraction keeps the dependency swappable should a better-maintained option emerge.
- NWaves issue #88 (`Pitch.FromYin` `IndexOutOfRangeException` for small windows / low pitches) is unfixed upstream; `NWavesPitchDetector` guards against the failing parameter combination by computing the minimum frame size required for the requested `MinHz` and throwing `ArgumentException` if the options are pathological.
- Tone-range output is **metadata only** — no actual pitch-shifting yet. Variant generation (resampling or phase-vocoder-based pitch shift) belongs to the Pitch editing and melodic mapping phase.
- Default YIN search range is 50 Hz – 2 kHz; instruments above C7 (~2093 Hz) need a wider `MaxHz` option to detect cleanly.
- **Pitch editing and melodic mapping (Phase 4):** `IPitchShifter` in `SynthSharp.Core.Music` and `NWavesPitchShifter` in `SynthSharp.Audio` backed by `NWaves.Effects.PitchShiftEffect` (phase-vocoder TSM, 1024/256 window/hop, duration-preserving). `Pitch.SnapToNearestSemitone` rounds any Hz to the nearest MIDI semitone. `SamplePitchVariant` + `IPitchVariantGenerator` + `DefaultPitchVariantGenerator` produce one variant per semitone across a `SampleToneRange`, including the un-shifted source at offset 0. Variants carry their MIDI number, note name (`Pitch.ToNoteName`), and target frequency (`Pitch.ToFrequencyHz`). 20+ unit tests including pitch-shift round-trip via the YIN detector and variant ordering / cancellation / null-guard coverage.

### Known limitations (Phase 4)

- Pitch-shift algorithm is NWaves' phase-vocoder TSM. Quality is acceptable for sustained instrument samples but can introduce transient smearing on very short percussive hits — choose carefully when pitch-shifting drums.
- Variant generation is metadata-only — no preset wiring yet. Pads can't yet be auto-assigned from a generated variant set; that integration belongs to the Sample lane evolution phase.
- `Pitch.SnapToNearestSemitone` is exposed but not yet wired into the MAUI pitch editor UI; an Apply-time snap toggle would naturally live next to the pitch entry.
- Pitch shifting beyond ±12 semitones is supported by the algorithm but quality degrades quickly outside that window; the variant generator does not warn or refuse extreme offsets.
- **Sample lane evolution (Phase 5):** `PadAssignment` extended with `SampleFileName` (null = synth) and `SampleGain` (linear multiplier). `SynthAudioEngine` takes optional `ISampleImporter` + `ISampleExporter` + samples directory; on `NoteOn` for sample pads, loads the WAV, applies pad gain and the ADSR envelope, re-encodes as PCM16 via `SampleRenderer`, and feeds the same `IAudioPlaybackBackend` as the synth path. MAUI pad editor now has a "Load sample…" button (cross-platform `FilePicker.PickAsync` with WAV filter), a "Clear sample" button, a current-filename label, and a "Gain (0–2)" entry. Picked WAVs are copied into `AppDataDirectory/samples/{guid}.wav` so the original source can move without breaking the pad. Preset JSON round-trips the new fields.

### Known limitations (Phase 5)

- No looping support — samples play once per NoteOn through their natural length. Holding a key past the sample's duration produces silence until release.
- No per-sample trim start/end yet — the full file is always played from offset 0.
- No per-channel pan or stereo width controls.
- ADSR envelope is applied as a multiplier across the sample's frames. For percussion this is fine; for sustained instrument samples you may want envelope = (0, 0, 1, 0) to avoid amplitude shaping on top of the source.
- `MainPage.GetSamplesDirectory()` is the single source of truth — moving the AppData location, e.g. via a roaming profile change, would orphan existing sample references in saved presets.
- **Track editor foundation (Phase 6):** `SynthSharp.Core.Patterns` namespace introduces `PatternEvent` (PadId, TimeOffsetMs, Velocity), `PatternClip` (Name, TempoBpm, StepsPerBar, LengthMs, events), `IPatternRecorder` + `DefaultPatternRecorder` (Stopwatch-based, lock-protected state), and `IPatternPlayer` + `DefaultPatternPlayer` (orders events by TimeOffsetMs, fires a caller-supplied `Func<PatternEvent, Task>` per event, holds remaining LengthMs when set, honours cancellation between events). MAUI pad editor now has a Pattern panel — Record / Play / Stop / Clear — and both keyboard input and on-screen pad presses feed the same clip while recording. 15 new Core tests cover ordering, cancellation, timing, length-honouring, and null guards.

### Known limitations (Phase 6)

- Single in-memory clip per session — no save/load of patterns, no clip library, no loop-on-end.
- `PatternEvent` carries no duration: pattern playback uses a fixed 180 ms hold-then-NoteOff per event, which is fine for percussion and most synth voices but cuts off sustained-sample loops early.
- Tempo and StepsPerBar are metadata only — neither the recorder nor the player quantises to the grid. Recorded timestamps reflect the user's actual key cadence to millisecond precision.
- No multi-track / layered clips, no per-event velocity wiring (Velocity is captured but the engine path uses 1.0 throughout).
- Pattern playback fires through the same `MainThread.InvokeOnMainThreadAsync` path as live triggers, so very high-density patterns (many events per 10 ms) may glitch.
- **Sound editor (Phase 7):** Per-pad filter and LFO. `FilterType` enum (None / LowPass / HighPass / BandPass) + `FilterSettings(Type, CutoffHz, Resonance)` in `SynthSharp.Core.Audio` with NWaves BiQuad implementation in `SynthSharp.Audio.AudioFilters`. `LfoTarget` enum (None / Amplitude / Pitch / FilterCutoff) + `LfoSettings(Target, RateHz, Depth)` + pure-math `Lfo.EvaluateSine`. Both renderers (`WavToneRenderer`, `SampleRenderer`) accept optional filter + LFO; engine threads `assignment.Filter` and `assignment.Lfo` through on every NoteOn. Pitch LFO uses an incremental phase accumulator to preserve bit-identical output for non-Pitch and Off paths. MAUI pad editor exposes filter type picker + cutoff/Q entries and LFO target picker + rate/depth entries. Preset JSON round-trips both. 27+ new tests across Core + Audio.

### Known limitations (Phase 7)

- Single LFO per pad; no LFO-to-LFO or envelope-to-LFO modulation.
- FilterCutoff LFO re-creates the BiQuad filter every 256 samples (~5.8 ms at 44.1 kHz) — coefficients change instantly rather than smoothly, which can introduce subtle clicks at extreme depths.
- Pitch LFO on sample pads is intentionally a no-op (would require per-chunk resampling — beyond MVP scope).
- No envelope-to-filter routing (the filter cutoff is static unless an LFO modulates it).
- BiQuad filter Q parameter affects the resonance peak but is not separately exposed as a "drive" or saturation control.
- **Packaging and distribution hardening (Phase 8):** `publish-tool-binaries` matrix expanded to six RIDs (linux-x64, linux-arm64, win-x64, win-arm64, osx-x64, osx-arm64). New `smoke-test-tool` job runs after `publish-nuget` on linux/windows/macos-latest — waits 60s for NuGet propagation, installs the just-published `SynthSharp.Tool` globally, runs it to render a 100 ms tone, asserts the WAV is at least 1 KB. Windows path also tries the `dnx SynthSharp.Tool` invocation with `continue-on-error: true`. Windows MAUI publish now exports a portable `.exe` alongside the zip and both ship in the GitHub release. Authenticode signing for Windows MAUI binaries is scaffolded as a commented-out step — enable by setting `WINDOWS_CERT_PFX_BASE64` and `WINDOWS_CERT_PASSWORD` repository secrets. README rewritten around the `dnx`-first quickstart with a "What's in this release" summary of user-facing features.

### Known limitations (Phase 8)

- Authenticode signing is scaffolded but **not active** — releases ship unsigned until certs are added as repo secrets. Windows SmartScreen will warn on first run.
- macOS notarization is not scaffolded yet — there is no `publish-maui-macos` job. A macOS MAUI build with notarization would need an Apple Developer Program enrolment (annual fee) and additional secret plumbing.
- Smoke tests depend on NuGet propagation timing — the 60s sleep is a heuristic; occasional flake under heavy NuGet load is possible. Re-running the job once recovers it.
- `dnx` invocation is best-effort; availability depends on the SDK build the runner happens to have.
- Tool arm64 binaries are built but not exercised by the smoke-test runners (GitHub-hosted runners are still x64 / Apple Silicon mac — the latter does cover macos-arm64 transitively via the macos-latest runner, but linux-arm64 and win-arm64 binaries are unverified beyond a clean build).

---

## Initial roadmap complete

All eight phases of the initial SynthSharp roadmap are now in `Completed baseline`. The project ships:

- A playable Windows MAUI desktop app (zip + portable launcher .exe) with four configurable pad rows, per-pad waveform/envelope/filter/LFO, sample import and per-pad gain, pattern record + playback, preset save/load.
- A cross-platform CLI tool (`SynthSharp.Tool`) for tone rendering and preset bootstrap via `dnx` or `dotnet tool install`, published for six RIDs.
- Four NuGet packages (`SynthSharp.Core`, `SynthSharp.Audio`, `SynthSharp.Input`, `SynthSharp.Tool`) exposing the engine + abstractions for downstream integration.
- A CI/release pipeline that runs tests, packs NuGets, builds tool binaries for six RIDs, builds the Windows MAUI app + portable launcher, and smoke-tests the published tool end-to-end on three operating systems.

### Beyond the initial roadmap

Ideas worth picking up next (not on the original eight-phase plan):

- **Looping samples and per-sample trim controls** — currently samples play once per NoteOn through their full length.
- **Multi-track patterns** — extend the recorder/player to layer multiple clips with independent tempos and loop points.
- **macOS MAUI build with notarization** — needs Apple Developer cert plumbing.
- **Real-time / low-latency audio output** — current Plugin.Maui.Audio path adds a perceptible warmup; switching to WASAPI / AudioGraph on Windows would tighten latency for performance use.
- **MIDI input** — current input is computer keyboard only; MIDI controllers would unlock a much wider performance surface.
- **Audible quality benchmarks** — automated regression tests that compare rendered WAVs against golden reference files via a perceptual hash, so DSP changes don't silently degrade output.
