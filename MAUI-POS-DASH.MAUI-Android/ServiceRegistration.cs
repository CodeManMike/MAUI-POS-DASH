using Microsoft.EntityFrameworkCore;
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
        RegisterCoreServices(services);
        RegisterPagesAndViewModels(services);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// This mirrors MAUI-POS-DASH/ServiceRegistration.cs's RegisterCoreServices — same Core
    /// services, same conventions — minus the sync/background-queue registrations, which this app
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
    /// We register every page and ViewModel Transient so each is only ever resolved once per app
    /// lifetime — Shell keeps every ShellContent's page/ViewModel alive after that first
    /// resolution, it doesn't create a fresh instance on later visits to the same route. That's
    /// unlike the sibling Blazor app, where each page got a fresh @code block per navigation; here
    /// per-visit state has to be cleared explicitly by the ViewModel itself (see
    /// AuthenticatedViewModelBase.ResetVisitState).
    /// </summary>
    private static void RegisterPagesAndViewModels(IServiceCollection services)
    {
        services.AddTransient<LoginPage>();
        services.AddTransient<LoginViewModel>();

        services.AddTransient<HomePage>();
        services.AddTransient<HomeViewModel>();

        services.AddTransient<AttendantManagementPage>();
        services.AddTransient<AttendantManagementViewModel>();

        services.AddTransient<ShiftOpenPage>();
        services.AddTransient<ShiftOpenViewModel>();

        services.AddTransient<ShiftClosePage>();
        services.AddTransient<ShiftCloseViewModel>();

        services.AddTransient<FleetCardSalePage>();
        services.AddTransient<FleetCardSaleViewModel>();

        services.AddTransient<CashSalePage>();
        services.AddTransient<CashSaleViewModel>();

        services.AddTransient<MobileMoneySalePage>();
        services.AddTransient<MobileMoneySaleViewModel>();
    }
    #endregion
}
