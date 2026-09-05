namespace MAUI_POS_DASH.Web.Tests.Api;

[TestFixture]
public class TransactionsApiTests
{
    #region Fields
    private static readonly DateTimeOffset ServerNow = new(2026, 9, 4, 8, 0, 0, TimeSpan.Zero);
    private SqliteConnection _connection = null!;
    private WebApplicationFactory<global::Program> _factory = null!;
    private HttpClient _client = null!;
    private Guid _saleId;
    #endregion

    #region Setup
    [SetUp]
    public void SetUp()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _factory = CreateFactory();
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        using IServiceScope scope = _factory.Services.CreateScope();
        BackofficeDbContext dbContext = scope.ServiceProvider.GetRequiredService<BackofficeDbContext>();
        dbContext.Database.EnsureCreated();
        _saleId = SeedSale(dbContext);
    }
    #endregion

    #region Teardown
    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
        _connection.Dispose();
    }
    #endregion

    #region Tests
    [Test]
    public async Task PostTransactions_ValidBatch_ReturnsPerItemSuccess()
    {
        #region Arrange
        TransactionDto transaction = CreateTransaction(Guid.NewGuid(), _saleId);
        #endregion

        #region Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/transactions", new[] { transaction });
        TransactionIngestionItemResult[]? body =
            await response.Content.ReadFromJsonAsync<TransactionIngestionItemResult[]>();
        #endregion

        #region Assert
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body, Has.Length.EqualTo(1));
            Assert.That(body![0].TransactionId, Is.EqualTo(transaction.Id));
            Assert.That(body[0].Status, Is.EqualTo(TransactionIngestionItemStatus.Inserted));
        });
        #endregion
    }

    [Test]
    public async Task PostTransactions_InvalidBatch_ReturnsBodyBearingBadRequest()
    {
        #region Arrange
        TransactionDto invalid = CreateTransaction(Guid.Empty, _saleId);
        #endregion

        #region Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/transactions", new[] { invalid });
        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        #endregion

        #region Assert
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
            Assert.That(problem?.Detail, Does.Contain("index 0"));
        });
        #endregion
    }

    [Test]
    public async Task PostTransactions_MissingSale_ReturnsBodyBearingConflict()
    {
        #region Arrange
        Guid missingSaleId = Guid.NewGuid();
        TransactionDto transaction = CreateTransaction(Guid.NewGuid(), missingSaleId);
        #endregion

        #region Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/transactions", new[] { transaction });
        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        #endregion

        #region Assert
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
            Assert.That(problem?.Detail, Does.Contain(missingSaleId.ToString()));
        });
        #endregion
    }

    [TestCase(TransactionIngestionStatus.TransactionConflict)]
    [TestCase(TransactionIngestionStatus.PersistenceConflict)]
    public async Task PostTransactions_ServiceConflict_ReturnsBodyBearingConflict(
        TransactionIngestionStatus status)
    {
        #region Arrange
        Guid offendingId = Guid.NewGuid();
        TransactionIngestionResult forcedResult = new(
            status,
            [],
            [offendingId],
            "Safe service conflict detail.");
        using WebApplicationFactory<global::Program> factory = CreateFactory(forcedResult);
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        TransactionDto transaction = CreateTransaction(Guid.NewGuid(), _saleId);
        #endregion

        #region Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/transactions", new[] { transaction });
        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        #endregion

        #region Assert
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
            Assert.That(problem?.Detail, Does.Contain("Safe service conflict detail"));
        });
        #endregion
    }
    #endregion

    #region Private Methods
    private WebApplicationFactory<global::Program> CreateFactory(
        TransactionIngestionResult? forcedResult = null)
    {
        return new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<BackofficeDbContext>>();
                services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<BackofficeDbContext>>();
                services.RemoveAll<BackofficeDbContext>();
                services.AddDbContext<BackofficeDbContext>(options => options.UseSqlite(_connection));
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(ServerNow));

                if (forcedResult is not null)
                {
                    services.RemoveAll<ITransactionIngestionService>();
                    services.AddScoped<ITransactionIngestionService>(
                        _ => new StubTransactionIngestionService(forcedResult));
                }
            });
        });
    }

    private static Guid SeedSale(BackofficeDbContext dbContext)
    {
        Guid attendantId = Guid.NewGuid();
        Guid shiftId = Guid.NewGuid();
        Guid saleId = Guid.NewGuid();
        dbContext.Attendants.Add(new Attendant
        {
            Id = attendantId,
            Name = "Marge Simpson",
            PinHash = "hashed-5678",
            Role = AttendantRole.Attendant
        });
        dbContext.Shifts.Add(new Shift
        {
            Id = shiftId,
            AttendantId = attendantId,
            OpenedAt = ServerNow.AddHours(-1),
            OpeningFloat = 100.00m,
            Status = ShiftStatus.Open
        });
        dbContext.Sales.Add(new Sale
        {
            Id = saleId,
            ShiftId = shiftId,
            OccurredAt = ServerNow.AddMinutes(-10)
        });
        dbContext.SaveChanges();
        return saleId;
    }

    private static TransactionDto CreateTransaction(Guid id, Guid saleId)
    {
        return new TransactionDto(
            id,
            saleId,
            PaymentMethod.Cash,
            50.00m,
            TransactionStatus.Pending,
            ServerNow.AddMinutes(-5),
            null);
    }
    #endregion

    #region Test Types
    private sealed class FixedTimeProvider : TimeProvider
    {
        #region Fields
        private readonly DateTimeOffset _utcNow;
        #endregion

        #region Constructor
        public FixedTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;
        #endregion

        #region Public Methods
        public override DateTimeOffset GetUtcNow() => _utcNow;
        #endregion
    }

    private sealed class StubTransactionIngestionService : ITransactionIngestionService
    {
        #region Fields
        private readonly TransactionIngestionResult _result;
        #endregion

        #region Constructor
        public StubTransactionIngestionService(TransactionIngestionResult result) => _result = result;
        #endregion

        #region Public Methods
        public Task<TransactionIngestionResult> IngestAsync(
            IReadOnlyList<TransactionDto> transactions,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_result);
        }
        #endregion
    }
    #endregion
}
