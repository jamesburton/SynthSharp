# SynthSharp

SynthSharp ships as two primary runtime surfaces:
1. `SynthSharp.Tool` (**primary run path**) for zero-install `dnx` and scripting workflows.
2. `SynthSharp.App` (MAUI desktop app) for interactive pad-based playback and editing.

## What's in this release

- Per-pad waveforms and ADSR envelope control.
- Sample import and playback (WAV files assignable per pad).
- Pitch detection with configurable pitch variants per pad.
- Filter (BiQuad) and LFO (amplitude/pitch/filter modulation).
- Pattern record and playback for pad sequences.

## Quick start (primary `dnx` path)

Replace `0.5.0` with the latest release tag from the [Releases page](https://github.com/jamesburton/SynthSharp/releases).

Use these first once NuGet propagation is complete:

```powershell
dnx SynthSharp.Tool tone --wave sawtooth --pitch C4 --duration-ms 500 --out tone.wav
dnx SynthSharp.Tool preset --out default-preset.json
```

If `dnx` cannot resolve the package yet, use the fallback:

```powershell
dotnet tool install --global SynthSharp.Tool --version 0.5.0
synthsharp tone --wave square --pitch A4 --duration-ms 350 --out tone.wav
```

## Running the MAUI app

`dnx` does **not** run the MAUI GUI host directly.

This is now confirmed from .NET tooling model: `dnx`/`dotnet tool exec` runs **.NET tool packages** (CLI tools), while MAUI is a GUI app packaging model.

Use one of these MAUI app paths:

1. Download the Windows app zip from the release page:  
   `SynthSharp.App-0.5.0-windows.zip`  
   **Extract the zip first**, then run `SynthSharp.App.exe` from *inside the extracted folder* — the app is an unpackaged MAUI Windows build and needs all its dependency DLLs co-located in the same directory. Running a copy of `SynthSharp.App.exe` on its own will silently fail to start.
2. Run locally from source:

```powershell
dotnet restore SynthSharp.slnx
dotnet run --project src\SynthSharp.App\SynthSharp.App.csproj -f net10.0-windows10.0.19041.0
```

## CLI commands (`SynthSharp.Tool`)

```powershell
# generate a tone WAV
dnx SynthSharp.Tool tone --wave sine --pitch A4 --duration-ms 350 --out tone.wav

# generate default 4-row preset JSON
dnx SynthSharp.Tool preset --out default-preset.json
```

Accepted `--wave` values: `sine`, `square`, `sawtooth` (`saw`), `triangle`  
Accepted `--pitch` values: note names (`A4`, `C#5`) or Hz (`440`, `523.25`)

## Release artifacts and publishing

Release pipeline (`release.yml`, on `v*` tags or manual dispatch) publishes:
- NuGet packages: `SynthSharp.Core`, `SynthSharp.Audio`, `SynthSharp.Input`, `SynthSharp.Tool`
- Tool binaries: `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`
- MAUI app artifact:
  - `SynthSharp.App-<version>-windows.zip` — full publish payload; extract and run `SynthSharp.App.exe` from inside the extracted folder.
- GitHub Release with all artifacts attached

CI pipeline (`ci.yml`) runs tests and validates Windows MAUI build path.

## Packaging model

- `dnx` zero-install is the preferred usage path for automation/CLI.
- MAUI remains app-style distribution (release artifact package).
- Shared logic lives in `Core` + `Audio` for reuse by both tool and app hosts.

## Linux/macOS equivalents (simple path)

- For cross-platform command-line usage, use `SynthSharp.Tool` assets:
  - `SynthSharp.Tool-<version>-linux-x64.tar.gz`
  - `SynthSharp.Tool-<version>-linux-arm64.tar.gz`
  - `SynthSharp.Tool-<version>-osx-x64.tar.gz`
  - `SynthSharp.Tool-<version>-osx-arm64.tar.gz`
- MAUI desktop app artifacts are currently produced for Windows in this release flow.
