namespace MAUI_POS_DASH.Web;

/// <summary>
/// We centralize this host's DI registrations here, the same way GlobalUsings.cs centralizes its
/// using directives — one dedicated file, so Program.cs stays a thin bootstrap sequence instead
/// of also carrying every service's wiring.
/// </summary>
public static class ServiceRegistration
{
    #region Public Methods
    public static void AddBackofficeServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddInteractiveWebAssemblyComponents();

        services.AddDbContext<BackofficeDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Backoffice")
                ?? "Host=localhost;Database=mauiposdash_dev;Username=postgres;Password=postgres"));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ITransactionIngestionService, TransactionIngestionService>();
    }
    #endregion
}
