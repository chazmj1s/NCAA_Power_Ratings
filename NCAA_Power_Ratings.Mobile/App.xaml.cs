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

        // On Windows dev machine, emulate iPhone 11 portrait dimensions (414 x 896 points)
        // On real iOS/Android devices, let the OS handle sizing naturally
        if (DeviceInfo.Platform == DevicePlatform.WinUI)
        {
            window.Width = 414;
            window.Height = 896;
        }

        return window;
    }
}
