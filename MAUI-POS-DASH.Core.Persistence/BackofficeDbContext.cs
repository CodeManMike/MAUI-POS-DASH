namespace MAUI_POS_DASH.Core.Persistence;

/// <summary>
/// We back this with Postgres — it's the server of record the dashboard reads from, populated
/// only by what terminals have successfully synced.
/// </summary>
public class BackofficeDbContext : DbContext
{
    #region Constructor
    public BackofficeDbContext(DbContextOptions<BackofficeDbContext> options) : base(options)
    {
    }
    #endregion

    #region DbSets
    public DbSet<Attendant> Attendants => Set<Attendant>();

    public DbSet<Shift> Shifts => Set<Shift>();

    public DbSet<Till> Tills => Set<Till>();

    public DbSet<Sale> Sales => Set<Sale>();

    public DbSet<SaleLine> SaleLines => Set<SaleLine>();

    public DbSet<Transaction> Transactions => Set<Transaction>();
    #endregion

    #region Overrides
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BackofficeDbContext).Assembly);

        // AttendantSession is terminal-local state — it belongs only in TerminalDbContext, never
        // synced to the back office. We exclude it here even though AttendantSessionConfiguration
        // gets picked up by the assembly scan above.
        modelBuilder.Ignore<AttendantSession>();
    }
    #endregion
}
