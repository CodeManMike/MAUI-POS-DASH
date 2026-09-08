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
        TransactionIngestionResult result = await _sut.IngestAsync([], [transaction]);
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
        TransactionIngestionResult result = await _sut.IngestAsync([], [first, second]);
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
        TransactionIngestionResult result = await _sut.IngestAsync([], transactions);
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
    public async Task IngestAsync_SalesWithNoTransactions_ReturnsInvalidRequestWithoutWrites()
    {
        #region Arrange
        // A Sale never exists to justify itself in this system — without this guard, this shape
        // would hit the empty-transactions early return and silently drop the Sale. SetUp already
        // seeds one unrelated Sale (_saleId), so we assert this new one specifically never lands,
        // not that the table is empty.
        Guid newSaleId = Guid.NewGuid();
        SaleDto sale = CreateSale(newSaleId, new SaleLineDto(Guid.NewGuid(), "Diesel", 22.00m, 10.00m));
        #endregion

        #region Act
        TransactionIngestionResult result = await _sut.IngestAsync([sale], []);
        #endregion

        #region Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(TransactionIngestionStatus.InvalidRequest));
            Assert.That(_dbContext.Sales.Any(s => s.Id == newSaleId), Is.False);
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
            _sut.IngestAsync([], [transaction], cancellationSource.Token));
        #endregion
    }

    [Test]
    public async Task IngestAsync_InvalidTransaction_ReturnsInvalidRequestWithoutWrites()
    {
        #region Arrange
        TransactionDto invalid = CreateTransaction(Guid.Empty, _saleId);
        #endregion

        #region Act
        TransactionIngestionResult result = await _sut.IngestAsync([], [invalid]);
        #endregion

        #region Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(TransactionIngestionStatus.InvalidRequest));
            Assert.That(result.Detail, Does.Contain("index 0"));
            Assert.That(_dbContext.Transactions, Is.Empty);
        });
        #endregion
    }

    [Test]
    public async Task IngestAsync_DuplicateRequestId_ReturnsInvalidRequestWithoutWrites()
    {
        #region Arrange
        Guid transactionId = Guid.NewGuid();
        TransactionDto first = CreateTransaction(transactionId, _saleId);
        TransactionDto duplicate = CreateTransaction(transactionId, _saleId, 75.00m);
        #endregion

        #region Act
        TransactionIngestionResult result = await _sut.IngestAsync([], [first, duplicate]);
        #endregion

        #region Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(TransactionIngestionStatus.InvalidRequest));
            Assert.That(result.OffendingIds, Is.EqualTo(new[] { transactionId }));
            Assert.That(_dbContext.Transactions, Is.Empty);
        });
        #endregion
    }

    [Test]
    public async Task IngestAsync_MissingSaleInBatch_ReturnsMissingSaleWithoutAnyWrites()
    {
        #region Arrange
        Guid missingSaleId = Guid.NewGuid();
        TransactionDto valid = CreateTransaction(Guid.NewGuid(), _saleId);
        TransactionDto missing = CreateTransaction(Guid.NewGuid(), missingSaleId);
        #endregion

        #region Act
        TransactionIngestionResult result = await _sut.IngestAsync([], [valid, missing]);
        #endregion

        #region Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(TransactionIngestionStatus.MissingSale));
            Assert.That(result.OffendingIds, Is.EqualTo(new[] { missingSaleId }));
            Assert.That(_dbContext.Transactions, Is.Empty);
        });
        #endregion
    }

    [TestCaseSource(nameof(InvalidTransactionCases))]
    public async Task IngestAsync_InvalidField_ReturnsInvalidRequestWithoutWrites(TransactionDto invalid)
    {
        #region Arrange
        TransactionDto transaction = invalid;
        #endregion

        #region Act
        TransactionIngestionResult result = await _sut.IngestAsync([], [transaction]);
        #endregion

        #region Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(TransactionIngestionStatus.InvalidRequest));
            Assert.That(_dbContext.Transactions, Is.Empty);
        });
        #endregion
    }

    [Test]
    public async Task IngestAsync_IdenticalRetry_ReturnsAlreadySyncedWithoutDuplicate()
    {
        #region Arrange
        Guid transactionId = Guid.NewGuid();
        DateTimeOffset originalSyncTime = ServerNow.AddHours(-2);
        TransactionDto retry = CreateTransaction(transactionId, _saleId);
        _dbContext.Transactions.Add(new Transaction
        {
            Id = transactionId,
            SaleId = retry.SaleId,
            Method = retry.Method,
            Amount = retry.Amount,
            Status = TransactionStatus.Synced,
            CreatedAt = retry.CreatedAt.ToUniversalTime(),
            SyncedAt = originalSyncTime
        });
        _dbContext.SaveChanges();
        #endregion

        #region Act
        TransactionIngestionResult result = await _sut.IngestAsync([], [retry]);
        #endregion

        #region Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(TransactionIngestionStatus.Success));
            Assert.That(result.Items.Single().Status, Is.EqualTo(TransactionIngestionItemStatus.AlreadySynced));
            Assert.That(result.Items.Single().SyncedAt, Is.EqualTo(originalSyncTime));
            Assert.That(_dbContext.Transactions.Count(), Is.EqualTo(1));
        });
        #endregion
    }

    [Test]
    public async Task IngestAsync_MixedNewAndRetry_ReturnsResultsInRequestOrder()
    {
        #region Arrange
        Guid existingId = Guid.NewGuid();
        TransactionDto existing = CreateTransaction(existingId, _saleId, 40.00m);
        TransactionDto added = CreateTransaction(Guid.NewGuid(), _saleId, 60.00m);
        _dbContext.Transactions.Add(new Transaction
        {
            Id = existing.Id,
            SaleId = existing.SaleId,
            Method = existing.Method,
            Amount = existing.Amount,
            Status = TransactionStatus.Synced,
            CreatedAt = existing.CreatedAt.ToUniversalTime(),
            SyncedAt = ServerNow.AddHours(-1)
        });
        _dbContext.SaveChanges();
        #endregion

        #region Act
        TransactionIngestionResult result = await _sut.IngestAsync([], [existing, added]);
        #endregion

        #region Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(TransactionIngestionStatus.Success));
            Assert.That(result.Items.Select(item => item.TransactionId),
                Is.EqualTo(new[] { existing.Id, added.Id }));
            Assert.That(result.Items.Select(item => item.Status),
                Is.EqualTo(new[]
                {
                    TransactionIngestionItemStatus.AlreadySynced,
                    TransactionIngestionItemStatus.Inserted
                }));
            Assert.That(_dbContext.Transactions.Count(), Is.EqualTo(2));
        });
        #endregion
    }

    [Test]
    public async Task IngestAsync_ExistingIdWithDifferentAmount_ReturnsConflictWithoutAnyWrites()
    {
        #region Arrange
        Guid existingId = Guid.NewGuid();
        TransactionDto original = CreateTransaction(existingId, _saleId, 40.00m);
        _dbContext.Transactions.Add(new Transaction
        {
            Id = original.Id,
            SaleId = original.SaleId,
            Method = original.Method,
            Amount = original.Amount,
            Status = TransactionStatus.Synced,
            CreatedAt = original.CreatedAt.ToUniversalTime(),
            SyncedAt = ServerNow.AddHours(-1)
        });
        _dbContext.SaveChanges();

        TransactionDto conflicting = original with { Amount = 41.00m };
        TransactionDto otherwiseNew = CreateTransaction(Guid.NewGuid(), _saleId, 20.00m);
        #endregion

        #region Act
        TransactionIngestionResult result = await _sut.IngestAsync([], [conflicting, otherwiseNew]);
        #endregion

        #region Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(TransactionIngestionStatus.TransactionConflict));
            Assert.That(result.OffendingIds, Is.EqualTo(new[] { existingId }));
            Assert.That(_dbContext.Transactions.Count(), Is.EqualTo(1));
        });
        #endregion
    }

    [Test]
    public async Task IngestAsync_NullTransactionEntry_ReturnsInvalidRequestWithoutWrites()
    {
        #region Arrange
        // We build the list with a null element the way System.Text.Json would for `[null]` in the
        // request body — TransactionDto's non-nullable annotation doesn't stop deserialization from
        // doing this, so the service has to defend against it explicitly.
        List<TransactionDto> transactions = [null!];
        #endregion

        #region Act
        TransactionIngestionResult result = await _sut.IngestAsync([], transactions);
        #endregion

        #region Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(TransactionIngestionStatus.InvalidRequest));
            Assert.That(result.Detail, Does.Contain("index 0"));
            Assert.That(_dbContext.Transactions, Is.Empty);
        });
        #endregion
    }

    [Test]
    public async Task IngestAsync_SaveRace_ReturnsPersistenceConflictAndDetachesInsert()
    {
        #region Arrange
        TransactionDto transaction = CreateTransaction(Guid.NewGuid(), _saleId);
        _dbContext.FailNextSave = true;
        #endregion

        #region Act
        TransactionIngestionResult result = await _sut.IngestAsync([], [transaction]);
        #endregion

        #region Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(TransactionIngestionStatus.PersistenceConflict));
            Assert.That(result.Detail, Does.Not.Contain("Synthetic"));
            Assert.That(_dbContext.ChangeTracker.Entries<Transaction>(), Is.Empty);
            Assert.That(_dbContext.Transactions.Count(), Is.Zero);
        });
        #endregion
    }

    [Test]
    public async Task IngestAsync_NewSaleIncludedInRequest_PersistsSaleLinesAndTransaction()
    {
        #region Arrange
        Guid newSaleId = Guid.NewGuid();
        SaleDto sale = CreateSale(newSaleId, new SaleLineDto(Guid.NewGuid(), "Unleaded 95", 21.50m, 40.00m));
        TransactionDto transaction = CreateTransaction(Guid.NewGuid(), newSaleId);
        #endregion

        #region Act
        TransactionIngestionResult result = await _sut.IngestAsync([sale], [transaction]);
        #endregion

        #region Assert
        Sale persistedSale = await _dbContext.Sales
            .Include(s => s.Lines)
            .SingleAsync(s => s.Id == newSaleId);
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(TransactionIngestionStatus.Success));
            Assert.That(result.Items.Single().Status, Is.EqualTo(TransactionIngestionItemStatus.Inserted));
            Assert.That(persistedSale.ShiftId, Is.EqualTo(sale.ShiftId));
            Assert.That(persistedSale.Lines.Single().Description, Is.EqualTo("Unleaded 95"));
            Assert.That(_dbContext.Transactions.Count(), Is.EqualTo(1));
        });
        #endregion
    }

    [Test]
    public async Task IngestAsync_SaleIncludedAgainOnRetry_DoesNotDuplicateSale()
    {
        #region Arrange
        Guid newSaleId = Guid.NewGuid();
        SaleDto sale = CreateSale(newSaleId, new SaleLineDto(Guid.NewGuid(), "Diesel", 22.00m, 30.00m));
        TransactionDto transaction = CreateTransaction(Guid.NewGuid(), newSaleId);
        await _sut.IngestAsync([sale], [transaction]);
        #endregion

        #region Act
        TransactionIngestionResult result = await _sut.IngestAsync([sale], [transaction]);
        #endregion

        #region Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(TransactionIngestionStatus.Success));
            Assert.That(result.Items.Single().Status, Is.EqualTo(TransactionIngestionItemStatus.AlreadySynced));
            Assert.That(_dbContext.Sales.Count(s => s.Id == newSaleId), Is.EqualTo(1));
            Assert.That(_dbContext.Transactions.Count(), Is.EqualTo(1));
        });
        #endregion
    }

    [Test]
    public async Task IngestAsync_DuplicateSaleIdInRequest_ReturnsInvalidRequestWithoutWrites()
    {
        #region Arrange
        // Two distinct SaleDto entries sharing an Id that isn't already stored anywhere — without
        // validating this, both land in the same AddRange call and EF's change tracker throws a
        // raw InvalidOperationException for the duplicate key, bypassing every other failure path's
        // ProblemDetails contract.
        Guid duplicateSaleId = Guid.NewGuid();
        SaleDto first = CreateSale(duplicateSaleId, new SaleLineDto(Guid.NewGuid(), "Diesel", 22.00m, 10.00m));
        SaleDto second = CreateSale(duplicateSaleId, new SaleLineDto(Guid.NewGuid(), "Unleaded 95", 21.50m, 5.00m));
        TransactionDto transaction = CreateTransaction(Guid.NewGuid(), duplicateSaleId);
        #endregion

        #region Act
        TransactionIngestionResult result = await _sut.IngestAsync([first, second], [transaction]);
        #endregion

        #region Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(TransactionIngestionStatus.InvalidRequest));
            Assert.That(result.OffendingIds, Is.EqualTo(new[] { duplicateSaleId }));
            Assert.That(_dbContext.Sales.Any(s => s.Id == duplicateSaleId), Is.False);
            Assert.That(_dbContext.Transactions, Is.Empty);
        });
        #endregion
    }

    [Test]
    public async Task IngestAsync_NullSaleEntry_ReturnsInvalidRequestWithoutWrites()
    {
        #region Arrange
        List<SaleDto> sales = [null!];
        TransactionDto transaction = CreateTransaction(Guid.NewGuid(), _saleId);
        #endregion

        #region Act
        TransactionIngestionResult result = await _sut.IngestAsync(sales, [transaction]);
        #endregion

        #region Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(TransactionIngestionStatus.InvalidRequest));
            Assert.That(result.Detail, Does.Contain("index 0"));
            Assert.That(_dbContext.Transactions, Is.Empty);
        });
        #endregion
    }

    [Test]
    public async Task IngestAsync_SaleNeitherStoredNorIncludedInSales_ReturnsMissingSaleWithoutAnyWrites()
    {
        #region Arrange
        Guid missingSaleId = Guid.NewGuid();
        Guid unrelatedSaleId = Guid.NewGuid();
        SaleDto unrelatedSale = CreateSale(unrelatedSaleId, new SaleLineDto(Guid.NewGuid(), "Diesel", 22.00m, 10.00m));
        TransactionDto transaction = CreateTransaction(Guid.NewGuid(), missingSaleId);
        #endregion

        #region Act
        TransactionIngestionResult result = await _sut.IngestAsync([unrelatedSale], [transaction]);
        #endregion

        #region Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(TransactionIngestionStatus.MissingSale));
            Assert.That(result.OffendingIds, Is.EqualTo(new[] { missingSaleId }));
            Assert.That(_dbContext.Transactions, Is.Empty);
            Assert.That(_dbContext.Sales.Any(s => s.Id == unrelatedSaleId), Is.False);
        });
        #endregion
    }
    #endregion

    #region Private Methods
    private static IEnumerable<TestCaseData> InvalidTransactionCases()
    {
        Guid saleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        DateTimeOffset createdAt = ServerNow.AddMinutes(-5);

        yield return new TestCaseData(CreateTransaction(Guid.NewGuid(), Guid.Empty))
            .SetName("IngestAsync_EmptySaleId_ReturnsInvalidRequestWithoutWrites");
        yield return new TestCaseData(new TransactionDto(
                Guid.NewGuid(), saleId, (PaymentMethod)999, 50.00m,
                TransactionStatus.Pending, createdAt, null))
            .SetName("IngestAsync_UndefinedPaymentMethod_ReturnsInvalidRequestWithoutWrites");
        yield return new TestCaseData(CreateTransaction(Guid.NewGuid(), saleId, 0m))
            .SetName("IngestAsync_ZeroAmount_ReturnsInvalidRequestWithoutWrites");
        yield return new TestCaseData(CreateTransaction(Guid.NewGuid(), saleId, -1m))
            .SetName("IngestAsync_NegativeAmount_ReturnsInvalidRequestWithoutWrites");
        yield return new TestCaseData(CreateTransaction(Guid.NewGuid(), saleId, 50.001m))
            .SetName("IngestAsync_AmountWithMoreThanTwoDecimalPlaces_ReturnsInvalidRequestWithoutWrites");
        yield return new TestCaseData(CreateTransaction(Guid.NewGuid(), saleId, 10_000_000_000_000_000.00m))
            .SetName("IngestAsync_AmountExceedingColumnRange_ReturnsInvalidRequestWithoutWrites");
        yield return new TestCaseData(CreateTransaction(
                Guid.NewGuid(), saleId, 50.00m, default(DateTimeOffset)))
            .SetName("IngestAsync_DefaultCreatedAt_ReturnsInvalidRequestWithoutWrites");
    }

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

    private static SaleDto CreateSale(Guid id, params SaleLineDto[] lines)
    {
        return new SaleDto(id, Guid.NewGuid(), ServerNow.AddMinutes(-10), lines);
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
