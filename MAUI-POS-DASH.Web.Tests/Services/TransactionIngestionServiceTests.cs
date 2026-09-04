namespace MAUI_POS_DASH.Web.Tests.Services;

[TestFixture]
public class TransactionIngestionServiceTests
{
    #region Fields
    private static readonly DateTimeOffset ServerNow = new(2026, 9, 4, 8, 0, 0, TimeSpan.Zero);
    private SqliteConnection _connection = null!;
    private FailingBackofficeDbContext _dbContext = null!;
    private TransactionIngestionService _sut = null!;
    private Guid _saleId;
    #endregion

    #region Setup
    [SetUp]
    public void SetUp()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        DbContextOptions<BackofficeDbContext> options =
            new DbContextOptionsBuilder<BackofficeDbContext>()
                .UseSqlite(_connection)
                .Options;

        _dbContext = new FailingBackofficeDbContext(options);
        _dbContext.Database.EnsureCreated();

        Guid attendantId = Guid.NewGuid();
        Guid shiftId = Guid.NewGuid();
        _saleId = Guid.NewGuid();

        _dbContext.Attendants.Add(new Attendant
        {
            Id = attendantId,
            Name = "Marge Simpson",
            PinHash = "hashed-5678",
            Role = AttendantRole.Attendant
        });
        _dbContext.Shifts.Add(new Shift
        {
            Id = shiftId,
            AttendantId = attendantId,
            OpenedAt = ServerNow.AddHours(-1),
            OpeningFloat = 100.00m,
            Status = ShiftStatus.Open
        });
        _dbContext.Sales.Add(new Sale
        {
            Id = _saleId,
            ShiftId = shiftId,
            OccurredAt = ServerNow.AddMinutes(-10)
        });
        _dbContext.SaveChanges();

        _sut = new TransactionIngestionService(_dbContext, new FixedTimeProvider(ServerNow));
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
    public async Task IngestAsync_NewTransaction_PersistsServerOwnedSyncState()
    {
        #region Arrange
        DateTimeOffset clientCreatedAt = new(2026, 9, 4, 9, 30, 0, TimeSpan.FromHours(2));
        DateTimeOffset untrustedClientSyncTime = ServerNow.AddDays(-5);
        TransactionDto transaction = CreateTransaction(
            Guid.NewGuid(),
            _saleId,
            123.45m,
            clientCreatedAt,
            TransactionStatus.Failed,
            untrustedClientSyncTime);
        #endregion

        #region Act
        TransactionIngestionResult result = await _sut.IngestAsync([transaction]);
        #endregion

        #region Assert
        Transaction persisted = await _dbContext.Transactions.SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(TransactionIngestionStatus.Success));
            Assert.That(result.Items.Single().Status, Is.EqualTo(TransactionIngestionItemStatus.Inserted));
            Assert.That(persisted.Status, Is.EqualTo(TransactionStatus.Synced));
            Assert.That(persisted.SyncedAt, Is.EqualTo(ServerNow));
            Assert.That(persisted.CreatedAt, Is.EqualTo(clientCreatedAt.ToUniversalTime()));
            Assert.That(persisted.Amount, Is.EqualTo(123.45m));
        });
        #endregion
    }

    [Test]
    public async Task IngestAsync_MultipleNewTransactions_PersistsCompleteBatch()
    {
        #region Arrange
        TransactionDto first = CreateTransaction(Guid.NewGuid(), _saleId, 40.00m);
        TransactionDto second = CreateTransaction(Guid.NewGuid(), _saleId, 60.00m);
        #endregion

        #region Act
        TransactionIngestionResult result = await _sut.IngestAsync([first, second]);
        #endregion

        #region Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(TransactionIngestionStatus.Success));
            Assert.That(result.Items.Select(item => item.TransactionId),
                Is.EqualTo(new[] { first.Id, second.Id }));
            Assert.That(result.Items.Select(item => item.SyncedAt),
                Is.All.EqualTo(ServerNow));
            Assert.That(_dbContext.Transactions.Count(), Is.EqualTo(2));
        });
        #endregion
    }

    [Test]
    public async Task IngestAsync_EmptyBatch_ReturnsSuccessWithoutWrites()
    {
        #region Arrange
        TransactionDto[] transactions = [];
        #endregion

        #region Act
        TransactionIngestionResult result = await _sut.IngestAsync(transactions);
        #endregion

        #region Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(TransactionIngestionStatus.Success));
            Assert.That(result.Items, Is.Empty);
            Assert.That(_dbContext.Transactions, Is.Empty);
        });
        #endregion
    }

    [Test]
    public void IngestAsync_CancelledToken_PropagatesCancellation()
    {
        #region Arrange
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        TransactionDto transaction = CreateTransaction(Guid.NewGuid(), _saleId);
        #endregion

        #region Act & Assert
        Assert.ThrowsAsync<OperationCanceledException>(() =>
            _sut.IngestAsync([transaction], cancellationSource.Token));
        #endregion
    }
    #endregion

    #region Private Methods
    private static TransactionDto CreateTransaction(
        Guid id,
        Guid saleId,
        decimal amount = 50.00m,
        DateTimeOffset? createdAt = null,
        TransactionStatus status = TransactionStatus.Pending,
        DateTimeOffset? syncedAt = null)
    {
        return new TransactionDto(
            id,
            saleId,
            PaymentMethod.Cash,
            amount,
            status,
            createdAt ?? ServerNow.AddMinutes(-5),
            syncedAt);
    }
    #endregion

    #region Test Types
    private sealed class FixedTimeProvider : TimeProvider
    {
        #region Fields
        private readonly DateTimeOffset _utcNow;
        #endregion

        #region Constructor
        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }
        #endregion

        #region Public Methods
        public override DateTimeOffset GetUtcNow() => _utcNow;
        #endregion
    }

    private sealed class FailingBackofficeDbContext : BackofficeDbContext
    {
        #region Constructor
        public FailingBackofficeDbContext(DbContextOptions<BackofficeDbContext> options)
            : base(options)
        {
        }
        #endregion

        #region Properties
        public bool FailNextSave { get; set; }
        #endregion

        #region Overrides
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (FailNextSave)
            {
                FailNextSave = false;
                throw new DbUpdateException("Synthetic uniqueness race for the service boundary test.");
            }

            return base.SaveChangesAsync(cancellationToken);
        }
        #endregion
    }
    #endregion
}
