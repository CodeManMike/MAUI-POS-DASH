namespace MAUI_POS_DASH;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new MainPage()) { Title = "MAUI-POS-DASH" };
    }
}
