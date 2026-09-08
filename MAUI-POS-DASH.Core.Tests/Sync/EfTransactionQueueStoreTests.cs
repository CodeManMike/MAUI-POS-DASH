using Microsoft.Data.Sqlite;
using MAUI_POS_DASH.Core.Persistence.Repositories;

namespace MAUI_POS_DASH.Core.Tests.Sync;

[TestFixture]
public class EfTransactionQueueStoreTests
{
    #region Fields
    private SqliteConnection _connection = null!;
    private TerminalDbContext _dbContext = null!;
    private EfTransactionQueueStore _sut = null!;
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
            PinHash = "hashed-2222",
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

        _sut = new EfTransactionQueueStore(_dbContext);
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
    public async Task GetPendingAsync_PendingTransaction_EagerLoadsSaleAndLines()
    {
        #region Arrange
        // This is what HttpTransactionSyncService depends on to build the Sales half of a sync
        // payload without a second query — if this Include ever regresses, that mapping would
        // silently see a null Sale instead of failing loudly here.
        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            ShiftId = _shiftId,
            OccurredAt = DateTimeOffset.UtcNow,
            Lines = [new SaleLine { Id = Guid.NewGuid(), Description = "Fuel", UnitPrice = 250.00m, Quantity = 1 }]
        };
        _dbContext.Sales.Add(sale);
        _dbContext.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(),
            SaleId = sale.Id,
            Method = PaymentMethod.Cash,
            Amount = 250.00m,
            Status = TransactionStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await _dbContext.SaveChangesAsync();
        #endregion

        #region Act
        IReadOnlyList<Transaction> pending = await _sut.GetPendingAsync();
        #endregion

        #region Assert
        Assert.Multiple(() =>
        {
            Assert.That(pending, Has.Count.EqualTo(1));
            Assert.That(pending[0].Sale, Is.Not.Null);
            Assert.That(pending[0].Sale!.Id, Is.EqualTo(sale.Id));
            Assert.That(pending[0].Sale!.Lines, Has.Count.EqualTo(1));
            Assert.That(pending[0].Sale!.Lines[0].Description, Is.EqualTo("Fuel"));
        });
        #endregion
    }

    [Test]
    public async Task GetPendingAsync_SyncedTransaction_IsExcluded()
    {
        #region Arrange
        var sale = new Sale { Id = Guid.NewGuid(), ShiftId = _shiftId, OccurredAt = DateTimeOffset.UtcNow };
        _dbContext.Sales.Add(sale);
        _dbContext.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(),
            SaleId = sale.Id,
            Method = PaymentMethod.Cash,
            Amount = 100.00m,
            Status = TransactionStatus.Synced,
            CreatedAt = DateTimeOffset.UtcNow,
            SyncedAt = DateTimeOffset.UtcNow
        });
        await _dbContext.SaveChangesAsync();
        #endregion

        #region Act
        IReadOnlyList<Transaction> pending = await _sut.GetPendingAsync();
        #endregion

        #region Assert
        Assert.That(pending, Is.Empty);
        #endregion
    }
    #endregion
}
