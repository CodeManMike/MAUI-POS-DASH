namespace MAUI_POS_DASH.MAUI_Android;

public partial class App : Application
{
    #region Constructor
    public App()
    {
        InitializeComponent();
    }
    #endregion

    #region Overrides
    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
    #endregion
}