using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace NCAA_Power_Ratings.Mobile;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new AppShell());

		// Default to iPhone 16 Pro Max portrait dimensions (430 x 932 points)
		window.Width = 430;
		window.Height = 932;

		return window;
	}
}
