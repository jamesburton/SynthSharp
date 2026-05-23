using Microsoft.Extensions.DependencyInjection;

namespace SynthSharp.App;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var services = activationState?.Context.Services ?? Handler?.MauiContext?.Services
		    ?? throw new InvalidOperationException("Maui service provider is unavailable.");
		var page = services.GetRequiredService<MainPage>();
		return new Window(page);
	}
}