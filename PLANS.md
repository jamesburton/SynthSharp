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

## Next planned phases

1. **Sample import/export foundation**
   - Add sample import pipeline for common formats (WAV first).
   - Add sample export workflow for edited/generated outputs.
   - Define reusable sample metadata schema for later editing/mapping phases.

2. **Pitch detection + tone-range extraction**
   - Analyze input samples for fundamental frequency.
   - Auto-generate playable pitch ranges from input material.

3. **Pitch editing and melodic mapping**
   - Add pitch-edit controls (coarse/fine tuning and note snapping).
   - Allow generating/assigning melodic note variants from imported sounds.
   - Support varied per-pad melodic assignments from one source sample.

4. **Sample lane evolution**
   - Add file-based sample assignment and playback for mixed sample row.
   - Add per-sample gain/trim controls.

5. **Track editor foundation**
   - Introduce timeline model and bar/step grid.
   - Record triggered events into pattern clips.

6. **Sound editor**
   - Add richer oscillator/envelope controls.
   - Add filter/LFO primitives.

7. **Packaging and distribution hardening**
   - Add signing/notarization flows for additional MAUI platform artifacts.
   - Expand release matrices (additional RIDs/architectures).
   - Add smoke tests for `dnx` + global-tool install paths in CI.
