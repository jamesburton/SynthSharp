# Perceptual audio regression goldens

This directory holds **golden WAV files** that pin the byte-level output of
SynthSharp's rendering paths (synth waveforms, envelope, filter, LFO,
sample mixing, sample looping).

`PerceptualRegressionTests` renders fresh WAVs from known configurations and
compares them against these goldens using normalised PCM16 mean-absolute-error
(default tolerance 0.005 per sample). A drift above the tolerance means a DSP
change has altered the output — that may be intentional or unintentional.

## Updating goldens after an intentional DSP change

1. Make the DSP change.
2. Run the test suite with the update flag set:

   ```powershell
   $env:SYNTHSHARP_UPDATE_GOLDEN='true'
   dotnet test tests/SynthSharp.Audio.Tests/SynthSharp.Audio.Tests.csproj
   Remove-Item Env:SYNTHSHARP_UPDATE_GOLDEN
   ```

3. Inspect the diff on the `*.wav` files in this folder. The bytes should
   change in a way that matches your DSP change's intent. Spot-check by
   playing the new goldens.
4. Commit the goldens alongside the DSP change.

## Adding a new golden test

1. Add a new `[Fact]` to `PerceptualRegressionTests` that renders the path
   you want to pin.
2. Run the test once. With no existing golden, the harness writes the
   actual output as the new golden and the test passes.
3. Commit the new `.wav` alongside the test.

## What's NOT covered

- **`WaveformType.Noise`** — uses `Random.Shared`; non-deterministic.
- **`NWavesPitchShifter`** — phase-vocoder output can vary by a few PCM
  values across builds (NWaves internal state caches).

Both are tested elsewhere via RMS / detected-pitch assertions that don't
require byte equality.
