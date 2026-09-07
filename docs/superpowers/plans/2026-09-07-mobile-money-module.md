# Mobile Money Module Implementation Plan

**Goal:** Replace the `MobileMoneySale.razor` placeholder with a working confirmation-based
payment flow, per `docs/superpowers/specs/2026-09-07-mobile-money-module-design.md`.

---

## Task 1: Simulated mobile money confirmation

**Files:**
- Create: `MAUI-POS-DASH.Core/MobileMoney/IMobileMoneyPaymentService.cs`
- Create: `MAUI-POS-DASH.Core/MobileMoney/SimulatedMobileMoneyPaymentService.cs`
- Create: `MAUI-POS-DASH.Core.Tests/MobileMoney/SimulatedMobileMoneyPaymentServiceTests.cs`

```csharp
// IMobileMoneyPaymentService.cs
namespace MAUI_POS_DASH.Core.MobileMoney;

public enum MobileMoneyPaymentStatus
{
    Confirmed,
    Declined,
    TimedOut
}

public record MobileMoneyPaymentResult(MobileMoneyPaymentStatus Status, string? DetailMessage);

/// <summary>
/// We shape this after IFleetCardAuthorizationService — a real mobile money provider adapter is a
/// drop-in implementation of this interface, not a rewrite of anything that calls it.
/// </summary>
public interface IMobileMoneyPaymentService
{
    Task<MobileMoneyPaymentResult> RequestPaymentAsync(decimal amount, CancellationToken cancellationToken = default);
}
```

```csharp
// SimulatedMobileMoneyPaymentService.cs
namespace MAUI_POS_DASH.Core.MobileMoney;

/// <summary>
/// We stand in for a real mobile money provider until one exists. A confirmation takes a
/// simulated few seconds — a customer checking their phone genuinely takes longer than a card
/// tap — and resolves to one of three outcomes so the terminal has real decline and timeout paths
/// to demonstrate without a real provider to trigger them against.
/// </summary>
public class SimulatedMobileMoneyPaymentService : IMobileMoneyPaymentService
{
    #region Fields
    private const double TimeoutChance = 0.15;
    private const double DeclineChance = 0.35;
    private readonly Random _random;
    private readonly TimeSpan _confirmationDelay;
    #endregion

    #region Constructor
    public SimulatedMobileMoneyPaymentService(Random random, TimeSpan confirmationDelay)
    {
        _random = random;
        _confirmationDelay = confirmationDelay;
    }
    #endregion

    #region Public Methods
    public async Task<MobileMoneyPaymentResult> RequestPaymentAsync(decimal amount, CancellationToken cancellationToken = default)
    {
        await Task.Delay(_confirmationDelay, cancellationToken);

        double roll = _random.NextDouble();
        if (roll < TimeoutChance)
        {
            return new MobileMoneyPaymentResult(MobileMoneyPaymentStatus.TimedOut, "The customer didn't confirm in time.");
        }

        if (roll < DeclineChance)
        {
            return new MobileMoneyPaymentResult(MobileMoneyPaymentStatus.Declined, "The customer declined the payment.");
        }

        return new MobileMoneyPaymentResult(MobileMoneyPaymentStatus.Confirmed, null);
    }
    #endregion
}
```

Test with a `Random` subclass forced to a fixed `NextDouble()` return (same `FixedRandom` pattern
as `SimulatedFleetCardAuthorizationServiceTests`), constructed with `TimeSpan.Zero` so tests don't
wait:

- [ ] Write tests: `0.10` → TimedOut; `0.25` → Declined; `0.60` → Confirmed
- [ ] Run, confirm failure (types don't exist)
- [ ] Add `IMobileMoneyPaymentService` and `SimulatedMobileMoneyPaymentService`
- [ ] Run, confirm pass
- [ ] Commit: "Add simulated mobile money confirmation"

## Task 2: MobileMoneySaleService

**Files:**
- Create: `MAUI-POS-DASH.Core/MobileMoney/MobileMoneySaleResult.cs`
- Create: `MAUI-POS-DASH.Core/MobileMoney/MobileMoneySaleService.cs`
- Create: `MAUI-POS-DASH.Core.Tests/MobileMoney/MobileMoneySaleServiceTests.cs`

```csharp
// MobileMoneySaleResult.cs
namespace MAUI_POS_DASH.Core.MobileMoney;

public enum MobileMoneySaleStatus
{
    Confirmed,
    Declined,
    TimedOut
}

public record MobileMoneySaleResult(MobileMoneySaleStatus Status, Sale? Sale, Transaction? Transaction, string DetailMessage);
```

```csharp
// MobileMoneySaleService.cs
using MAUI_POS_DASH.Core.Sales;
using MAUI_POS_DASH.Core.Shifts;

namespace MAUI_POS_DASH.Core.MobileMoney;

/// <summary>
/// We own the whole confirmation-based payment flow — requesting confirmation and recording the
/// Sale, Transaction, and Till update on success — so MobileMoneySale.razor only presents state.
/// </summary>
public class MobileMoneySaleService
{
    #region Fields
    private readonly IMobileMoneyPaymentService _paymentService;
    private readonly ISaleRepository _saleRepository;
    private readonly ITillRepository _tillRepository;
    #endregion

    #region Constructor
    public MobileMoneySaleService(
        IMobileMoneyPaymentService paymentService,
        ISaleRepository saleRepository,
        ITillRepository tillRepository)
    {
        _paymentService = paymentService;
        _saleRepository = saleRepository;
        _tillRepository = tillRepository;
    }
    #endregion

    #region Public Methods
    public async Task<MobileMoneySaleResult> ProcessSaleAsync(Guid shiftId, decimal amount, CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("A mobile money sale must have a positive amount.", nameof(amount));
        }

        MobileMoneyPaymentResult payment = await _paymentService.RequestPaymentAsync(amount, cancellationToken);
        if (payment.Status == MobileMoneyPaymentStatus.Declined)
        {
            return new MobileMoneySaleResult(MobileMoneySaleStatus.Declined, null, null, payment.DetailMessage ?? "The customer declined the payment.");
        }

        if (payment.Status == MobileMoneyPaymentStatus.TimedOut)
        {
            return new MobileMoneySaleResult(MobileMoneySaleStatus.TimedOut, null, null, payment.DetailMessage ?? "The customer didn't confirm in time.");
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
            Method = PaymentMethod.MobileMoney,
            Amount = amount,
            Status = TransactionStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _saleRepository.AddSaleWithTransactionAsync(sale, transaction, cancellationToken);

        var till = await _tillRepository.GetByShiftIdAsync(shiftId, cancellationToken)
            ?? throw new InvalidOperationException($"Shift {shiftId} has no till — every open shift should have one.");
        till.MobileMoneyTotal += amount;
        await _tillRepository.UpdateAsync(till, cancellationToken);

        return new MobileMoneySaleResult(MobileMoneySaleStatus.Confirmed, sale, transaction, "Payment confirmed.");
    }
    #endregion
}
```

Fake for the test (hand-written, not a mock, per project convention — mirrors
`FakeFleetCardAuthorizationService`):

```csharp
private sealed class FakeMobileMoneyPaymentService : IMobileMoneyPaymentService
{
    public MobileMoneyPaymentResult NextResult { get; set; } =
        new(MobileMoneyPaymentStatus.Confirmed, null);
    public int RequestPaymentCallCount { get; private set; }
    public Task<MobileMoneyPaymentResult> RequestPaymentAsync(decimal amount, CancellationToken cancellationToken = default)
    {
        RequestPaymentCallCount++;
        return Task.FromResult(NextResult);
    }
}
```

- [ ] Write tests against a real seeded `TerminalDbContext` + `EfSaleRepository` +
      `EfTillRepository` (with a Till pre-seeded for the test shift) + the fake: Confirmed
      persists Sale+Transaction and increments `Till.MobileMoneyTotal`; Declined persists nothing
      and leaves the Till untouched; TimedOut persists nothing and leaves the Till untouched;
      invalid amount throws `ArgumentException` and never calls the fake (assert via
      `RequestPaymentCallCount`); missing Till on an otherwise-Confirmed outcome throws
      `InvalidOperationException`
- [ ] Run, confirm failure (types don't exist)
- [ ] Add `MobileMoneySaleResult` and `MobileMoneySaleService`
- [ ] Run, confirm pass
- [ ] Commit: "Add MobileMoneySaleService"

## Task 3: Wire DI and build the page

**Files:**
- Modify: `MAUI-POS-DASH/MauiProgram.cs`
- Modify: `MAUI-POS-DASH/Components/_Imports.razor`
- Modify: `MAUI-POS-DASH/Components/Pages/MobileMoneySale.razor`

DI additions in `RegisterCoreServices`:
```csharp
services.AddSingleton<IMobileMoneyPaymentService>(
    _ => new SimulatedMobileMoneyPaymentService(Random.Shared, TimeSpan.FromSeconds(4)));
services.AddScoped<MobileMoneySaleService>();
```
(A factory registration, not `AddSingleton<IMobileMoneyPaymentService, SimulatedMobileMoneyPaymentService>()`,
because the constructor needs the `TimeSpan` argument alongside the already-registered `Random`.)

`_Imports.razor` gains `@using MAUI_POS_DASH.Core.MobileMoney`.

`MobileMoneySale.razor` — same guard/try-catch pattern as `FleetCardSale.razor`:

```razor
@page "/mobile-money"
@inject AttendantService AttendantService
@inject ShiftService ShiftService
@inject MobileMoneySaleService MobileMoneySaleService
@inject NavigationManager Navigation

<PageTitle>Mobile Money</PageTitle>
<h1>Mobile Money</h1>

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
        @(_processing ? "Waiting for customer confirmation…" : "Request Payment")
    </button>
    @if (_errorMessage is not null)
    {
        <p>@_errorMessage</p>
    }
}
else
{
    <p>@_result.DetailMessage</p>
    @if (_result.Status == MobileMoneySaleStatus.Confirmed)
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
    private MobileMoneySaleResult? _result;

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
            _result = await MobileMoneySaleService.ProcessSaleAsync(_shift!.Id, _amount);
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
- [ ] Update `_Imports.razor`
- [ ] Replace `MobileMoneySale.razor`
- [ ] Build the MAUI Android target, confirm 0 warnings/0 errors
- [ ] Commit: "Wire MobileMoney module into DI and the terminal UI"

## Task 4: Full verification and PR

- [ ] `dotnet test MAUI-POS-DASH.Core.Tests` — confirm all pass, no regressions
- [ ] `dotnet build MAUI-POS-DASH.slnx` — confirm 0 warnings/0 errors including Android
- [ ] Update `docs/ARCHITECTURE.md` row to `Architect (done)` and the module README
- [ ] Push `task/05-mobile-money-module`, open PR
