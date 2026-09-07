using Microsoft.Data.Sqlite;
using MAUI_POS_DASH.Core.Persistence.Repositories;

namespace MAUI_POS_DASH.Core.Tests.Sales;

[TestFixture]
public class EfSaleRepositoryTests
{
    #region Fields
    private SqliteConnection _connection = null!;
    private TerminalDbContext _dbContext = null!;
    private EfSaleRepository _sut = null!;
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

        _sut = new EfSaleRepository(_dbContext);
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
    public async Task AddSaleWithTransactionAsync_ValidSaleAndTransaction_PersistsBoth()
    {
        #region Arrange
        Guid shiftId = Guid.NewGuid();
        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            ShiftId = shiftId,
            OccurredAt = DateTimeOffset.UtcNow,
            Lines =
            [
                new SaleLine { Id = Guid.NewGuid(), Description = "Fuel", UnitPrice = 250.00m, Quantity = 1 }
            ]
        };
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            SaleId = sale.Id,
            Method = PaymentMethod.FleetCard,
            Amount = 250.00m,
            Status = TransactionStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
        #endregion

        #region Act
        await _sut.AddSaleWithTransactionAsync(sale, transaction);
        #endregion

        #region Assert
        Sale persistedSale = await _dbContext.Sales.Include(s => s.Lines).SingleAsync();
        Transaction persistedTransaction = await _dbContext.Transactions.SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(persistedSale.ShiftId, Is.EqualTo(shiftId));
            Assert.That(persistedSale.Lines, Has.Count.EqualTo(1));
            Assert.That(persistedSale.Lines[0].SaleId, Is.EqualTo(sale.Id));
            Assert.That(persistedSale.Total, Is.EqualTo(250.00m));
            Assert.That(persistedTransaction.SaleId, Is.EqualTo(sale.Id));
            Assert.That(persistedTransaction.Amount, Is.EqualTo(250.00m));
            Assert.That(persistedTransaction.Status, Is.EqualTo(TransactionStatus.Pending));
        });
        #endregion
    }
    #endregion
}
