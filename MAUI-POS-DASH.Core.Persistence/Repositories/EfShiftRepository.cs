using MAUI_POS_DASH.Core.Shifts;

namespace MAUI_POS_DASH.Core.Persistence.Repositories;

/// <summary>
/// We implement IShiftRepository against TerminalDbContext — the real, on-device storage a
/// running terminal uses.
/// </summary>
public class EfShiftRepository : IShiftRepository
{
    #region Fields
    private readonly TerminalDbContext _dbContext;
    #endregion

    #region Constructor
    public EfShiftRepository(TerminalDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    #endregion

    #region Public Methods
    public Task<Shift?> GetActiveShiftAsync(Guid attendantId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Shifts
            .Where(shift => shift.AttendantId == attendantId && shift.Status == ShiftStatus.Open)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<Shift?> GetByIdAsync(Guid shiftId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Shifts.SingleOrDefaultAsync(shift => shift.Id == shiftId, cancellationToken);
    }

    public async Task<Shift> OpenAsync(Shift shift, CancellationToken cancellationToken = default)
    {
        _dbContext.Shifts.Add(shift);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return shift;
    }

    public async Task CloseAsync(Shift shift, CancellationToken cancellationToken = default)
    {
        _dbContext.Shifts.Update(shift);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
    #endregion
}
