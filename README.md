# SynthSharp 0.1.0

SynthSharp is a .NET 10 synthesis workspace with:
- A MAUI app host (`SynthSharp.App`) for interactive pad-based playback.
- A companion CLI/tool package (`SynthSharp.Tool`) for automation, preset bootstrap, and zero-install `dnx` execution.

## MVP status

This repository currently includes:
- A modular solution split into `Core`, `Audio`, `Input`, and `App`.
- A playable MAUI UI with a 4-row keyboard layout (including number row).
- Reconfigurable per-pad assignment for label, key binding, waveform, and pitch.
- Generated waveform playback (sine, square, sawtooth, triangle).
- JSON preset save/load support.

## Layout model

Default row roles:
1. Melodic A
2. Melodic B
3. Percussion (generated)
4. Mixed sample lane (placeholder behavior in MVP)

All pad-to-key mappings are user-reconfigurable in-app.

## Build and run (local)

```powershell
dotnet restore SynthSharp.slnx
dotnet test tests\SynthSharp.Core.Tests\SynthSharp.Core.Tests.csproj
dotnet test tests\SynthSharp.Audio.Tests\SynthSharp.Audio.Tests.csproj
dotnet build src\SynthSharp.App\SynthSharp.App.csproj -f net10.0-windows10.0.19041.0
dotnet run --project src\SynthSharp.App\SynthSharp.App.csproj -f net10.0-windows10.0.19041.0
```

## Tool/CLI usage

Once `SynthSharp.Tool` is published to NuGet:

### Preferred zero-install path (when available)
```powershell
dnx SynthSharp.Tool tone --wave sawtooth --pitch C4 --duration-ms 500 --out tone.wav
dnx SynthSharp.Tool preset --out default-preset.json
```

### Global install path
```powershell
dotnet tool install --global SynthSharp.Tool --version 0.1.0
synthsharp tone --wave square --pitch A4 --duration-ms 350 --out tone.wav
```

`dnx` availability can lag shortly after package publication while feeds index new versions.

## Release and publishing pipeline

Two GitHub Actions workflows are provided:

1. `ci.yml`  
   - Runs tests.
   - Builds Windows MAUI target.

2. `release.yml` (tag `v*` or manual dispatch)  
   - Packs/publishes NuGet packages (`Core`, `Audio`, `Input`, `Tool`) using `NUGET_API_KEY`.
   - Publishes cross-platform `SynthSharp.Tool` binaries (`win-x64`, `linux-x64`, `osx-x64`).
   - Publishes Windows MAUI app artifact zip.
   - Creates GitHub release with all artifacts attached.

## Packaging model

- MAUI app distribution remains app-style packaging (installer/store/package artifacts).
- `dnx` zero-install execution is provided by the `SynthSharp.Tool` package.
- The codebase is structured so `Core` + `Audio` are shared by both MAUI and tool workflows.
