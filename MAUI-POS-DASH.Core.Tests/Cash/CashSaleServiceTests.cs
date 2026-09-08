using Microsoft.Data.Sqlite;
using MAUI_POS_DASH.Core.Cash;
using MAUI_POS_DASH.Core.Persistence.Repositories;

namespace MAUI_POS_DASH.Core.Tests.Cash;

[TestFixture]
public class CashSaleServiceTests
{
    #region Fields
    private SqliteConnection _connection = null!;
    private TerminalDbContext _dbContext = null!;
    private CashSaleService _sut = null!;
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
            Name = "Otto Mann",
            PinHash = "hashed-9999",
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
        _dbContext.Tills.Add(new Till { Id = Guid.NewGuid(), ShiftId = _shiftId, CashTotal = 0, FleetCardTotal = 0, MobileMoneyTotal = 0 });
        _dbContext.SaveChanges();

        _sut = new CashSaleService(new EfSaleRepository(_dbContext), new EfTillRepository(_dbContext));
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
    public async Task ProcessSaleAsync_ValidTender_PersistsSaleAndIncrementsTill()
    {
        #region Act
        CashSaleResult result = await _sut.ProcessSaleAsync(_shiftId, amountOwed: 200.00m, amountTendered: 250.00m);
        #endregion

        #region Assert
        Till till = await _dbContext.Tills.SingleAsync(t => t.ShiftId == _shiftId);
        Assert.Multiple(() =>
        {
            Assert.That(result.Change, Is.EqualTo(50.00m));
            Assert.That(result.Transaction.Amount, Is.EqualTo(200.00m));
            Assert.That(result.Transaction.Method, Is.EqualTo(PaymentMethod.Cash));
            Assert.That(result.Transaction.Status, Is.EqualTo(TransactionStatus.Pending));
            Assert.That(result.Sale.ShiftId, Is.EqualTo(_shiftId));
            Assert.That(_dbContext.Sales.Count(), Is.EqualTo(1));
            Assert.That(_dbContext.Transactions.Count(), Is.EqualTo(1));
            Assert.That(till.CashTotal, Is.EqualTo(200.00m));
        });
        #endregion
    }

    [Test]
    public async Task ProcessSaleAsync_ExactTender_ZeroChange()
    {
        #region Act
        CashSaleResult result = await _sut.ProcessSaleAsync(_shiftId, amountOwed: 100.00m, amountTendered: 100.00m);
        #endregion

        #region Assert
        Assert.That(result.Change, Is.Zero);
        #endregion
    }

    [TestCase(0)]
    [TestCase(-5)]
    public void ProcessSaleAsync_NonPositiveAmountOwed_ThrowsAndPersistsNothing(decimal amountOwed)
    {
        #region Act & Assert
        Assert.ThrowsAsync<ArgumentException>(() => _sut.ProcessSaleAsync(_shiftId, amountOwed, amountTendered: 100.00m));
        Assert.That(_dbContext.Sales, Is.Empty);
        Assert.That(_dbContext.Tills.Single().CashTotal, Is.Zero);
        #endregion
    }

    [Test]
    public void ProcessSaleAsync_TenderedLessThanOwed_ThrowsAndPersistsNothing()
    {
        #region Act & Assert
        Assert.ThrowsAsync<ArgumentException>(() => _sut.ProcessSaleAsync(_shiftId, amountOwed: 100.00m, amountTendered: 99.00m));
        Assert.That(_dbContext.Sales, Is.Empty);
        Assert.That(_dbContext.Tills.Single().CashTotal, Is.Zero);
        #endregion
    }

    [Test]
    public async Task ProcessSaleAsync_ShiftHasNoTill_ThrowsAndPersistsNothing()
    {
        #region Arrange
        Guid otherAttendantId = Guid.NewGuid();
        Guid otherShiftId = Guid.NewGuid();
        _dbContext.Attendants.Add(new Attendant
        {
            Id = otherAttendantId,
            Name = "Lenny Leonard",
            PinHash = "hashed-0000",
            Role = AttendantRole.Attendant
        });
        _dbContext.Shifts.Add(new Shift
        {
            Id = otherShiftId,
            AttendantId = otherAttendantId,
            OpenedAt = DateTimeOffset.UtcNow,
            OpeningFloat = 0,
            Status = ShiftStatus.Open
        });
        _dbContext.SaveChanges();
        #endregion

        #region Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ProcessSaleAsync(otherShiftId, amountOwed: 50.00m, amountTendered: 50.00m));
        // The Till check must happen before anything is written — otherwise a missing Till leaves
        // an orphaned Sale/Transaction that no Till total will ever reflect.
        Assert.That(_dbContext.Sales, Is.Empty);
        Assert.That(_dbContext.Transactions, Is.Empty);
        #endregion
    }
    #endregion
}
