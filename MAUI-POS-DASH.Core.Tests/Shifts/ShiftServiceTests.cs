using Microsoft.Data.Sqlite;
using MAUI_POS_DASH.Core.Persistence.Repositories;
using MAUI_POS_DASH.Core.Shifts;

namespace MAUI_POS_DASH.Core.Tests.Shifts;

[TestFixture]
public class ShiftServiceTests
{
    #region Fields
    private SqliteConnection _connection = null!;
    private TerminalDbContext _dbContext = null!;
    private ShiftService _sut = null!;
    private Guid _attendantId;
    #endregion

    #region Setup
    [SetUp]
    public void SetUp()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<TerminalDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new TerminalDbContext(options);
        _dbContext.Database.EnsureCreated();

        _attendantId = Guid.NewGuid();
        _dbContext.Attendants.Add(new Attendant
        {
            Id = _attendantId,
            Name = "Marge Simpson",
            PinHash = "hashed-5678",
            Role = AttendantRole.Attendant
        });
        _dbContext.SaveChanges();

        _sut = new ShiftService(
            new EfShiftRepository(_dbContext),
            new EfTillRepository(_dbContext),
            new TillReconciliationService());
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

    #region Tests
    [Test]
    public async Task OpenShiftAsync_NoActiveShift_CreatesOpenShiftWithGivenFloat()
    {
        #region Arrange
        const decimal openingFloat = 150.00m;
        #endregion

        #region Act
        var shift = await _sut.OpenShiftAsync(_attendantId, openingFloat);
        #endregion

        #region Assert
        Assert.Multiple(() =>
        {
            Assert.That(shift.Status, Is.EqualTo(ShiftStatus.Open));
            Assert.That(shift.OpeningFloat, Is.EqualTo(openingFloat));
            Assert.That(_dbContext.Shifts.Count(), Is.EqualTo(1));

            Till? till = _dbContext.Tills.SingleOrDefault(t => t.ShiftId == shift.Id);
            Assert.That(till, Is.Not.Null);
            Assert.That(till!.CashTotal, Is.Zero);
            Assert.That(till.FleetCardTotal, Is.Zero);
            Assert.That(till.MobileMoneyTotal, Is.Zero);
        });
        #endregion
    }

    [Test]
    public void OpenShiftAsync_AttendantAlreadyHasOpenShift_Throws()
    {
        #region Arrange
        _dbContext.Shifts.Add(new Shift
        {
            Id = Guid.NewGuid(),
            AttendantId = _attendantId,
            OpenedAt = DateTimeOffset.UtcNow.AddHours(-1),
            OpeningFloat = 100.00m,
            Status = ShiftStatus.Open
        });
        _dbContext.SaveChanges();
        #endregion

        #region Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(() => _sut.OpenShiftAsync(_attendantId, 100.00m));
        #endregion
    }

    [Test]
    public async Task PreviewCloseAsync_CashMatchesTillTotal_ReturnsWithinTolerance()
    {
        #region Arrange
        var shift = await _sut.OpenShiftAsync(_attendantId, 100.00m);
        Till till = await _dbContext.Tills.SingleAsync(t => t.ShiftId == shift.Id);
        till.CashTotal = 500.00m;
        await _dbContext.SaveChangesAsync();
        #endregion

        #region Act
        ReconciliationResult result = await _sut.PreviewCloseAsync(shift, cashCounted: 500.00m);
        #endregion

        #region Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.ExpectedCash, Is.EqualTo(500.00m));
            Assert.That(result.CountedCash, Is.EqualTo(500.00m));
            Assert.That(result.Variance, Is.Zero);
            Assert.That(result.WithinTolerance, Is.True);
        });
        #endregion
    }

    [Test]
    public async Task PreviewCloseAsync_CashOutsideTolerance_ReturnsNotWithinTolerance()
    {
        #region Arrange
        var shift = await _sut.OpenShiftAsync(_attendantId, 100.00m);
        Till till = await _dbContext.Tills.SingleAsync(t => t.ShiftId == shift.Id);
        till.CashTotal = 500.00m;
        await _dbContext.SaveChangesAsync();
        #endregion

        #region Act
        ReconciliationResult result = await _sut.PreviewCloseAsync(shift, cashCounted: 490.00m);
        #endregion

        #region Assert
        Assert.That(result.WithinTolerance, Is.False);
        #endregion
    }

    [Test]
    public async Task PreviewCloseAsync_DoesNotMutateShiftOrTill()
    {
        #region Arrange
        var shift = await _sut.OpenShiftAsync(_attendantId, 100.00m);
        #endregion

        #region Act
        await _sut.PreviewCloseAsync(shift, cashCounted: 999.00m);
        #endregion

        #region Assert
        Shift reloadedShift = await _dbContext.Shifts.SingleAsync(s => s.Id == shift.Id);
        Till reloadedTill = await _dbContext.Tills.SingleAsync(t => t.ShiftId == shift.Id);
        Assert.Multiple(() =>
        {
            Assert.That(reloadedShift.Status, Is.EqualTo(ShiftStatus.Open));
            Assert.That(reloadedShift.ClosedAt, Is.Null);
            Assert.That(reloadedTill.CashTotal, Is.Zero);
        });
        #endregion
    }

    [Test]
    public void PreviewCloseAsync_ShiftHasNoTill_Throws()
    {
        #region Arrange
        var shift = new Shift
        {
            Id = Guid.NewGuid(),
            AttendantId = _attendantId,
            OpenedAt = DateTimeOffset.UtcNow,
            OpeningFloat = 100.00m,
            Status = ShiftStatus.Open
        };
        _dbContext.Shifts.Add(shift);
        _dbContext.SaveChanges();
        #endregion

        #region Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(() => _sut.PreviewCloseAsync(shift, cashCounted: 0m));
        #endregion
    }
    #endregion
}
