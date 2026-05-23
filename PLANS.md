# PLANS.md

## Completed baseline

- Greenfield solution scaffolded for .NET 10.
- Core synthesis/playback/input modular split implemented.
- MVP 4-row playable pad editor and key-triggered playback implemented.
- Preset JSON save/load added.
- Release automation for v0.1.0 added (`ci.yml`, `release.yml`).
- Companion CLI tool project added for NuGet global tool and `dnx` execution path.

## Next planned phases

1. **Audio robustness**
   - Add note-off handling and sustained playback.
   - Introduce optional polyphony via voice pool.
   - Improve envelope controls per pad.

2. **Sample lane evolution**
   - Add file-based sample assignment and playback for mixed sample row.
   - Add per-sample gain/trim controls.

3. **Track editor foundation**
   - Introduce timeline model and bar/step grid.
   - Record triggered events into pattern clips.

4. **Sound editor**
   - Add richer oscillator/envelope controls.
   - Add filter/LFO primitives.

5. **Pitch detection + tone-range extraction**
   - Analyze input samples for fundamental frequency.
   - Auto-generate playable pitch ranges from input material.

6. **Packaging and distribution hardening**
   - Add signing/notarization flows for additional MAUI platform artifacts.
   - Expand release matrices (additional RIDs/architectures).
   - Add smoke tests for `dnx` + global-tool install paths in CI.
