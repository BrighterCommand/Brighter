# Paramore.Brighter.Test.Generator

Generates the test suites that every Brighter provider implementation must pass, from Liquid
templates plus a `test-configuration.json` in each test project. One template becomes one test per
provider, so a behaviour is stated once and proved everywhere.

Two families are generated:

| Family | What it covers | Test projects |
|---|---|---|
| **Messaging gateway** | Transport conformance — the twelve canonical behaviours below, in a Reactor (sync) and a Proactor (async) variant | 12 projects, **24 configurations** (e.g. `AWS.Tests` wires `SnsStandard`, `SnsFifo`, `SqsStandard`, `SqsFifo`) |
| **Outbox** | Outbox store behaviour, sync/async/causation | 8 projects (MSSQL, PostgreSQL, MySQL, SQLite, DynamoDB ×2, MongoDB, GCP) |

## ⛔ Never edit a generated test

Generated files are overwritten on every run; hand-edits are lost silently. Change one of the three
real seams instead:

- **The Liquid template** (`Templates/`) — when the *behaviour under test* changes. Affects every
  provider, so regenerate all of them.
- **The provider class** — each test project hand-writes the implementation of the generated
  `IAmAMessageGatewayReactorProvider` / `…ProactorProvider` (or the outbox equivalents). This is
  where transport-specific setup, cleanup and dead-letter reads live.
- **`test-configuration.json`** — provider-specific details, plus the feature flags that decide
  which templates are generated at all.

## Running it

From the repository root, for every test project:

```bash
./generate-test.sh        # Linux/macOS
.\generate-test.ps1       # Windows
```

For a single project, note that the generator uses the **current working directory** as its output
root, and that the build is what copies the templates into `bin/` — skip it and you generate from
stale templates:

```bash
dotnet build tools/Paramore.Brighter.Test.Generator
cd tests/Paramore.Brighter.MSSQL.Tests
dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator
```

Generated output is checked in. A change to a template is not finished until every project has been
regenerated and the new files committed — `GeneratedTreeAudit` in the `build` job fails on a tree
that disagrees with the templates.

## The twelve canonical transport behaviours

Each has a Reactor and a Proactor template, and a column in the conformance ledger.
`CanonicalBehaviours.cs` is the authority — read it rather than copying this table:

| Behaviour | Ledger column |
|---|---|
| canonical plain requeue | FR-22 |
| requeue with delay | FR-2 |
| explicit zero-delay requeue | FR-15 |
| requeue budget exhausted to DLQ | FR-23 |
| delayed send | FR-9 |
| Nack redelivers | FR-16 |
| reject with delivery error to DLQ | FR-4 |
| reject with unacceptable reason to invalid channel | FR-5 |
| fallback: unacceptable, DLQ-only | FR-6 |
| no channels configured: acknowledge and log | FR-7 |
| reject with None reason to DLQ | FR-17 |
| rejection metadata stamping | FR-8 |

## The conformance ledger

`ConformanceLedger.cs` parses the matrix in
[`specs/0036-universal-transport-conformance-tests/conformance-status.md`](../../specs/0036-universal-transport-conformance-tests/conformance-status.md)
— that file is the single source of truth for what each transport is known to do, and the generator
reads it to decide whether a given test runs or is skipped:

| Cell value | Generated test |
|---|---|
| `Pass` / `Fixed (#PR)` | runs |
| `Deferred -> #NNNN (sign-off: @maintainer)` | skipped, naming the issue — **this is how you find work to pick up** |
| `Unknown` | skipped with a placeholder issue number; the transient state while a behaviour is being driven to conformance |

## Reference

- [`.agent_instructions/generated_tests.md`](../../.agent_instructions/generated_tests.md) — the
  full reference: every template, every configuration key, every feature flag, the regeneration
  recipe, the tree audit, and the CI-flakiness rules.
- [ADR 0035](../../docs/adr/0035-generated-test.md) — why tests are generated at all.
- [ADR 0066](../../docs/adr/0066-conformance-test-provider-and-ungating.md) — the provider
  interface and how a transport is ungated.
- [ADR 0067](../../docs/adr/0067-conformance-rollout-and-deferral-governance.md) — rollout and
  deferral governance: who signs off a `Deferred` cell and on what terms.
