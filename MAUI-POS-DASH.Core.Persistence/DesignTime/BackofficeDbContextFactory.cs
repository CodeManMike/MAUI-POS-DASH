using Microsoft.EntityFrameworkCore.Design;

namespace MAUI_POS_DASH.Core.Persistence.DesignTime;

/// <summary>
/// We exist only for `dotnet ef migrations add` tooling — nothing at runtime uses this factory.
/// The connection string here is a local-only default; it is never a real deployed database.
/// </summary>
public class BackofficeDbContextFactory : IDesignTimeDbContextFactory<BackofficeDbContext>
{
    public BackofficeDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BackofficeDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=mauiposdash_dev;Username=postgres;Password=postgres");
        return new BackofficeDbContext(optionsBuilder.Options);
    }
}
