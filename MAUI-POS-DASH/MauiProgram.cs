using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MAUI_POS_DASH.Core.Contracts;
using MAUI_POS_DASH.Core.Persistence;
using MAUI_POS_DASH.Core.Persistence.Repositories;
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

        return builder.Build();
    }
    #endregion

    #region Private Methods
    private static void RegisterCoreServices(IServiceCollection services)
    {
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "terminal.db");
        services.AddDbContext<TerminalDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));

        services.AddScoped<IShiftRepository, EfShiftRepository>();
        services.AddScoped<ITransactionQueueStore, EfTransactionQueueStore>();
        services.AddScoped<ShiftService>();
        services.AddScoped<TillReconciliationService>();
        services.AddScoped<OfflineTransactionQueue>();
        services.AddSingleton<EntityMapper>();

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
    #endregion
}
