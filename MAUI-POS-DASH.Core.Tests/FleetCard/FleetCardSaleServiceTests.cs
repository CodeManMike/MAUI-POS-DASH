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

        _shiftId = Guid.NewGuid();
        _cardReader = new FakeCardReaderService();
        _authorizationService = new FakeFleetCardAuthorizationService();

        _sut = new FleetCardSaleService(_cardReader, _authorizationService, new EfSaleRepository(_dbContext));
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
        });
        #endregion
    }

    [Test]
    public async Task ProcessSaleAsync_Declined_PersistsNothing()
    {
        #region Arrange
        _authorizationService.NextResult = new FleetCardAuthorizationResult(FleetCardAuthorizationStatus.Declined, "Over limit.");
        #endregion

        #region Act
        FleetCardSaleResult result = await _sut.ProcessSaleAsync(_shiftId, 250.00m);
        #endregion

        #region Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(FleetCardSaleStatus.Declined));
            Assert.That(result.Sale, Is.Null);
            Assert.That(result.Transaction, Is.Null);
            Assert.That(result.DetailMessage, Is.EqualTo("Over limit."));
            Assert.That(_dbContext.Sales, Is.Empty);
            Assert.That(_dbContext.Transactions, Is.Empty);
        });
        #endregion
    }

    [Test]
    public async Task ProcessSaleAsync_CardReadTimesOut_PersistsNothing()
    {
        #region Arrange
        _cardReader.NextResult = new CardReadResult(CardReadStatus.Timeout, null, null);
        #endregion

        #region Act
        FleetCardSaleResult result = await _sut.ProcessSaleAsync(_shiftId, 250.00m);
        #endregion

        #region Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(FleetCardSaleStatus.CardReadFailed));
            Assert.That(_dbContext.Sales, Is.Empty);
            Assert.That(_dbContext.Transactions, Is.Empty);
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
