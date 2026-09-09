using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.View;

namespace MAUI_POS_DASH;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    #region Overrides
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Android enforces edge-to-edge rendering regardless of target SDK now, which draws our
        // content behind the status bar unless we opt back into the system inset it the way every
        // earlier Android version already did.
        WindowCompat.SetDecorFitsSystemWindows(Window!, true);
    }
    #endregion
}
