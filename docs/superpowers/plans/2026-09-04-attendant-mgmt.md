# Attendant Management Module Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build real PIN-based attendant login, attendant CRUD, and wire the signed-in attendant through to shift open/close, per `docs/superpowers/specs/2026-09-04-attendant-mgmt-design.md`.

**Architecture:** A new `Core/Attendants/` folder (mirroring the existing `Core/Shifts/` pattern) holds `IPinHasher`/`Pbkdf2PinHasher`, `IAttendantRepository`, `IAttendantSessionStore`, `LoginResult`, and `AttendantService`. `Core.Persistence` gets EF implementations backed by `TerminalDbContext` only — `AttendantSession` is terminal-local state, explicitly excluded from `BackofficeDbContext`. The MAUI terminal's `Login.razor`, `TerminalLayout.razor`, `ShiftOpen.razor`, `ShiftClose.razor`, and `AttendantManagement.razor` are updated to use real services instead of placeholder messages.

**Tech Stack:** .NET 10, EF Core 10 (SQLite), PBKDF2 via `System.Security.Cryptography` (BCL, no new package), NUnit 4.6.

**Build order note:** unlike a strict per-unit TDD order, this plan follows the same sequencing the platform skeleton used successfully — domain and service code first, then the EF persistence layer, then tests that exercise the real EF implementations (matching how `ShiftServiceTests` was built against `EfShiftRepository`). The one exception is `Pbkdf2PinHasher`, which has no database dependency and is tested immediately in its own task.

---

## Task 1: Domain additions

**Files:**
- Modify: `MAUI-POS-DASH.Core/Domain/Attendant.cs`
- Create: `MAUI-POS-DASH.Core/Domain/AttendantSession.cs`

- [ ] **Step 1: Add `IsActive` to `Attendant`**

Replace the full contents of `MAUI-POS-DASH.Core/Domain/Attendant.cs`:

```csharp
namespace MAUI_POS_DASH.Core.Domain;

/// <summary>
/// We represent a person who can sign in and work the terminal.
/// </summary>
public class Attendant
{
    #region Properties
    public Guid Id { get; set; }

    public required string Name { get; set; }

    /// <summary>
    /// We only ever store a hash of the PIN, never the PIN itself — the AttendantMgmt module
    /// owns the actual hashing scheme when it's built.
    /// </summary>
    public required string PinHash { get; set; }

    public AttendantRole Role { get; set; }

    /// <summary>
    /// We deactivate attendants instead of deleting them — Shift and Sale reference AttendantId,
    /// so removing the row would either violate that foreign key or destroy history.
    /// </summary>
    public bool IsActive { get; set; } = true;
    #endregion
}
```

**Note found on build:** adding `IsActive` triggers the same Mapperly `RMG020` warning `PinHash` already has on `EntityMapper.ToDto(Attendant)` — `AttendantDto` deliberately doesn't expose it (same reasoning as `PinHash`: the DTO stays minimal). Fix by stacking a second `[MapperIgnoreSource]` in `MAUI-POS-DASH.Core/Contracts/EntityMapper.cs`:

```csharp
    #region Attendant
    [MapperIgnoreSource(nameof(Attendant.PinHash))]
    [MapperIgnoreSource(nameof(Attendant.IsActive))]
    public partial AttendantDto ToDto(Attendant attendant);
    #endregion
```

- [ ] **Step 2: Write `AttendantSession`**

`MAUI-POS-DASH.Core/Domain/AttendantSession.cs`:

```csharp
namespace MAUI_POS_DASH.Core.Domain;

/// <summary>
/// We represent one attendant's period of being signed in to this specific terminal. A row with
/// SignedOutAt still null is the currently active session. This is terminal-local state — it
/// lives only in TerminalDbContext, never synced to the back office.
/// </summary>
public class AttendantSession
{
    #region Properties
    public Guid Id { get; set; }

    public Guid AttendantId { get; set; }

    public Attendant? Attendant { get; set; }

    public DateTimeOffset SignedInAt { get; set; }

    public DateTimeOffset? SignedOutAt { get; set; }
    #endregion
}
```

- [ ] **Step 3: Build**

```bash
dotnet build MAUI-POS-DASH.Core/MAUI-POS-DASH.Core.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add MAUI-POS-DASH.Core/Domain/Attendant.cs MAUI-POS-DASH.Core/Domain/AttendantSession.cs
git commit -m "Add Attendant.IsActive and the AttendantSession entity"
```

---

## Task 2: Core/Attendants — interfaces and LoginResult

**Files:**
- Create: `MAUI-POS-DASH.Core/Attendants/IPinHasher.cs`
- Create: `MAUI-POS-DASH.Core/Attendants/IAttendantRepository.cs`
- Create: `MAUI-POS-DASH.Core/Attendants/IAttendantSessionStore.cs`
- Create: `MAUI-POS-DASH.Core/Attendants/LoginResult.cs`

- [ ] **Step 1: Write `IPinHasher`**

`MAUI-POS-DASH.Core/Attendants/IPinHasher.cs`:

```csharp
namespace MAUI_POS_DASH.Core.Attendants;

/// <summary>
/// We hash and verify PINs — never store or compare one in plain text.
/// </summary>
public interface IPinHasher
{
    string Hash(string pin);

    bool Verify(string pin, string hash);
}
```

- [ ] **Step 2: Write `IAttendantRepository`**

`MAUI-POS-DASH.Core/Attendants/IAttendantRepository.cs`:

```csharp
namespace MAUI_POS_DASH.Core.Attendants;

/// <summary>
/// We keep AttendantService's storage access behind this interface so it doesn't know or care
/// whether attendants live in SQLite, Postgres, or an in-memory fixture in a test.
/// </summary>
public interface IAttendantRepository
{
    Task<IReadOnlyList<Attendant>> GetActiveAttendantsAsync(CancellationToken cancellationToken = default);

    Task<Attendant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Attendant attendant, CancellationToken cancellationToken = default);

    Task UpdateAsync(Attendant attendant, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 3: Write `IAttendantSessionStore`**

`MAUI-POS-DASH.Core/Attendants/IAttendantSessionStore.cs`:

```csharp
namespace MAUI_POS_DASH.Core.Attendants;

/// <summary>
/// We track which attendant is currently signed in to this terminal. Session state is
/// terminal-local — see AttendantSession's own doc comment.
/// </summary>
public interface IAttendantSessionStore
{
    Task<AttendantSession?> GetActiveSessionAsync(CancellationToken cancellationToken = default);

    Task SignInAsync(Guid attendantId, CancellationToken cancellationToken = default);

    Task SignOutAsync(CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: Write `LoginResult`**

`MAUI-POS-DASH.Core/Attendants/LoginResult.cs`:

```csharp
namespace MAUI_POS_DASH.Core.Attendants;

public enum LoginStatus
{
    Success,
    InvalidPin,
    AttendantInactive
}

/// <summary>
/// We return this instead of throwing — a wrong PIN is expected user behavior, not an
/// exceptional condition.
/// </summary>
public record LoginResult(LoginStatus Status, AttendantSession? Session);
```

- [ ] **Step 5: Build and commit**

```bash
dotnet build MAUI-POS-DASH.Core/MAUI-POS-DASH.Core.csproj
git add MAUI-POS-DASH.Core/Attendants/IPinHasher.cs MAUI-POS-DASH.Core/Attendants/IAttendantRepository.cs MAUI-POS-DASH.Core/Attendants/IAttendantSessionStore.cs MAUI-POS-DASH.Core/Attendants/LoginResult.cs
git commit -m "Add Attendant Mgmt interfaces and LoginResult"
```

---

## Task 3: Core/Attendants — PIN hasher (with tests, no DB dependency)

**Files:**
- Create: `MAUI-POS-DASH.Core/Attendants/Pbkdf2PinHasher.cs`
- Create: `MAUI-POS-DASH.Core.Tests/Attendants/Pbkdf2PinHasherTests.cs`

- [ ] **Step 1: Write the failing tests**

`MAUI-POS-DASH.Core.Tests/Attendants/Pbkdf2PinHasherTests.cs`:

```csharp
using MAUI_POS_DASH.Core.Attendants;

namespace MAUI_POS_DASH.Core.Tests.Attendants;

[TestFixture]
public class Pbkdf2PinHasherTests
{
    #region Fields
    private Pbkdf2PinHasher _sut = null!;
    #endregion

    #region Setup
    [SetUp]
    public void SetUp()
    {
        _sut = new Pbkdf2PinHasher();
    }
    #endregion

    #region Tests
    [Test]
    public void Verify_CorrectPin_ReturnsTrue()
    {
        #region Arrange
        var hash = _sut.Hash("1234");
        #endregion

        #region Act
        var result = _sut.Verify("1234", hash);
        #endregion

        #region Assert
        Assert.That(result, Is.True);
        #endregion
    }

    [Test]
    public void Verify_IncorrectPin_ReturnsFalse()
    {
        #region Arrange
        var hash = _sut.Hash("1234");
        #endregion

        #region Act
        var result = _sut.Verify("9999", hash);
        #endregion

        #region Assert
        Assert.That(result, Is.False);
        #endregion
    }

    [Test]
    public void Hash_CalledTwiceForSamePin_ProducesDifferentHashes()
    {
        #region Arrange & Act
        var first = _sut.Hash("1234");
        var second = _sut.Hash("1234");
        #endregion

        #region Assert
        Assert.That(first, Is.Not.EqualTo(second));
        #endregion
    }
    #endregion
}
```

- [ ] **Step 2: Run the tests to confirm they fail**

```bash
dotnet test MAUI-POS-DASH.Core.Tests/MAUI-POS-DASH.Core.Tests.csproj --filter Pbkdf2PinHasherTests
```

Expected: build error — `Pbkdf2PinHasher` doesn't exist yet.

- [ ] **Step 3: Write `Pbkdf2PinHasher`**

`MAUI-POS-DASH.Core/Attendants/Pbkdf2PinHasher.cs`:

```csharp
using System.Security.Cryptography;

namespace MAUI_POS_DASH.Core.Attendants;

/// <summary>
/// We use PBKDF2 (via the BCL's Rfc2898DeriveBytes) rather than a third-party package — Core
/// stays dependency-free, and it's the same primitive ASP.NET Core Identity's own
/// PasswordHasher uses internally. The salt and iteration count are packed into the returned
/// string so no separate salt column is needed.
/// </summary>
public class Pbkdf2PinHasher : IPinHasher
{
    #region Fields
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;
    #endregion

    #region Public Methods
    public string Hash(string pin)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, Iterations, Algorithm, HashSize);

        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string pin, string hash)
    {
        var parts = hash.Split('.');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations))
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[1]);
        var expectedHash = Convert.FromBase64String(parts[2]);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, iterations, Algorithm, expectedHash.Length);

        // We use a fixed-time comparison rather than == or SequenceEqual — a short-circuiting
        // comparison would leak how many leading bytes matched via response timing.
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
    #endregion
}
```

- [ ] **Step 4: Run the tests to confirm they pass**

```bash
dotnet test MAUI-POS-DASH.Core.Tests/MAUI-POS-DASH.Core.Tests.csproj --filter Pbkdf2PinHasherTests
```

Expected: `Passed! - Failed: 0, Passed: 3, Skipped: 0`.

- [ ] **Step 5: Commit**

```bash
git add MAUI-POS-DASH.Core/Attendants/Pbkdf2PinHasher.cs MAUI-POS-DASH.Core.Tests/Attendants/Pbkdf2PinHasherTests.cs
git commit -m "Add PBKDF2 PIN hasher with tests"
```

---

## Task 4: Core/Attendants — AttendantService

**Files:**
- Create: `MAUI-POS-DASH.Core/Attendants/AttendantService.cs`

- [ ] **Step 1: Write `AttendantService`**

`MAUI-POS-DASH.Core/Attendants/AttendantService.cs`:

```csharp
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
```

- [ ] **Step 2: Build**

```bash
dotnet build MAUI-POS-DASH.Core/MAUI-POS-DASH.Core.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add MAUI-POS-DASH.Core/Attendants/AttendantService.cs
git commit -m "Add AttendantService (login, CRUD, default-attendant seeding)"
```

---

## Task 5: Core.Persistence — EF configurations and DbContext updates

**Files:**
- Modify: `MAUI-POS-DASH.Core.Persistence/Configurations/AttendantConfiguration.cs`
- Create: `MAUI-POS-DASH.Core.Persistence/Configurations/AttendantSessionConfiguration.cs`
- Modify: `MAUI-POS-DASH.Core.Persistence/TerminalDbContext.cs`
- Modify: `MAUI-POS-DASH.Core.Persistence/BackofficeDbContext.cs`

- [ ] **Step 1: Add `IsActive` to `AttendantConfiguration`**

Replace the full contents of `MAUI-POS-DASH.Core.Persistence/Configurations/AttendantConfiguration.cs`:

```csharp
namespace MAUI_POS_DASH.Core.Persistence.Configurations;

/// <summary>We configure the Attendant table's key and required/length constraints.</summary>
public class AttendantConfiguration : IEntityTypeConfiguration<Attendant>
{
    public void Configure(EntityTypeBuilder<Attendant> builder)
    {
        builder.HasKey(attendant => attendant.Id);
        builder.Property(attendant => attendant.Name).IsRequired().HasMaxLength(100);
        builder.Property(attendant => attendant.PinHash).IsRequired();
        builder.Property(attendant => attendant.IsActive).IsRequired().HasDefaultValue(true);
    }
}
```

- [ ] **Step 2: Write `AttendantSessionConfiguration`**

`MAUI-POS-DASH.Core.Persistence/Configurations/AttendantSessionConfiguration.cs`:

```csharp
namespace MAUI_POS_DASH.Core.Persistence.Configurations;

/// <summary>We configure AttendantSession's key and its relationship to Attendant.</summary>
public class AttendantSessionConfiguration : IEntityTypeConfiguration<AttendantSession>
{
    public void Configure(EntityTypeBuilder<AttendantSession> builder)
    {
        builder.HasKey(session => session.Id);

        builder.HasOne(session => session.Attendant)
            .WithMany()
            .HasForeignKey(session => session.AttendantId);
    }
}
```

- [ ] **Step 3: Add the `AttendantSessions` `DbSet` to `TerminalDbContext`**

In `MAUI-POS-DASH.Core.Persistence/TerminalDbContext.cs`, add this line inside the `#region DbSets` block, after `Transactions`:

```csharp
    public DbSet<AttendantSession> AttendantSessions => Set<AttendantSession>();
```

- [ ] **Step 4: Exclude `AttendantSession` from `BackofficeDbContext`**

**Why this step exists:** both DbContexts call `modelBuilder.ApplyConfigurationsFromAssembly(...)`,
which scans the *whole assembly* for `IEntityTypeConfiguration<T>` implementations — including
the new `AttendantSessionConfiguration` from Step 2. Without this explicit exclusion,
`BackofficeDbContext` would silently pick up `AttendantSession` too, even though it has no
`DbSet<AttendantSession>` property, which contradicts the design (`AttendantSession` is
terminal-local, never synced).

In `MAUI-POS-DASH.Core.Persistence/BackofficeDbContext.cs`, update the `OnModelCreating` override:

```csharp
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BackofficeDbContext).Assembly);

        // AttendantSession is terminal-local state — it belongs only in TerminalDbContext, never
        // synced to the back office. We exclude it here even though AttendantSessionConfiguration
        // gets picked up by the assembly scan above.
        modelBuilder.Ignore<AttendantSession>();
    }
```

- [ ] **Step 5: Build**

```bash
dotnet build MAUI-POS-DASH.Core.Persistence/MAUI-POS-DASH.Core.Persistence.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```bash
git add MAUI-POS-DASH.Core.Persistence/Configurations/AttendantConfiguration.cs MAUI-POS-DASH.Core.Persistence/Configurations/AttendantSessionConfiguration.cs MAUI-POS-DASH.Core.Persistence/TerminalDbContext.cs MAUI-POS-DASH.Core.Persistence/BackofficeDbContext.cs
git commit -m "Add AttendantSession to TerminalDbContext, exclude it from BackofficeDbContext"
```

---

## Task 6: Core.Persistence — repository implementations

**Files:**
- Create: `MAUI-POS-DASH.Core.Persistence/Repositories/EfAttendantRepository.cs`
- Create: `MAUI-POS-DASH.Core.Persistence/Repositories/EfAttendantSessionStore.cs`

- [ ] **Step 1: Write `EfAttendantRepository`**

`MAUI-POS-DASH.Core.Persistence/Repositories/EfAttendantRepository.cs`:

```csharp
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
```

- [ ] **Step 2: Write `EfAttendantSessionStore`**

`MAUI-POS-DASH.Core.Persistence/Repositories/EfAttendantSessionStore.cs`:

```csharp
using MAUI_POS_DASH.Core.Attendants;

namespace MAUI_POS_DASH.Core.Persistence.Repositories;

/// <summary>
/// We implement IAttendantSessionStore against TerminalDbContext.
/// </summary>
public class EfAttendantSessionStore : IAttendantSessionStore
{
    #region Fields
    private readonly TerminalDbContext _dbContext;
    #endregion

    #region Constructor
    public EfAttendantSessionStore(TerminalDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// We include the Attendant navigation — every caller of this method wants the attendant's
    /// name/role alongside the session, not just the bare AttendantId.
    /// </summary>
    public Task<AttendantSession?> GetActiveSessionAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.AttendantSessions
            .Include(session => session.Attendant)
            .Where(session => session.SignedOutAt == null)
            .SingleOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// We sign out any existing active session before starting a new one — a terminal has one
    /// attendant signed in at a time, so this covers the case where a previous attendant forgot
    /// to sign out (or the app restarted) before the next one taps in.
    /// </summary>
    public async Task SignInAsync(Guid attendantId, CancellationToken cancellationToken = default)
    {
        await SignOutAsync(cancellationToken);

        _dbContext.AttendantSessions.Add(new AttendantSession
        {
            Id = Guid.NewGuid(),
            AttendantId = attendantId,
            SignedInAt = DateTimeOffset.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        var active = await _dbContext.AttendantSessions
            .Where(session => session.SignedOutAt == null)
            .SingleOrDefaultAsync(cancellationToken);

        if (active is null)
        {
            return;
        }

        active.SignedOutAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
    #endregion
}
```

- [ ] **Step 3: Build**

```bash
dotnet build MAUI-POS-DASH.Core.Persistence/MAUI-POS-DASH.Core.Persistence.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add MAUI-POS-DASH.Core.Persistence/Repositories/EfAttendantRepository.cs MAUI-POS-DASH.Core.Persistence/Repositories/EfAttendantSessionStore.cs
git commit -m "Add EF repository implementations for attendants and sessions"
```

---

## Task 7: Core.Persistence — new EF migration

**Files:**
- Create (generated): `MAUI-POS-DASH.Core.Persistence/Migrations/Terminal/*AddAttendantSessionAndIsActive*`

- [ ] **Step 1: Generate the migration**

Only `TerminalDbContext`'s model changed (new `AttendantSessions` table, new `IsActive` column on
`Attendants`) — `BackofficeDbContext`'s model is unchanged because of Task 5 Step 4's
`Ignore<AttendantSession>()`, so no Backoffice migration is needed.

```bash
dotnet ef migrations add AddAttendantSessionAndIsActive --project MAUI-POS-DASH.Core.Persistence --context TerminalDbContext --output-dir Migrations/Terminal
```

Expected: creates `MAUI-POS-DASH.Core.Persistence/Migrations/Terminal/<timestamp>_AddAttendantSessionAndIsActive.cs`, `.Designer.cs`, and updates `TerminalDbContextModelSnapshot.cs`.

- [ ] **Step 2: Build**

```bash
dotnet build MAUI-POS-DASH.Core.Persistence/MAUI-POS-DASH.Core.Persistence.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add MAUI-POS-DASH.Core.Persistence/Migrations
git commit -m "Add EF migration for AttendantSession and Attendant.IsActive"
```

---

## Task 8: Core.Tests — AttendantService tests

**Files:**
- Create: `MAUI-POS-DASH.Core.Tests/Attendants/AttendantServiceTests.cs`

- [ ] **Step 1: Write the tests**

`MAUI-POS-DASH.Core.Tests/Attendants/AttendantServiceTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run the tests**

```bash
dotnet test MAUI-POS-DASH.Core.Tests/MAUI-POS-DASH.Core.Tests.csproj --filter AttendantServiceTests
```

Expected: `Passed! - Failed: 0, Passed: 7, Skipped: 0`.

- [ ] **Step 3: Run the full test suite to confirm nothing else broke**

```bash
dotnet test MAUI-POS-DASH.Core.Tests/MAUI-POS-DASH.Core.Tests.csproj
```

Expected: `Passed! - Failed: 0, Passed: 14, Skipped: 0` (4 from the platform skeleton + 3 `Pbkdf2PinHasherTests` + 7 `AttendantServiceTests`).

- [ ] **Step 4: Commit**

```bash
git add MAUI-POS-DASH.Core.Tests/Attendants/AttendantServiceTests.cs
git commit -m "Add AttendantService tests"
```

---

## Task 9: MAUI — DI wiring

**Files:**
- Modify: `MAUI-POS-DASH/MauiProgram.cs`

- [ ] **Step 1: Register the new services**

In `MAUI-POS-DASH/MauiProgram.cs`, add this using near the top (alphabetical with the existing block):

```csharp
using MAUI_POS_DASH.Core.Attendants;
```

Then, inside `RegisterCoreServices`, add these lines right after the existing `services.AddSingleton<EntityMapper>();` line:

```csharp
        services.AddScoped<IAttendantRepository, EfAttendantRepository>();
        services.AddScoped<IAttendantSessionStore, EfAttendantSessionStore>();
        services.AddScoped<AttendantService>();
        services.AddSingleton<IPinHasher, Pbkdf2PinHasher>();
```

- [ ] **Step 2: Build**

```bash
dotnet build MAUI-POS-DASH/MAUI-POS-DASH.csproj -f net10.0-android
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add MAUI-POS-DASH/MauiProgram.cs
git commit -m "Wire attendant services into MAUI DI"
```

---

## Task 10: MAUI — `_Imports.razor` update

**Files:**
- Modify: `MAUI-POS-DASH/Components/_Imports.razor`

- [ ] **Step 1: Add the new namespaces**

In `MAUI-POS-DASH/Components/_Imports.razor`, add these two lines at the end:

```razor
@using MAUI_POS_DASH.Core.Attendants
@using MAUI_POS_DASH.Core.Domain
```

- [ ] **Step 2: Commit**

```bash
git add MAUI-POS-DASH/Components/_Imports.razor
git commit -m "Add Core.Attendants and Core.Domain to MAUI's Razor imports"
```

---

## Task 11: MAUI — Login.razor

**Files:**
- Modify: `MAUI-POS-DASH/Components/Pages/Login.razor`

- [ ] **Step 1: Replace the page**

Replace the full contents of `MAUI-POS-DASH/Components/Pages/Login.razor`:

```razor
@page "/login"
@inject AttendantService AttendantService
@inject NavigationManager Navigation

<PageTitle>Attendant Login</PageTitle>

<h1>Attendant Login</h1>

@if (_selectedAttendant is null)
{
    <div class="module-grid">
        @foreach (var attendant in _attendants)
        {
            <button class="module-tile" @onclick="() => SelectAttendant(attendant)">@attendant.Name</button>
        }
    </div>
}
else
{
    <p>Signing in as @_selectedAttendant.Name</p>

    <input type="password" maxlength="6" placeholder="Enter PIN" class="mock-input" @bind="_pin" @bind:event="oninput" />
    <button class="mock-button" @onclick="SubmitPinAsync">Sign in</button>
    <button class="mock-button" @onclick="() => _selectedAttendant = null">Back</button>

    @if (_errorMessage is not null)
    {
        <p>@_errorMessage</p>
    }
}

@code {
    private IReadOnlyList<Attendant> _attendants = [];
    private Attendant? _selectedAttendant;
    private string _pin = string.Empty;
    private string? _errorMessage;

    protected override async Task OnInitializedAsync()
    {
        await AttendantService.EnsureDefaultAttendantSeededAsync();
        _attendants = await AttendantService.GetActiveAttendantsAsync();
    }

    private void SelectAttendant(Attendant attendant)
    {
        _selectedAttendant = attendant;
        _pin = string.Empty;
        _errorMessage = null;
    }

    private async Task SubmitPinAsync()
    {
        var result = await AttendantService.TryLoginAsync(_selectedAttendant!.Id, _pin);

        if (result.Status == LoginStatus.Success)
        {
            Navigation.NavigateTo("/");
            return;
        }

        _errorMessage = result.Status switch
        {
            LoginStatus.InvalidPin => "Wrong PIN — try again.",
            LoginStatus.AttendantInactive => "This attendant is no longer active.",
            _ => "Something went wrong — try again."
        };
        _pin = string.Empty;
    }
}
```

- [ ] **Step 2: Build**

```bash
dotnet build MAUI-POS-DASH/MAUI-POS-DASH.csproj -f net10.0-android
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add MAUI-POS-DASH/Components/Pages/Login.razor
git commit -m "Build real PIN login: tap attendant name, then PIN"
```

---

## Task 12: MAUI — TerminalLayout.razor

**Files:**
- Modify: `MAUI-POS-DASH/Components/Layout/TerminalLayout.razor`
- Modify: `MAUI-POS-DASH.Shared/wwwroot/app.css`

- [ ] **Step 1: Replace the layout**

Replace the full contents of `MAUI-POS-DASH/Components/Layout/TerminalLayout.razor`:

```razor
@inherits LayoutComponentBase
@inject AttendantService AttendantService
@inject NavigationManager Navigation
@implements IDisposable

<div class="terminal-shell">
    <header class="terminal-shell__status-bar">
        <span class="terminal-shell__title">MAUI-POS-DASH Terminal</span>
        @if (_signedInAttendantName is not null)
        {
            <span class="terminal-shell__attendant">@_signedInAttendantName</span>
            <button class="terminal-shell__sign-out" @onclick="SignOutAsync">Sign out</button>
        }
        else
        {
            <span class="terminal-shell__attendant">Not signed in</span>
        }
        <span class="terminal-shell__connectivity" title="Simulated — this skeleton is always online">● Online</span>
    </header>

    <main class="terminal-shell__body">
        @Body
    </main>
</div>

<div id="blazor-error-ui" data-nosnippet>
    An unhandled error has occurred.
    <a href="." class="reload">Reload</a>
    <span class="dismiss">🗙</span>
</div>

@code {
    private string? _signedInAttendantName;

    protected override async Task OnInitializedAsync()
    {
        // We refresh on every navigation, not just once — the layout instance is reused across
        // page changes, so without this the status bar would still say "Not signed in" right
        // after a successful login navigates to Home.
        Navigation.LocationChanged += OnLocationChanged;
        await RefreshSignedInAttendantAsync();
    }

    private async void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        await RefreshSignedInAttendantAsync();
        StateHasChanged();
    }

    private async Task RefreshSignedInAttendantAsync()
    {
        var session = await AttendantService.GetActiveSessionAsync();
        _signedInAttendantName = session?.Attendant?.Name;
    }

    private async Task SignOutAsync()
    {
        await AttendantService.LogoutAsync();
        _signedInAttendantName = null;
        Navigation.NavigateTo("/login");
    }

    public void Dispose()
    {
        Navigation.LocationChanged -= OnLocationChanged;
    }
}
```

- [ ] **Step 2: Add CSS for the new status bar elements**

Append to `MAUI-POS-DASH.Shared/wwwroot/app.css`:

```css
.terminal-shell__attendant {
    font-size: 0.85rem;
    opacity: 0.85;
}

.terminal-shell__sign-out {
    background: transparent;
    border: 1px solid rgba(255, 255, 255, 0.4);
    color: white;
    border-radius: 4px;
    padding: 0.15rem 0.5rem;
    font-size: 0.8rem;
    cursor: pointer;
}
```

- [ ] **Step 3: Build**

```bash
dotnet build MAUI-POS-DASH/MAUI-POS-DASH.csproj -f net10.0-android
```

Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add MAUI-POS-DASH/Components/Layout/TerminalLayout.razor MAUI-POS-DASH.Shared/wwwroot/app.css
git commit -m "Show the signed-in attendant and a sign-out action in the terminal status bar"
```

---

## Task 13: MAUI — wire ShiftOpen/ShiftClose to real ShiftService

**Files:**
- Modify: `MAUI-POS-DASH.Core/Shifts/ShiftService.cs`
- Modify: `MAUI-POS-DASH/Components/Pages/ShiftOpen.razor`
- Modify: `MAUI-POS-DASH/Components/Pages/ShiftClose.razor`

- [ ] **Step 1: Add `GetActiveShiftAsync` to `ShiftService`**

In `MAUI-POS-DASH.Core/Shifts/ShiftService.cs`, add this method inside `#region Public Methods`,
right after the constructor's region and before `OpenShiftAsync`:

```csharp
    /// <summary>
    /// We expose this so pages can find the shift to close without depending on
    /// IShiftRepository directly — pages talk to services, not repositories.
    /// </summary>
    public Task<Shift?> GetActiveShiftAsync(Guid attendantId, CancellationToken cancellationToken = default) =>
        _shiftRepository.GetActiveShiftAsync(attendantId, cancellationToken);
```

- [ ] **Step 2: Rewrite `ShiftOpen.razor`**

Replace the full contents of `MAUI-POS-DASH/Components/Pages/ShiftOpen.razor`:

```razor
@page "/shift/open"
@inject AttendantService AttendantService
@inject ShiftService ShiftService
@inject NavigationManager Navigation

<PageTitle>Open Shift</PageTitle>

<h1>Open Shift</h1>

@if (_attendantId is not null)
{
    <label>
        Opening float
        <input type="number" step="0.01" @bind="_openingFloat" class="mock-input" />
    </label>

    <button class="mock-button" @onclick="OpenShiftAsync">Open Shift</button>

    @if (_message is not null)
    {
        <p>@_message</p>
    }
}

@code {
    private Guid? _attendantId;
    private decimal _openingFloat;
    private string? _message;

    protected override async Task OnInitializedAsync()
    {
        var session = await AttendantService.GetActiveSessionAsync();
        if (session is null)
        {
            Navigation.NavigateTo("/login");
            return;
        }

        _attendantId = session.AttendantId;
    }

    private async Task OpenShiftAsync()
    {
        try
        {
            await ShiftService.OpenShiftAsync(_attendantId!.Value, _openingFloat);
            Navigation.NavigateTo("/");
        }
        catch (InvalidOperationException ex)
        {
            _message = ex.Message;
        }
    }
}
```

- [ ] **Step 3: Rewrite `ShiftClose.razor`**

Replace the full contents of `MAUI-POS-DASH/Components/Pages/ShiftClose.razor`:

```razor
@page "/shift/close"
@inject AttendantService AttendantService
@inject ShiftService ShiftService
@inject NavigationManager Navigation

<PageTitle>Close Shift</PageTitle>

<h1>Close Shift</h1>

@if (_shift is not null)
{
    <label>
        Cash counted
        <input type="number" step="0.01" @bind="_cashCounted" class="mock-input" />
    </label>

    <button class="mock-button" @onclick="CloseShiftAsync">Close Shift</button>
}
else if (_message is not null)
{
    <p>@_message</p>
}

@code {
    private Shift? _shift;
    private decimal _cashCounted;
    private string? _message;

    protected override async Task OnInitializedAsync()
    {
        var session = await AttendantService.GetActiveSessionAsync();
        if (session is null)
        {
            Navigation.NavigateTo("/login");
            return;
        }

        _shift = await ShiftService.GetActiveShiftAsync(session.AttendantId);
        if (_shift is null)
        {
            _message = "There's no open shift to close.";
        }
    }

    private async Task CloseShiftAsync()
    {
        await ShiftService.CloseShiftAsync(_shift!, _cashCounted);
        Navigation.NavigateTo("/");
    }
}
```

- [ ] **Step 4: Build**

```bash
dotnet build MAUI-POS-DASH.Core/MAUI-POS-DASH.Core.csproj
dotnet build MAUI-POS-DASH/MAUI-POS-DASH.csproj -f net10.0-android
```

Expected: both `Build succeeded.`

- [ ] **Step 5: Run the full test suite**

```bash
dotnet test MAUI-POS-DASH.Core.Tests/MAUI-POS-DASH.Core.Tests.csproj
```

Expected: `Passed! - Failed: 0, Passed: 14, Skipped: 0` (the new `GetActiveShiftAsync` method has no dedicated test — it's a one-line passthrough with the same shape `EfShiftRepository.GetActiveShiftAsync` already exercises indirectly via `ShiftServiceTests`; add one if you want tighter coverage, but it's not required for this pass).

- [ ] **Step 6: Commit**

```bash
git add MAUI-POS-DASH.Core/Shifts/ShiftService.cs MAUI-POS-DASH/Components/Pages/ShiftOpen.razor MAUI-POS-DASH/Components/Pages/ShiftClose.razor
git commit -m "Wire ShiftOpen/ShiftClose to the real ShiftService via the signed-in attendant"
```

---

## Task 14: MAUI — AttendantManagement.razor (CRUD)

**Files:**
- Modify: `MAUI-POS-DASH/Components/Pages/AttendantManagement.razor`

- [ ] **Step 1: Replace the page**

Replace the full contents of `MAUI-POS-DASH/Components/Pages/AttendantManagement.razor`:

```razor
@page "/attendants"
@inject AttendantService AttendantService

<PageTitle>Attendants</PageTitle>

<h1>Attendants</h1>

@if (_loading)
{
    <p>Loading…</p>
}
else if (!_isManager)
{
    <p>Managers only — you don't have access to this page.</p>
    <a href="/">Back to terminal home</a>
}
else
{
    <table>
        <thead>
            <tr>
                <th>Name</th>
                <th>Role</th>
                <th></th>
            </tr>
        </thead>
        <tbody>
            @foreach (var attendant in _attendants)
            {
                <tr>
                    <td>@attendant.Name</td>
                    <td>@attendant.Role</td>
                    <td>
                        <button class="mock-button" @onclick="() => StartEdit(attendant)">Edit</button>
                        <button class="mock-button" @onclick="() => DeactivateAsync(attendant)">Deactivate</button>
                    </td>
                </tr>
            }
        </tbody>
    </table>

    <h2>@(_editingId is null ? "New attendant" : "Edit attendant")</h2>

    <label>
        Name
        <input class="mock-input" @bind="_formName" />
    </label>

    <label>
        Role
        <select class="mock-input" @bind="_formRole">
            @foreach (var role in Enum.GetValues<AttendantRole>())
            {
                <option value="@role">@role</option>
            }
        </select>
    </label>

    <label>
        @(_editingId is null ? "PIN" : "New PIN (leave blank to keep current)")
        <input type="password" maxlength="6" class="mock-input" @bind="_formPin" />
    </label>

    <button class="mock-button" @onclick="SaveAsync">Save</button>
    @if (_editingId is not null)
    {
        <button class="mock-button" @onclick="ResetForm">Cancel</button>
    }

    @if (_errorMessage is not null)
    {
        <p>@_errorMessage</p>
    }
}

@code {
    private bool _loading = true;
    private bool _isManager;
    private IReadOnlyList<Attendant> _attendants = [];
    private Guid? _editingId;
    private string _formName = string.Empty;
    private AttendantRole _formRole = AttendantRole.Attendant;
    private string _formPin = string.Empty;
    private string? _errorMessage;

    protected override async Task OnInitializedAsync()
    {
        var session = await AttendantService.GetActiveSessionAsync();
        _isManager = session?.Attendant?.Role == AttendantRole.Manager;

        if (_isManager)
        {
            _attendants = await AttendantService.GetActiveAttendantsAsync();
        }

        _loading = false;
    }

    private void StartEdit(Attendant attendant)
    {
        _editingId = attendant.Id;
        _formName = attendant.Name;
        _formRole = attendant.Role;
        _formPin = string.Empty;
        _errorMessage = null;
    }

    private void ResetForm()
    {
        _editingId = null;
        _formName = string.Empty;
        _formRole = AttendantRole.Attendant;
        _formPin = string.Empty;
        _errorMessage = null;
    }

    private async Task SaveAsync()
    {
        try
        {
            if (_editingId is null)
            {
                await AttendantService.CreateAttendantAsync(_formName, _formPin, _formRole);
            }
            else
            {
                await AttendantService.UpdateAttendantAsync(_editingId.Value, _formName, _formRole);
                if (!string.IsNullOrEmpty(_formPin))
                {
                    await AttendantService.ResetPinAsync(_editingId.Value, _formPin);
                }
            }

            _attendants = await AttendantService.GetActiveAttendantsAsync();
            ResetForm();
        }
        catch (ArgumentException ex)
        {
            _errorMessage = ex.Message;
        }
    }

    private async Task DeactivateAsync(Attendant attendant)
    {
        await AttendantService.DeactivateAttendantAsync(attendant.Id);
        _attendants = await AttendantService.GetActiveAttendantsAsync();
    }
}
```

- [ ] **Step 2: Build**

```bash
dotnet build MAUI-POS-DASH/MAUI-POS-DASH.csproj -f net10.0-android
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add MAUI-POS-DASH/Components/Pages/AttendantManagement.razor
git commit -m "Build Manager-only attendant CRUD page"
```

---

## Task 15: Docs updates

**Files:**
- Modify: `docs/ARCHITECTURE.md`
- Modify: `MAUI-POS-DASH.Core/Modules/AttendantMgmt/README.md`
- Modify: `README.md`

- [ ] **Step 1: Update the module ownership table and placeholder list in `docs/ARCHITECTURE.md`**

In `docs/ARCHITECTURE.md`, change this line in the `## Module ownership` table:

```markdown
| `Core/Modules/AttendantMgmt/` | unclaimed | `Attendant`, auth |
```

to:

```markdown
| `Core/Modules/AttendantMgmt/` | Architect (done) | `Attendant`, auth |
```

Then replace the `## What's a placeholder right now` section with:

```markdown
## What's a placeholder right now

- `FleetCardSale.razor`, `MobileMoneySale.razor`, `CashSale.razor` are "coming soon" pages
  pointing at their module's README.
- `POST /api/transactions` returns `501 Not Implemented` — it proves the contract compiles, not
  that it persists anything yet.
- The dashboard's `Dashboard.razor` renders fixed sample data, not a live query.
- Session idle/timeout isn't implemented — a signed-in attendant stays signed in until they
  explicitly sign out (see `docs/superpowers/specs/2026-09-04-attendant-mgmt-design.md` §8).
```

- [ ] **Step 2: Update the module's own README**

Replace the full contents of `MAUI-POS-DASH.Core/Modules/AttendantMgmt/README.md`:

```markdown
# Attendant Management module

**Status:** done. **Owner:** Architect.

Handles attendant PIN login (tap a name, then enter PIN), PIN hashing/verification (PBKDF2 via
`Core/Attendants/Pbkdf2PinHasher.cs`), and CRUD for attendant records. Wired into
`ShiftOpen.razor`/`ShiftClose.razor` so shift open/close use the real signed-in attendant instead
of a placeholder message.

See `docs/superpowers/specs/2026-09-04-attendant-mgmt-design.md` for the full design.
```

- [ ] **Step 3: Document the seeded default credential in the root `README.md`**

In `README.md`, add this paragraph to the `## Running it` section, under the existing "MAUI
terminal (Android)" bullet:

```markdown
On first run, if no attendants exist yet, a default "Manager" attendant is seeded with PIN
`0000` — a known local/demo credential, not a real one. Sign in with it once to create real
attendants via the Attendants page, then deactivate or change it.
```

- [ ] **Step 4: Commit**

```bash
git add docs/ARCHITECTURE.md MAUI-POS-DASH.Core/Modules/AttendantMgmt/README.md README.md
git commit -m "Update docs: Attendant Mgmt module is done"
```

---

## Task 16: Full verification

**Files:** none (verification only)

- [ ] **Step 1: Restore and build the whole solution**

```bash
dotnet restore MAUI-POS-DASH.slnx
dotnet build MAUI-POS-DASH.slnx
```

Expected: `Build succeeded.` for every project.

- [ ] **Step 2: Run the full test suite**

```bash
dotnet test MAUI-POS-DASH.Core.Tests/MAUI-POS-DASH.Core.Tests.csproj
```

Expected: `Passed! - Failed: 0, Passed: 14, Skipped: 0`.

- [ ] **Step 3: Known verification limitation — be explicit about it, don't claim more than is true**

This module's actual UI (`Login.razor`, `TerminalLayout.razor`, `ShiftOpen.razor`,
`ShiftClose.razor`, `AttendantManagement.razor`) only runs inside the MAUI Android host. There is
no Android emulator available in this environment, so — exactly as with the platform skeleton
pass — the Razor markup and event wiring in this task are verified by compilation
(`net10.0-android` build succeeding) and code review only, **not** live interaction. The business
logic behind them (`AttendantService`, `Pbkdf2PinHasher`) *is* verified live via the NUnit suite
in Step 2. If Overmind has access to an Android emulator or device, running through
login → open shift → close shift → attendant CRUD there is the remaining verification this plan
can't do here.

- [ ] **Step 4: Final status check**

```bash
git status
git log --oneline -20
```

Expected: working tree clean, one commit per task above.
