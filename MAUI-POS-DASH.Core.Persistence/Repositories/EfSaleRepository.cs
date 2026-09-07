using MAUI_POS_DASH.Core.Sales;

namespace MAUI_POS_DASH.Core.Persistence.Repositories;

/// <summary>We implement ISaleRepository against TerminalDbContext.</summary>
public class EfSaleRepository : ISaleRepository
{
    #region Fields
    private readonly TerminalDbContext _dbContext;
    #endregion

    #region Constructor
    public EfSaleRepository(TerminalDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    #endregion

    #region Public Methods
    public async Task AddSaleWithTransactionAsync(Sale sale, Transaction transaction, CancellationToken cancellationToken = default)
    {
        _dbContext.Sales.Add(sale);
        _dbContext.Transactions.Add(transaction);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
    #endregion
}
