# Fleet Card Module Implementation Plan

**Goal:** Replace the `FleetCardSale.razor` placeholder with a working tap-card payment flow, per
`docs/superpowers/specs/2026-09-05-fleet-card-module-design.md`.

**Architecture:** `Core/Sales/ISaleRepository` (shared) + `Core/FleetCard/IFleetCardAuthorizationService`
+ `Core/FleetCard/FleetCardSaleService` (orchestration) + a dumb `FleetCardSale.razor` page.

---

## Task 1: Shared Sale persistence

**Files:**
- Create: `MAUI-POS-DASH.Core/Sales/ISaleRepository.cs`
- Create: `MAUI-POS-DASH.Core.Persistence/Repositories/EfSaleRepository.cs`
- Create: `MAUI-POS-DASH.Core.Tests/Sales/EfSaleRepositoryTests.cs`

```csharp
// ISaleRepository.cs
namespace MAUI_POS_DASH.Core.Sales;

/// <summary>
/// We keep Sale/Transaction persistence behind this interface so payment modules (FleetCard,
/// Cash, MobileMoney) don't touch TerminalDbContext directly — every module records a completed
/// tender the same way: one Sale, one Transaction, written together.
/// </summary>
public interface ISaleRepository
{
    Task AddSaleWithTransactionAsync(Sale sale, Transaction transaction, CancellationToken cancellationToken = default);
}
```

```csharp
// EfSaleRepository.cs
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
```

Test with a seeded SQLite in-memory `TerminalDbContext` (same setup as `AttendantServiceTests`):
one test asserting the Sale, its SaleLine, and the Transaction are all readable back afterward
with the right FKs and amounts.

- [ ] Write `EfSaleRepositoryTests.AddSaleWithTransactionAsync_ValidSaleAndTransaction_PersistsBoth`
- [ ] Run it, confirm it fails (types don't exist yet)
- [ ] Add `ISaleRepository` and `EfSaleRepository`
- [ ] Run it, confirm it passes
- [ ] Commit: "Add ISaleRepository for shared Sale+Transaction persistence"

## Task 2: Simulated fleet card authorization

**Files:**
- Create: `MAUI-POS-DASH.Core/FleetCard/IFleetCardAuthorizationService.cs`
- Create: `MAUI-POS-DASH.Core/FleetCard/SimulatedFleetCardAuthorizationService.cs`
- Create: `MAUI-POS-DASH.Core.Tests/FleetCard/SimulatedFleetCardAuthorizationServiceTests.cs`

```csharp
// IFleetCardAuthorizationService.cs
namespace MAUI_POS_DASH.Core.FleetCard;

public enum FleetCardAuthorizationStatus
{
    Approved,
    Declined
}

public record FleetCardAuthorizationResult(FleetCardAuthorizationStatus Status, string? DeclineReason);

/// <summary>
/// We shape this after ICardReaderService — a real fleet-card backend adapter is a drop-in
/// implementation of this interface, not a rewrite of anything that calls it.
/// </summary>
public interface IFleetCardAuthorizationService
{
    Task<FleetCardAuthorizationResult> AuthorizeAsync(decimal amount, CancellationToken cancellationToken = default);
}
```

```csharp
// SimulatedFleetCardAuthorizationService.cs
namespace MAUI_POS_DASH.Core.FleetCard;

/// <summary>
/// We stand in for a real fleet-card backend until one exists. About one in five authorizations
/// is declined, so the terminal UI has a real decline path to demonstrate without needing a real
/// backend to simulate declining against.
/// </summary>
public class SimulatedFleetCardAuthorizationService : IFleetCardAuthorizationService
{
    #region Fields
    private const double DeclineChance = 0.2;
    private readonly Random _random;
    #endregion

    #region Constructor
    public SimulatedFleetCardAuthorizationService(Random random)
    {
        _random = random;
    }
    #endregion

    #region Public Methods
    public Task<FleetCardAuthorizationResult> AuthorizeAsync(decimal amount, CancellationToken cancellationToken = default)
    {
        bool declined = _random.NextDouble() < DeclineChance;
        var result = declined
            ? new FleetCardAuthorizationResult(FleetCardAuthorizationStatus.Declined, "Card declined by the fleet operator.")
            : new FleetCardAuthorizationResult(FleetCardAuthorizationStatus.Approved, null);

        return Task.FromResult(result);
    }
    #endregion
}
```

Test with a `Random` subclass forced to a fixed `NextDouble()` return (mirrors
`FixedTimeProvider` in `TransactionIngestionServiceTests`):

```csharp
private sealed class FixedRandom : Random
{
    private readonly double _value;
    public FixedRandom(double value) => _value = value;
    public override double NextDouble() => _value;
}
```

- [ ] Write tests: `NextDouble` returning `0.1` (below threshold) → Declined; `0.5` (above) → Approved
- [ ] Run them, confirm they fail (types don't exist yet)
- [ ] Add `IFleetCardAuthorizationService` and `SimulatedFleetCardAuthorizationService`
- [ ] Run them, confirm they pass
- [ ] Commit: "Add simulated fleet card authorization"

## Task 3: FleetCardSaleService orchestration

**Files:**
- Create: `MAUI-POS-DASH.Core/FleetCard/FleetCardSaleResult.cs`
- Create: `MAUI-POS-DASH.Core/FleetCard/FleetCardSaleService.cs`
- Create: `MAUI-POS-DASH.Core.Tests/FleetCard/FleetCardSaleServiceTests.cs`

```csharp
// FleetCardSaleResult.cs
namespace MAUI_POS_DASH.Core.FleetCard;

public enum FleetCardSaleStatus
{
    Approved,
    Declined,
    CardReadFailed
}

public record FleetCardSaleResult(FleetCardSaleStatus Status, Sale? Sale, Transaction? Transaction, string DetailMessage);
```

```csharp
// FleetCardSaleService.cs
using MAUI_POS_DASH.Core.Devices;
using MAUI_POS_DASH.Core.Sales;

namespace MAUI_POS_DASH.Core.FleetCard;

/// <summary>
/// We own the whole tap-card payment flow — waiting for the card, authorizing it, and recording
/// the Sale and Transaction on approval — so FleetCardSale.razor only has to present state.
/// </summary>
public class FleetCardSaleService
{
    #region Fields
    private readonly ICardReaderService _cardReader;
    private readonly IFleetCardAuthorizationService _authorizationService;
    private readonly ISaleRepository _saleRepository;
    #endregion

    #region Constructor
    public FleetCardSaleService(
        ICardReaderService cardReader,
        IFleetCardAuthorizationService authorizationService,
        ISaleRepository saleRepository)
    {
        _cardReader = cardReader;
        _authorizationService = authorizationService;
        _saleRepository = saleRepository;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// We validate the amount before ever waiting for a card tap — an attendant shouldn't have to
    /// tap a card only to be told the amount they typed was invalid.
    /// </summary>
    public async Task<FleetCardSaleResult> ProcessSaleAsync(Guid shiftId, decimal amount, CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("A fleet card sale must have a positive amount.", nameof(amount));
        }

        CardReadResult cardRead = await _cardReader.WaitForCardAsync(cancellationToken);
        if (cardRead.Status != CardReadStatus.Success)
        {
            return new FleetCardSaleResult(
                FleetCardSaleStatus.CardReadFailed,
                Sale: null,
                Transaction: null,
                DetailMessage: DescribeCardReadFailure(cardRead.Status));
        }

        FleetCardAuthorizationResult authorization = await _authorizationService.AuthorizeAsync(amount, cancellationToken);
        if (authorization.Status == FleetCardAuthorizationStatus.Declined)
        {
            return new FleetCardSaleResult(
                FleetCardSaleStatus.Declined,
                Sale: null,
                Transaction: null,
                DetailMessage: authorization.DeclineReason ?? "Card declined by the fleet operator.");
        }

        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            ShiftId = shiftId,
            OccurredAt = DateTimeOffset.UtcNow,
            Lines =
            [
                new SaleLine
                {
                    Id = Guid.NewGuid(),
                    Description = "Fuel",
                    UnitPrice = amount,
                    Quantity = 1
                }
            ]
        };
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            SaleId = sale.Id,
            Method = PaymentMethod.FleetCard,
            Amount = amount,
            Status = TransactionStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _saleRepository.AddSaleWithTransactionAsync(sale, transaction, cancellationToken);

        return new FleetCardSaleResult(FleetCardSaleStatus.Approved, sale, transaction, "Payment approved.");
    }
    #endregion

    #region Private Methods
    private static string DescribeCardReadFailure(CardReadStatus status) => status switch
    {
        CardReadStatus.Timeout => "No card was presented in time.",
        CardReadStatus.DeviceUnavailable => "The card reader isn't available right now.",
        CardReadStatus.Cancelled => "The card read was cancelled.",
        _ => "The card couldn't be read."
    };
    #endregion
}
```

Note `SaleLine.SaleId` isn't set explicitly above — it's populated by EF Core's shadow FK fixup
when the line is added to `Sale.Lines` and the graph is saved (same as how
`TransactionIngestionServiceTests` doesn't manually set `Sale`/`SaleId` navigation properties
either). Verify this is actually true against the real `SaleConfiguration` FK mapping when
writing the test below — if not, set `SaleId = sale.Id` explicitly on the line.

Fakes for the test (hand-written, not mocks, per project convention):

```csharp
private sealed class FakeCardReaderService : ICardReaderService
{
    public event EventHandler<CardTapEventArgs>? CardPresented;
    public CardReadResult NextResult { get; set; } = new(CardReadStatus.Success, "**** 4242", null);
    public Task<CardReadResult> WaitForCardAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(NextResult);
}

private sealed class FakeFleetCardAuthorizationService : IFleetCardAuthorizationService
{
    public FleetCardAuthorizationResult NextResult { get; set; } =
        new(FleetCardAuthorizationStatus.Approved, null);
    public Task<FleetCardAuthorizationResult> AuthorizeAsync(decimal amount, CancellationToken cancellationToken = default) =>
        Task.FromResult(NextResult);
}
```

- [ ] Write tests against a real seeded `TerminalDbContext` + `EfSaleRepository` + the two fakes:
      Approved persists Sale+Transaction; Declined persists nothing; CardReadFailed persists
      nothing; invalid amount throws `ArgumentException` and never calls the fake card reader
      (assert via a call-count field on the fake)
- [ ] Run them, confirm they fail (types don't exist yet)
- [ ] Add `FleetCardSaleResult` and `FleetCardSaleService`
- [ ] Run them, confirm they pass
- [ ] Commit: "Add FleetCardSaleService orchestration"

## Task 4: Wire DI and build the page

**Files:**
- Modify: `MAUI-POS-DASH/MauiProgram.cs`
- Modify: `MAUI-POS-DASH/Components/Pages/FleetCardSale.razor`

In `RegisterCoreServices`:
```csharp
services.AddScoped<ISaleRepository, EfSaleRepository>();
services.AddSingleton<Random>(Random.Shared);
services.AddSingleton<IFleetCardAuthorizationService, SimulatedFleetCardAuthorizationService>();
services.AddScoped<FleetCardSaleService>();
```

Page, following `ShiftClose.razor`'s guard pattern and `AttendantManagement.razor`'s
try/catch-and-show-message pattern:

```razor
@page "/fleet-card"
@inject AttendantService AttendantService
@inject ShiftService ShiftService
@inject FleetCardSaleService FleetCardSaleService
@inject NavigationManager Navigation

<PageTitle>Fleet Card</PageTitle>

<h1>Fleet Card</h1>

@if (_shift is null && _message is null)
{
    <p>Loading…</p>
}
else if (_shift is null)
{
    <p>@_message</p>
    <a href="/">Back to terminal home</a>
}
else if (_result is null)
{
    <label>
        Amount
        <input type="number" step="0.01" @bind="_amount" class="mock-input" />
    </label>
    <button class="mock-button" @onclick="ProcessSaleAsync" disabled="@_processing">
        @(_processing ? "Waiting for card…" : "Take Payment")
    </button>
    @if (_errorMessage is not null)
    {
        <p>@_errorMessage</p>
    }
}
else
{
    <p>@_result.DetailMessage</p>
    @if (_result.Status == FleetCardSaleStatus.Approved)
    {
        <p>Sale @_result.Sale!.Id — R@_result.Transaction!.Amount</p>
    }
    <button class="mock-button" @onclick="ResetForm">Take Another Payment</button>
    <a href="/">Back to terminal home</a>
}

@code {
    private Shift? _shift;
    private string? _message;
    private decimal _amount;
    private bool _processing;
    private string? _errorMessage;
    private FleetCardSaleResult? _result;

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
            _message = "There's no open shift — open one before taking payments.";
        }
    }

    private async Task ProcessSaleAsync()
    {
        _errorMessage = null;
        _processing = true;

        try
        {
            _result = await FleetCardSaleService.ProcessSaleAsync(_shift!.Id, _amount);
        }
        catch (ArgumentException ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            _processing = false;
        }
    }

    private void ResetForm()
    {
        _amount = 0;
        _result = null;
        _errorMessage = null;
    }
}
```

- [ ] Add the DI registrations
- [ ] Build the razor page
- [ ] Build the MAUI Android target, confirm 0 warnings/0 errors
- [ ] Commit: "Wire Fleet Card module into DI and the terminal UI"

## Task 5: Full verification and PR

- [ ] `dotnet test MAUI-POS-DASH.Core.Tests` — confirm all pass, no regressions
- [ ] `dotnet build MAUI-POS-DASH.slnx` — confirm 0 warnings/0 errors including Android
- [ ] Update `docs/ARCHITECTURE.md` row to `Architect (done)` and the module README status
- [ ] Push `task/03-fleet-card-module`, open PR
