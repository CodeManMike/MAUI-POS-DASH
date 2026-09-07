# Cash Module Implementation Plan

**Goal:** Replace the `CashSale.razor` placeholder with a working cash-tender flow, add eager
Till creation at shift open, and surface till reconciliation before a shift close is confirmed —
per `docs/superpowers/specs/2026-09-07-cash-module-design.md`.

---

## Task 1: ITillRepository

**Files:**
- Create: `MAUI-POS-DASH.Core/Shifts/ITillRepository.cs`
- Create: `MAUI-POS-DASH.Core.Persistence/Repositories/EfTillRepository.cs`
- Create: `MAUI-POS-DASH.Core.Tests/Shifts/EfTillRepositoryTests.cs`

```csharp
// ITillRepository.cs
namespace MAUI_POS_DASH.Core.Shifts;

/// <summary>
/// We keep Till persistence behind this interface — one Till per shift, created when the shift
/// opens and updated by whichever payment module records a tender against it.
/// </summary>
public interface ITillRepository
{
    Task CreateAsync(Till till, CancellationToken cancellationToken = default);

    Task<Till?> GetByShiftIdAsync(Guid shiftId, CancellationToken cancellationToken = default);

    Task UpdateAsync(Till till, CancellationToken cancellationToken = default);
}
```

```csharp
// EfTillRepository.cs
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
```

- [ ] Write `EfTillRepositoryTests`: create-then-fetch-by-shift round-trips; update persists a
      changed `CashTotal`
- [ ] Run, confirm failure (types don't exist)
- [ ] Add `ITillRepository` and `EfTillRepository`
- [ ] Run, confirm pass
- [ ] Commit: "Add ITillRepository"

## Task 2: ShiftService gets a Till lifecycle

**Files:**
- Modify: `MAUI-POS-DASH.Core/Shifts/ShiftService.cs`
- Modify: `MAUI-POS-DASH.Core.Tests/Shifts/ShiftServiceTests.cs`

Constructor grows to take `ITillRepository` and `TillReconciliationService`:

```csharp
public ShiftService(
    IShiftRepository shiftRepository,
    ITillRepository tillRepository,
    TillReconciliationService tillReconciliationService)
{
    _shiftRepository = shiftRepository;
    _tillRepository = tillRepository;
    _tillReconciliationService = tillReconciliationService;
}
```

`OpenShiftAsync` gains, right after `_shiftRepository.OpenAsync(shift)` succeeds and before
returning:

```csharp
var till = new Till
{
    Id = Guid.NewGuid(),
    ShiftId = shift.Id,
    CashTotal = 0,
    FleetCardTotal = 0,
    MobileMoneyTotal = 0
};
await _tillRepository.CreateAsync(till, cancellationToken);
```

New method:

```csharp
/// <summary>
/// We compare what the till should have against what the attendant counted, without writing
/// anything — ShiftClose.razor shows this before the attendant confirms the close is final.
/// </summary>
public async Task<ReconciliationResult> PreviewCloseAsync(Shift shift, decimal cashCounted, CancellationToken cancellationToken = default)
{
    var till = await _tillRepository.GetByShiftIdAsync(shift.Id, cancellationToken)
        ?? throw new InvalidOperationException($"Shift {shift.Id} has no till — every open shift should have one.");

    return _tillReconciliationService.Reconcile(till, cashCounted);
}
```

`CloseShiftAsync` is unchanged.

- [ ] Update `ShiftServiceTests`'s `SetUp` to construct `ShiftService` with a real
      `EfTillRepository` and a real `TillReconciliationService` (no fakes needed — both are cheap,
      real collaborators, consistent with this project's fixture-over-mock preference)
- [ ] Write new tests: `OpenShiftAsync_NoActiveShift_CreatesOpenShiftWithGivenFloat` (extend the
      existing test to also assert a matching zero-total Till now exists);
      `PreviewCloseAsync_CashMatchesTillTotal_ReturnsWithinTolerance`;
      `PreviewCloseAsync_CashOutsideTolerance_ReturnsNotWithinTolerance`;
      `PreviewCloseAsync_DoesNotMutateShiftOrTill` (shift stays Open, Till.CashTotal unchanged)
- [ ] Run, confirm the new/changed tests fail first (compile error until the constructor changes,
      then behavioral failure for the new tests)
- [ ] Apply the `ShiftService` changes above
- [ ] Run, confirm all pass
- [ ] Commit: "Give ShiftService a Till lifecycle: eager creation, close-preview reconciliation"

## Task 3: CashSaleService

**Files:**
- Create: `MAUI-POS-DASH.Core/Cash/CashSaleResult.cs`
- Create: `MAUI-POS-DASH.Core/Cash/CashSaleService.cs`
- Create: `MAUI-POS-DASH.Core.Tests/Cash/CashSaleServiceTests.cs`

```csharp
// CashSaleResult.cs
namespace MAUI_POS_DASH.Core.Cash;

public record CashSaleResult(Sale Sale, Transaction Transaction, decimal Change);
```

```csharp
// CashSaleService.cs
using MAUI_POS_DASH.Core.Sales;
using MAUI_POS_DASH.Core.Shifts;

namespace MAUI_POS_DASH.Core.Cash;

/// <summary>
/// We record a cash tender against a Sale and feed the shift's Till — no card, no network, just
/// arithmetic and persistence, so this is simpler than FleetCardSaleService by design.
/// </summary>
public class CashSaleService
{
    #region Fields
    private readonly ISaleRepository _saleRepository;
    private readonly ITillRepository _tillRepository;
    #endregion

    #region Constructor
    public CashSaleService(ISaleRepository saleRepository, ITillRepository tillRepository)
    {
        _saleRepository = saleRepository;
        _tillRepository = tillRepository;
    }
    #endregion

    #region Public Methods
    public async Task<CashSaleResult> ProcessSaleAsync(
        Guid shiftId,
        decimal amountOwed,
        decimal amountTendered,
        CancellationToken cancellationToken = default)
    {
        if (amountOwed <= 0)
        {
            throw new ArgumentException("A cash sale must have a positive amount owed.", nameof(amountOwed));
        }

        if (amountTendered < amountOwed)
        {
            throw new ArgumentException("Cash tendered must cover the amount owed.", nameof(amountTendered));
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
                    UnitPrice = amountOwed,
                    Quantity = 1
                }
            ]
        };
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            SaleId = sale.Id,
            Method = PaymentMethod.Cash,
            Amount = amountOwed,
            Status = TransactionStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _saleRepository.AddSaleWithTransactionAsync(sale, transaction, cancellationToken);

        var till = await _tillRepository.GetByShiftIdAsync(shiftId, cancellationToken)
            ?? throw new InvalidOperationException($"Shift {shiftId} has no till — every open shift should have one.");
        till.CashTotal += amountOwed;
        await _tillRepository.UpdateAsync(till, cancellationToken);

        return new CashSaleResult(sale, transaction, amountTendered - amountOwed);
    }
    #endregion
}
```

- [ ] Write tests against a real seeded `TerminalDbContext` + `EfSaleRepository` +
      `EfTillRepository` (with a Till pre-seeded for the test shift, mirroring how `OpenShiftAsync`
      will have created one): valid sale persists Sale+Transaction, increments `Till.CashTotal`,
      and returns the right `Change`; non-positive `amountOwed` throws and touches nothing;
      `amountTendered < amountOwed` throws and touches nothing; missing Till throws
      `InvalidOperationException`
- [ ] Run, confirm failure (types don't exist)
- [ ] Add `CashSaleResult` and `CashSaleService`
- [ ] Run, confirm pass
- [ ] Commit: "Add CashSaleService"

## Task 4: Wire DI and build both pages

**Files:**
- Modify: `MAUI-POS-DASH/MauiProgram.cs`
- Modify: `MAUI-POS-DASH/Components/_Imports.razor`
- Modify: `MAUI-POS-DASH/Components/Pages/CashSale.razor`
- Modify: `MAUI-POS-DASH/Components/Pages/ShiftClose.razor`

DI additions in `RegisterCoreServices`:
```csharp
services.AddScoped<ITillRepository, EfTillRepository>();
services.AddScoped<CashSaleService>();
```
(`ShiftService`'s registration line doesn't change — DI resolves its new constructor parameters
automatically since `ITillRepository` and `TillReconciliationService` are already registered or
newly registered here; `TillReconciliationService` has no constructor dependencies so it just
needs `services.AddScoped<TillReconciliationService>();` added if not already present — check
first, since the skeleton phase may have already registered it for future use.)

`_Imports.razor` gains `@using MAUI_POS_DASH.Core.Cash`.

`CashSale.razor` — same guard pattern as `FleetCardSale.razor`, two inputs instead of one:

```razor
@page "/cash"
@inject AttendantService AttendantService
@inject ShiftService ShiftService
@inject CashSaleService CashSaleService
@inject NavigationManager Navigation

<PageTitle>Cash</PageTitle>
<h1>Cash</h1>

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
        Amount owed
        <input type="number" step="0.01" @bind="_amountOwed" class="mock-input" />
    </label>
    <label>
        Cash tendered
        <input type="number" step="0.01" @bind="_amountTendered" class="mock-input" />
    </label>
    <button class="mock-button" @onclick="ProcessSaleAsync">Take Payment</button>
    @if (_errorMessage is not null)
    {
        <p>@_errorMessage</p>
    }
}
else
{
    <p>Change due: R@_result.Change</p>
    <p>Sale @_result.Sale.Id — R@_result.Transaction.Amount</p>
    <button class="mock-button" @onclick="ResetForm">Take Another Payment</button>
    <a href="/">Back to terminal home</a>
}

@code {
    private Shift? _shift;
    private string? _message;
    private decimal _amountOwed;
    private decimal _amountTendered;
    private string? _errorMessage;
    private CashSaleResult? _result;

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

        try
        {
            _result = await CashSaleService.ProcessSaleAsync(_shift!.Id, _amountOwed, _amountTendered);
        }
        catch (ArgumentException ex)
        {
            _errorMessage = ex.Message;
        }
    }

    private void ResetForm()
    {
        _amountOwed = 0;
        _amountTendered = 0;
        _result = null;
        _errorMessage = null;
    }
}
```

`ShiftClose.razor` — two-step reconciliation:

```razor
@page "/shift/close"
@inject AttendantService AttendantService
@inject ShiftService ShiftService
@inject NavigationManager Navigation

<PageTitle>Close Shift</PageTitle>
<h1>Close Shift</h1>

@if (_shift is not null && _reconciliation is null)
{
    <label>
        Cash counted
        <input type="number" step="0.01" @bind="_cashCounted" class="mock-input" />
    </label>
    <button class="mock-button" @onclick="ReviewAsync">Review</button>
}
else if (_reconciliation is not null)
{
    <p>Expected cash: R@_reconciliation.ExpectedCash</p>
    <p>Counted cash: R@_reconciliation.CountedCash</p>
    <p>Variance: R@_reconciliation.Variance @(_reconciliation.WithinTolerance ? "(within tolerance)" : "(OUT OF TOLERANCE)")</p>
    <button class="mock-button" @onclick="ConfirmCloseAsync">Confirm Close</button>
    <button class="mock-button" @onclick="() => _reconciliation = null">Recount</button>
}
else if (_message is not null)
{
    <p>@_message</p>
}

@code {
    private Shift? _shift;
    private decimal _cashCounted;
    private string? _message;
    private ReconciliationResult? _reconciliation;

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

    private async Task ReviewAsync()
    {
        _reconciliation = await ShiftService.PreviewCloseAsync(_shift!, _cashCounted);
    }

    private async Task ConfirmCloseAsync()
    {
        await ShiftService.CloseShiftAsync(_shift!, _cashCounted);
        Navigation.NavigateTo("/");
    }
}
```

- [ ] Add the DI registrations
- [ ] Update `_Imports.razor`
- [ ] Replace `CashSale.razor` and `ShiftClose.razor`
- [ ] Build the MAUI Android target, confirm 0 warnings/0 errors
- [ ] Commit: "Wire Cash module into DI and the terminal UI"

## Task 5: Full verification and PR

- [ ] `dotnet test MAUI-POS-DASH.Core.Tests` — confirm all pass, no regressions
- [ ] `dotnet build MAUI-POS-DASH.slnx` — confirm 0 warnings/0 errors including Android
- [ ] Update `docs/ARCHITECTURE.md` row to `Architect (done)`, update the "placeholder" note, and
      the module README
- [ ] Push `task/04-cash-module`, open PR
