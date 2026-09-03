using Microsoft.EntityFrameworkCore.Design;

namespace MAUI_POS_DASH.Core.Persistence.DesignTime;

/// <summary>
/// We exist only for `dotnet ef migrations add` tooling — nothing at runtime uses this factory.
/// </summary>
public class TerminalDbContextFactory : IDesignTimeDbContextFactory<TerminalDbContext>
{
    public TerminalDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TerminalDbContext>();
        optionsBuilder.UseSqlite("Data Source=terminal.designtime.db");
        return new TerminalDbContext(optionsBuilder.Options);
    }
}
