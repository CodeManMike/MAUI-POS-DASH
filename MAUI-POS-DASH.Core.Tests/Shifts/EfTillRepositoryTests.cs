using Microsoft.Data.Sqlite;
using MAUI_POS_DASH.Core.Persistence.Repositories;
using MAUI_POS_DASH.Core.Shifts;

namespace MAUI_POS_DASH.Core.Tests.Shifts;

[TestFixture]
public class EfTillRepositoryTests
{
    #region Fields
    private SqliteConnection _connection = null!;
    private TerminalDbContext _dbContext = null!;
    private EfTillRepository _sut = null!;
    private Guid _shiftId;
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

        Guid attendantId = Guid.NewGuid();
        _shiftId = Guid.NewGuid();
        _dbContext.Attendants.Add(new Attendant
        {
            Id = attendantId,
            Name = "Ned Flanders",
            PinHash = "hashed-1234",
            Role = AttendantRole.Attendant
        });
        _dbContext.Shifts.Add(new Shift
        {
            Id = _shiftId,
            AttendantId = attendantId,
            OpenedAt = DateTimeOffset.UtcNow,
            OpeningFloat = 100.00m,
            Status = ShiftStatus.Open
        });
        _dbContext.SaveChanges();

        _sut = new EfTillRepository(_dbContext);
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
    public async Task CreateAsync_NewTill_CanBeFetchedByShiftId()
    {
        #region Arrange
        var till = new Till { Id = Guid.NewGuid(), ShiftId = _shiftId, CashTotal = 0, FleetCardTotal = 0, MobileMoneyTotal = 0 };
        #endregion

        #region Act
        await _sut.CreateAsync(till);
        #endregion

        #region Assert
        Till? fetched = await _sut.GetByShiftIdAsync(_shiftId);
        Assert.That(fetched, Is.Not.Null);
        Assert.That(fetched!.Id, Is.EqualTo(till.Id));
        #endregion
    }

    [Test]
    public async Task GetByShiftIdAsync_NoTillForShift_ReturnsNull()
    {
        #region Act
        Till? fetched = await _sut.GetByShiftIdAsync(Guid.NewGuid());
        #endregion

        #region Assert
        Assert.That(fetched, Is.Null);
        #endregion
    }

    [Test]
    public async Task UpdateAsync_ChangedCashTotal_Persists()
    {
        #region Arrange
        var till = new Till { Id = Guid.NewGuid(), ShiftId = _shiftId, CashTotal = 0, FleetCardTotal = 0, MobileMoneyTotal = 0 };
        await _sut.CreateAsync(till);
        till.CashTotal = 250.00m;
        #endregion

        #region Act
        await _sut.UpdateAsync(till);
        #endregion

        #region Assert
        Till? fetched = await _sut.GetByShiftIdAsync(_shiftId);
        Assert.That(fetched!.CashTotal, Is.EqualTo(250.00m));
        #endregion
    }
    #endregion
}
