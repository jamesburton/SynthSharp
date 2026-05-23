# CLAUDE.md

## Project intent

Build an extensible synthesis platform in iterative slices, starting from a playable waveform MVP.

## Architecture rules

1. `SynthSharp.Core` contains domain contracts, pitch math, layout, and persistence abstractions.
2. `SynthSharp.Audio` contains synthesis and playback interfaces/implementations, but no MAUI UI dependencies.
3. `SynthSharp.Input` contains input contracts and routing logic.
4. `SynthSharp.App` contains MAUI-specific composition and platform integrations.
5. `SynthSharp.Tool` contains CLI/tool entrypoints intended for NuGet tool and `dnx` workflows.

## Current technical decisions

- Runtime baseline: .NET 10 LTS.
- MVP platform focus: Windows.
- Keyboard capture: app-focused only (no global hotkeys).
- Default layout: 4 rows including number row; mappings reconfigurable.
- Playback policy: monophonic for MVP (new note cancels previous note).
- Audio backend seam: `IAudioPlaybackBackend` to keep backend swap possible.
- Release version: 0.1.0.

## Agent extension guidance

When extending features:
1. Add/modify domain contracts in `Core` first.
2. Keep platform-specific behavior in `SynthSharp.App\Platforms\...`.
3. Preserve small explicit interfaces and avoid leaking UI concepts into `Core`/`Audio`.
4. Add tests for pitch conversion, waveform rendering, routing, and serialization when behavior changes.
5. Track new feature phases in `PLANS.md` before broad implementation.
6. Release pipeline changes must keep both app-artifact and tool-artifact paths working.
