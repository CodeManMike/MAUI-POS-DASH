using Microsoft.Data.Sqlite;
using MAUI_POS_DASH.Core.Persistence.Repositories;
using MAUI_POS_DASH.Core.Shifts;

namespace MAUI_POS_DASH.Core.Tests.Shifts;

[TestFixture]
public class ShiftServiceTests
{
    #region Fields
    private SqliteConnection _connection = null!;
    private TerminalDbContext _dbContext = null!;
    private ShiftService _sut = null!;
    private Guid _attendantId;
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

        _attendantId = Guid.NewGuid();
        _dbContext.Attendants.Add(new Attendant
        {
            Id = _attendantId,
            Name = "Marge Simpson",
            PinHash = "hashed-5678",
            Role = AttendantRole.Attendant
        });
        _dbContext.SaveChanges();

        _sut = new ShiftService(new EfShiftRepository(_dbContext));
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
    public async Task OpenShiftAsync_NoActiveShift_CreatesOpenShiftWithGivenFloat()
    {
        #region Arrange
        const decimal openingFloat = 150.00m;
        #endregion

        #region Act
        var shift = await _sut.OpenShiftAsync(_attendantId, openingFloat);
        #endregion

        #region Assert
        Assert.That(shift.Status, Is.EqualTo(ShiftStatus.Open));
        Assert.That(shift.OpeningFloat, Is.EqualTo(openingFloat));
        Assert.That(_dbContext.Shifts.Count(), Is.EqualTo(1));
        #endregion
    }

    [Test]
    public void OpenShiftAsync_AttendantAlreadyHasOpenShift_Throws()
    {
        #region Arrange
        _dbContext.Shifts.Add(new Shift
        {
            Id = Guid.NewGuid(),
            AttendantId = _attendantId,
            OpenedAt = DateTimeOffset.UtcNow.AddHours(-1),
            OpeningFloat = 100.00m,
            Status = ShiftStatus.Open
        });
        _dbContext.SaveChanges();
        #endregion

        #region Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(() => _sut.OpenShiftAsync(_attendantId, 100.00m));
        #endregion
    }
    #endregion
}
