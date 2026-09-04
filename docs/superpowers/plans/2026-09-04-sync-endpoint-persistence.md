# Sync Endpoint Persistence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the transaction-sync stub with a thin Minimal API endpoint backed by an idempotent, atomic EF Core ingestion service.

**Architecture:** `TransactionsApi` maps HTTP requests and service outcomes only. A scoped Web-local `TransactionIngestionService` validates complete batches, verifies Sales, detects retries/conflicts, applies server-owned sync state, and writes through `BackofficeDbContext`; a dedicated NUnit project tests the service against SQLite and the route through ASP.NET Core's test host.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs, EF Core 10, SQLite test provider, NUnit 4, `Microsoft.AspNetCore.Mvc.Testing`, `TimeProvider`.

---

### Task 1: Add the Web test project

**Files:**
- Create: `MAUI-POS-DASH.Web.Tests/MAUI-POS-DASH.Web.Tests.csproj`
- Create: `MAUI-POS-DASH.Web.Tests/GlobalUsings.cs`
- Modify: `MAUI-POS-DASH.slnx`

- [ ] **Step 1: Create the test project file**

Create `MAUI-POS-DASH.Web.Tests/MAUI-POS-DASH.Web.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.0" />
    <PackageReference Include="Microsoft.Data.Sqlite" Version="10.0.11" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.9.0" />
    <PackageReference Include="NUnit" Version="4.6.1" />
    <PackageReference Include="NUnit3TestAdapter" Version="6.3.0" />
    <PackageReference Include="NUnit.Analyzers" Version="4.6.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\MAUI-POS-DASH.Web\MAUI-POS-DASH.Web.csproj" />
    <ProjectReference Include="..\MAUI-POS-DASH.Core.Persistence\MAUI-POS-DASH.Core.Persistence.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Add test global usings**

Create `MAUI-POS-DASH.Web.Tests/GlobalUsings.cs`:

```csharp
global using System.Net;
global using System.Net.Http.Json;
global using MAUI_POS_DASH.Core.Contracts;
global using MAUI_POS_DASH.Core.Domain;
global using MAUI_POS_DASH.Core.Persistence;
global using Microsoft.AspNetCore.Mvc;
global using Microsoft.AspNetCore.Mvc.Testing;
global using Microsoft.AspNetCore.TestHost;
global using Microsoft.Data.Sqlite;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.DependencyInjection.Extensions;
global using NUnit.Framework;
```

- [ ] **Step 3: Add the project to the solution**

Run:

```powershell
dotnet sln MAUI-POS-DASH.slnx add MAUI-POS-DASH.Web.Tests/MAUI-POS-DASH.Web.Tests.csproj
dotnet restore MAUI-POS-DASH.slnx
```

Expected: the solution lists `MAUI-POS-DASH.Web.Tests`, and restore succeeds.

- [ ] **Step 4: Verify the empty test project builds**

Run:

```powershell
dotnet test MAUI-POS-DASH.Web.Tests/MAUI-POS-DASH.Web.Tests.csproj --no-restore
```

Expected: exit code 0 with no discovered tests.

- [ ] **Step 5: Commit the test scaffold**

```powershell
git add MAUI-POS-DASH.Web.Tests MAUI-POS-DASH.slnx
git commit -m "Add Web test project" -m "Builder"
```

### Task 2: Ingest a new transaction with server-owned sync state

**Files:**
- Create: `MAUI-POS-DASH.Web.Tests/Services/TransactionIngestionServiceTests.cs`
- Create: `MAUI-POS-DASH.Web/Services/ITransactionIngestionService.cs`
- Create: `MAUI-POS-DASH.Web/Services/TransactionIngestionResult.cs`
- Create: `MAUI-POS-DASH.Web/Services/TransactionIngestionService.cs`
- Modify: `MAUI-POS-DASH.Web.Tests/GlobalUsings.cs`
- Modify: `MAUI-POS-DASH.Web/GlobalUsings.cs`

- [ ] **Step 1: Write the first failing service tests**

Create `MAUI-POS-DASH.Web.Tests/Services/TransactionIngestionServiceTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run the tests to verify RED**

Run:

```powershell
dotnet test MAUI-POS-DASH.Web.Tests/MAUI-POS-DASH.Web.Tests.csproj --no-restore --filter TransactionIngestionServiceTests
```

Expected: compilation fails because `MAUI_POS_DASH.Web.Services` and the ingestion types do not exist.

- [ ] **Step 3: Add the service contract and result model**

Create `MAUI-POS-DASH.Web/Services/ITransactionIngestionService.cs`:

```csharp
namespace MAUI_POS_DASH.Web.Services;

/// <summary>
/// We accept terminal transaction batches into the back-office store without exposing EF Core to
/// the HTTP endpoint.
/// </summary>
public interface ITransactionIngestionService
{
    /// <summary>
    /// We validate and atomically ingest one complete batch, treating identical retries as
    /// successful no-ops.
    /// </summary>
    Task<TransactionIngestionResult> IngestAsync(
        IReadOnlyList<TransactionDto> transactions,
        CancellationToken cancellationToken = default);
}
```

Create `MAUI-POS-DASH.Web/Services/TransactionIngestionResult.cs`:

```csharp
namespace MAUI_POS_DASH.Web.Services;

/// <summary>We describe the overall outcome of one complete ingestion request.</summary>
public enum TransactionIngestionStatus
{
    /// <summary>Every transaction was inserted or already existed identically.</summary>
    Success,
    /// <summary>The request contains a malformed or internally duplicated transaction.</summary>
    InvalidRequest,
    /// <summary>At least one referenced Sale does not exist in the back-office store.</summary>
    MissingSale,
    /// <summary>An existing transaction ID has different terminal-owned values.</summary>
    TransactionConflict,
    /// <summary>The database rejected a write after preflight checks completed.</summary>
    PersistenceConflict
}

/// <summary>We classify what happened to one successfully accepted transaction.</summary>
public enum TransactionIngestionItemStatus
{
    /// <summary>The service inserted the transaction during this request.</summary>
    Inserted,
    /// <summary>The same transaction had already been accepted.</summary>
    AlreadySynced
}

/// <summary>We return the server-owned result for one accepted transaction.</summary>
/// <param name="TransactionId">The terminal-provided transaction identity.</param>
/// <param name="Status">Whether this request inserted the row or found an identical retry.</param>
/// <param name="SyncedAt">The authoritative timestamp currently stored by the server.</param>
public sealed record TransactionIngestionItemResult(
    Guid TransactionId,
    TransactionIngestionItemStatus Status,
    DateTimeOffset? SyncedAt);

/// <summary>We return one overall status plus item results or the IDs that blocked acceptance.</summary>
/// <param name="Status">The complete-batch outcome.</param>
/// <param name="Items">Per-item results when the batch succeeds.</param>
/// <param name="OffendingIds">Sale or transaction IDs responsible for a rejected batch.</param>
/// <param name="Detail">A safe explanation suitable for a Problem Details response.</param>
public sealed record TransactionIngestionResult(
    TransactionIngestionStatus Status,
    IReadOnlyList<TransactionIngestionItemResult> Items,
    IReadOnlyList<Guid> OffendingIds,
    string? Detail = null);
```

- [ ] **Step 4: Add the service-layer global usings**

Add to `MAUI-POS-DASH.Web.Tests/GlobalUsings.cs`:

```csharp
global using MAUI_POS_DASH.Web.Services;
```

Add to `MAUI-POS-DASH.Web/GlobalUsings.cs`:

```csharp
global using MAUI_POS_DASH.Core.Domain;
```

- [ ] **Step 5: Add the minimal happy-path implementation**

Create `MAUI-POS-DASH.Web/Services/TransactionIngestionService.cs`:

```csharp
namespace MAUI_POS_DASH.Web.Services;

/// <summary>
/// We own transaction-ingestion rules and persistence so the Minimal API remains an HTTP adapter.
/// </summary>
public class TransactionIngestionService : ITransactionIngestionService
{
    #region Fields
    private readonly BackofficeDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    #endregion

    #region Constructor
    /// <summary>We create the ingestion boundary over the scoped database and server clock.</summary>
    public TransactionIngestionService(BackofficeDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }
    #endregion

    #region Public Methods
    /// <inheritdoc />
    public async Task<TransactionIngestionResult> IngestAsync(
        IReadOnlyList<TransactionDto> transactions,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset serverTimestamp = _timeProvider.GetUtcNow();
        List<Transaction> entities = transactions.Select(transaction => new Transaction
        {
            Id = transaction.Id,
            SaleId = transaction.SaleId,
            Method = transaction.Method,
            Amount = transaction.Amount,
            Status = TransactionStatus.Synced,
            CreatedAt = transaction.CreatedAt.ToUniversalTime(),
            SyncedAt = serverTimestamp
        }).ToList();

        _dbContext.Transactions.AddRange(entities);
        await _dbContext.SaveChangesAsync(cancellationToken);

        TransactionIngestionItemResult[] items = entities
            .Select(entity => new TransactionIngestionItemResult(
                entity.Id,
                TransactionIngestionItemStatus.Inserted,
                entity.SyncedAt))
            .ToArray();

        return new TransactionIngestionResult(
            TransactionIngestionStatus.Success,
            items,
            []);
    }
    #endregion
}
```

- [ ] **Step 6: Run the targeted tests to verify GREEN**

Run the Step 2 command again.

Expected: 4 tests pass.

- [ ] **Step 7: Commit the first service slice**

```powershell
git add MAUI-POS-DASH.Web/Services MAUI-POS-DASH.Web/GlobalUsings.cs MAUI-POS-DASH.Web.Tests/Services MAUI-POS-DASH.Web.Tests/GlobalUsings.cs
git commit -m "Add transaction ingestion service" -m "Builder"
```

### Task 3: Validate complete batches and require existing Sales

**Files:**
- Modify: `MAUI-POS-DASH.Web.Tests/Services/TransactionIngestionServiceTests.cs`
- Modify: `MAUI-POS-DASH.Web/Services/TransactionIngestionService.cs`

- [ ] **Step 1: Add failing validation and dependency tests**

Add these methods inside the test fixture's `Tests` region:

```csharp
[Test]
public async Task IngestAsync_InvalidTransaction_ReturnsInvalidRequestWithoutWrites()
{
    #region Arrange
    TransactionDto invalid = CreateTransaction(Guid.Empty, _saleId);
    #endregion

    #region Act
    TransactionIngestionResult result = await _sut.IngestAsync([invalid]);
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
    TransactionIngestionResult result = await _sut.IngestAsync([first, duplicate]);
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
    TransactionIngestionResult result = await _sut.IngestAsync([valid, missing]);
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
```

Add one parameterized test covering the remaining invalid fields:

```csharp
[TestCaseSource(nameof(InvalidTransactionCases))]
public async Task IngestAsync_InvalidField_ReturnsInvalidRequestWithoutWrites(TransactionDto invalid)
{
    #region Arrange
    TransactionDto transaction = invalid;
    #endregion

    #region Act
    TransactionIngestionResult result = await _sut.IngestAsync([transaction]);
    #endregion

    #region Assert
    Assert.Multiple(() =>
    {
        Assert.That(result.Status, Is.EqualTo(TransactionIngestionStatus.InvalidRequest));
        Assert.That(_dbContext.Transactions, Is.Empty);
    });
    #endregion
}
```

Add this source inside `Private Methods`:

```csharp
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
    yield return new TestCaseData(CreateTransaction(
            Guid.NewGuid(), saleId, 50.00m, default(DateTimeOffset)))
        .SetName("IngestAsync_DefaultCreatedAt_ReturnsInvalidRequestWithoutWrites");
}
```

- [ ] **Step 2: Run the new tests to verify RED**

Run:

```powershell
dotnet test MAUI-POS-DASH.Web.Tests/MAUI-POS-DASH.Web.Tests.csproj --no-restore --filter TransactionIngestionServiceTests
```

Expected: invalid requests are inserted or fail at EF, duplicate IDs throw, and missing Sales reach an FK failure instead of returning the designed statuses.

- [ ] **Step 3: Add validation and Sale preflight**

Replace `IngestAsync` with this version and add the private helpers:

```csharp
public async Task<TransactionIngestionResult> IngestAsync(
    IReadOnlyList<TransactionDto> transactions,
    CancellationToken cancellationToken = default)
{
    TransactionIngestionResult? validationFailure = Validate(transactions);
    if (validationFailure is not null)
    {
        return validationFailure;
    }

    if (transactions.Count == 0)
    {
        return Success([]);
    }

    Guid[] requestedSaleIds = transactions
        .Select(transaction => transaction.SaleId)
        .Distinct()
        .ToArray();
    Guid[] existingSaleIds = await _dbContext.Sales
        .Where(sale => requestedSaleIds.Contains(sale.Id))
        .Select(sale => sale.Id)
        .ToArrayAsync(cancellationToken);
    Guid[] missingSaleIds = requestedSaleIds
        .Except(existingSaleIds)
        .Order()
        .ToArray();

    if (missingSaleIds.Length > 0)
    {
        return Failure(
            TransactionIngestionStatus.MissingSale,
            missingSaleIds,
            "One or more referenced sales do not exist.");
    }

    DateTimeOffset serverTimestamp = _timeProvider.GetUtcNow();
    List<Transaction> entities = transactions.Select(transaction => new Transaction
    {
        Id = transaction.Id,
        SaleId = transaction.SaleId,
        Method = transaction.Method,
        Amount = transaction.Amount,
        Status = TransactionStatus.Synced,
        CreatedAt = transaction.CreatedAt.ToUniversalTime(),
        SyncedAt = serverTimestamp
    }).ToList();

    _dbContext.Transactions.AddRange(entities);
    await _dbContext.SaveChangesAsync(cancellationToken);

    return Success(entities.Select(entity => new TransactionIngestionItemResult(
        entity.Id,
        TransactionIngestionItemStatus.Inserted,
        entity.SyncedAt)).ToArray());
}

private static TransactionIngestionResult? Validate(IReadOnlyList<TransactionDto> transactions)
{
    HashSet<Guid> transactionIds = [];

    for (int index = 0; index < transactions.Count; index++)
    {
        TransactionDto transaction = transactions[index];
        string? detail = transaction switch
        {
            { Id: var id } when id == Guid.Empty => $"Transaction at index {index} must have a non-empty ID.",
            { SaleId: var saleId } when saleId == Guid.Empty => $"Transaction at index {index} must reference a Sale.",
            { Method: var method } when !Enum.IsDefined(method) => $"Transaction at index {index} has an unsupported payment method.",
            { Amount: <= 0 } => $"Transaction at index {index} must have a positive amount.",
            { CreatedAt: var createdAt } when createdAt == default => $"Transaction at index {index} must have a creation timestamp.",
            _ => null
        };

        if (detail is not null)
        {
            return Failure(TransactionIngestionStatus.InvalidRequest, [transaction.Id], detail);
        }

        if (!transactionIds.Add(transaction.Id))
        {
            return Failure(
                TransactionIngestionStatus.InvalidRequest,
                [transaction.Id],
                $"Transaction {transaction.Id} appears more than once in this request.");
        }
    }

    return null;
}

private static TransactionIngestionResult Success(
    IReadOnlyList<TransactionIngestionItemResult> items)
{
    return new TransactionIngestionResult(TransactionIngestionStatus.Success, items, []);
}

private static TransactionIngestionResult Failure(
    TransactionIngestionStatus status,
    IReadOnlyList<Guid> offendingIds,
    string detail)
{
    return new TransactionIngestionResult(status, [], offendingIds, detail);
}
```

- [ ] **Step 4: Run the targeted tests to verify GREEN**

Run the Step 2 command again.

Expected: 12 tests pass (the parameterized method contributes five cases).

- [ ] **Step 5: Commit validation and dependency checks**

```powershell
git add MAUI-POS-DASH.Web/Services/TransactionIngestionService.cs MAUI-POS-DASH.Web.Tests/Services/TransactionIngestionServiceTests.cs
git commit -m "Validate transaction sync batches" -m "Builder"
```

### Task 4: Make ingestion idempotent and conflict-safe

**Files:**
- Modify: `MAUI-POS-DASH.Web.Tests/Services/TransactionIngestionServiceTests.cs`
- Modify: `MAUI-POS-DASH.Web/Services/TransactionIngestionService.cs`

- [ ] **Step 1: Add failing retry and conflict tests**

Add these methods inside the test fixture's `Tests` region:

```csharp
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
    TransactionIngestionResult result = await _sut.IngestAsync([retry]);
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
    TransactionIngestionResult result = await _sut.IngestAsync([existing, added]);
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
    TransactionIngestionResult result = await _sut.IngestAsync([conflicting, otherwiseNew]);
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
```

- [ ] **Step 2: Run the tests to verify RED**

Run the targeted command from Task 3 Step 2.

Expected: the retry tests fail with duplicate-key exceptions, and the conflict test does not return `TransactionConflict`.

- [ ] **Step 3: Add existing-row classification**

In `IngestAsync`, insert this block after the missing-Sale check and before obtaining the server timestamp:

```csharp
Guid[] requestedTransactionIds = transactions
    .Select(transaction => transaction.Id)
    .ToArray();
Dictionary<Guid, Transaction> existingTransactions = await _dbContext.Transactions
    .Where(transaction => requestedTransactionIds.Contains(transaction.Id))
    .ToDictionaryAsync(transaction => transaction.Id, cancellationToken);
Guid[] conflictingTransactionIds = transactions
    .Where(transaction => existingTransactions.TryGetValue(transaction.Id, out Transaction? existing)
        && !MatchesImmutableFields(transaction, existing))
    .Select(transaction => transaction.Id)
    .Order()
    .ToArray();

if (conflictingTransactionIds.Length > 0)
{
    return Failure(
        TransactionIngestionStatus.TransactionConflict,
        conflictingTransactionIds,
        "One or more transaction IDs already contain different values.");
}
```

Replace the entity projection, add, save, and success-return block with:

```csharp
DateTimeOffset serverTimestamp = _timeProvider.GetUtcNow();
List<Transaction> newEntities = [];
List<TransactionIngestionItemResult> itemResults = new(transactions.Count);

foreach (TransactionDto transaction in transactions)
{
    if (existingTransactions.TryGetValue(transaction.Id, out Transaction? existing))
    {
        itemResults.Add(new TransactionIngestionItemResult(
            transaction.Id,
            TransactionIngestionItemStatus.AlreadySynced,
            existing.SyncedAt));
        continue;
    }

    var entity = new Transaction
    {
        Id = transaction.Id,
        SaleId = transaction.SaleId,
        Method = transaction.Method,
        Amount = transaction.Amount,
        Status = TransactionStatus.Synced,
        CreatedAt = transaction.CreatedAt.ToUniversalTime(),
        SyncedAt = serverTimestamp
    };
    newEntities.Add(entity);
    itemResults.Add(new TransactionIngestionItemResult(
        entity.Id,
        TransactionIngestionItemStatus.Inserted,
        entity.SyncedAt));
}

if (newEntities.Count > 0)
{
    _dbContext.Transactions.AddRange(newEntities);
    await _dbContext.SaveChangesAsync(cancellationToken);
}

return Success(itemResults);
```

Add this private helper:

```csharp
private static bool MatchesImmutableFields(TransactionDto requested, Transaction existing)
{
    return requested.SaleId == existing.SaleId
        && requested.Method == existing.Method
        && requested.Amount == existing.Amount
        && requested.CreatedAt.ToUniversalTime() == existing.CreatedAt.ToUniversalTime();
}
```

- [ ] **Step 4: Run the targeted tests to verify GREEN**

Expected: 15 tests pass.

- [ ] **Step 5: Commit idempotency**

```powershell
git add MAUI-POS-DASH.Web/Services/TransactionIngestionService.cs MAUI-POS-DASH.Web.Tests/Services/TransactionIngestionServiceTests.cs
git commit -m "Make transaction ingestion idempotent" -m "Builder"
```

### Task 5: Convert database races into retry-safe service conflicts

**Files:**
- Modify: `MAUI-POS-DASH.Web.Tests/Services/TransactionIngestionServiceTests.cs`
- Modify: `MAUI-POS-DASH.Web/Services/TransactionIngestionService.cs`

- [ ] **Step 1: Add the failing persistence-conflict test**

Add inside the test fixture's `Tests` region:

```csharp
[Test]
public async Task IngestAsync_SaveRace_ReturnsPersistenceConflictAndDetachesInsert()
{
    #region Arrange
    TransactionDto transaction = CreateTransaction(Guid.NewGuid(), _saleId);
    _dbContext.FailNextSave = true;
    #endregion

    #region Act
    TransactionIngestionResult result = await _sut.IngestAsync([transaction]);
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
```

- [ ] **Step 2: Run the test to verify RED**

Run:

```powershell
dotnet test MAUI-POS-DASH.Web.Tests/MAUI-POS-DASH.Web.Tests.csproj --no-restore --filter IngestAsync_SaveRace
```

Expected: `DbUpdateException` escapes the service.

- [ ] **Step 3: Catch only EF write conflicts and detach the attempted inserts**

Replace the `if (newEntities.Count > 0)` block with:

```csharp
if (newEntities.Count > 0)
{
    _dbContext.Transactions.AddRange(newEntities);

    try
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateException)
    {
        foreach (Transaction entity in newEntities)
        {
            _dbContext.Entry(entity).State = EntityState.Detached;
        }

        return Failure(
            TransactionIngestionStatus.PersistenceConflict,
            newEntities.Select(entity => entity.Id).Order().ToArray(),
            "The database changed while this batch was being accepted. Retry the complete batch.");
    }
}
```

Do not catch `OperationCanceledException`; cancellation must continue to propagate.

- [ ] **Step 4: Run the service tests to verify GREEN**

Run the full `TransactionIngestionServiceTests` filter.

Expected: 16 tests pass.

- [ ] **Step 5: Commit the persistence boundary**

```powershell
git add MAUI-POS-DASH.Web/Services/TransactionIngestionService.cs MAUI-POS-DASH.Web.Tests/Services/TransactionIngestionServiceTests.cs
git commit -m "Handle transaction persistence races" -m "Builder"
```

### Task 6: Wire the thin HTTP endpoint and verify its observable contract

**Files:**
- Create: `MAUI-POS-DASH.Web.Tests/Api/TransactionsApiTests.cs`
- Modify: `MAUI-POS-DASH.Web/Api/TransactionsApi.cs`
- Modify: `MAUI-POS-DASH.Web/GlobalUsings.cs`
- Modify: `MAUI-POS-DASH.Web/Program.cs`

- [ ] **Step 1: Add failing HTTP integration tests**

Create `MAUI-POS-DASH.Web.Tests/Api/TransactionsApiTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run the API tests to verify RED**

Run:

```powershell
dotnet test MAUI-POS-DASH.Web.Tests/MAUI-POS-DASH.Web.Tests.csproj --no-restore --filter TransactionsApiTests
```

Expected: compilation fails because `Program` is internal. This is the intended RED state; Step 3
makes the entry point testable before Step 4 replaces the `501` route behavior.

- [ ] **Step 3: Register the service and expose the test-host entry point**

Add to `MAUI-POS-DASH.Web/GlobalUsings.cs`:

```csharp
global using MAUI_POS_DASH.Web.Services;
```

In `Program.cs`, change the declaration to:

```csharp
/// <summary>We configure and run the MAUI-POS-DASH back-office Web host.</summary>
public partial class Program
```

Add these registrations immediately after `AddDbContext`:

```csharp
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ITransactionIngestionService, TransactionIngestionService>();
```

- [ ] **Step 4: Replace the stub with thin HTTP mapping**

Replace `TransactionsApi.cs` with:

```csharp
namespace MAUI_POS_DASH.Web.Api;

/// <summary>We map transaction-sync HTTP requests onto the ingestion service.</summary>
public static class TransactionsApi
{
    #region Public Methods
    /// <summary>We expose the machine-to-machine endpoint used by terminal sync clients.</summary>
    public static IEndpointRouteBuilder MapTransactionsApi(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/transactions", IngestTransactionsAsync)
            // The MAUI HttpClient has no browser antiforgery token; terminal authentication is a
            // separate follow-up boundary and antiforgery does not secure machine clients.
            .DisableAntiforgery();

        return app;
    }
    #endregion

    #region Private Methods
    private static async Task<IResult> IngestTransactionsAsync(
        List<TransactionDto> transactions,
        ITransactionIngestionService ingestionService,
        CancellationToken cancellationToken)
    {
        TransactionIngestionResult result =
            await ingestionService.IngestAsync(transactions, cancellationToken);

        return result.Status switch
        {
            TransactionIngestionStatus.Success => TypedResults.Ok(result.Items),
            TransactionIngestionStatus.InvalidRequest => TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid transaction batch",
                detail: result.Detail),
            TransactionIngestionStatus.MissingSale => TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Referenced sale is missing",
                detail: WithIds(result.Detail, result.OffendingIds)),
            TransactionIngestionStatus.TransactionConflict => TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Transaction identity conflict",
                detail: WithIds(result.Detail, result.OffendingIds)),
            TransactionIngestionStatus.PersistenceConflict => TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Transaction persistence conflict",
                detail: WithIds(result.Detail, result.OffendingIds)),
            _ => TypedResults.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Unknown transaction ingestion result",
                detail: "The ingestion service returned an unsupported outcome.")
        };
    }

    private static string WithIds(string? detail, IReadOnlyList<Guid> ids)
    {
        string prefix = detail ?? "The transaction batch could not be accepted.";
        return ids.Count == 0
            ? prefix
            : $"{prefix} IDs: {string.Join(", ", ids)}.";
    }
    #endregion
}
```

- [ ] **Step 5: Run API and full Web tests to verify GREEN**

Run:

```powershell
dotnet test MAUI-POS-DASH.Web.Tests/MAUI-POS-DASH.Web.Tests.csproj --no-restore --filter TransactionsApiTests
dotnet test MAUI-POS-DASH.Web.Tests/MAUI-POS-DASH.Web.Tests.csproj --no-restore
```

Expected: 5 API cases pass, then all 21 Web test cases pass with no failures or warnings.

- [ ] **Step 6: Commit endpoint wiring**

```powershell
git add MAUI-POS-DASH.Web MAUI-POS-DASH.Web.Tests/Api
git commit -m "Persist transaction sync batches" -m "Builder"
```

### Task 7: Update status documentation and verify the complete branch

**Files:**
- Modify: `docs/ARCHITECTURE.md`
- Modify: `docs/superpowers/plans/2026-09-04-sync-endpoint-persistence.md`

- [ ] **Step 1: Update the architecture status list**

Replace the current `POST /api/transactions` stub-status bullet with:

```markdown
- `POST /api/transactions` persists idempotent transaction batches whose Sales already exist in
  the back office. Sale/Shift graph synchronization and terminal authentication remain separate
  follow-up work before the sync boundary is production-complete.
```

- [ ] **Step 2: Restore tools and run every automated verification gate**

Run:

```powershell
dotnet tool restore
dotnet restore MAUI-POS-DASH.slnx
dotnet test MAUI-POS-DASH.Core.Tests/MAUI-POS-DASH.Core.Tests.csproj --no-restore
dotnet test MAUI-POS-DASH.Web.Tests/MAUI-POS-DASH.Web.Tests.csproj --no-restore
dotnet build MAUI-POS-DASH.slnx --no-restore
dotnet format MAUI-POS-DASH.slnx --verify-no-changes --no-restore
git diff --check
```

Expected: tools and packages restore; Core and Web tests report zero failures; the complete
solution builds with zero warnings/errors; formatting and whitespace checks exit 0.

- [ ] **Step 3: Check whether a real Postgres smoke is available**

Run:

```powershell
docker info
```

If Docker is unavailable, record the Postgres smoke as skipped and rely only on the explicitly
reported SQLite service/integration coverage, and skip Step 4. If Docker is available, run:

```powershell
$syncContainer = "mauiposdash-sync-builder-20260904"
$syncConnection = "Host=localhost;Port=55432;Database=mauiposdash_sync_builder;Username=postgres;Password=postgres"
$existingContainer = docker ps -a --filter "name=^/$syncContainer$" --format "{{.Names}}"
if ($existingContainer) { throw "Refusing to replace existing container $syncContainer" }
docker run --name $syncContainer -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=mauiposdash_sync_builder -p 55432:5432 -d postgres:17-alpine
$syncReady = $false
foreach ($attempt in 1..30) {
    docker exec $syncContainer pg_isready -U postgres -d mauiposdash_sync_builder
    if ($LASTEXITCODE -eq 0) {
        $syncReady = $true
        break
    }
    Start-Sleep -Seconds 1
}
if (-not $syncReady) { throw "Disposable Postgres did not become ready" }
dotnet ef database update --project MAUI-POS-DASH.Core.Persistence --context BackofficeDbContext --connection $syncConnection
$env:ConnectionStrings__Backoffice = $syncConnection
```

Expected: the explicitly named container becomes ready, and the Backoffice migrations apply.

- [ ] **Step 4: Run the Web host and perform the live HTTP smoke**

Run the Web host on an HTTP-only local test port so certificate trust does not obscure endpoint
behavior:

```powershell
dotnet run --no-build --project MAUI-POS-DASH.Web --no-launch-profile --urls http://localhost:5076
```

This step runs only when Step 3 prepared the disposable Postgres instance. From a second shell,
POST this body:

```powershell
$syncBody = @'
[
  {
    "id": "22222222-2222-2222-2222-222222222222",
    "saleId": "33333333-3333-3333-3333-333333333333",
    "method": 0,
    "amount": 50.00,
    "status": 0,
    "createdAt": "2026-09-04T08:00:00Z",
    "syncedAt": null
  }
]
'@
$response = Invoke-WebRequest -Uri http://localhost:5076/api/transactions -Method Post -ContentType application/json -Body $syncBody -SkipHttpErrorCheck
$response.StatusCode
$response.Headers.ContentType
$response.Content
```

Expected against an empty migrated back office: `409 Conflict`, content type
`application/problem+json`, and a detail containing Sale ID
`33333333-3333-3333-3333-333333333333`. Stop the host after the response, then clean up only the
explicit test state created in Step 3:

```powershell
Remove-Item Env:\ConnectionStrings__Backoffice -ErrorAction SilentlyContinue
docker rm -f mauiposdash-sync-builder-20260904
```

- [ ] **Step 5: Review branch scope and mark plan checkboxes accurately**

Run:

```powershell
git status --short
git diff --stat master...HEAD
git diff --name-only master...HEAD
git log --oneline master..HEAD
```

Expected: changes remain limited to `AGENTS.md`, sync documentation, Web endpoint/service/DI,
the new Web test project, and the solution file. No Core, Core.Persistence, Core.Tests, MAUI, or
Architect-owned Attendant Management source file is modified by Builder.

- [ ] **Step 6: Commit verified documentation state**

```powershell
git add docs/ARCHITECTURE.md docs/superpowers/plans/2026-09-04-sync-endpoint-persistence.md
git commit -m "Document transaction sync persistence" -m "Builder"
```

- [ ] **Step 7: Re-run the completion gate after the final commit**

Run:

```powershell
dotnet test MAUI-POS-DASH.Core.Tests/MAUI-POS-DASH.Core.Tests.csproj --no-restore
dotnet test MAUI-POS-DASH.Web.Tests/MAUI-POS-DASH.Web.Tests.csproj --no-restore
dotnet build MAUI-POS-DASH.slnx --no-restore
git status --short --branch
```

Expected: both suites pass, the solution builds with zero warnings/errors, and the Builder
worktree is clean on `code/sync-endpoint-persistence`.
