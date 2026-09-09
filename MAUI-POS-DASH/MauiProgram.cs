using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MAUI_POS_DASH.Core.Persistence;

namespace MAUI_POS_DASH;

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
            });

        builder.Services.AddMauiBlazorWebView();

        builder.Services.AddTerminalServices();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
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
    /// returns from CreateMauiApp — the `dotnet ef database update` commands in README.md only
    /// ever touch a design-time file on the dev machine, never the real runtime database under
    /// FileSystem.AppDataDirectory. Without this, a fresh install (or an upgrade that adds a new
    /// migration) has no schema at all, and the very first page to query TerminalDbContext
    /// throws. Migrate() is idempotent, so running it on every launch is safe.
    /// </summary>
    private static void MigrateTerminalDatabase(MauiApp app)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TerminalDbContext>();
        dbContext.Database.Migrate();
    }
    #endregion
}
