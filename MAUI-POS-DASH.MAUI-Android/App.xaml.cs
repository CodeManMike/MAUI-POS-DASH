using Microsoft.Extensions.DependencyInjection;

namespace MAUI_POS_DASH.MAUI_Android;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
}