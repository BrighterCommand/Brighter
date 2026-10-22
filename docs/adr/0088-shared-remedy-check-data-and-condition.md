---
id: 0088-shared-remedy-check-data-and-condition
title: "Shared remedy check data and condition value"
status: Proposed
author:
  - "Ian Cooper"
created: 2026-10-22
summary: "Amends ADR 0086 and 0087, which had each remedy check build its own Data. Core gains RemedyCondition, a readonly record struct of a condition's name, description and values, and two RemedyCheckConvention members that build the remedy Data dictionary and join condition descriptions with a semicolon and a space. The four remedy checks call them instead of carrying four copies. Both additions are public and additive; the fold into a HealthCheckResult stays in each check's package."
tags:
  - "observability"
  - "error-handling"
---

# 88. Shared remedy check data and condition value

Date: 2026-10-22

## Status

Proposed

## Context

The four remedy checks (`OutboxSweeperRestartHealthCheck`, `OutboxSweeperInvestigateHealthCheck`, `ConsumerRestartHealthCheck`, `ConsumerTakeOutOfServiceHealthCheck`) each carry a private copy of the same `Data(string[] conditions)` builder, and the investigate and consumer checks each fold a list of `(Name, Description, Values)` tuples into a description and a `Data` dictionary by hand. A review of the finished code with `/refactor:smells` found this as Duplicated Code and Primitive Obsession. It matters because the `Data` shape is the contract that hosts route on, and four copies can drift apart.

**Parent Requirement**: [specs/0038-add-sweeper-health-check/requirements.md](../../specs/0038-add-sweeper-health-check/requirements.md)

**Scope**: This ADR decides one thing: **where the shared remedy `Data` builder and the condition value live, and that both are public API in core.** It changes no behaviour of any check.

It does not decide the tags, the data keys, the description wording, the thresholds or the statuses. ADR 0086 and ADR 0087 decided those and they stand.

### Where this ADR sits

| ADR | Decides |
| --- | --- |
| [0085](0085-track-outbox-sweeper-state-and-publish-gauges.md) | how the sweeper records its state and the two gauges that read it |
| [0086](0086-sweeper-remedy-health-checks.md) | the sweeper remedy checks and the remedy-kind convention |
| [0087](0087-consumer-remedy-health-checks.md) | the consumer remedy checks |
| **0088** *(this one)* | where the shared `Data` builder and the condition value live |

Health checks and gauges give operators actionable guidance on the sweeper and the consumers, and never stall waiting on a stuck thread.

### What 0086 said, and what changes

ADR 0086 put the convention in core "as constants" and said each check builds its own `Data`. That kept core small while the shape was new. With four checks written, the shape is settled and the copies are the cost.

### The forces

- Core must not reference `Microsoft.Extensions.Diagnostics.HealthChecks`, so nothing in core can build a `HealthCheckResult`.
- The maintainer's constraint stands: no new role (abstraction) without necessity. A value type and static members are not roles.
- Source-compatible additive public API is accepted (NFR-6).
- New public API is behaviour, so it is specified by tests first.

## Decision

**Core gains a public `RemedyCondition` readonly record struct and two static `RemedyCheckConvention` members, `Data` and `Describe`; each check keeps only the step that needs `HealthCheckResult`.**

### The mechanism, end to end

A check collects the conditions that apply as `RemedyCondition` values. It calls `RemedyCheckConvention.Data(kind, conditions)` for the dictionary and `RemedyCheckConvention.Describe(conditions)` for the description, then builds its own `HealthCheckResult` with the status it owns. A check with no conditions calls `Data(kind)`, which gives the same dictionary with an empty `conditions` array.

### Where the pieces live

| Assembly | Type | Change |
| --- | --- | --- |
| `Paramore.Brighter` | `RemedyCondition` | New public readonly record struct: `Name`, `Description`, `Values`. |
| `Paramore.Brighter` | `RemedyCheckConvention` | New static `Data(RemedyKind)`, `Data(RemedyKind, IReadOnlyCollection<RemedyCondition>)` and `Describe(IEnumerable<RemedyCondition>)`. |
| `Paramore.Brighter.Outbox.Hosting`, `Paramore.Brighter.ServiceActivator.Extensions.Diagnostics` | the four checks | Drop their private `Data` copies and the condition tuples; call the convention. |

Unchanged: the tags, the keys, `RemedyKind`, every check's status and description text, and `BrighterServiceActivatorHealthCheck`.

### Key Components

#### The roles, and what each is responsible for

| Role | Type | Stereotype | Responsibility |
| --- | --- | --- | --- |
| Remedy check | the four existing `IHealthCheck` classes | **deciding** | Decide which conditions apply and which status results. |
| Remedy convention | `RemedyCheckConvention` (static, core) | **knowing** | Know the `Data` shape and the description separator. |
| Condition | `RemedyCondition` (value, core) | **knowing** | Carry one applicable condition's name, description and values. |

No new abstraction is added; the checks keep the one role they already implement.

#### Contract

| Member | Input | Output | Error conditions |
| --- | --- | --- | --- |
| `Data(kind)` | a `RemedyKind` | a mutable dictionary with `remedy_kind` and an empty `conditions` array | `ArgumentOutOfRangeException` for an undefined kind |
| `Data(kind, conditions)` | a kind and the applicable conditions | the same, with `conditions` holding the names in order and one entry per condition holding its `Values` | as above |
| `Describe(conditions)` | the applicable conditions | their descriptions joined with `"; "` | none |

The dictionary is mutable so a check can add its own extra keys, as `ConsumerRestartHealthCheck` does with `consumer_operations_in_flight`.

### Implementation Approach

1. `/test-first` `RemedyCondition` and the three members, in `tests/Paramore.Brighter.Core.Tests`, pinning the dictionary shape, the order and the separator.
2. `/tidy-first` each check onto the convention. Existing suites stay green; `src/` builds with warnings as errors and targets `netstandard2.0`.

## Consequences

### Positive

- One definition of the `Data` shape and the separator.
- The checks lose their condition tuples and four private copies.

### Negative

- Two more public members in core and a public value type, which are a compatibility commitment once released. `IAmABrighterSweeperMeter` and the checks have not shipped, so the surface is cheap to settle now.

### Risks and Mitigations

| Risk | Mitigation |
| --- | --- |
| The shared builder changes a check's `Data` | The existing check tests, each asserting `Data["remedy_kind"]`, and the AC-49 convention test in `Extensions.AspNetCore.Tests` guard the shape. |

## Alternatives Considered

**1. A private copy per package.** No new public API. **Rejected because** the four copies already exist and the contract they implement would stay unowned.

**2. Put the fold into a `HealthCheckResult` in core.** **Rejected because** core cannot reference the health-checks package.

**3. A new health-check base class.** **Rejected because** it adds a role the maintainer ruled out when static members and a value suffice.

## References

- Requirements: [specs/0038-add-sweeper-health-check/requirements.md](../../specs/0038-add-sweeper-health-check/requirements.md)
- Related ADRs:
  - [ADR 0086](0086-sweeper-remedy-health-checks.md): placed the convention in core as constants; this ADR amends where the builder lives.
  - [ADR 0087](0087-consumer-remedy-health-checks.md): the consumer checks that also use the builder.
