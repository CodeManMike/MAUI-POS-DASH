using MAUI_POS_DASH.Core.Attendants;

namespace MAUI_POS_DASH.Core.Persistence.Repositories;

/// <summary>
/// We implement IAttendantRepository against TerminalDbContext.
/// </summary>
public class EfAttendantRepository : IAttendantRepository
{
    #region Fields
    private readonly TerminalDbContext _dbContext;
    #endregion

    #region Constructor
    public EfAttendantRepository(TerminalDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    #endregion

    #region Public Methods
    public async Task<IReadOnlyList<Attendant>> GetActiveAttendantsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Attendants
            .Where(attendant => attendant.IsActive)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> AnyExistAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.Attendants.AnyAsync(cancellationToken);
    }

    public Task<Attendant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Attendants.SingleOrDefaultAsync(attendant => attendant.Id == id, cancellationToken);
    }

    public async Task AddAsync(Attendant attendant, CancellationToken cancellationToken = default)
    {
        _dbContext.Attendants.Add(attendant);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Attendant attendant, CancellationToken cancellationToken = default)
    {
        _dbContext.Attendants.Update(attendant);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
    #endregion
}
