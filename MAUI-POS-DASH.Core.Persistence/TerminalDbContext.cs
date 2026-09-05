namespace MAUI_POS_DASH.Core.Persistence;

/// <summary>
/// We back this with SQLite on-device — every write lands here first, offline, before the
/// OfflineTransactionQueue ever tries to reach the back office.
/// </summary>
public class TerminalDbContext : DbContext
{
    #region Constructor
    public TerminalDbContext(DbContextOptions<TerminalDbContext> options) : base(options)
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

    public DbSet<AttendantSession> AttendantSessions => Set<AttendantSession>();
    #endregion

    #region Overrides
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TerminalDbContext).Assembly);
    }
    #endregion
}
