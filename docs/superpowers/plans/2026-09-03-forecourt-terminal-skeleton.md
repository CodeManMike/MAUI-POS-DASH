# Forecourt Terminal Skeleton Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the stock MAUI Blazor Hybrid + Web App template into the platform skeleton described in `docs/superpowers/specs/2026-09-03-forecourt-terminal-architecture-design.md` — real domain models, contracts, device/sync interfaces, two EF-Core-backed persistence contexts with generated migrations, a working MAUI terminal shell, a working Blazor back-office dashboard sharing UI components with the terminal, one NUnit test project demonstrating the team's testing convention, and the docs (`README.md`, `docs/ARCHITECTURE.md`, `AGENTS.md`, `CLAUDE.md`) that let Overmind, Builder, and Architect pick up modules independently afterward.

**Architecture:** `MAUI-POS-DASH.Core` (domain models, DTOs, interfaces, application services — zero EF/ASP.NET dependency) is referenced by everything. `MAUI-POS-DASH.Core.Persistence` (new — EF Core, holds `TerminalDbContext`/SQLite and `BackofficeDbContext`/Postgres) is referenced only by the MAUI terminal app and the Web server, **not** by `.Web.Client` (WASM), so EF Core and Npgsql never end up in the browser bundle. `MAUI-POS-DASH.Shared` stays a pure UI component library and now also references `Core` for the DTO types its components bind to. The MAUI app gets its own local `Routes`/pages (terminal); `.Web.Client` gets its own dashboard pages routed through `Shared`'s `Routes` component via a new `AdditionalAssemblies` parameter.

**Tech Stack:** .NET 10, .NET MAUI (Android-only), Blazor Hybrid + Blazor Web App, EF Core 10 (Sqlite + Npgsql providers), Riok.Mapperly 4.3, NUnit 4.6.

**Note on the spec's file tree:** the spec listed `Persistence/` as a folder inside `Core`. This plan splits it into its own project, `Core.Persistence`, for a concrete technical reason: `.Web.Client` runs in the browser via WebAssembly, and if it referenced `Core` with EF Core/Npgsql baked in, that would pull a database driver into the client bundle (Npgsql doesn't function in browser-wasm at all). Splitting persistence out keeps `Core` — and therefore `Shared` and `.Web.Client` — dependency-light. Everything else matches the spec as approved.

---

## Task 1: Solution restructure — new projects, references, Android-only MAUI target

**Files:**
- Create: `MAUI-POS-DASH.Core/MAUI-POS-DASH.Core.csproj`, `MAUI-POS-DASH.Core/GlobalUsings.cs`
- Create: `MAUI-POS-DASH.Core.Persistence/MAUI-POS-DASH.Core.Persistence.csproj`, `MAUI-POS-DASH.Core.Persistence/GlobalUsings.cs`
- Create: `MAUI-POS-DASH.Core.Tests/MAUI-POS-DASH.Core.Tests.csproj`, `MAUI-POS-DASH.Core.Tests/GlobalUsings.cs`
- Modify: `MAUI-POS-DASH.slnx`
- Modify: `MAUI-POS-DASH/MAUI-POS-DASH.csproj` (trim to Android-only, add project references)
- Modify: `MAUI-POS-DASH.Web/MAUI-POS-DASH.Web.csproj` (add project references)
- Modify: `MAUI-POS-DASH.Web.Client/MAUI-POS-DASH.Web.Client.csproj` (add Core reference)
- Modify: `MAUI-POS-DASH.Shared/MAUI-POS-DASH.Shared.csproj` (add Core reference)

- [ ] **Step 1: Create the three new projects**

```bash
dotnet new classlib -n MAUI-POS-DASH.Core -o MAUI-POS-DASH.Core
dotnet new classlib -n MAUI-POS-DASH.Core.Persistence -o MAUI-POS-DASH.Core.Persistence
dotnet new nunit -n MAUI-POS-DASH.Core.Tests -o MAUI-POS-DASH.Core.Tests
```

Delete the template-generated placeholder source files so they don't collide with what we add later:

```bash
rm MAUI-POS-DASH.Core/Class1.cs
rm MAUI-POS-DASH.Core.Persistence/Class1.cs
rm MAUI-POS-DASH.Core.Tests/UnitTest1.cs
```

- [ ] **Step 2: Set each new project's `.csproj`**

`MAUI-POS-DASH.Core/MAUI-POS-DASH.Core.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Riok.Mapperly" Version="4.3.1" PrivateAssets="all" />
  </ItemGroup>

</Project>
```

`MAUI-POS-DASH.Core.Persistence/MAUI-POS-DASH.Core.Persistence.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.11" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.11" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.11" PrivateAssets="all" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.3" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\MAUI-POS-DASH.Core\MAUI-POS-DASH.Core.csproj" />
  </ItemGroup>

</Project>
```

`MAUI-POS-DASH.Core.Tests/MAUI-POS-DASH.Core.Tests.csproj` (the `nunit` template already adds NUnit/NUnit3TestAdapter/Microsoft.NET.Test.Sdk — replace the whole file so the versions are pinned and the target framework matches the rest of the solution):

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.9.0" />
    <PackageReference Include="NUnit" Version="4.6.1" />
    <PackageReference Include="NUnit3TestAdapter" Version="6.3.0" />
    <PackageReference Include="NUnit.Analyzers" Version="4.6.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\MAUI-POS-DASH.Core\MAUI-POS-DASH.Core.csproj" />
    <ProjectReference Include="..\MAUI-POS-DASH.Core.Persistence\MAUI-POS-DASH.Core.Persistence.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Add GlobalUsings.cs to each new project**

`MAUI-POS-DASH.Core/GlobalUsings.cs`:

```csharp
global using MAUI_POS_DASH.Core.Domain;
```

`MAUI-POS-DASH.Core.Persistence/GlobalUsings.cs`:

```csharp
global using Microsoft.EntityFrameworkCore;
global using Microsoft.EntityFrameworkCore.Metadata.Builders;
global using MAUI_POS_DASH.Core.Domain;
```

`MAUI-POS-DASH.Core.Tests/GlobalUsings.cs`:

```csharp
global using NUnit.Framework;
global using Microsoft.EntityFrameworkCore;
global using MAUI_POS_DASH.Core.Domain;
global using MAUI_POS_DASH.Core.Persistence;
```

- [ ] **Step 4: Add the three new projects to the solution**

```bash
dotnet sln MAUI-POS-DASH.slnx add MAUI-POS-DASH.Core/MAUI-POS-DASH.Core.csproj
dotnet sln MAUI-POS-DASH.slnx add MAUI-POS-DASH.Core.Persistence/MAUI-POS-DASH.Core.Persistence.csproj
dotnet sln MAUI-POS-DASH.slnx add MAUI-POS-DASH.Core.Tests/MAUI-POS-DASH.Core.Tests.csproj
```

- [ ] **Step 5: Trim the MAUI app to Android-only**

In `MAUI-POS-DASH/MAUI-POS-DASH.csproj`, replace the multi-target `<TargetFrameworks>` block and the now-irrelevant iOS/MacCatalyst/Windows `<SupportedOSPlatformVersion>`/`<TargetPlatformMinVersion>` lines:

```xml
  <PropertyGroup>
    <TargetFramework>net10.0-android</TargetFramework>

    <OutputType>Exe</OutputType>
    <RootNamespace>MAUI_POS_DASH</RootNamespace>
    <UseMaui>true</UseMaui>
    <SingleProject>true</SingleProject>
    <ImplicitUsings>enable</ImplicitUsings>
    <EnableDefaultCssItems>false</EnableDefaultCssItems>
    <Nullable>enable</Nullable>
    <MauiXamlInflator>SourceGen</MauiXamlInflator>

    <!-- Display name -->
    <ApplicationTitle>MAUI-POS-DASH</ApplicationTitle>

    <!-- App Identifier -->
    <ApplicationId>com.companyname.mauiposdash</ApplicationId>

    <!-- Versions -->
    <ApplicationDisplayVersion>1.0</ApplicationDisplayVersion>
    <ApplicationVersion>1</ApplicationVersion>

    <SupportedOSPlatformVersion>24.0</SupportedOSPlatformVersion>
  </PropertyGroup>
```

(Leave the `<ItemGroup>` blocks for `MauiIcon`/`MauiSplashScreen`/`MauiImage`/`MauiFont`/`MauiAsset` exactly as they are.)

- [ ] **Step 6: Wire project references**

```bash
dotnet add MAUI-POS-DASH/MAUI-POS-DASH.csproj reference MAUI-POS-DASH.Core/MAUI-POS-DASH.Core.csproj
dotnet add MAUI-POS-DASH/MAUI-POS-DASH.csproj reference MAUI-POS-DASH.Core.Persistence/MAUI-POS-DASH.Core.Persistence.csproj
dotnet add MAUI-POS-DASH/MAUI-POS-DASH.csproj package Microsoft.Extensions.Http --version 10.0.11

dotnet add MAUI-POS-DASH.Web/MAUI-POS-DASH.Web.csproj reference MAUI-POS-DASH.Core/MAUI-POS-DASH.Core.csproj
dotnet add MAUI-POS-DASH.Web/MAUI-POS-DASH.Web.csproj reference MAUI-POS-DASH.Core.Persistence/MAUI-POS-DASH.Core.Persistence.csproj

dotnet add MAUI-POS-DASH.Web.Client/MAUI-POS-DASH.Web.Client.csproj reference MAUI-POS-DASH.Core/MAUI-POS-DASH.Core.csproj

dotnet add MAUI-POS-DASH.Shared/MAUI-POS-DASH.Shared.csproj reference MAUI-POS-DASH.Core/MAUI-POS-DASH.Core.csproj
```

- [ ] **Step 7: Verify the solution still restores**

```bash
dotnet restore MAUI-POS-DASH.slnx
```

Expected: restores cleanly (Core/Core.Persistence/Core.Tests have no source yet — that's fine, they still compile as empty projects).

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "Restructure solution: add Core/Core.Persistence/Core.Tests, trim MAUI to Android-only"
```

---

## Task 2: Core domain models

**Files:**
- Create: `MAUI-POS-DASH.Core/Domain/AttendantRole.cs`
- Create: `MAUI-POS-DASH.Core/Domain/Attendant.cs`
- Create: `MAUI-POS-DASH.Core/Domain/ShiftStatus.cs`
- Create: `MAUI-POS-DASH.Core/Domain/Shift.cs`
- Create: `MAUI-POS-DASH.Core/Domain/Till.cs`
- Create: `MAUI-POS-DASH.Core/Domain/SaleLine.cs`
- Create: `MAUI-POS-DASH.Core/Domain/Sale.cs`
- Create: `MAUI-POS-DASH.Core/Domain/PaymentMethod.cs`
- Create: `MAUI-POS-DASH.Core/Domain/TransactionStatus.cs`
- Create: `MAUI-POS-DASH.Core/Domain/Transaction.cs`

- [ ] **Step 1: Write the enums**

`MAUI-POS-DASH.Core/Domain/AttendantRole.cs`:

```csharp
namespace MAUI_POS_DASH.Core.Domain;

/// <summary>
/// We use this to gate what an attendant can do on the terminal — a plain Attendant can ring
/// sales, a Supervisor can approve variances, a Manager can do both plus manage other attendants.
/// </summary>
public enum AttendantRole
{
    Attendant,
    Supervisor,
    Manager
}
```

`MAUI-POS-DASH.Core/Domain/ShiftStatus.cs`:

```csharp
namespace MAUI_POS_DASH.Core.Domain;

/// <summary>
/// We track whether a shift is still being worked (<see cref="Open"/>) or has been counted out
/// and finished (<see cref="Closed"/>).
/// </summary>
public enum ShiftStatus
{
    Open,
    Closed
}
```

`MAUI-POS-DASH.Core/Domain/PaymentMethod.cs`:

```csharp
namespace MAUI_POS_DASH.Core.Domain;

/// <summary>
/// We record how a Transaction was tendered — the three ways this terminal accepts payment.
/// </summary>
public enum PaymentMethod
{
    Cash,
    FleetCard,
    MobileMoney
}
```

`MAUI-POS-DASH.Core/Domain/TransactionStatus.cs`:

```csharp
namespace MAUI_POS_DASH.Core.Domain;

/// <summary>
/// We track this per transaction so the offline queue knows what still needs to reach the back
/// office, without needing to ask the network first.
/// </summary>
public enum TransactionStatus
{
    Pending,
    Synced,
    Failed
}
```

- [ ] **Step 2: Write `Attendant`**

`MAUI-POS-DASH.Core/Domain/Attendant.cs`:

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
    #endregion
}
```

- [ ] **Step 3: Write `Shift` and `Till`**

`MAUI-POS-DASH.Core/Domain/Shift.cs`:

```csharp
namespace MAUI_POS_DASH.Core.Domain;

/// <summary>
/// We represent one attendant's time on the till, from open to close, including the float they
/// opened with and (once closed) what they counted at the end.
/// </summary>
public class Shift
{
    #region Properties
    public Guid Id { get; set; }

    public Guid AttendantId { get; set; }

    public Attendant? Attendant { get; set; }

    public DateTimeOffset OpenedAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    public decimal OpeningFloat { get; set; }

    public decimal? ClosingCashCounted { get; set; }

    public ShiftStatus Status { get; set; }

    public Till? Till { get; set; }
    #endregion
}
```

`MAUI-POS-DASH.Core/Domain/Till.cs`:

```csharp
namespace MAUI_POS_DASH.Core.Domain;

/// <summary>
/// We keep one running total per tender type for a shift, so reconciliation at close-out has
/// something concrete to compare the attendant's physical count against.
/// </summary>
public class Till
{
    #region Properties
    public Guid Id { get; set; }

    public Guid ShiftId { get; set; }

    public decimal CashTotal { get; set; }

    public decimal FleetCardTotal { get; set; }

    public decimal MobileMoneyTotal { get; set; }
    #endregion
}
```

- [ ] **Step 4: Write `Sale` and `SaleLine`**

`MAUI-POS-DASH.Core/Domain/SaleLine.cs`:

```csharp
namespace MAUI_POS_DASH.Core.Domain;

/// <summary>
/// We represent one priced line within a Sale — a fuel grade or a shop product, quantity and
/// unit price, with the line total computed rather than stored.
/// </summary>
public class SaleLine
{
    #region Properties
    public Guid Id { get; set; }

    public Guid SaleId { get; set; }

    public required string Description { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal Quantity { get; set; }

    public decimal LineTotal => UnitPrice * Quantity;
    #endregion
}
```

`MAUI-POS-DASH.Core/Domain/Sale.cs`:

```csharp
namespace MAUI_POS_DASH.Core.Domain;

/// <summary>
/// We represent one rung-up sale — one or more fuel/product lines paid for by one or more
/// transactions (split tender is a real forecourt scenario, so Sale and Transaction are
/// separate entities rather than one flat record).
/// </summary>
public class Sale
{
    #region Properties
    public Guid Id { get; set; }

    public Guid ShiftId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public List<SaleLine> Lines { get; set; } = [];

    public decimal Total => Lines.Sum(line => line.LineTotal);
    #endregion
}
```

- [ ] **Step 5: Write `Transaction`**

`MAUI-POS-DASH.Core/Domain/Transaction.cs`:

```csharp
namespace MAUI_POS_DASH.Core.Domain;

/// <summary>
/// We represent one payment against a Sale. This is the unit the offline queue and the sync
/// service operate on.
/// </summary>
public class Transaction
{
    #region Properties
    public Guid Id { get; set; }

    public Guid SaleId { get; set; }

    public Sale? Sale { get; set; }

    public PaymentMethod Method { get; set; }

    public decimal Amount { get; set; }

    public TransactionStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? SyncedAt { get; set; }
    #endregion
}
```

- [ ] **Step 6: Build Core**

```bash
dotnet build MAUI-POS-DASH.Core/MAUI-POS-DASH.Core.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 7: Commit**

```bash
git add MAUI-POS-DASH.Core/Domain
git commit -m "Add Core domain models"
```

---

## Task 3: Core DTOs and the Mapperly mapper

**Files:**
- Create: `MAUI-POS-DASH.Core/Contracts/AttendantDto.cs`
- Create: `MAUI-POS-DASH.Core/Contracts/ShiftDto.cs`
- Create: `MAUI-POS-DASH.Core/Contracts/TillDto.cs`
- Create: `MAUI-POS-DASH.Core/Contracts/SaleLineDto.cs`
- Create: `MAUI-POS-DASH.Core/Contracts/SaleDto.cs`
- Create: `MAUI-POS-DASH.Core/Contracts/TransactionDto.cs`
- Create: `MAUI-POS-DASH.Core/Contracts/EntityMapper.cs`

- [ ] **Step 1: Write the DTOs**

`MAUI-POS-DASH.Core/Contracts/AttendantDto.cs`:

```csharp
namespace MAUI_POS_DASH.Core.Contracts;

/// <summary>
/// We expose just enough of an Attendant to render a name and role in the UI — never the PIN hash.
/// </summary>
public record AttendantDto(Guid Id, string Name, AttendantRole Role);
```

`MAUI-POS-DASH.Core/Contracts/ShiftDto.cs`:

```csharp
namespace MAUI_POS_DASH.Core.Contracts;

/// <summary>
/// We flatten the attendant's name onto the shift so UI components don't need a second lookup.
/// Callers must eager-load <c>Shift.Attendant</c> before mapping — the mapper can't do that for
/// them, it only reshapes what's already loaded.
/// </summary>
public record ShiftDto(
    Guid Id,
    Guid AttendantId,
    string AttendantName,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    decimal OpeningFloat,
    decimal? ClosingCashCounted,
    ShiftStatus Status);
```

`MAUI-POS-DASH.Core/Contracts/TillDto.cs`:

```csharp
namespace MAUI_POS_DASH.Core.Contracts;

/// <summary>
/// We mirror Till's per-tender totals and add GrandTotal as a derived convenience for the UI —
/// GrandTotal isn't mapped from the entity, it's computed here from the three totals.
/// </summary>
public record TillDto(Guid Id, Guid ShiftId, decimal CashTotal, decimal FleetCardTotal, decimal MobileMoneyTotal)
{
    public decimal GrandTotal => CashTotal + FleetCardTotal + MobileMoneyTotal;
}
```

`MAUI-POS-DASH.Core/Contracts/SaleLineDto.cs`:

```csharp
namespace MAUI_POS_DASH.Core.Contracts;

/// <summary>
/// We mirror SaleLine's priced fields; LineTotal is computed here rather than mapped.
/// </summary>
public record SaleLineDto(Guid Id, string Description, decimal UnitPrice, decimal Quantity)
{
    public decimal LineTotal => UnitPrice * Quantity;
}
```

`MAUI-POS-DASH.Core/Contracts/SaleDto.cs`:

```csharp
namespace MAUI_POS_DASH.Core.Contracts;

/// <summary>
/// We mirror Sale's lines; Total is computed here from the mapped Lines, not mapped itself.
/// </summary>
public record SaleDto(Guid Id, Guid ShiftId, DateTimeOffset OccurredAt, IReadOnlyList<SaleLineDto> Lines)
{
    public decimal Total => Lines.Sum(line => line.LineTotal);
}
```

`MAUI-POS-DASH.Core/Contracts/TransactionDto.cs`:

```csharp
namespace MAUI_POS_DASH.Core.Contracts;

/// <summary>
/// We mirror Transaction 1:1 — this is what crosses the wire to the sync endpoint and what the
/// dashboard reads back.
/// </summary>
public record TransactionDto(
    Guid Id,
    Guid SaleId,
    PaymentMethod Method,
    decimal Amount,
    TransactionStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SyncedAt);
```

- [ ] **Step 2: Write the Mapperly mapper**

`MAUI-POS-DASH.Core/Contracts/EntityMapper.cs`:

```csharp
using Riok.Mapperly.Abstractions;

namespace MAUI_POS_DASH.Core.Contracts;

/// <summary>
/// We generate entity&lt;-&gt;DTO mapping at compile time with Mapperly instead of AutoMapper —
/// AutoMapper moved to a commercial license in 2024, and Mapperly's source-generated output is
/// inspectable (check the generated .g.cs under obj/ if you want to see exactly what runs).
/// </summary>
[Mapper]
public partial class EntityMapper
{
    #region Attendant
    [MapperIgnoreSource(nameof(Attendant.PinHash))]
    public partial AttendantDto ToDto(Attendant attendant);
    #endregion

    #region Shift
    [MapperIgnoreSource(nameof(Shift.Till))]
    public partial ShiftDto ToDto(Shift shift);
    #endregion

    #region Till
    public partial TillDto ToDto(Till till);
    #endregion

    #region Sale
    [MapperIgnoreSource(nameof(SaleLine.SaleId))]
    public partial SaleLineDto ToDto(SaleLine line);

    public partial SaleDto ToDto(Sale sale);
    #endregion

    #region Transaction
    [MapperIgnoreSource(nameof(Transaction.Sale))]
    public partial TransactionDto ToDto(Transaction transaction);
    #endregion
}
```

- [ ] **Step 3: Build and confirm Mapperly generated the bodies**

```bash
dotnet build MAUI-POS-DASH.Core/MAUI-POS-DASH.Core.csproj
```

Expected: `Build succeeded.` with 0 warnings — the `[MapperIgnoreSource]` attributes above declare, on purpose, the four source members that don't have a DTO counterpart (`PinHash` never leaves Core; `Shift.Till`/`Transaction.Sale` navigations are exposed as their own DTOs rather than nested; `SaleLine.SaleId` is redundant once nested under `SaleDto`). Without them Mapperly still generates working code, just with `RMG020` "unmapped source member" warnings — if you see one for a member not in this list, that's a real gap, not an expected one. If it instead reports an unmapped-*target*-member diagnostic (e.g. `RMG012`) on `ShiftDto.AttendantName`, confirm `Attendant.Name` exists and re-run; Mapperly's flattening convention matches `Attendant.Name` to `AttendantName` by name concatenation automatically.

- [ ] **Step 4: Commit**

```bash
git add MAUI-POS-DASH.Core/Contracts
git commit -m "Add Core DTOs and Mapperly entity mapper"
```

---

## Task 4: Core device interfaces

**Files:**
- Create: `MAUI-POS-DASH.Core/Devices/ICardReaderService.cs`
- Create: `MAUI-POS-DASH.Core/Devices/IReceiptPrinterService.cs`
- Create: `MAUI-POS-DASH.Core/Devices/IBarcodeScannerService.cs`

- [ ] **Step 1: Write the card reader contract**

`MAUI-POS-DASH.Core/Devices/ICardReaderService.cs`:

```csharp
namespace MAUI_POS_DASH.Core.Devices;

public enum CardReadStatus
{
    Success,
    Timeout,
    DeviceUnavailable,
    Cancelled
}

public record CardReadResult(CardReadStatus Status, string? MaskedPan, string? TrackData);

public class CardTapEventArgs : EventArgs
{
    public required string MaskedPan { get; init; }

    public required DateTimeOffset PresentedAt { get; init; }
}

/// <summary>
/// We shape this after PAX's real card reader SDKs — an awaitable read plus a fire-and-forget
/// presented event — so a real PAX adapter is a drop-in implementation of this interface rather
/// than a rewrite of anything that calls it.
/// </summary>
public interface ICardReaderService
{
    event EventHandler<CardTapEventArgs>? CardPresented;

    Task<CardReadResult> WaitForCardAsync(CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Write the receipt printer contract**

`MAUI-POS-DASH.Core/Devices/IReceiptPrinterService.cs`:

```csharp
namespace MAUI_POS_DASH.Core.Devices;

public enum PrintStatus
{
    Success,
    DeviceUnavailable,
    OutOfPaper
}

public record PrintResult(PrintStatus Status);

public record ReceiptLine(string Text, bool Bold = false, bool CenterAligned = false);

/// <summary>
/// We use a plain line-based document, matching how ESC/POS printers (what PAX devices use)
/// actually render — no rich layout, just ordered lines with a couple of style flags.
/// </summary>
public record ReceiptDocument(IReadOnlyList<ReceiptLine> Lines);

/// <summary>
/// We shape this after PAX's ESC/POS-style printer SDK — a document of ordered lines in, a
/// result out — so a real PAX adapter is a drop-in implementation.
/// </summary>
public interface IReceiptPrinterService
{
    Task<PrintResult> PrintAsync(ReceiptDocument document, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 3: Write the barcode scanner contract**

`MAUI-POS-DASH.Core/Devices/IBarcodeScannerService.cs`:

```csharp
namespace MAUI_POS_DASH.Core.Devices;

public class BarcodeScannedEventArgs : EventArgs
{
    public required string Value { get; init; }

    public required DateTimeOffset ScannedAt { get; init; }
}

/// <summary>
/// We model the scanner as a plain event source, matching how PAX's hardware scanner callback
/// works — there's no "wait for a scan" call, just a stream of scan events while active.
/// </summary>
public interface IBarcodeScannerService
{
    event EventHandler<BarcodeScannedEventArgs>? BarcodeScanned;
}
```

- [ ] **Step 4: Build and commit**

```bash
dotnet build MAUI-POS-DASH.Core/MAUI-POS-DASH.Core.csproj
git add MAUI-POS-DASH.Core/Devices
git commit -m "Add Core device interfaces (PAX-shaped, simulator-backed for now)"
```

---

## Task 5: Core shift and sync abstractions and services

**Files:**
- Create: `MAUI-POS-DASH.Core/Shifts/IShiftRepository.cs`
- Create: `MAUI-POS-DASH.Core/Shifts/ShiftService.cs`
- Create: `MAUI-POS-DASH.Core/Shifts/TillReconciliationService.cs`
- Create: `MAUI-POS-DASH.Core/Sync/ITransactionSyncService.cs`
- Create: `MAUI-POS-DASH.Core/Sync/ITransactionQueueStore.cs`
- Create: `MAUI-POS-DASH.Core/Sync/OfflineTransactionQueue.cs`

- [ ] **Step 1: Write `IShiftRepository` and `ShiftService`**

`MAUI-POS-DASH.Core/Shifts/IShiftRepository.cs`:

```csharp
namespace MAUI_POS_DASH.Core.Shifts;

/// <summary>
/// We keep ShiftService's storage access behind this interface so it doesn't know or care
/// whether shifts live in SQLite, Postgres, or an in-memory fixture in a test.
/// </summary>
public interface IShiftRepository
{
    Task<Shift?> GetActiveShiftAsync(Guid attendantId, CancellationToken cancellationToken = default);

    Task<Shift> OpenAsync(Shift shift, CancellationToken cancellationToken = default);

    Task CloseAsync(Shift shift, CancellationToken cancellationToken = default);
}
```

`MAUI-POS-DASH.Core/Shifts/ShiftService.cs`:

```csharp
namespace MAUI_POS_DASH.Core.Shifts;

/// <summary>
/// We own the shift lifecycle — opening a shift for an attendant and closing it out again.
/// </summary>
public class ShiftService
{
    #region Fields
    private readonly IShiftRepository _shiftRepository;
    #endregion

    #region Constructor
    public ShiftService(IShiftRepository shiftRepository)
    {
        _shiftRepository = shiftRepository;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// We open a new shift for the given attendant with the counted opening float. We refuse if
    /// that attendant already has an open shift — one attendant, one till, one shift at a time.
    /// </summary>
    public async Task<Shift> OpenShiftAsync(Guid attendantId, decimal openingFloat, CancellationToken cancellationToken = default)
    {
        var existing = await _shiftRepository.GetActiveShiftAsync(attendantId, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException(
                $"We can't open a new shift for attendant {attendantId} — shift {existing.Id} is still open.");
        }

        var shift = new Shift
        {
            Id = Guid.NewGuid(),
            AttendantId = attendantId,
            OpenedAt = DateTimeOffset.UtcNow,
            OpeningFloat = openingFloat,
            Status = ShiftStatus.Open
        };

        return await _shiftRepository.OpenAsync(shift, cancellationToken);
    }

    /// <summary>
    /// We close the shift and record what the attendant counted in the till.
    /// TODO(Builder/Overmind): once the Cash module lands, run TillReconciliationService here
    /// and surface the variance before allowing close to complete.
    /// </summary>
    public Task CloseShiftAsync(Shift shift, decimal closingCashCounted, CancellationToken cancellationToken = default)
    {
        shift.ClosedAt = DateTimeOffset.UtcNow;
        shift.ClosingCashCounted = closingCashCounted;
        shift.Status = ShiftStatus.Closed;

        return _shiftRepository.CloseAsync(shift, cancellationToken);
    }
    #endregion
}
```

- [ ] **Step 2: Write `TillReconciliationService`**

`MAUI-POS-DASH.Core/Shifts/TillReconciliationService.cs`:

```csharp
namespace MAUI_POS_DASH.Core.Shifts;

public record ReconciliationResult(decimal ExpectedCash, decimal CountedCash, decimal Variance, bool WithinTolerance);

/// <summary>
/// We compare a till's recorded totals against what an attendant physically counted at close-out.
/// </summary>
public class TillReconciliationService
{
    #region Fields
    private const decimal ToleranceAmount = 0.50m;
    #endregion

    #region Public Methods
    /// <summary>
    /// We compare the till's recorded cash total against what the attendant physically counted,
    /// flagging anything outside a small tolerance for a supervisor to review. A real till never
    /// balances to the cent — a small tolerance is normal, not a bug.
    /// </summary>
    public ReconciliationResult Reconcile(Till till, decimal countedCash)
    {
        var variance = countedCash - till.CashTotal;
        var withinTolerance = Math.Abs(variance) <= ToleranceAmount;

        return new ReconciliationResult(till.CashTotal, countedCash, variance, withinTolerance);
    }
    #endregion
}
```

- [ ] **Step 3: Write the sync abstractions**

`MAUI-POS-DASH.Core/Sync/ITransactionSyncService.cs`:

```csharp
namespace MAUI_POS_DASH.Core.Sync;

public enum SyncStatus
{
    Success,
    NetworkUnavailable,
    ServerRejected
}

public record SyncResult(SyncStatus Status, int TransactionsSynced);

/// <summary>
/// We keep this decoupled from HTTP on purpose — Core doesn't know or care that the real
/// implementation is an HttpClient call to the Web app's sync endpoint.
/// </summary>
public interface ITransactionSyncService
{
    Task<SyncResult> SyncAsync(IReadOnlyList<Transaction> pendingTransactions, CancellationToken cancellationToken = default);
}
```

`MAUI-POS-DASH.Core/Sync/ITransactionQueueStore.cs`:

```csharp
namespace MAUI_POS_DASH.Core.Sync;

/// <summary>
/// We give the offline queue somewhere to read and update pending transactions without knowing
/// whether that storage is SQLite, Postgres, or something else — Core.Persistence supplies the
/// real implementation.
/// </summary>
public interface ITransactionQueueStore
{
    Task<IReadOnlyList<Transaction>> GetPendingAsync(CancellationToken cancellationToken = default);

    Task MarkSyncedAsync(IReadOnlyList<Guid> transactionIds, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: Write `OfflineTransactionQueue`**

`MAUI-POS-DASH.Core/Sync/OfflineTransactionQueue.cs`:

```csharp
namespace MAUI_POS_DASH.Core.Sync;

/// <summary>
/// We hold transactions that have been saved locally but not yet confirmed by the back office,
/// and periodically try to flush them once connectivity allows.
/// </summary>
public class OfflineTransactionQueue
{
    #region Fields
    private readonly ITransactionQueueStore _store;
    private readonly ITransactionSyncService _syncService;
    #endregion

    #region Constructor
    public OfflineTransactionQueue(ITransactionQueueStore store, ITransactionSyncService syncService)
    {
        _store = store;
        _syncService = syncService;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// We attempt to flush every pending transaction to the back office. A sale is never blocked
    /// on this running — by the time it's called, the sale is already safely on local disk.
    /// </summary>
    public async Task<SyncResult> TryFlushAsync(CancellationToken cancellationToken = default)
    {
        var pending = await _store.GetPendingAsync(cancellationToken);
        if (pending.Count == 0)
        {
            return new SyncResult(SyncStatus.Success, 0);
        }

        var result = await _syncService.SyncAsync(pending, cancellationToken);
        if (result.Status == SyncStatus.Success)
        {
            await _store.MarkSyncedAsync(pending.Select(transaction => transaction.Id).ToList(), cancellationToken);
        }

        return result;
    }
    #endregion
}
```

- [ ] **Step 5: Build and commit**

```bash
dotnet build MAUI-POS-DASH.Core/MAUI-POS-DASH.Core.csproj
git add MAUI-POS-DASH.Core/Shifts MAUI-POS-DASH.Core/Sync
git commit -m "Add Core shift and offline-sync services"
```

---

## Task 6: Core module stub folders

**Files:**
- Create: `MAUI-POS-DASH.Core/Modules/FleetCard/README.md`
- Create: `MAUI-POS-DASH.Core/Modules/MobileMoney/README.md`
- Create: `MAUI-POS-DASH.Core/Modules/Cash/README.md`
- Create: `MAUI-POS-DASH.Core/Modules/AttendantMgmt/README.md`

- [ ] **Step 1: Write each module README**

`MAUI-POS-DASH.Core/Modules/FleetCard/README.md`:

```markdown
# Fleet Card module

**Status:** not started. **Owner:** unclaimed.

Handles fleet card authorization and settlement for a fuel sale — the fleet operator's card is
tapped/inserted via `ICardReaderService`, the terminal authorizes against the fleet card
backend, and a `Transaction` with `PaymentMethod.FleetCard` is created.

**Depends on:** `Core/Devices/ICardReaderService.cs`, `Core/Domain/PaymentMethod.cs`.

**To claim this module:** add your name to the Owner line above, update the row in
`docs/ARCHITECTURE.md`, and open a PR/branch scoped to this folder plus the
`MAUI-POS-DASH/Components/Pages/FleetCardSale.razor` placeholder page.
```

`MAUI-POS-DASH.Core/Modules/MobileMoney/README.md`:

```markdown
# Mobile Money module

**Status:** not started. **Owner:** unclaimed.

Handles mobile money payment for a fuel sale — a QR code or USSD prompt is shown, the customer
confirms on their phone, and the terminal polls (or is notified) that the payment cleared before
creating a `Transaction` with `PaymentMethod.MobileMoney`.

**Depends on:** `Core/Sync/ITransactionSyncService.cs` (mobile money confirmation is inherently a
network-round-trip, unlike cash or fleet card).

**To claim this module:** add your name to the Owner line above, update the row in
`docs/ARCHITECTURE.md`, and open a PR/branch scoped to this folder plus the
`MAUI-POS-DASH/Components/Pages/MobileMoneySale.razor` placeholder page.
```

`MAUI-POS-DASH.Core/Modules/Cash/README.md`:

```markdown
# Cash module

**Status:** not started. **Owner:** unclaimed.

Handles cash tender and change calculation for a fuel sale, and feeds `Till.CashTotal` so
`TillReconciliationService` has something real to reconcile against at shift close.

**Depends on:** `Core/Shifts/TillReconciliationService.cs`, `Core/Domain/Till.cs`.

**To claim this module:** add your name to the Owner line above, update the row in
`docs/ARCHITECTURE.md`, and open a PR/branch scoped to this folder plus the
`MAUI-POS-DASH/Components/Pages/CashSale.razor` placeholder page.
```

`MAUI-POS-DASH.Core/Modules/AttendantMgmt/README.md`:

```markdown
# Attendant Management module

**Status:** not started. **Owner:** unclaimed.

Handles attendant PIN login, PIN hashing/verification, and CRUD for attendant records —
currently `MAUI-POS-DASH/Components/Pages/Login.razor` is a UI-only placeholder with no real
authentication behind it.

**Depends on:** `Core/Domain/Attendant.cs`.

**To claim this module:** add your name to the Owner line above, update the row in
`docs/ARCHITECTURE.md`, and open a PR/branch scoped to this folder plus the
`MAUI-POS-DASH/Components/Pages/Login.razor` and
`MAUI-POS-DASH/Components/Pages/AttendantManagement.razor` pages.
```

- [ ] **Step 2: Commit**

```bash
git add MAUI-POS-DASH.Core/Modules
git commit -m "Add module stub folders with ownership READMEs"
```

---

## Task 7: Core.Persistence — DbContexts, entity configurations, repositories

**Files:**
- Create: `MAUI-POS-DASH.Core.Persistence/Configurations/AttendantConfiguration.cs`
- Create: `MAUI-POS-DASH.Core.Persistence/Configurations/ShiftConfiguration.cs`
- Create: `MAUI-POS-DASH.Core.Persistence/Configurations/TillConfiguration.cs`
- Create: `MAUI-POS-DASH.Core.Persistence/Configurations/SaleConfiguration.cs`
- Create: `MAUI-POS-DASH.Core.Persistence/Configurations/SaleLineConfiguration.cs`
- Create: `MAUI-POS-DASH.Core.Persistence/Configurations/TransactionConfiguration.cs`
- Create: `MAUI-POS-DASH.Core.Persistence/TerminalDbContext.cs`
- Create: `MAUI-POS-DASH.Core.Persistence/BackofficeDbContext.cs`
- Create: `MAUI-POS-DASH.Core.Persistence/Repositories/EfShiftRepository.cs`
- Create: `MAUI-POS-DASH.Core.Persistence/Repositories/EfTransactionQueueStore.cs`

- [ ] **Step 1: Write the entity configurations**

`MAUI-POS-DASH.Core.Persistence/Configurations/AttendantConfiguration.cs`:

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
    }
}
```

`MAUI-POS-DASH.Core.Persistence/Configurations/ShiftConfiguration.cs`:

```csharp
namespace MAUI_POS_DASH.Core.Persistence.Configurations;

/// <summary>We configure Shift's key, decimal precision, and its relationships to Attendant and Till.</summary>
public class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.HasKey(shift => shift.Id);
        builder.Property(shift => shift.OpeningFloat).HasPrecision(18, 2);
        builder.Property(shift => shift.ClosingCashCounted).HasPrecision(18, 2);

        builder.HasOne(shift => shift.Attendant)
            .WithMany()
            .HasForeignKey(shift => shift.AttendantId);

        builder.HasOne(shift => shift.Till)
            .WithOne()
            .HasForeignKey<Till>(till => till.ShiftId);
    }
}
```

`MAUI-POS-DASH.Core.Persistence/Configurations/TillConfiguration.cs`:

```csharp
namespace MAUI_POS_DASH.Core.Persistence.Configurations;

/// <summary>We configure Till's key and the decimal precision of its three running totals.</summary>
public class TillConfiguration : IEntityTypeConfiguration<Till>
{
    public void Configure(EntityTypeBuilder<Till> builder)
    {
        builder.HasKey(till => till.Id);
        builder.Property(till => till.CashTotal).HasPrecision(18, 2);
        builder.Property(till => till.FleetCardTotal).HasPrecision(18, 2);
        builder.Property(till => till.MobileMoneyTotal).HasPrecision(18, 2);
    }
}
```

`MAUI-POS-DASH.Core.Persistence/Configurations/SaleConfiguration.cs`:

```csharp
namespace MAUI_POS_DASH.Core.Persistence.Configurations;

/// <summary>We configure Sale's key and its one-to-many relationship to SaleLine.</summary>
public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.HasKey(sale => sale.Id);
        builder.HasMany(sale => sale.Lines)
            .WithOne()
            .HasForeignKey(line => line.SaleId);
    }
}
```

`MAUI-POS-DASH.Core.Persistence/Configurations/SaleLineConfiguration.cs`:

```csharp
namespace MAUI_POS_DASH.Core.Persistence.Configurations;

/// <summary>We configure SaleLine's key and the decimal precision of price and quantity.</summary>
public class SaleLineConfiguration : IEntityTypeConfiguration<SaleLine>
{
    public void Configure(EntityTypeBuilder<SaleLine> builder)
    {
        builder.HasKey(line => line.Id);
        builder.Property(line => line.Description).IsRequired().HasMaxLength(200);
        builder.Property(line => line.UnitPrice).HasPrecision(18, 2);
        builder.Property(line => line.Quantity).HasPrecision(18, 3);
    }
}
```

`MAUI-POS-DASH.Core.Persistence/Configurations/TransactionConfiguration.cs`:

```csharp
namespace MAUI_POS_DASH.Core.Persistence.Configurations;

/// <summary>We configure Transaction's key, amount precision, and its relationship to Sale.</summary>
public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.HasKey(transaction => transaction.Id);
        builder.Property(transaction => transaction.Amount).HasPrecision(18, 2);

        builder.HasOne(transaction => transaction.Sale)
            .WithMany()
            .HasForeignKey(transaction => transaction.SaleId);
    }
}
```

- [ ] **Step 2: Write the two DbContexts**

`MAUI-POS-DASH.Core.Persistence/TerminalDbContext.cs`:

```csharp
namespace MAUI_POS_DASH.Core.Persistence;

/// <summary>
/// We back this with SQLite on-device — every write lands here first, offline, before the
/// OfflineTransactionQueue ever tries to reach the back office.
/// </summary>
public class TerminalDbContext : DbContext
{
    #region Constructor
    public TerminalDbContext(DbContextOptions<TerminalDbContext> options) : base(options)
    {
    }
    #endregion

    #region DbSets
    public DbSet<Attendant> Attendants => Set<Attendant>();

    public DbSet<Shift> Shifts => Set<Shift>();

    public DbSet<Till> Tills => Set<Till>();

    public DbSet<Sale> Sales => Set<Sale>();

    public DbSet<SaleLine> SaleLines => Set<SaleLine>();

    public DbSet<Transaction> Transactions => Set<Transaction>();
    #endregion

    #region Overrides
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TerminalDbContext).Assembly);
    }
    #endregion
}
```

`MAUI-POS-DASH.Core.Persistence/BackofficeDbContext.cs`:

```csharp
namespace MAUI_POS_DASH.Core.Persistence;

/// <summary>
/// We back this with Postgres — it's the server of record the dashboard reads from, populated
/// only by what terminals have successfully synced.
/// </summary>
public class BackofficeDbContext : DbContext
{
    #region Constructor
    public BackofficeDbContext(DbContextOptions<BackofficeDbContext> options) : base(options)
    {
    }
    #endregion

    #region DbSets
    public DbSet<Attendant> Attendants => Set<Attendant>();

    public DbSet<Shift> Shifts => Set<Shift>();

    public DbSet<Till> Tills => Set<Till>();

    public DbSet<Sale> Sales => Set<Sale>();

    public DbSet<SaleLine> SaleLines => Set<SaleLine>();

    public DbSet<Transaction> Transactions => Set<Transaction>();
    #endregion

    #region Overrides
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BackofficeDbContext).Assembly);
    }
    #endregion
}
```

- [ ] **Step 3: Write the repository implementations**

`MAUI-POS-DASH.Core.Persistence/Repositories/EfShiftRepository.cs`:

```csharp
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
```

`MAUI-POS-DASH.Core.Persistence/Repositories/EfTransactionQueueStore.cs`:

```csharp
using MAUI_POS_DASH.Core.Sync;

namespace MAUI_POS_DASH.Core.Persistence.Repositories;

/// <summary>
/// We implement ITransactionQueueStore against TerminalDbContext so OfflineTransactionQueue has
/// somewhere real to read pending transactions from and mark them synced.
/// </summary>
public class EfTransactionQueueStore : ITransactionQueueStore
{
    #region Fields
    private readonly TerminalDbContext _dbContext;
    #endregion

    #region Constructor
    public EfTransactionQueueStore(TerminalDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    #endregion

    #region Public Methods
    public async Task<IReadOnlyList<Transaction>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        // List<T> implicitly satisfies IReadOnlyList<T> as a return value, so this awaits and
        // returns directly with no extra casting.
        return await _dbContext.Transactions
            .Where(transaction => transaction.Status == TransactionStatus.Pending)
            .ToListAsync(cancellationToken);
    }

    public async Task MarkSyncedAsync(IReadOnlyList<Guid> transactionIds, CancellationToken cancellationToken = default)
    {
        var toUpdate = await _dbContext.Transactions
            .Where(transaction => transactionIds.Contains(transaction.Id))
            .ToListAsync(cancellationToken);

        foreach (var transaction in toUpdate)
        {
            transaction.Status = TransactionStatus.Synced;
            transaction.SyncedAt = DateTimeOffset.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
    #endregion
}
```

- [ ] **Step 4: Build and commit**

```bash
dotnet build MAUI-POS-DASH.Core.Persistence/MAUI-POS-DASH.Core.Persistence.csproj
git add MAUI-POS-DASH.Core.Persistence/Configurations MAUI-POS-DASH.Core.Persistence/TerminalDbContext.cs MAUI-POS-DASH.Core.Persistence/BackofficeDbContext.cs MAUI-POS-DASH.Core.Persistence/Repositories
git commit -m "Add EF Core DbContexts, entity configurations, and repositories"
```

---

## Task 8: Design-time factories, dotnet-ef tool, and initial migrations

**Files:**
- Create: `dotnet-tools.json` (path as created by this SDK's `dotnet new tool-manifest` — older SDKs use `.config/dotnet-tools.json`)
- Create: `MAUI-POS-DASH.Core.Persistence/DesignTime/TerminalDbContextFactory.cs`
- Create: `MAUI-POS-DASH.Core.Persistence/DesignTime/BackofficeDbContextFactory.cs`
- Create (generated): `MAUI-POS-DASH.Core.Persistence/Migrations/Terminal/*`
- Create (generated): `MAUI-POS-DASH.Core.Persistence/Migrations/Backoffice/*`

- [ ] **Step 1: Add a local `dotnet-ef` tool so every contributor runs the same version**

```bash
dotnet new tool-manifest
dotnet tool install dotnet-ef --version 10.0.11
```

Expected: creates a tool manifest (this SDK puts it at repo-root `dotnet-tools.json`), committed so `dotnet tool restore` gives Overmind, Builder, and Architect the identical `dotnet-ef` version.

- [ ] **Step 2: Write the design-time factories**

These exist **only** so `dotnet ef migrations add` can construct a DbContext without a startup
project — they use a fixed local dev connection string and are never referenced at runtime (each
host wires its own real options via DI in later tasks).

`MAUI-POS-DASH.Core.Persistence/DesignTime/TerminalDbContextFactory.cs`:

```csharp
using Microsoft.EntityFrameworkCore.Design;

namespace MAUI_POS_DASH.Core.Persistence.DesignTime;

/// <summary>
/// We exist only for `dotnet ef migrations add` tooling — nothing at runtime uses this factory.
/// </summary>
public class TerminalDbContextFactory : IDesignTimeDbContextFactory<TerminalDbContext>
{
    public TerminalDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TerminalDbContext>();
        optionsBuilder.UseSqlite("Data Source=terminal.designtime.db");
        return new TerminalDbContext(optionsBuilder.Options);
    }
}
```

`MAUI-POS-DASH.Core.Persistence/DesignTime/BackofficeDbContextFactory.cs`:

```csharp
using Microsoft.EntityFrameworkCore.Design;

namespace MAUI_POS_DASH.Core.Persistence.DesignTime;

/// <summary>
/// We exist only for `dotnet ef migrations add` tooling — nothing at runtime uses this factory.
/// The connection string here is a local-only default; it is never a real deployed database.
/// </summary>
public class BackofficeDbContextFactory : IDesignTimeDbContextFactory<BackofficeDbContext>
{
    public BackofficeDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BackofficeDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=mauiposdash_dev;Username=postgres;Password=postgres");
        return new BackofficeDbContext(optionsBuilder.Options);
    }
}
```

- [ ] **Step 3: Generate the SQLite migration**

```bash
dotnet ef migrations add InitialCreate --project MAUI-POS-DASH.Core.Persistence --context TerminalDbContext --output-dir Migrations/Terminal
```

Expected: creates `MAUI-POS-DASH.Core.Persistence/Migrations/Terminal/<timestamp>_InitialCreate.cs`, `.Designer.cs`, and `TerminalDbContextModelSnapshot.cs`.

- [ ] **Step 4: Generate the Postgres migration**

This one needs a reachable Postgres server to *apply* later, but `migrations add` only needs to
build the model — it does not connect to the database. If it fails because no Postgres server is
reachable at all (some EF provider validation does touch the connection), start a throwaway local
Postgres container first:

```bash
docker run --rm -d --name mauiposdash-devpg -e POSTGRES_PASSWORD=postgres -p 5432:5432 postgres:16-alpine
```

Then:

```bash
dotnet ef migrations add InitialCreate --project MAUI-POS-DASH.Core.Persistence --context BackofficeDbContext --output-dir Migrations/Backoffice
```

Expected: creates `MAUI-POS-DASH.Core.Persistence/Migrations/Backoffice/<timestamp>_InitialCreate.cs`, `.Designer.cs`, and `BackofficeDbContextModelSnapshot.cs`. If a container was started for this, stop it afterward:

```bash
docker stop mauiposdash-devpg
```

- [ ] **Step 5: Commit**

Note: this SDK's `dotnet new tool-manifest` places the manifest at the repo root
(`dotnet-tools.json`), not under `.config/` as older SDKs did — commit whichever path it actually
created.

```bash
git add dotnet-tools.json MAUI-POS-DASH.Core.Persistence/DesignTime MAUI-POS-DASH.Core.Persistence/Migrations
git commit -m "Add design-time DbContext factories and initial EF Core migrations"
```

---

## Task 9: Core.Tests — seeded-fixture NUnit tests

**Files:**
- Create: `MAUI-POS-DASH.Core.Tests/Shifts/TillReconciliationServiceTests.cs`
- Create: `MAUI-POS-DASH.Core.Tests/Shifts/ShiftServiceTests.cs`

- [ ] **Step 1: Write the reconciliation tests (seeded SQLite in-memory fixture)**

`MAUI-POS-DASH.Core.Tests/Shifts/TillReconciliationServiceTests.cs`:

```csharp
using Microsoft.Data.Sqlite;
using MAUI_POS_DASH.Core.Shifts;

namespace MAUI_POS_DASH.Core.Tests.Shifts;

[TestFixture]
public class TillReconciliationServiceTests
{
    #region Fields
    private SqliteConnection _connection = null!;
    private TerminalDbContext _dbContext = null!;
    private TillReconciliationService _sut = null!;
    #endregion

    #region Setup
    [SetUp]
    public void SetUp()
    {
        // We keep one open SQLite in-memory connection alive for the test's lifetime so the
        // schema and foreign keys behave like a real database — the moment the connection
        // closes, the in-memory database is gone, which is exactly the teardown we want.
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<TerminalDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new TerminalDbContext(options);
        _dbContext.Database.EnsureCreated();

        SeedData();

        _sut = new TillReconciliationService();
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

    #region Seed
    private void SeedData()
    {
        var attendant = new Attendant
        {
            Id = Guid.NewGuid(),
            Name = "Homer Simpson",
            PinHash = "hashed-1234",
            Role = AttendantRole.Attendant
        };

        var shift = new Shift
        {
            Id = Guid.NewGuid(),
            AttendantId = attendant.Id,
            Attendant = attendant,
            OpenedAt = DateTimeOffset.UtcNow.AddHours(-4),
            OpeningFloat = 100.00m,
            Status = ShiftStatus.Open
        };

        var till = new Till
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            CashTotal = 250.00m,
            FleetCardTotal = 180.00m,
            MobileMoneyTotal = 60.00m
        };

        _dbContext.Attendants.Add(attendant);
        _dbContext.Shifts.Add(shift);
        _dbContext.Tills.Add(till);
        _dbContext.SaveChanges();
    }
    #endregion

    #region Tests
    [Test]
    public void Reconcile_CountedCashMatchesRecordedCash_ReportsWithinTolerance()
    {
        #region Arrange
        var till = _dbContext.Tills.Single();
        #endregion

        #region Act
        var result = _sut.Reconcile(till, countedCash: 250.00m);
        #endregion

        #region Assert
        Assert.That(result.WithinTolerance, Is.True);
        Assert.That(result.Variance, Is.EqualTo(0m));
        #endregion
    }

    [Test]
    public void Reconcile_CountedCashIsShort_ReportsVarianceOutsideTolerance()
    {
        #region Arrange
        var till = _dbContext.Tills.Single();
        #endregion

        #region Act
        var result = _sut.Reconcile(till, countedCash: 235.00m);
        #endregion

        #region Assert
        Assert.That(result.WithinTolerance, Is.False);
        Assert.That(result.Variance, Is.EqualTo(-15.00m));
        #endregion
    }
    #endregion
}
```

- [ ] **Step 2: Add the SQLite provider's ADO package for the test project**

The `Microsoft.Data.Sqlite` connection type is a transitive dependency already (it ships with
`Microsoft.EntityFrameworkCore.Sqlite`, referenced by `Core.Persistence`), so no new package
reference is needed — just confirm the `using Microsoft.Data.Sqlite;` at the top of the test file
above resolves once you build.

- [ ] **Step 3: Write the shift-open/close tests, using the EF repository directly**

`MAUI-POS-DASH.Core.Tests/Shifts/ShiftServiceTests.cs`:

```csharp
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
```

- [ ] **Step 4: Run the tests**

```bash
dotnet test MAUI-POS-DASH.Core.Tests/MAUI-POS-DASH.Core.Tests.csproj
```

Expected: `Passed! - Failed: 0, Passed: 4, Skipped: 0`.

- [ ] **Step 5: Commit**

```bash
git add MAUI-POS-DASH.Core.Tests/Shifts
git commit -m "Add seeded-fixture NUnit tests for ShiftService and TillReconciliationService"
```

---

## Task 10: MAUI — simulator adapters and the HTTP sync service

**Files:**
- Create: `MAUI-POS-DASH/GlobalUsings.cs`
- Create: `MAUI-POS-DASH/Platforms/Android/Devices/SimulatedPaxCardReader.cs`
- Create: `MAUI-POS-DASH/Platforms/Android/Devices/SimulatedPaxReceiptPrinter.cs`
- Create: `MAUI-POS-DASH/Platforms/Android/Devices/SimulatedPaxBarcodeScanner.cs`
- Create: `MAUI-POS-DASH/Services/HttpTransactionSyncService.cs`

- [ ] **Step 1: Add GlobalUsings**

`MAUI-POS-DASH/GlobalUsings.cs`:

```csharp
global using MAUI_POS_DASH.Core.Domain;
global using MAUI_POS_DASH.Core.Devices;
```

- [ ] **Step 2: Write the simulator adapters**

`MAUI-POS-DASH/Platforms/Android/Devices/SimulatedPaxCardReader.cs`:

```csharp
namespace MAUI_POS_DASH.Platforms.Android.Devices;

/// <summary>
/// We stand in for the real PAX card reader SDK until hardware is available. The shape mirrors
/// PAX's async/callback style so swapping in the real adapter later is a one-file change — no
/// caller of ICardReaderService needs to know the difference.
/// </summary>
public class SimulatedPaxCardReader : ICardReaderService
{
    #region Events
    public event EventHandler<CardTapEventArgs>? CardPresented;
    #endregion

    #region Public Methods
    public async Task<CardReadResult> WaitForCardAsync(CancellationToken cancellationToken = default)
    {
        // We fake a two-second tap delay so the terminal UI has something realistic to show
        // while it waits, without needing real PAX hardware attached.
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

        const string maskedPan = "**** **** **** 4242";
        CardPresented?.Invoke(this, new CardTapEventArgs { MaskedPan = maskedPan, PresentedAt = DateTimeOffset.UtcNow });

        return new CardReadResult(CardReadStatus.Success, maskedPan, TrackData: null);
    }
    #endregion
}
```

`MAUI-POS-DASH/Platforms/Android/Devices/SimulatedPaxReceiptPrinter.cs`:

```csharp
namespace MAUI_POS_DASH.Platforms.Android.Devices;

/// <summary>
/// We stand in for the real PAX receipt printer SDK until hardware is available.
/// </summary>
public class SimulatedPaxReceiptPrinter : IReceiptPrinterService
{
    #region Public Methods
    public Task<PrintResult> PrintAsync(ReceiptDocument document, CancellationToken cancellationToken = default)
    {
        // We write to the debug console instead of driving a real ESC/POS printer — the document
        // shape here is exactly what the real PAX printer adapter will consume.
        foreach (var line in document.Lines)
        {
            System.Diagnostics.Debug.WriteLine(line.Text);
        }

        return Task.FromResult(new PrintResult(PrintStatus.Success));
    }
    #endregion
}
```

`MAUI-POS-DASH/Platforms/Android/Devices/SimulatedPaxBarcodeScanner.cs`:

```csharp
namespace MAUI_POS_DASH.Platforms.Android.Devices;

/// <summary>
/// We stand in for the real PAX barcode scanner SDK until hardware is available.
/// </summary>
public class SimulatedPaxBarcodeScanner : IBarcodeScannerService
{
    #region Events
    public event EventHandler<BarcodeScannedEventArgs>? BarcodeScanned;
    #endregion

    #region Public Methods
    /// <summary>
    /// We expose this so a future demo UI can trigger a fake scan — the real PAX adapter raises
    /// BarcodeScanned from the hardware scanner's own callback instead.
    /// </summary>
    public void SimulateScan(string value)
    {
        BarcodeScanned?.Invoke(this, new BarcodeScannedEventArgs { Value = value, ScannedAt = DateTimeOffset.UtcNow });
    }
    #endregion
}
```

- [ ] **Step 3: Write the HTTP sync service**

`MAUI-POS-DASH/Services/HttpTransactionSyncService.cs`:

```csharp
using System.Net.Http.Json;
using MAUI_POS_DASH.Core.Contracts;
using MAUI_POS_DASH.Core.Sync;

namespace MAUI_POS_DASH.Services;

/// <summary>
/// We post pending transactions to the back office's sync endpoint over HTTP. This is the one
/// piece that actually needs a real network connection — everything upstream of it (the sale,
/// the local write, the queue) already worked offline.
/// </summary>
public class HttpTransactionSyncService : ITransactionSyncService
{
    #region Fields
    private readonly HttpClient _httpClient;
    private readonly EntityMapper _mapper;
    #endregion

    #region Constructor
    public HttpTransactionSyncService(HttpClient httpClient, EntityMapper mapper)
    {
        _httpClient = httpClient;
        _mapper = mapper;
    }
    #endregion

    #region Public Methods
    public async Task<SyncResult> SyncAsync(IReadOnlyList<Transaction> pendingTransactions, CancellationToken cancellationToken = default)
    {
        var dtos = pendingTransactions.Select(_mapper.ToDto).ToList();

        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/transactions", dtos, cancellationToken);
            return response.IsSuccessStatusCode
                ? new SyncResult(SyncStatus.Success, dtos.Count)
                : new SyncResult(SyncStatus.ServerRejected, 0);
        }
        catch (HttpRequestException)
        {
            // We treat a dropped connection as an expected condition on a forecourt, not an
            // error to surface — the queue simply retries next time TryFlushAsync runs.
            return new SyncResult(SyncStatus.NetworkUnavailable, 0);
        }
    }
    #endregion
}
```

- [ ] **Step 4: Commit**

```bash
git add MAUI-POS-DASH/GlobalUsings.cs MAUI-POS-DASH/Platforms/Android/Devices MAUI-POS-DASH/Services
git commit -m "Add PAX-shaped simulator adapters and the HTTP transaction sync service"
```

---

## Task 11: MAUI — DI wiring in MauiProgram.cs

**Files:**
- Modify: `MAUI-POS-DASH/MauiProgram.cs`

- [ ] **Step 1: Replace `MauiProgram.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MAUI_POS_DASH.Core.Contracts;
using MAUI_POS_DASH.Core.Persistence;
using MAUI_POS_DASH.Core.Persistence.Repositories;
using MAUI_POS_DASH.Core.Shifts;
using MAUI_POS_DASH.Core.Sync;
using MAUI_POS_DASH.Platforms.Android.Devices;
using MAUI_POS_DASH.Services;

namespace MAUI_POS_DASH;

public static class MauiProgram
{
    #region Public Methods
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

        RegisterCoreServices(builder.Services);

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
    #endregion

    #region Private Methods
    private static void RegisterCoreServices(IServiceCollection services)
    {
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "terminal.db");
        services.AddDbContext<TerminalDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));

        services.AddScoped<IShiftRepository, EfShiftRepository>();
        services.AddScoped<ITransactionQueueStore, EfTransactionQueueStore>();
        services.AddScoped<ShiftService>();
        services.AddScoped<TillReconciliationService>();
        services.AddScoped<OfflineTransactionQueue>();
        services.AddSingleton<EntityMapper>();

        services.AddSingleton<ICardReaderService, SimulatedPaxCardReader>();
        services.AddSingleton<IReceiptPrinterService, SimulatedPaxReceiptPrinter>();
        services.AddSingleton<IBarcodeScannerService, SimulatedPaxBarcodeScanner>();

        services.AddHttpClient<ITransactionSyncService, HttpTransactionSyncService>(client =>
        {
            // We point at the back office's local dev URL for now — production config will come
            // from appsettings once the Sync module is actually built out.
            client.BaseAddress = new Uri("https://localhost:7135/");
        });
    }
    #endregion
}
```

- [ ] **Step 2: Build the MAUI app for Android**

```bash
dotnet build MAUI-POS-DASH/MAUI-POS-DASH.csproj -f net10.0-android
```

Expected: `Build succeeded.` (This is the first point where a missing project reference or DI registration typo would show up as a compile error — if it fails, re-check Task 1 Step 6's `dotnet add ... reference` calls landed in the `.csproj`.)

- [ ] **Step 3: Commit**

```bash
git add MAUI-POS-DASH/MauiProgram.cs
git commit -m "Wire Core services, EF Core, and simulator adapters into MAUI DI"
```

---

## Task 12: MAUI — terminal shell (routing, layout, pages)

**Files:**
- Create: `MAUI-POS-DASH/Components/Routes.razor`
- Create: `MAUI-POS-DASH/Components/Layout/TerminalLayout.razor`
- Create: `MAUI-POS-DASH/Components/Pages/Home.razor`
- Create: `MAUI-POS-DASH/Components/Pages/Login.razor`
- Create: `MAUI-POS-DASH/Components/Pages/ShiftOpen.razor`
- Create: `MAUI-POS-DASH/Components/Pages/ShiftClose.razor`
- Create: `MAUI-POS-DASH/Components/Pages/FleetCardSale.razor`
- Create: `MAUI-POS-DASH/Components/Pages/MobileMoneySale.razor`
- Create: `MAUI-POS-DASH/Components/Pages/CashSale.razor`
- Create: `MAUI-POS-DASH/Components/Pages/AttendantManagement.razor`
- Modify: `MAUI-POS-DASH/Components/_Imports.razor`
- Modify: `MAUI-POS-DASH/MainPage.xaml`

- [ ] **Step 1: Write the MAUI-local router and terminal layout**

`MAUI-POS-DASH/Components/Routes.razor`:

```razor
<Router AppAssembly="typeof(Routes).Assembly" NotFoundPage="typeof(MAUI_POS_DASH.Shared.Pages.NotFound)">
    <Found Context="routeData">
        <RouteView RouteData="routeData" DefaultLayout="typeof(Layout.TerminalLayout)" />
        <FocusOnNavigate RouteData="routeData" Selector="h1" />
    </Found>
</Router>
```

`MAUI-POS-DASH/Components/Layout/TerminalLayout.razor`:

```razor
@inherits LayoutComponentBase

<div class="terminal-shell">
    <header class="terminal-shell__status-bar">
        <span class="terminal-shell__title">MAUI-POS-DASH Terminal</span>
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
```

- [ ] **Step 2: Write the terminal home page (module tile grid)**

`MAUI-POS-DASH/Components/Pages/Home.razor`:

```razor
@page "/"

<PageTitle>Terminal Home</PageTitle>

<h1>Forecourt Terminal</h1>

<div class="module-grid">
    <a class="module-tile" href="/fleet-card">Fleet Card</a>
    <a class="module-tile" href="/mobile-money">Mobile Money</a>
    <a class="module-tile" href="/cash">Cash</a>
    <a class="module-tile" href="/attendants">Attendants</a>
    <a class="module-tile" href="/shift/open">Open Shift</a>
    <a class="module-tile" href="/shift/close">Close Shift</a>
</div>
```

- [ ] **Step 3: Write the login and shift pages**

`MAUI-POS-DASH/Components/Pages/Login.razor`:

```razor
@page "/login"
@inject NavigationManager Navigation

<PageTitle>Attendant Login</PageTitle>

<h1>Attendant Login</h1>

<p>PIN entry is a UI placeholder in this skeleton — the AttendantMgmt module owns real authentication.</p>

<input type="password" maxlength="6" placeholder="Enter PIN" class="mock-input" />
<button class="mock-button" @onclick='() => Navigation.NavigateTo("/")'>Sign in</button>
```

`MAUI-POS-DASH/Components/Pages/ShiftOpen.razor`:

```razor
@page "/shift/open"

<PageTitle>Open Shift</PageTitle>

<h1>Open Shift</h1>

<label>
    Opening float
    <input type="number" step="0.01" @bind="_openingFloat" class="mock-input" />
</label>

<button class="mock-button" @onclick="OpenShift">Open Shift</button>

@if (_message is not null)
{
    <p>@_message</p>
}

@code {
    private decimal _openingFloat;
    private string? _message;

    private void OpenShift()
    {
        // We don't call ShiftService yet — that needs a signed-in attendant, which the
        // AttendantMgmt module hasn't landed. This proves the page and its field wiring only.
        _message = $"Would open a shift with a float of {_openingFloat:C}.";
    }
}
```

`MAUI-POS-DASH/Components/Pages/ShiftClose.razor`:

```razor
@page "/shift/close"

<PageTitle>Close Shift</PageTitle>

<h1>Close Shift</h1>

<label>
    Cash counted
    <input type="number" step="0.01" @bind="_cashCounted" class="mock-input" />
</label>

<button class="mock-button" @onclick="CloseShift">Close Shift</button>

@if (_message is not null)
{
    <p>@_message</p>
}

@code {
    private decimal _cashCounted;
    private string? _message;

    private void CloseShift()
    {
        // Same story as ShiftOpen — proves the page, not the business logic behind it yet.
        _message = $"Would close the shift with {_cashCounted:C} counted.";
    }
}
```

- [ ] **Step 4: Write the four "coming soon" module pages**

All four follow the same pattern — full content for `FleetCardSale.razor`, then the exact
substitutions for the other three.

`MAUI-POS-DASH/Components/Pages/FleetCardSale.razor`:

```razor
@page "/fleet-card"

<PageTitle>Fleet Card</PageTitle>

<h1>Fleet Card</h1>

<p>This module isn't built yet — see <code>MAUI-POS-DASH.Core/Modules/FleetCard/README.md</code> for its scope and how to claim it.</p>

<a href="/">Back to terminal home</a>
```

Create the other three with these exact substitutions (route, title, heading, and README path change; everything else is identical):

| File | `@page` | `<PageTitle>` | `<h1>` | README path |
|---|---|---|---|---|
| `MobileMoneySale.razor` | `/mobile-money` | `Mobile Money` | `Mobile Money` | `MAUI-POS-DASH.Core/Modules/MobileMoney/README.md` |
| `CashSale.razor` | `/cash` | `Cash` | `Cash` | `MAUI-POS-DASH.Core/Modules/Cash/README.md` |
| `AttendantManagement.razor` | `/attendants` | `Attendants` | `Attendants` | `MAUI-POS-DASH.Core/Modules/AttendantMgmt/README.md` |

- [ ] **Step 5: Update `_Imports.razor` and `MainPage.xaml`**

`MAUI-POS-DASH/Components/_Imports.razor` — add these two lines at the end:

```razor
@using MAUI_POS_DASH.Shared.Components
@using MAUI_POS_DASH.Core.Contracts
```

`MAUI-POS-DASH/MainPage.xaml` — point the root component at the new local `Routes` and drop the now-unused `shared:` namespace:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:local="clr-namespace:MAUI_POS_DASH"
             x:Class="MAUI_POS_DASH.MainPage"
             BackgroundColor="{DynamicResource PageBackgroundColor}">

    <BlazorWebView x:Name="blazorWebView" HostPage="wwwroot/index.html">
        <BlazorWebView.RootComponents>
            <RootComponent Selector="#app" ComponentType="{x:Type local:Components.Routes}" />
        </BlazorWebView.RootComponents>
    </BlazorWebView>

</ContentPage>
```

- [ ] **Step 6: Build**

```bash
dotnet build MAUI-POS-DASH/MAUI-POS-DASH.csproj -f net10.0-android
```

Expected: `Build succeeded.`

- [ ] **Step 7: Commit**

```bash
git add MAUI-POS-DASH/Components MAUI-POS-DASH/MainPage.xaml
git commit -m "Add MAUI terminal shell: routing, layout, and placeholder pages"
```

---

## Task 13: Shared — UI components, Routes parameterization, CSS

**Files:**
- Create: `MAUI-POS-DASH.Shared/Components/StatusBadge.razor`
- Create: `MAUI-POS-DASH.Shared/Components/CurrencyText.razor`
- Create: `MAUI-POS-DASH.Shared/Components/ShiftSummaryCard.razor`
- Create: `MAUI-POS-DASH.Shared/Components/TransactionRow.razor`
- Modify: `MAUI-POS-DASH.Shared/Routes.razor`
- Modify: `MAUI-POS-DASH.Shared/wwwroot/app.css`
- Delete: `MAUI-POS-DASH.Shared/Pages/Home.razor`

- [ ] **Step 1: Write the leaf components first**

`MAUI-POS-DASH.Shared/Components/StatusBadge.razor`:

```razor
<span class="status-badge status-badge--@Status.ToLowerInvariant()">@Status</span>

@code {
    [Parameter, EditorRequired]
    public required string Status { get; set; }
}
```

`MAUI-POS-DASH.Shared/Components/CurrencyText.razor`:

```razor
<span class="currency-text">@Amount.ToString("C")</span>

@code {
    [Parameter]
    public decimal Amount { get; set; }
}
```

- [ ] **Step 2: Write the composite components**

`MAUI-POS-DASH.Shared/Components/ShiftSummaryCard.razor`:

```razor
@using MAUI_POS_DASH.Core.Contracts
@using MAUI_POS_DASH.Core.Domain

<div class="shift-summary-card">
    <div class="shift-summary-card__header">
        <span class="shift-summary-card__attendant">@Shift.AttendantName</span>
        <StatusBadge Status="@(Shift.Status == ShiftStatus.Open ? "Open" : "Closed")" />
    </div>
    <div class="shift-summary-card__body">
        <p>Opened: @Shift.OpenedAt.ToLocalTime().ToString("t")</p>
        @if (Shift.ClosedAt is not null)
        {
            <p>Closed: @Shift.ClosedAt.Value.ToLocalTime().ToString("t")</p>
        }
        <p>
            Opening float: <CurrencyText Amount="Shift.OpeningFloat" />
        </p>
    </div>
</div>

@code {
    [Parameter, EditorRequired]
    public required ShiftDto Shift { get; set; }
}
```

`MAUI-POS-DASH.Shared/Components/TransactionRow.razor`:

```razor
@using MAUI_POS_DASH.Core.Contracts

<div class="transaction-row">
    <span class="transaction-row__method">@Transaction.Method</span>
    <CurrencyText Amount="Transaction.Amount" />
    <StatusBadge Status="@Transaction.Status.ToString()" />
</div>

@code {
    [Parameter, EditorRequired]
    public required TransactionDto Transaction { get; set; }
}
```

- [ ] **Step 3: Parameterize `Routes.razor` so Web can route to `.Web.Client` pages, and drop the stock Home page**

`MAUI-POS-DASH.Shared/Routes.razor`:

```razor
@using System.Reflection

<Router AppAssembly="typeof(Layout.MainLayout).Assembly" AdditionalAssemblies="AdditionalAssemblies" NotFoundPage="typeof(Pages.NotFound)">
    <Found Context="routeData">
        <RouteView RouteData="routeData" DefaultLayout="typeof(Layout.MainLayout)" />
        <FocusOnNavigate RouteData="routeData" Selector="h1" />
    </Found>
</Router>

@code {
    /// <summary>
    /// We accept extra assemblies to scan for routable pages so each host can contribute its own
    /// pages (the Web app's dashboard pages live in .Web.Client) without Shared needing to
    /// reference them directly — that would be a circular project reference.
    /// </summary>
    [Parameter]
    public IEnumerable<Assembly>? AdditionalAssemblies { get; set; }
}
```

```bash
rm MAUI-POS-DASH.Shared/Pages/Home.razor
```

(The MAUI terminal now has its own `Home.razor` at `/`, and Task 15 adds a dashboard page at `/`
in `.Web.Client` — keeping the stock one here would collide with both.)

- [ ] **Step 4: Add CSS for the new components and layouts**

Append to `MAUI-POS-DASH.Shared/wwwroot/app.css`:

```css
.status-badge {
    display: inline-block;
    padding: 0.15rem 0.6rem;
    border-radius: 999px;
    font-size: 0.75rem;
    font-weight: 600;
    text-transform: uppercase;
}

.status-badge--open, .status-badge--synced, .status-badge--success {
    background: #d7f5e3;
    color: #1c7c46;
}

.status-badge--closed, .status-badge--pending {
    background: #fff2cc;
    color: #8a6d00;
}

.status-badge--failed {
    background: #fbdada;
    color: #a31515;
}

.currency-text {
    font-variant-numeric: tabular-nums;
    font-weight: 600;
}

.shift-summary-card, .transaction-row {
    border: 1px solid #e2e2e2;
    border-radius: 8px;
    padding: 0.75rem 1rem;
    margin-bottom: 0.5rem;
}

.shift-summary-card__header, .transaction-row {
    display: flex;
    justify-content: space-between;
    align-items: center;
}

.terminal-shell__status-bar {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 0.75rem 1.25rem;
    background: #1b1b2f;
    color: white;
}

.terminal-shell__body {
    padding: 1.25rem;
}

.module-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(140px, 1fr));
    gap: 1rem;
}

.module-tile {
    display: flex;
    align-items: center;
    justify-content: center;
    height: 100px;
    border-radius: 12px;
    background: #f0f0f5;
    color: #1b1b2f;
    font-weight: 600;
    text-decoration: none;
}

.mock-input, .mock-button {
    display: block;
    margin: 0.5rem 0;
    padding: 0.5rem 0.75rem;
    border-radius: 6px;
    border: 1px solid #ccc;
}

.mock-button {
    background: #1b1b2f;
    color: white;
    border: none;
    cursor: pointer;
}

.dashboard-shell__header {
    padding: 0.75rem 1.25rem;
    background: #14213d;
    color: white;
}

.dashboard-shell__body {
    padding: 1.25rem;
}
```

- [ ] **Step 5: Build**

```bash
dotnet build MAUI-POS-DASH.Shared/MAUI-POS-DASH.Shared.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```bash
git add -A MAUI-POS-DASH.Shared
git commit -m "Add shared UI components, parameterize Routes for multi-host routing"
```

---

## Task 14: Web — BackofficeDbContext wiring and stub sync endpoint

**Files:**
- Create: `MAUI-POS-DASH.Web/GlobalUsings.cs`
- Create: `MAUI-POS-DASH.Web/Api/TransactionsApi.cs`
- Modify: `MAUI-POS-DASH.Web/Program.cs`
- Modify: `MAUI-POS-DASH.Web/Components/App.razor`
- Modify: `MAUI-POS-DASH.Web/appsettings.Development.json`

- [ ] **Step 1: Add GlobalUsings**

`MAUI-POS-DASH.Web/GlobalUsings.cs`:

```csharp
global using MAUI_POS_DASH.Core.Contracts;
global using MAUI_POS_DASH.Core.Persistence;
```

- [ ] **Step 2: Write the stub sync endpoint**

`MAUI-POS-DASH.Web/Api/TransactionsApi.cs`:

```csharp
namespace MAUI_POS_DASH.Web.Api;

public static class TransactionsApi
{
    #region Public Methods
    /// <summary>
    /// We expose the sync endpoint terminals post pending transactions to. This proves the
    /// contract compiles end to end — the actual persist-to-Postgres logic is left for whoever
    /// picks up the Sync side of the back office (see docs/ARCHITECTURE.md).
    /// </summary>
    public static IEndpointRouteBuilder MapTransactionsApi(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/transactions", (List<TransactionDto> transactions) =>
        {
            // TODO(Builder): persist these to BackofficeDbContext and return per-transaction results.
            return Results.StatusCode(StatusCodes.Status501NotImplemented);
        });

        return app;
    }
    #endregion
}
```

- [ ] **Step 3: Register `BackofficeDbContext` and map the endpoint in `Program.cs`**

```csharp
using MAUI_POS_DASH.Web.Api;
using MAUI_POS_DASH.Web.Components;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddInteractiveWebAssemblyComponents();

        builder.Services.AddDbContext<BackofficeDbContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("Backoffice")
                ?? "Host=localhost;Database=mauiposdash_dev;Username=postgres;Password=postgres"));

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if(app.Environment.IsDevelopment())
        {
            app.UseWebAssemblyDebugging();
        }
        else
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }
        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseHttpsRedirection();

        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapTransactionsApi();

        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(
                typeof(MAUI_POS_DASH.Shared._Imports).Assembly,
                typeof(MAUI_POS_DASH.Web.Client._Imports).Assembly);

        app.Run();
    }
}
```

- [ ] **Step 4: Pass `.Web.Client`'s assembly into `Routes`' new `AdditionalAssemblies` parameter**

`MAUI-POS-DASH.Web/Components/App.razor` — change the `<Routes ... />` line:

```razor
    <Routes @rendermode="InteractiveAuto" AdditionalAssemblies="new[] { typeof(MAUI_POS_DASH.Web.Client._Imports).Assembly }" />
```

(Everything else in `App.razor` stays as-is.)

- [ ] **Step 5: Add the local-dev Postgres connection string**

`MAUI-POS-DASH.Web/appsettings.Development.json` — read the current file, then add a
`ConnectionStrings` section (do not commit anything but a local-dev-only placeholder value; note
in `README.md`, added in Task 16, that real environments use user-secrets or an environment
variable instead):

```json
{
  "DetailedErrors": true,
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "Backoffice": "Host=localhost;Database=mauiposdash_dev;Username=postgres;Password=postgres"
  }
}
```

(If `appsettings.Development.json` doesn't already exist with different content, check its
current contents first and merge — don't blindly overwrite `DetailedErrors`/`Logging` if they
differ from the above.)

- [ ] **Step 6: Build**

```bash
dotnet build MAUI-POS-DASH.Web/MAUI-POS-DASH.Web.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 7: Commit**

```bash
git add MAUI-POS-DASH.Web
git commit -m "Wire BackofficeDbContext and add the stub transaction sync endpoint"
```

---

## Task 15: Web.Client — back-office dashboard shell

**Files:**
- Create: `MAUI-POS-DASH.Web.Client/Layout/DashboardLayout.razor`
- Create: `MAUI-POS-DASH.Web.Client/Pages/Dashboard.razor`
- Modify: `MAUI-POS-DASH.Web.Client/_Imports.razor`
- Modify: `MAUI-POS-DASH.Web.Client/MAUI-POS-DASH.Web.Client.csproj` (already referenced Core in Task 1 — confirm only)

- [ ] **Step 1: Write the dashboard layout**

`MAUI-POS-DASH.Web.Client/Layout/DashboardLayout.razor`:

```razor
@inherits LayoutComponentBase

<div class="dashboard-shell">
    <header class="dashboard-shell__header">
        <span>MAUI-POS-DASH Back Office</span>
    </header>

    <main class="dashboard-shell__body">
        @Body
    </main>
</div>
```

- [ ] **Step 2: Write the dashboard page**

`MAUI-POS-DASH.Web.Client/Pages/Dashboard.razor`:

```razor
@page "/"
@layout DashboardLayout

<PageTitle>Back Office Dashboard</PageTitle>

<h1>Shift Reconciliation</h1>

<p>This dashboard will read synced data once the Sync module's persistence lands — for now it's wired to fixed sample data so the shared components can be seen working end-to-end.</p>

<ShiftSummaryCard Shift="_sampleShift" />

<h2>Recent Transactions</h2>
@foreach (var transaction in _sampleTransactions)
{
    <TransactionRow Transaction="transaction" />
}

@code {
    // We use a couple of made-up sample records so the shared components render something real
    // during a demo — Builder/Overmind swap this for a live call to /api/shifts once that
    // endpoint exists.
    private readonly ShiftDto _sampleShift = new(
        Id: Guid.NewGuid(),
        AttendantId: Guid.NewGuid(),
        AttendantName: "Marge Simpson",
        OpenedAt: DateTimeOffset.UtcNow.AddHours(-3),
        ClosedAt: null,
        OpeningFloat: 100.00m,
        ClosingCashCounted: null,
        Status: ShiftStatus.Open);

    private readonly List<TransactionDto> _sampleTransactions =
    [
        new(Guid.NewGuid(), Guid.NewGuid(), PaymentMethod.FleetCard, 82.50m, TransactionStatus.Synced, DateTimeOffset.UtcNow.AddMinutes(-40), DateTimeOffset.UtcNow.AddMinutes(-39)),
        new(Guid.NewGuid(), Guid.NewGuid(), PaymentMethod.Cash, 45.00m, TransactionStatus.Synced, DateTimeOffset.UtcNow.AddMinutes(-25), DateTimeOffset.UtcNow.AddMinutes(-24)),
        new(Guid.NewGuid(), Guid.NewGuid(), PaymentMethod.MobileMoney, 30.00m, TransactionStatus.Pending, DateTimeOffset.UtcNow.AddMinutes(-5), null)
    ];
}
```

- [ ] **Step 3: Update `_Imports.razor`**

`MAUI-POS-DASH.Web.Client/_Imports.razor` — add these three lines at the end:

```razor
@using MAUI_POS_DASH.Web.Client.Layout
@using MAUI_POS_DASH.Shared.Components
@using MAUI_POS_DASH.Core.Contracts
@using MAUI_POS_DASH.Core.Domain
```

- [ ] **Step 4: Build**

```bash
dotnet build MAUI-POS-DASH.Web.Client/MAUI-POS-DASH.Web.Client.csproj
dotnet build MAUI-POS-DASH.Web/MAUI-POS-DASH.Web.csproj
```

Expected: both `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add MAUI-POS-DASH.Web.Client
git commit -m "Add back-office dashboard shell to Web.Client"
```

---

## Task 16: Team docs — README, ARCHITECTURE, AGENTS, CLAUDE

**Files:**
- Modify: `README.md`
- Create: `docs/ARCHITECTURE.md`
- Create: `AGENTS.md`
- Create: `CLAUDE.md`

- [ ] **Step 1: Write `README.md`**

```markdown
# MAUI-POS-DASH

A forecourt payment terminal, built as a portfolio/showcase project demonstrating .NET MAUI
(Android) and Blazor working together: a MAUI Blazor Hybrid terminal app and a Blazor Web
back-office dashboard, sharing a UI component library and a common domain/persistence layer.

It's scoped and modeled after a real job: an Android terminal on PAX hardware consolidating
fleet card processing, mobile money, cash handling, shift management, and attendant management
for a fuel forecourt. See `docs/ARCHITECTURE.md` for the full design and `AGENTS.md` for how the
team (human and AI) works in this repo.

## Projects

- **MAUI-POS-DASH** — the terminal app (MAUI Blazor Hybrid, Android only).
- **MAUI-POS-DASH.Web** / **MAUI-POS-DASH.Web.Client** — the back-office dashboard (Blazor Web
  App, server + WASM).
- **MAUI-POS-DASH.Shared** — Razor UI components used by both hosts.
- **MAUI-POS-DASH.Core** — domain models, DTOs, and interfaces. No EF Core, no ASP.NET — usable
  from anywhere.
- **MAUI-POS-DASH.Core.Persistence** — EF Core: `TerminalDbContext` (SQLite, on-device) and
  `BackofficeDbContext` (Postgres, back office).
- **MAUI-POS-DASH.Core.Tests** — NUnit tests for `Core`.

## Running it

**Web dashboard:**

```bash
dotnet run --project MAUI-POS-DASH.Web
```

Needs a local Postgres reachable at the connection string in
`MAUI-POS-DASH.Web/appsettings.Development.json` (defaults to
`Host=localhost;Database=mauiposdash_dev;Username=postgres;Password=postgres` — a throwaway dev
value, never a real credential). Override it with a user-secret or the
`ConnectionStrings__Backoffice` environment variable rather than editing the committed file.

**MAUI terminal (Android):**

Open the solution in Visual Studio / Rider with the Android workload installed, set
`MAUI-POS-DASH` as the startup project, and run on an emulator or device. It uses a local SQLite
file under the app's data directory — no external database needed.

## Tests

```bash
dotnet test MAUI-POS-DASH.Core.Tests
```

## EF Core migrations

A local tool manifest pins `dotnet-ef` to the same version for everyone:

```bash
dotnet tool restore
dotnet ef database update --project MAUI-POS-DASH.Core.Persistence --context TerminalDbContext
dotnet ef database update --project MAUI-POS-DASH.Core.Persistence --context BackofficeDbContext
```

## Status

This is a platform skeleton — see the module ownership table in `docs/ARCHITECTURE.md` for
what's built versus what's an open claim.
```

- [ ] **Step 2: Write `docs/ARCHITECTURE.md`**

```markdown
# Architecture

This is the living reference for how MAUI-POS-DASH is put together — see
`docs/superpowers/specs/2026-09-03-forecourt-terminal-architecture-design.md` for the
point-in-time design record this was built from.

## Layering

```
MAUI-POS-DASH.Core            domain models, DTOs, interfaces — no EF, no ASP.NET
MAUI-POS-DASH.Core.Persistence   EF Core: TerminalDbContext (SQLite), BackofficeDbContext (Postgres)
MAUI-POS-DASH.Shared           Razor UI components — references Core for DTO types only
MAUI-POS-DASH                  MAUI Blazor Hybrid terminal — references Core + Core.Persistence + Shared
MAUI-POS-DASH.Web              ASP.NET host + sync API — references Core + Core.Persistence + Web.Client
MAUI-POS-DASH.Web.Client       WASM dashboard pages — references Core + Shared only (no EF Core in the browser bundle)
```

## Data flow

1. A sale on the terminal writes to `TerminalDbContext` (SQLite) immediately — never blocked on
   network.
2. `OfflineTransactionQueue` periodically calls `ITransactionSyncService`, POSTing pending
   transactions to `MAUI-POS-DASH.Web`'s `/api/transactions`.
3. The dashboard (`MAUI-POS-DASH.Web.Client`) reads from `BackofficeDbContext` (Postgres) — only
   what's been synced, never talking to a terminal directly.

## Device abstraction

`Core/Devices/*` interfaces are shaped like PAX's real Android SDKs. The current implementations
(`MAUI-POS-DASH/Platforms/Android/Devices/Simulated*`) are simulators — swapping in the real PAX
SDK later means implementing the same interfaces, not rewriting callers.

## Module ownership

| Module | Owner | Depends on |
|---|---|---|
| `Core/Modules/FleetCard/` | unclaimed | `ICardReaderService`, `PaymentMethod` |
| `Core/Modules/MobileMoney/` | unclaimed | `ITransactionSyncService` |
| `Core/Modules/Cash/` | unclaimed | `TillReconciliationService` |
| `Core/Modules/AttendantMgmt/` | unclaimed | `Attendant`, auth |
| Sync endpoint persistence (`Web/Api/TransactionsApi.cs`) | unclaimed | `BackofficeDbContext` |

Claim a row by editing this table and the module's own `README.md`, in the same commit that
starts the work.

## What's a placeholder right now

- `Login.razor`, `ShiftOpen.razor`, `ShiftClose.razor` in the terminal app are UI-only — they
  don't call `ShiftService` yet (that needs a signed-in attendant).
- `FleetCardSale.razor`, `MobileMoneySale.razor`, `CashSale.razor`, `AttendantManagement.razor`
  are "coming soon" pages pointing at their module's README.
- `POST /api/transactions` returns `501 Not Implemented` — it proves the contract compiles, not
  that it persists anything yet.
- The dashboard's `Dashboard.razor` renders fixed sample data, not a live query.
```

- [ ] **Step 3: Write `AGENTS.md`**

```markdown
# AGENTS.md

Conventions for anyone — human or AI — working in this repository. This is the canonical file;
`CLAUDE.md` just points here.

## Who's who

- **Overmind** — the human developer.
- **Architect** — Claude, working in this repo.
- **Builder** — Codex, working in this repo.

Use these names in docs, comments, and the module ownership table in `docs/ARCHITECTURE.md`.

## Before touching a module

Check the ownership table in `docs/ARCHITECTURE.md` and the module's own `Core/Modules/*/README.md`.
Claim a row before writing code in that folder — edit both files in the same commit that starts
the work, so two contributors don't pick the same module at the same time.

## Code conventions

- **Database access:** EF Core + LINQ only. No Dapper, no raw SQL. Code-first migrations via the
  local `dotnet-ef` tool (`dotnet tool restore` first).
- **Mapping:** Riok.Mapperly for entity↔DTO (`Core/Contracts/EntityMapper.cs`). Not AutoMapper —
  it moved to a commercial license in 2024.
- **Testing:** NUnit. Seed the relevant `DbContext` at the top of a `[SetUp]` (real schema, real
  relationships — not mocks), dispose it in `[TearDown]`. Every `[Test]` method uses
  `#region Arrange` / `#region Act` / `#region Assert`.
- **Code organization:** `#region` blocks in every C# file (Fields, Constructor, Properties,
  Public Methods, Private Methods, Events, etc. — whichever apply). `GlobalUsings.cs` per project
  instead of repeating common `using`s per file.
- **Documentation:** XML docs (`///`) on all public types and members. No top-of-file comment
  headers. Short inline comments only where an interaction is genuinely non-obvious.
- **Voice:** comments and docs in first person / team voice ("we"/"I"), not third-person or
  passive.
- **Naming:** a little personality where it fits (seed/sample data is a reasonable place for
  this) — without undercutting the professional read of the repo.
- **Commits:** no attribution trailers.
- **Secrets:** never commit a real connection string, API key, or credential. Local dev defaults
  in `appsettings.Development.json` are throwaway values only — real environments use
  user-secrets or environment variables.

## Layering rule

`Core` must never reference `Core.Persistence`, MAUI, or ASP.NET. `Core.Persistence` must never
be referenced by `MAUI-POS-DASH.Web.Client` (it would pull EF Core/Npgsql into the browser
bundle). If a change seems to require breaking either rule, stop and raise it rather than
routing around it.
```

- [ ] **Step 4: Write `CLAUDE.md`**

```markdown
# CLAUDE.md

See [AGENTS.md](./AGENTS.md) — that's the canonical conventions file for this repository, read
by every contributor (human or AI). Keeping one file instead of two prevents them drifting apart.
```

- [ ] **Step 5: Commit**

```bash
git add README.md docs/ARCHITECTURE.md AGENTS.md CLAUDE.md
git commit -m "Add team docs: README, ARCHITECTURE, AGENTS, CLAUDE"
```

---

## Task 17: Full build verification and dashboard click-through

**Files:** none (verification only)

- [ ] **Step 1: Restore and build the whole solution**

```bash
dotnet restore MAUI-POS-DASH.slnx
dotnet build MAUI-POS-DASH.slnx
```

Expected: `Build succeeded.` for every project (MAUI builds for `net10.0-android` since that's now its only `TargetFramework`).

- [ ] **Step 2: Run the full test suite**

```bash
dotnet test MAUI-POS-DASH.Core.Tests/MAUI-POS-DASH.Core.Tests.csproj
```

Expected: `Passed! - Failed: 0, Passed: 4, Skipped: 0`.

- [ ] **Step 3: Run the Web app and click through the dashboard in a browser**

```bash
dotnet run --project MAUI-POS-DASH.Web
```

Open the URL from `MAUI-POS-DASH.Web/Properties/launchSettings.json` (`https://localhost:7135`).
Confirm:
- The page loads with the `DashboardLayout` header ("MAUI-POS-DASH Back Office").
- The sample `ShiftSummaryCard` renders with "Marge Simpson", an "Open" badge, and the opening
  float formatted as currency.
- Three `TransactionRow` entries render with method, amount, and status badge (two "Synced" green,
  one "Pending" yellow).
- No console errors in the browser dev tools.

- [ ] **Step 4: If a Postgres instance is reachable, apply migrations and confirm the API endpoint responds**

```bash
dotnet tool restore
dotnet ef database update --project MAUI-POS-DASH.Core.Persistence --context BackofficeDbContext
curl -i -X POST https://localhost:7135/api/transactions -H "Content-Type: application/json" -d "[]"
```

Expected: `501 Not Implemented` (confirms the endpoint is reachable and the DTO shape
deserializes — the 501 is the intended stub response, not a failure).

- [ ] **Step 5: Stop the Web app, final status check**

```bash
git status
git log --oneline -20
```

Expected: working tree clean, one commit per task above.
