using MAUI_POS_DASH.Core.Shifts;

namespace MAUI_POS_DASH.Core.Persistence.Repositories;

/// <summary>We implement ITillRepository against TerminalDbContext.</summary>
public class EfTillRepository : ITillRepository
{
    #region Fields
    private readonly TerminalDbContext _dbContext;
    #endregion

    #region Constructor
    public EfTillRepository(TerminalDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    #endregion

    #region Public Methods
    public async Task CreateAsync(Till till, CancellationToken cancellationToken = default)
    {
        _dbContext.Tills.Add(till);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<Till?> GetByShiftIdAsync(Guid shiftId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Tills.SingleOrDefaultAsync(till => till.ShiftId == shiftId, cancellationToken);
    }

    public async Task UpdateAsync(Till till, CancellationToken cancellationToken = default)
    {
        _dbContext.Tills.Update(till);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
    #endregion
}
