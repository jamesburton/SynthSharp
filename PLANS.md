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

## Next planned phases

1. **Pitch detection + tone-range extraction**
   - Analyze input samples for fundamental frequency.
   - Auto-generate playable pitch ranges from input material.

2. **Pitch editing and melodic mapping**
   - Add pitch-edit controls (coarse/fine tuning and note snapping).
   - Allow generating/assigning melodic note variants from imported sounds.
   - Support varied per-pad melodic assignments from one source sample.

3. **Sample lane evolution**
   - Add file-based sample assignment and playback for mixed sample row.
   - Add per-sample gain/trim controls.
   - Introduce MAUI file picker and "Load sample" UI affordance on a pad.

4. **Track editor foundation**
   - Introduce timeline model and bar/step grid.
   - Record triggered events into pattern clips.

5. **Sound editor**
   - Add richer oscillator/envelope controls.
   - Add filter/LFO primitives.

6. **Packaging and distribution hardening**
   - Add signing/notarization flows for additional MAUI platform artifacts.
   - Expand release matrices (additional RIDs/architectures).
   - Add smoke tests for `dnx` + global-tool install paths in CI.
