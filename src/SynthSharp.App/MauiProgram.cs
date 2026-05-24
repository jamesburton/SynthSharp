using Microsoft.Extensions.Logging;
using Plugin.Maui.Audio;
using SynthSharp.Audio;
using SynthSharp.Core.Layout;
using SynthSharp.Core.Patterns;
using SynthSharp.Core.Persistence;
using SynthSharp.Input;

namespace SynthSharp.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.AddAudio()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddSingleton(DefaultPresetFactory.CreateFourRowDefault());
		builder.Services.AddSingleton<PadTriggerRouter>(sp => new PadTriggerRouter(sp.GetRequiredService<KeyboardLayoutPreset>().Pads));
		builder.Services.AddSingleton<IKeyboardInputSource, KeyboardInputSource>();
		builder.Services.AddSingleton<IAudioPlaybackBackend, MauiAudioPlaybackBackend>();
		builder.Services.AddSingleton<ISampleImporter, WavSampleImporter>();
		builder.Services.AddSingleton<ISampleExporter, WavSampleExporter>();
		builder.Services.AddSingleton<ISynthAudioEngine>(sp => new SynthAudioEngine(
			playbackBackend: sp.GetRequiredService<IAudioPlaybackBackend>(),
			sampleImporter: sp.GetRequiredService<ISampleImporter>(),
			sampleExporter: sp.GetRequiredService<ISampleExporter>(),
			samplesDirectory: MainPage.GetSamplesDirectory(),
			maxPolyphony: 8));
		builder.Services.AddSingleton<IPatternRecorder, DefaultPatternRecorder>();
		builder.Services.AddSingleton<IPatternPlayer, DefaultPatternPlayer>();
		builder.Services.AddSingleton<PatternClip>(_ => new PatternClip { Name = "current" });
		builder.Services.AddSingleton<MainPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
