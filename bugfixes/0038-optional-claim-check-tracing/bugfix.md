# Bugfix: Claim checks require optional tracing

**Linked Issue**: [#4433](https://github.com/BrighterCommand/Brighter/issues/4433)
**Status**: Verified

## Symptom

Posting a claim-checked message through dependency injection fails without an
`IAmABrighterTracer` registration, even when a real luggage store is configured.
The reported reproduction uses `AddBrighter`, in-memory messaging,
`UseExternalLuggageStore<InMemoryStorageProvider>()`, and a mapper with a zero
claim-check threshold. Posting throws a configuration exception whose inner
exception reports the missing tracer. Registering a tracer makes posting work.

Without a real storage provider, the same failure masks the default
`NullLuggageStore` error. Claim checking should work without tracing while still
using a registered tracer and reporting missing storage correctly.

## Suspected Location

- `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:1023,1043,1064`:
  all three public registration overloads use the same helper.
- `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:1080,1087`:
  both interface factories require a tracer before ensuring storage exists.
- `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:236`:
  default storage registration uses the same helper.
- `src/Paramore.Brighter/Transforms/Storage/IAmAStorageProvider.cs:42` and
  `src/Paramore.Brighter/Transforms/Storage/IAmAStorageProviderAsync.cs:43`:
  both contracts declare nullable tracers.
- `src/Paramore.Brighter/Transforms/Storage/NullLuggageStore.cs:33,63`:
  intended missing-store errors.

## Root-Cause Hypothesis

UNVERIFIED at triage: the registration helper accidentally makes tracing
mandatory with two `GetRequiredService<IAmABrighterTracer>()` calls. Storage
providers already support absent tracing. The issue suggests optional service
resolution; confirmation must check both factories, all overloads, existing
tracer injection, and the default store's failure behavior.

## Confirmed Root Cause

Independent code-trace confirms both registration factories require an optional
tracer before storage initialization. Both storage contracts permit null, and
the in-memory provider already guards its telemetry calls. The production
`ClaimCheckTransformer` requires both interfaces, so fixing just one factory
would still leave ordinary transformer activation broken.

Use optional tracer resolution in both factories while preserving required
concrete-store resolution, singleton registration, and store initialization.
Diagnosis and regression tests approved on 2026-09-28.

## Evidence

Investigation baseline: upstream master `ea294324d`.
Before the fix, the new tests reported ten failures and six passes on each of
net9.0 and net10.0. Every failure involved required tracer resolution: six
registration cases, two posting cases, and two incorrect missing-store errors.
With optional resolution, all sixteen cases pass on both frameworks.

- `ServiceCollectionExtensions.cs:1023-1068`: all three overloads delegate to
  `RegisterLuggageStore`.
- `ServiceCollectionExtensions.cs:1079-1088`: the two required tracer lookups
  precede storage initialization and throw when tracing is absent.
- `IAmAStorageProvider.cs:42` and `IAmAStorageProviderAsync.cs:43`: nullable
  tracer contracts.
- `InMemoryStorageProvider.cs:101-115,147-159,188-198`: null-safe tracing around
  actual storage operations.
- `ClaimCheckTransformer.cs:62-65`: construction requires both storage interfaces.
- `ServiceCollectionExtensions.cs:236` and `NullLuggageStore.cs:33,63`: the
  default provider's intended missing-store error is masked by tracer resolution.

Service collection references are in
`src/Paramore.Brighter.Extensions.DependencyInjection/`. Storage references are
in `src/Paramore.Brighter/Transforms/Storage/`, and the transformer is in
`src/Paramore.Brighter/Transforms/Transformers/`.

## Scope Notes

Existing core claim-check tests construct stores directly and bypass the failing
registration path. The existing Extensions claim-check mapper is a validation
stub, not a working message mapper. Regression coverage must use the public DI
registration path with real in-memory storage.

Cover all three registration overloads and both interface factories. Check that
registered tracers are still supplied by reference and both interfaces share the
same concrete singleton. Resolve async storage independently so a sync-factory
failure cannot hide an unfixed async lookup. Exercise sync and async claim-check
behavior through production DI, not a manually constructed transformer.

The default store must continue to fail clearly when no real store is registered.
No provider, transformer, public API, or lifetime changes are needed. Tracer
assignment remains owned by DI; preserving an independently prepopulated tracer
would introduce a different ownership rule and is outside this fix.

## Regression Test

Written in
`tests/Paramore.Brighter.Extensions.Tests/When_registering_a_luggage_store_should_allow_optional_tracing.cs`.
Reviewed, compiled, and run before and after the fix on net9.0 and net10.0.

The 16 cases cover:

- Twelve registration cases: type, instance, and factory registration; sync or
  async interface resolved first; with or without a registered tracer. They
  verify the concrete singleton is shared and the optional tracer is assigned.
- Two posting cases: `Post` and `PostAsync` use an in-memory producer and the
  real DI-created claim-check pipeline without tracing. They verify a claim-check
  message is published and the original payload is retrievable from storage.
- Two missing-store cases: independently resolving either default interface
  without tracing must report the intended missing-store error.

The posting cases use the dedicated `OptionalTracingClaimCheckEvent` and
`OptionalTracingClaimCheckMapper` in `TestDoubles/`. The new closed wrap pipeline
types are registered in the test logging initializer to avoid logger binding
races, as required by the test guidelines.

The first full Extensions run exposed a fixture-lifetime problem: `AddLogging`
created a container-owned logger factory that `BuildCommandProcessor` installed
as the process-wide factory. Container cleanup then disposed it, causing other
tests to fail with `ObjectDisposedException`. The posting fixture now registers
the suite-owned `Initializer.Factory` instance, which the container does not
own or dispose. No assertions were changed or tests disabled.

The six registered-tracer cases also passed before the fix. To verify their
assertions, both tracer assignments were temporarily replaced with
`store.Tracer = null`. All six failed their tracer-identity assertion on both
frameworks; the ten other cases passed. The mutation was reverted before running
the broader suites and is not part of the fix.

## Fix

Replace both `GetRequiredService<IAmABrighterTracer>()` calls in
`ServiceCollectionExtensions.RegisterLuggageStore` with
`GetService<IAmABrighterTracer>()`. Required concrete-store resolution,
initialization calls, singleton lifetimes, and tracer assignment ownership remain
unchanged. No public API or dependency changes are needed.

The DI library builds for netstandard2.0, net8.0, net9.0, and net10.0 with zero
warnings or errors. After correcting the fixture's logger ownership:

- Full Extensions suite: 530 passed on net9.0 and 527 on net10.0, no failures or
  skips. This includes all sixteen new cases.
- Full Core suite: 1,484 passed and 7 existing skips on each framework, no failures.
- `git diff --check` passes; the temporary tracer mutation is absent.

The full solution and external broker/database suites were not run. No public
API changes or external infrastructure are involved in the fix.

Local test results are under `/private/tmp/brighter-4433-test-results/`:

- `4433-red_net9.0_20260928125117.trx` and
  `4433-red_net10.0_20260928125117.trx`: ten failures and six passes each.
- `4433-green_net9.0_20260928125501.trx` and
  `4433-green_net10.0_20260928125501.trx`: sixteen passes each.
- `4433-mutation-null-tracer_net9.0_20260928125930.trx` and
  `4433-mutation-null-tracer_net10.0_20260928125930.trx`: six expected failures
  and ten passes each under the temporary mutation.
- `4433-full-extensions-stable-logging_net9.0_20260928130333.trx` and
  `4433-full-extensions-stable-logging_net10.0_20260928130331.trx`: final full
  Extensions suite results after correcting the fixture.
- `4433-full-core_net9.0_20260928130235.trx` and
  `4433-full-core_net10.0_20260928130236.trx`: full Core suite results.
