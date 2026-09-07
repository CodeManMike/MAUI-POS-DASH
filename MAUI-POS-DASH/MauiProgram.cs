using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MAUI_POS_DASH.Core.Attendants;
using MAUI_POS_DASH.Core.Cash;
using MAUI_POS_DASH.Core.Contracts;
using MAUI_POS_DASH.Core.FleetCard;
using MAUI_POS_DASH.Core.Persistence;
using MAUI_POS_DASH.Core.Persistence.Repositories;
using MAUI_POS_DASH.Core.Sales;
using MAUI_POS_DASH.Core.Shifts;
using MAUI_POS_DASH.Core.Sync;
using MAUI_POS_DASH.Platforms.Android.Devices;
using MAUI_POS_DASH.Services;

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

        RegisterCoreServices(builder.Services);

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
    private static void RegisterCoreServices(IServiceCollection services)
    {
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "terminal.db");
        services.AddDbContext<TerminalDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));

        services.AddScoped<IShiftRepository, EfShiftRepository>();
        services.AddScoped<ITillRepository, EfTillRepository>();
        services.AddScoped<ITransactionQueueStore, EfTransactionQueueStore>();
        services.AddScoped<ShiftService>();
        services.AddScoped<TillReconciliationService>();
        services.AddScoped<OfflineTransactionQueue>();
        services.AddSingleton<EntityMapper>();

        services.AddScoped<IAttendantRepository, EfAttendantRepository>();
        services.AddScoped<IAttendantSessionStore, EfAttendantSessionStore>();
        services.AddScoped<AttendantService>();
        services.AddSingleton<IPinHasher, Pbkdf2PinHasher>();

        services.AddScoped<ISaleRepository, EfSaleRepository>();
        services.AddSingleton(Random.Shared);
        services.AddSingleton<IFleetCardAuthorizationService, SimulatedFleetCardAuthorizationService>();
        services.AddScoped<FleetCardSaleService>();
        services.AddScoped<CashSaleService>();

        services.AddSingleton<ICardReaderService, SimulatedPaxCardReader>();
        services.AddSingleton<IReceiptPrinterService, SimulatedPaxReceiptPrinter>();
        services.AddSingleton<IBarcodeScannerService, SimulatedPaxBarcodeScanner>();

        services.AddHttpClient<ITransactionSyncService, HttpTransactionSyncService>(client =>
        {
            // We point at the back office's local dev URL for now — production config will come
            // from appsettings once the Sync module is actually built out.
            client.BaseAddress = new Uri("https://localhost:7135/");
        });
    }

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
