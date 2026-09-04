using Microsoft.Data.Sqlite;
using MAUI_POS_DASH.Core.Attendants;
using MAUI_POS_DASH.Core.Persistence.Repositories;

namespace MAUI_POS_DASH.Core.Tests.Attendants;

[TestFixture]
public class AttendantServiceTests
{
    #region Fields
    private SqliteConnection _connection = null!;
    private TerminalDbContext _dbContext = null!;
    private AttendantService _sut = null!;
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

        _sut = new AttendantService(
            new EfAttendantRepository(_dbContext),
            new EfAttendantSessionStore(_dbContext),
            new Pbkdf2PinHasher());
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
    public async Task TryLoginAsync_CorrectPin_SucceedsAndCreatesSession()
    {
        #region Arrange
        var attendant = await _sut.CreateAttendantAsync("Homer Simpson", "1234", AttendantRole.Attendant);
        #endregion

        #region Act
        var result = await _sut.TryLoginAsync(attendant.Id, "1234");
        #endregion

        #region Assert
        Assert.That(result.Status, Is.EqualTo(LoginStatus.Success));
        Assert.That(result.Session, Is.Not.Null);
        Assert.That(result.Session!.AttendantId, Is.EqualTo(attendant.Id));
        #endregion
    }

    [Test]
    public async Task TryLoginAsync_WrongPin_FailsAndCreatesNoSession()
    {
        #region Arrange
        var attendant = await _sut.CreateAttendantAsync("Marge Simpson", "1234", AttendantRole.Attendant);
        #endregion

        #region Act
        var result = await _sut.TryLoginAsync(attendant.Id, "9999");
        #endregion

        #region Assert
        Assert.That(result.Status, Is.EqualTo(LoginStatus.InvalidPin));
        Assert.That(result.Session, Is.Null);
        Assert.That(await _dbContext.AttendantSessions.CountAsync(), Is.EqualTo(0));
        #endregion
    }

    [Test]
    public async Task TryLoginAsync_InactiveAttendant_FailsEvenWithCorrectPin()
    {
        #region Arrange
        var attendant = await _sut.CreateAttendantAsync("Ned Flanders", "1234", AttendantRole.Attendant);
        await _sut.DeactivateAttendantAsync(attendant.Id);
        #endregion

        #region Act
        var result = await _sut.TryLoginAsync(attendant.Id, "1234");
        #endregion

        #region Assert
        Assert.That(result.Status, Is.EqualTo(LoginStatus.AttendantInactive));
        #endregion
    }

    [Test]
    public async Task DeactivateAttendantAsync_ActiveAttendant_NoLongerAppearsInActiveList()
    {
        #region Arrange
        var attendant = await _sut.CreateAttendantAsync("Moe Szyslak", "1234", AttendantRole.Attendant);
        #endregion

        #region Act
        await _sut.DeactivateAttendantAsync(attendant.Id);
        #endregion

        #region Assert
        var active = await _sut.GetActiveAttendantsAsync();
        Assert.That(active.Any(a => a.Id == attendant.Id), Is.False);
        #endregion
    }

    [Test]
    public async Task ResetPinAsync_NewPin_OldPinNoLongerVerifiesAndNewPinDoes()
    {
        #region Arrange
        var attendant = await _sut.CreateAttendantAsync("Apu Nahasapeemapetilon", "1234", AttendantRole.Attendant);
        #endregion

        #region Act
        await _sut.ResetPinAsync(attendant.Id, "5678");
        #endregion

        #region Assert
        var oldPinResult = await _sut.TryLoginAsync(attendant.Id, "1234");
        var newPinResult = await _sut.TryLoginAsync(attendant.Id, "5678");
        Assert.That(oldPinResult.Status, Is.EqualTo(LoginStatus.InvalidPin));
        Assert.That(newPinResult.Status, Is.EqualTo(LoginStatus.Success));
        #endregion
    }

    [Test]
    public async Task EnsureDefaultAttendantSeededAsync_NoAttendantsExist_CreatesDefaultManager()
    {
        #region Arrange & Act
        await _sut.EnsureDefaultAttendantSeededAsync();
        #endregion

        #region Assert
        var active = await _sut.GetActiveAttendantsAsync();
        Assert.That(active, Has.Count.EqualTo(1));
        Assert.That(active[0].Role, Is.EqualTo(AttendantRole.Manager));
        #endregion
    }

    [Test]
    public async Task EnsureDefaultAttendantSeededAsync_AttendantsAlreadyExist_DoesNotAddAnother()
    {
        #region Arrange
        await _sut.CreateAttendantAsync("Homer Simpson", "1234", AttendantRole.Attendant);
        #endregion

        #region Act
        await _sut.EnsureDefaultAttendantSeededAsync();
        #endregion

        #region Assert
        var active = await _sut.GetActiveAttendantsAsync();
        Assert.That(active, Has.Count.EqualTo(1));
        #endregion
    }
    #endregion
}
