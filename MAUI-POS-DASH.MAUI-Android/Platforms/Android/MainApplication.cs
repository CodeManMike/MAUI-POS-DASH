using Android.App;
using Android.Runtime;

namespace MAUI_POS_DASH.MAUI_Android;

[Application]
public class MainApplication : MauiApplication
{
    #region Constructor
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }
    #endregion

    #region Overrides
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    #endregion
}
