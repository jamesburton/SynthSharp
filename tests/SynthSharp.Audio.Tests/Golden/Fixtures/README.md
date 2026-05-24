# Test fixtures for sample-aware goldens

This folder contains a small synthetic-but-instrument-like WAV used as input
for SampleRenderer perceptual goldens.

The fixture is generated deterministically by `InstrumentFixtureFactory` from
parameters baked into the helper. If the fixture file is missing, the helper
regenerates it on demand — so deleting `synthetic_pluck_220hz_200ms.wav` and
re-running the tests reproduces the same bytes.

This means we don't need to commit a "real" instrument sample (no licensing
concern) but the goldens still exercise SampleRenderer against a sample shape
with real transients, an envelope decay, and a filter roll-off — closer to
production usage than the ramp arrays used by the original goldens.

To regenerate the fixture intentionally (after changing the synthesis params):
delete the .wav, run the test suite once. The first sample-aware test invocation
recreates it via the factory.
