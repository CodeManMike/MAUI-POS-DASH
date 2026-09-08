using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MAUI_POS_DASH.Core.Attendants;
using MAUI_POS_DASH.Core.Cash;
using MAUI_POS_DASH.Core.Devices;
using MAUI_POS_DASH.Core.FleetCard;
using MAUI_POS_DASH.Core.MobileMoney;
using MAUI_POS_DASH.Core.Persistence;
using MAUI_POS_DASH.Core.Persistence.Repositories;
using MAUI_POS_DASH.Core.Sales;
using MAUI_POS_DASH.Core.Shifts;
using MAUI_POS_DASH.MAUI_Android.Platforms.Android.Devices;
using MAUI_POS_DASH.MAUI_Android.ViewModels;
using MAUI_POS_DASH.MAUI_Android.Views;

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

        RegisterCoreServices(builder.Services);
        RegisterPagesAndViewModels(builder.Services);

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
    /// This mirrors MAUI-POS-DASH/MauiProgram.cs's RegisterCoreServices — same Core services,
    /// same conventions — minus the sync/background-queue registrations, which this app
    /// deliberately doesn't include (see the MVVM POC's README for why).
    /// </summary>
    private static void RegisterCoreServices(IServiceCollection services)
    {
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "terminal.db");
        services.AddDbContext<TerminalDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));

        services.AddScoped<IShiftRepository, EfShiftRepository>();
        services.AddScoped<ITillRepository, EfTillRepository>();
        services.AddScoped<ShiftService>();
        services.AddScoped<TillReconciliationService>();

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
    }

    /// <summary>
    /// We register every page and ViewModel Transient — Shell resolves a fresh instance each time
    /// a route is navigated to, matching how each Blazor page in the sibling app got a fresh
    /// @code block per navigation.
    /// </summary>
    private static void RegisterPagesAndViewModels(IServiceCollection services)
    {
        services.AddTransient<LoginPage>();
        services.AddTransient<LoginViewModel>();

        services.AddTransient<HomePage>();
        services.AddTransient<HomeViewModel>();
    }

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
