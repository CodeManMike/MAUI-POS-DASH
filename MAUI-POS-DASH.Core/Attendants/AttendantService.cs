namespace MAUI_POS_DASH.Core.Attendants;

/// <summary>
/// We own attendant sign-in and CRUD — the two are handled together because both work through
/// the same PIN-hashing rules.
/// </summary>
public class AttendantService
{
    #region Fields
    private readonly IAttendantRepository _attendantRepository;
    private readonly IAttendantSessionStore _sessionStore;
    private readonly IPinHasher _pinHasher;
    #endregion

    #region Constructor
    public AttendantService(IAttendantRepository attendantRepository, IAttendantSessionStore sessionStore, IPinHasher pinHasher)
    {
        _attendantRepository = attendantRepository;
        _sessionStore = sessionStore;
        _pinHasher = pinHasher;
    }
    #endregion

    #region Public Methods
    public Task<IReadOnlyList<Attendant>> GetActiveAttendantsAsync(CancellationToken cancellationToken = default) =>
        _attendantRepository.GetActiveAttendantsAsync(cancellationToken);

    public Task<AttendantSession?> GetActiveSessionAsync(CancellationToken cancellationToken = default) =>
        _sessionStore.GetActiveSessionAsync(cancellationToken);

    /// <summary>
    /// We verify the PIN against the given attendant only — the caller has already picked a name
    /// from the login tile grid, so this never scans every attendant's hash.
    /// </summary>
    public async Task<LoginResult> TryLoginAsync(Guid attendantId, string pin, CancellationToken cancellationToken = default)
    {
        var attendant = await _attendantRepository.GetByIdAsync(attendantId, cancellationToken);
        if (attendant is null || !attendant.IsActive)
        {
            return new LoginResult(LoginStatus.AttendantInactive, null);
        }

        if (!_pinHasher.Verify(pin, attendant.PinHash))
        {
            return new LoginResult(LoginStatus.InvalidPin, null);
        }

        await _sessionStore.SignInAsync(attendant.Id, cancellationToken);
        var session = await _sessionStore.GetActiveSessionAsync(cancellationToken);

        return new LoginResult(LoginStatus.Success, session);
    }

    public Task LogoutAsync(CancellationToken cancellationToken = default) =>
        _sessionStore.SignOutAsync(cancellationToken);

    /// <summary>
    /// We validate the PIN is 4-6 numeric digits before hashing it — everything downstream only
    /// ever sees the hash, so this is the one place that can catch a malformed PIN.
    /// </summary>
    public async Task<Attendant> CreateAttendantAsync(string name, string pin, AttendantRole role, CancellationToken cancellationToken = default)
    {
        ValidatePin(pin);

        var attendant = new Attendant
        {
            Id = Guid.NewGuid(),
            Name = name,
            PinHash = _pinHasher.Hash(pin),
            Role = role,
            IsActive = true
        };

        await _attendantRepository.AddAsync(attendant, cancellationToken);
        return attendant;
    }

    public async Task UpdateAttendantAsync(Guid id, string name, AttendantRole role, CancellationToken cancellationToken = default)
    {
        var attendant = await _attendantRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"We can't update attendant {id} — it doesn't exist.");

        attendant.Name = name;
        attendant.Role = role;

        await _attendantRepository.UpdateAsync(attendant, cancellationToken);
    }

    public async Task ResetPinAsync(Guid id, string newPin, CancellationToken cancellationToken = default)
    {
        ValidatePin(newPin);

        var attendant = await _attendantRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"We can't reset the PIN for attendant {id} — it doesn't exist.");

        attendant.PinHash = _pinHasher.Hash(newPin);

        await _attendantRepository.UpdateAsync(attendant, cancellationToken);
    }

    public async Task DeactivateAttendantAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var attendant = await _attendantRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"We can't deactivate attendant {id} — it doesn't exist.");

        attendant.IsActive = false;

        await _attendantRepository.UpdateAsync(attendant, cancellationToken);
    }

    /// <summary>
    /// We seed one default Manager attendant (PIN 0000) the first time this runs against an
    /// empty attendant list, so a fresh install always has someone who can sign in and create
    /// real attendants. Documented in README.md as a known local/demo credential.
    /// </summary>
    public async Task EnsureDefaultAttendantSeededAsync(CancellationToken cancellationToken = default)
    {
        var existing = await _attendantRepository.GetActiveAttendantsAsync(cancellationToken);
        if (existing.Count > 0)
        {
            return;
        }

        await CreateAttendantAsync("Manager", "0000", AttendantRole.Manager, cancellationToken);
    }
    #endregion

    #region Private Methods
    private static void ValidatePin(string pin)
    {
        if (pin.Length is < 4 or > 6 || !pin.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("A PIN must be 4-6 numeric digits.", nameof(pin));
        }
    }
    #endregion
}
