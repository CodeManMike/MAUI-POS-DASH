using Microsoft.EntityFrameworkCore;
using MAUI_POS_DASH.Core.Attendants;
using MAUI_POS_DASH.Core.Cash;
using MAUI_POS_DASH.Core.Contracts;
using MAUI_POS_DASH.Core.FleetCard;
using MAUI_POS_DASH.Core.MobileMoney;
using MAUI_POS_DASH.Core.Persistence;
using MAUI_POS_DASH.Core.Persistence.Repositories;
using MAUI_POS_DASH.Core.Sales;
using MAUI_POS_DASH.Core.Shifts;
using MAUI_POS_DASH.Core.Sync;
using MAUI_POS_DASH.Platforms.Android.Devices;
using MAUI_POS_DASH.Services;

namespace MAUI_POS_DASH;

/// <summary>
/// We centralize this app's DI registrations here, the same way GlobalUsings.cs centralizes its
/// using directives — one dedicated file, so MauiProgram.cs stays a thin bootstrap sequence
/// instead of also carrying every service's wiring.
/// </summary>
public static class ServiceRegistration
{
    #region Public Methods
    public static void AddTerminalServices(this IServiceCollection services)
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

        services.AddSingleton<IMobileMoneyPaymentService>(
            _ => new SimulatedMobileMoneyPaymentService(Random.Shared, TimeSpan.FromSeconds(4)));
        services.AddScoped<MobileMoneySaleService>();

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
