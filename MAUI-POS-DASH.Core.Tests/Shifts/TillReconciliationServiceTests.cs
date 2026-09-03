using Microsoft.Data.Sqlite;
using MAUI_POS_DASH.Core.Shifts;

namespace MAUI_POS_DASH.Core.Tests.Shifts;

[TestFixture]
public class TillReconciliationServiceTests
{
    #region Fields
    private SqliteConnection _connection = null!;
    private TerminalDbContext _dbContext = null!;
    private TillReconciliationService _sut = null!;
    #endregion

    #region Setup
    [SetUp]
    public void SetUp()
    {
        // We keep one open SQLite in-memory connection alive for the test's lifetime so the
        // schema and foreign keys behave like a real database — the moment the connection
        // closes, the in-memory database is gone, which is exactly the teardown we want.
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<TerminalDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new TerminalDbContext(options);
        _dbContext.Database.EnsureCreated();

        SeedData();

        _sut = new TillReconciliationService();
    }
    #endregion

    #region Teardown
    [TearDown]
    public void TearDown()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }
    #endregion

    #region Seed
    private void SeedData()
    {
        var attendant = new Attendant
        {
            Id = Guid.NewGuid(),
            Name = "Homer Simpson",
            PinHash = "hashed-1234",
            Role = AttendantRole.Attendant
        };

        var shift = new Shift
        {
            Id = Guid.NewGuid(),
            AttendantId = attendant.Id,
            Attendant = attendant,
            OpenedAt = DateTimeOffset.UtcNow.AddHours(-4),
            OpeningFloat = 100.00m,
            Status = ShiftStatus.Open
        };

        var till = new Till
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            CashTotal = 250.00m,
            FleetCardTotal = 180.00m,
            MobileMoneyTotal = 60.00m
        };

        _dbContext.Attendants.Add(attendant);
        _dbContext.Shifts.Add(shift);
        _dbContext.Tills.Add(till);
        _dbContext.SaveChanges();
    }
    #endregion

    #region Tests
    [Test]
    public void Reconcile_CountedCashMatchesRecordedCash_ReportsWithinTolerance()
    {
        #region Arrange
        var till = _dbContext.Tills.Single();
        #endregion

        #region Act
        var result = _sut.Reconcile(till, countedCash: 250.00m);
        #endregion

        #region Assert
        Assert.That(result.WithinTolerance, Is.True);
        Assert.That(result.Variance, Is.EqualTo(0m));
        #endregion
    }

    [Test]
    public void Reconcile_CountedCashIsShort_ReportsVarianceOutsideTolerance()
    {
        #region Arrange
        var till = _dbContext.Tills.Single();
        #endregion

        #region Act
        var result = _sut.Reconcile(till, countedCash: 235.00m);
        #endregion

        #region Assert
        Assert.That(result.WithinTolerance, Is.False);
        Assert.That(result.Variance, Is.EqualTo(-15.00m));
        #endregion
    }
    #endregion
}
