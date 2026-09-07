using Microsoft.Data.Sqlite;
using MAUI_POS_DASH.Core.MobileMoney;
using MAUI_POS_DASH.Core.Persistence.Repositories;

namespace MAUI_POS_DASH.Core.Tests.MobileMoney;

[TestFixture]
public class MobileMoneySaleServiceTests
{
    #region Fields
    private SqliteConnection _connection = null!;
    private TerminalDbContext _dbContext = null!;
    private FakeMobileMoneyPaymentService _paymentService = null!;
    private MobileMoneySaleService _sut = null!;
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
            Name = "Selma Bouvier",
            PinHash = "hashed-4321",
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

        _paymentService = new FakeMobileMoneyPaymentService();
        _sut = new MobileMoneySaleService(_paymentService, new EfSaleRepository(_dbContext), new EfTillRepository(_dbContext));
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
    public async Task ProcessSaleAsync_Confirmed_PersistsSaleAndIncrementsTill()
    {
        #region Act
        MobileMoneySaleResult result = await _sut.ProcessSaleAsync(_shiftId, 250.00m);
        #endregion

        #region Assert
        Till till = await _dbContext.Tills.SingleAsync(t => t.ShiftId == _shiftId);
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(MobileMoneySaleStatus.Confirmed));
            Assert.That(result.Sale, Is.Not.Null);
            Assert.That(result.Sale!.ShiftId, Is.EqualTo(_shiftId));
            Assert.That(result.Transaction, Is.Not.Null);
            Assert.That(result.Transaction!.Amount, Is.EqualTo(250.00m));
            Assert.That(result.Transaction!.Method, Is.EqualTo(PaymentMethod.MobileMoney));
            Assert.That(result.Transaction!.Status, Is.EqualTo(TransactionStatus.Pending));
            Assert.That(_dbContext.Sales.Count(), Is.EqualTo(1));
            Assert.That(_dbContext.Transactions.Count(), Is.EqualTo(1));
            Assert.That(till.MobileMoneyTotal, Is.EqualTo(250.00m));
        });
        #endregion
    }

    [Test]
    public async Task ProcessSaleAsync_Declined_PersistsNothingAndLeavesTillUntouched()
    {
        #region Arrange
        _paymentService.NextResult = new MobileMoneyPaymentResult(MobileMoneyPaymentStatus.Declined, "The customer declined the payment.");
        #endregion

        #region Act
        MobileMoneySaleResult result = await _sut.ProcessSaleAsync(_shiftId, 250.00m);
        #endregion

        #region Assert
        Till till = await _dbContext.Tills.SingleAsync(t => t.ShiftId == _shiftId);
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(MobileMoneySaleStatus.Declined));
            Assert.That(result.Sale, Is.Null);
            Assert.That(result.Transaction, Is.Null);
            Assert.That(_dbContext.Sales, Is.Empty);
            Assert.That(_dbContext.Transactions, Is.Empty);
            Assert.That(till.MobileMoneyTotal, Is.Zero);
        });
        #endregion
    }

    [Test]
    public async Task ProcessSaleAsync_TimedOut_PersistsNothingAndLeavesTillUntouched()
    {
        #region Arrange
        _paymentService.NextResult = new MobileMoneyPaymentResult(MobileMoneyPaymentStatus.TimedOut, "The customer didn't confirm in time.");
        #endregion

        #region Act
        MobileMoneySaleResult result = await _sut.ProcessSaleAsync(_shiftId, 250.00m);
        #endregion

        #region Assert
        Till till = await _dbContext.Tills.SingleAsync(t => t.ShiftId == _shiftId);
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(MobileMoneySaleStatus.TimedOut));
            Assert.That(_dbContext.Sales, Is.Empty);
            Assert.That(till.MobileMoneyTotal, Is.Zero);
        });
        #endregion
    }

    [TestCase(0)]
    [TestCase(-10)]
    public void ProcessSaleAsync_InvalidAmount_ThrowsAndNeverRequestsPayment(decimal amount)
    {
        #region Act & Assert
        Assert.ThrowsAsync<ArgumentException>(() => _sut.ProcessSaleAsync(_shiftId, amount));
        Assert.That(_paymentService.RequestPaymentCallCount, Is.Zero);
        #endregion
    }

    [Test]
    public void ProcessSaleAsync_ShiftHasNoTill_Throws()
    {
        #region Arrange
        Guid otherAttendantId = Guid.NewGuid();
        Guid otherShiftId = Guid.NewGuid();
        _dbContext.Attendants.Add(new Attendant
        {
            Id = otherAttendantId,
            Name = "Patty Bouvier",
            PinHash = "hashed-8888",
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
        Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ProcessSaleAsync(otherShiftId, 50.00m));
        #endregion
    }
    #endregion

    #region Test Types
    private sealed class FakeMobileMoneyPaymentService : IMobileMoneyPaymentService
    {
        #region Properties
        public MobileMoneyPaymentResult NextResult { get; set; } =
            new(MobileMoneyPaymentStatus.Confirmed, null);

        public int RequestPaymentCallCount { get; private set; }
        #endregion

        #region Public Methods
        public Task<MobileMoneyPaymentResult> RequestPaymentAsync(decimal amount, CancellationToken cancellationToken = default)
        {
            RequestPaymentCallCount++;
            return Task.FromResult(NextResult);
        }
        #endregion
    }
    #endregion
}
