using Microsoft.Data.Sqlite;
using MAUI_POS_DASH.Core.Devices;
using MAUI_POS_DASH.Core.FleetCard;
using MAUI_POS_DASH.Core.Persistence.Repositories;

namespace MAUI_POS_DASH.Core.Tests.FleetCard;

[TestFixture]
public class FleetCardSaleServiceTests
{
    #region Fields
    private SqliteConnection _connection = null!;
    private TerminalDbContext _dbContext = null!;
    private FakeCardReaderService _cardReader = null!;
    private FakeFleetCardAuthorizationService _authorizationService = null!;
    private FleetCardSaleService _sut = null!;
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
            Name = "Homer Simpson",
            PinHash = "hashed-6789",
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

        _cardReader = new FakeCardReaderService();
        _authorizationService = new FakeFleetCardAuthorizationService();

        _sut = new FleetCardSaleService(_cardReader, _authorizationService, new EfSaleRepository(_dbContext), new EfTillRepository(_dbContext), new EfShiftRepository(_dbContext));
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
    public async Task ProcessSaleAsync_Approved_PersistsSaleAndTransaction()
    {
        #region Act
        FleetCardSaleResult result = await _sut.ProcessSaleAsync(_shiftId, 250.00m);
        #endregion

        #region Assert
        Till till = await _dbContext.Tills.SingleAsync(t => t.ShiftId == _shiftId);
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(FleetCardSaleStatus.Approved));
            Assert.That(result.Sale, Is.Not.Null);
            Assert.That(result.Sale!.ShiftId, Is.EqualTo(_shiftId));
            Assert.That(result.Transaction, Is.Not.Null);
            Assert.That(result.Transaction!.Amount, Is.EqualTo(250.00m));
            Assert.That(result.Transaction!.Method, Is.EqualTo(PaymentMethod.FleetCard));
            Assert.That(result.Transaction!.Status, Is.EqualTo(TransactionStatus.Pending));
            Assert.That(_dbContext.Sales.Count(), Is.EqualTo(1));
            Assert.That(_dbContext.Transactions.Count(), Is.EqualTo(1));
            Assert.That(till.FleetCardTotal, Is.EqualTo(250.00m));
        });
        #endregion
    }

    [Test]
    public async Task ProcessSaleAsync_Declined_PersistsNothingAndLeavesTillUntouched()
    {
        #region Arrange
        _authorizationService.NextResult = new FleetCardAuthorizationResult(FleetCardAuthorizationStatus.Declined, "Over limit.");
        #endregion

        #region Act
        FleetCardSaleResult result = await _sut.ProcessSaleAsync(_shiftId, 250.00m);
        #endregion

        #region Assert
        Till till = await _dbContext.Tills.SingleAsync(t => t.ShiftId == _shiftId);
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(FleetCardSaleStatus.Declined));
            Assert.That(result.Sale, Is.Null);
            Assert.That(result.Transaction, Is.Null);
            Assert.That(result.DetailMessage, Is.EqualTo("Over limit."));
            Assert.That(_dbContext.Sales, Is.Empty);
            Assert.That(_dbContext.Transactions, Is.Empty);
            Assert.That(till.FleetCardTotal, Is.Zero);
        });
        #endregion
    }

    [Test]
    public async Task ProcessSaleAsync_CardReadTimesOut_PersistsNothingAndLeavesTillUntouched()
    {
        #region Arrange
        _cardReader.NextResult = new CardReadResult(CardReadStatus.Timeout, null, null);
        #endregion

        #region Act
        FleetCardSaleResult result = await _sut.ProcessSaleAsync(_shiftId, 250.00m);
        #endregion

        #region Assert
        Till till = await _dbContext.Tills.SingleAsync(t => t.ShiftId == _shiftId);
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(FleetCardSaleStatus.CardReadFailed));
            Assert.That(_dbContext.Sales, Is.Empty);
            Assert.That(_dbContext.Transactions, Is.Empty);
            Assert.That(till.FleetCardTotal, Is.Zero);
        });
        #endregion
    }

    [TestCase(0)]
    [TestCase(-10)]
    public void ProcessSaleAsync_InvalidAmount_ThrowsAndNeverWaitsForCard(decimal amount)
    {
        #region Act & Assert
        Assert.ThrowsAsync<ArgumentException>(() => _sut.ProcessSaleAsync(_shiftId, amount));
        Assert.That(_cardReader.WaitForCardCallCount, Is.Zero);
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
            Name = "Barney Gumble",
            PinHash = "hashed-1111",
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
        // The Till check must happen before anything is written — otherwise a missing Till leaves
        // an orphaned Sale/Transaction that no Till total will ever reflect.
        Assert.That(_dbContext.Sales, Is.Empty);
        Assert.That(_dbContext.Transactions, Is.Empty);
        #endregion
    }

    [Test]
    public async Task ProcessSaleAsync_ShiftIsClosed_ThrowsAndPersistsNothing()
    {
        #region Arrange
        Guid otherAttendantId = Guid.NewGuid();
        Guid otherShiftId = Guid.NewGuid();
        _dbContext.Attendants.Add(new Attendant
        {
            Id = otherAttendantId,
            Name = "Moe Szyslak",
            PinHash = "hashed-2222",
            Role = AttendantRole.Attendant
        });
        _dbContext.Shifts.Add(new Shift
        {
            Id = otherShiftId,
            AttendantId = otherAttendantId,
            OpenedAt = DateTimeOffset.UtcNow,
            OpeningFloat = 0,
            Status = ShiftStatus.Closed
        });
        _dbContext.Tills.Add(new Till { Id = Guid.NewGuid(), ShiftId = otherShiftId, CashTotal = 0, FleetCardTotal = 0, MobileMoneyTotal = 0 });
        _dbContext.SaveChanges();
        #endregion

        #region Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ProcessSaleAsync(otherShiftId, 50.00m));
        Assert.That(_dbContext.Sales, Is.Empty);
        Assert.That(_dbContext.Transactions, Is.Empty);
        #endregion
    }

    [Test]
    public void ProcessSaleAsync_AmountHasMoreThanTwoDecimalPlaces_ThrowsAndNeverWaitsForCard()
    {
        #region Act & Assert
        Assert.ThrowsAsync<ArgumentException>(() => _sut.ProcessSaleAsync(_shiftId, 10.005m));
        Assert.That(_cardReader.WaitForCardCallCount, Is.Zero);
        #endregion
    }
    #endregion

    #region Test Types
    private sealed class FakeCardReaderService : ICardReaderService
    {
        #region Events
        // The interface requires this event, but nothing in these tests needs to raise it.
        #pragma warning disable CS0067
        public event EventHandler<CardTapEventArgs>? CardPresented;
        #pragma warning restore CS0067
        #endregion

        #region Properties
        public CardReadResult NextResult { get; set; } = new(CardReadStatus.Success, "**** 4242", null);

        public int WaitForCardCallCount { get; private set; }
        #endregion

        #region Public Methods
        public Task<CardReadResult> WaitForCardAsync(CancellationToken cancellationToken = default)
        {
            WaitForCardCallCount++;
            return Task.FromResult(NextResult);
        }
        #endregion
    }

    private sealed class FakeFleetCardAuthorizationService : IFleetCardAuthorizationService
    {
        #region Properties
        public FleetCardAuthorizationResult NextResult { get; set; } =
            new(FleetCardAuthorizationStatus.Approved, null);
        #endregion

        #region Public Methods
        public Task<FleetCardAuthorizationResult> AuthorizeAsync(decimal amount, CancellationToken cancellationToken = default) =>
            Task.FromResult(NextResult);
        #endregion
    }
    #endregion
}
