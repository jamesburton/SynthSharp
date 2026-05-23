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

## Next planned phases

1. **Pitch editing and melodic mapping**
   - Add pitch-edit controls (coarse/fine tuning and note snapping).
   - Allow generating/assigning melodic note variants from imported sounds.
   - Support varied per-pad melodic assignments from one source sample.

2. **Sample lane evolution**
   - Add file-based sample assignment and playback for mixed sample row.
   - Add per-sample gain/trim controls.
   - Introduce MAUI file picker and "Load sample" UI affordance on a pad.

3. **Track editor foundation**
   - Introduce timeline model and bar/step grid.
   - Record triggered events into pattern clips.

4. **Sound editor**
   - Add richer oscillator/envelope controls.
   - Add filter/LFO primitives.

5. **Packaging and distribution hardening**
   - Add signing/notarization flows for additional MAUI platform artifacts.
   - Expand release matrices (additional RIDs/architectures).
   - Add smoke tests for `dnx` + global-tool install paths in CI.
