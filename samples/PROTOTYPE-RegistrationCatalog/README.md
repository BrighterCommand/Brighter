# PROTOTYPE — RegistrationCatalog API feel (THROWAWAY)

**Do not ship anything in this directory.** It exists to answer one design question for
[ADR 0062](../../docs/adr/0062-source-generated-handler-registration.md) / PR #4138:

> What does the proposed `RegistrationCatalog` API feel like at the call site — and do
> NServiceBus-style *named convention groups* (`[RegistrationGroup]`, cf. their
> `RegistrationMethodNamePatterns`) earn their keep over a flatter catalog with runtime
> combinators?

## Run it

```bash
dotnet run --project samples/PROTOTYPE-RegistrationCatalog/Orders.Host
```

The driver builds a fresh container per composition variant, prints the catalog contents
(demonstrating inspectability), then fires every message and reports which handlers were —
and deliberately were not — registered.

## Layout

- **`Orders.Domain`** — the handler-owning project. References **core `Paramore.Brighter`
  only** (that's the design's key claim: no DI package, no Polly, in domain assemblies).
  - `Billing/`, `Fulfilment/`, `Notifications/` — 6 handlers (sync + async mix) and a mapper.
  - `OrdersRegistrations.cs` — what the **user writes**: the declared holder + group conventions.
  - `OrdersRegistrations.g.cs` — hand-written stand-in for what the **generator would emit**.
  - `Proposed/` — stand-ins for types that would ship in core `Paramore.Brighter`.
- **`Orders.Host`** — the composition root. References the domain project + the DI package.
  - `Proposed/BrighterBuilderRegistrationExtensions.cs` — stand-in for the `AddRegistrations`
    extension that would ship in `Paramore.Brighter.Extensions.DependencyInjection`.
  - `Program.cs` — the variants under evaluation, side by side.

## The three shapes being compared

```csharp
// A. Named groups, declared once by convention, generated as static data:
.AddRegistrations(OrdersRegistrations.Billing, OrdersRegistrations.Urgent)

// B. Flat catalog + runtime combinators, no generator features involved:
.AddRegistrations(OrdersRegistrations.Catalog.InNamespace("Orders.Domain.Billing"))

// C. Phase-2 opt-in fluent sugar ([BrighterRegistrations(GenerateBuilderExtensions = true)]):
.AddBillingRegistrations().AddUrgentRegistrations()
```

Note the group patterns (`[RegistrationGroup("Urgent", TypeNamePattern = "^Urgent")]`) are
evaluated **at build time** by the generator against discovered type names — a group is just
another pre-filtered static catalog, so shapes A and C are sugar over shape B's data model.
Shape C's extensions take `IBrighterBuilder`, so opting in couples the declaring assembly to
the DI package — that's the trade; see `Orders.Host/OrdersRegistrations.BuilderExtensions.g.cs`
for why the prototype parks that file in the host.
