using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MAUI_POS_DASH.Core.Persistence;

namespace MAUI_POS_DASH.MAUI_Android;

public static class MauiProgram
{
    #region Public Methods
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddTerminalServices();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();
        MigrateTerminalDatabase(app);

        return app;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// We apply pending migrations to the on-device SQLite file here, once, before the app
    /// returns from CreateMauiApp — mirrors MAUI-POS-DASH/MauiProgram.cs's MigrateTerminalDatabase
    /// for the same reason: MAUI has no natural async startup hook, and Migrate() is idempotent
    /// so running it on every launch is safe.
    /// </summary>
    private static void MigrateTerminalDatabase(MauiApp app)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TerminalDbContext>();
        dbContext.Database.Migrate();
    }
    #endregion
}
