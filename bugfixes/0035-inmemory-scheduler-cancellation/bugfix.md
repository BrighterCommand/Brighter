# Bugfix: In-memory scheduler cancellation across creation paths

**Linked Issue**: #4437
**Status**: Verified

## Symptom

Requests scheduled through the command processor still execute after cancellation
through the DI-resolved scheduler. Cancellation returns normally; rescheduling
returns `false` and leaves the original execution time unchanged. Scheduling and
cancelling through the same scheduler instance works.

The reported paths include scheduled Send, Publish, and Post, synchronously and
asynchronously, with either a delay or an absolute execution time.

## Suspected Location

References use baseline `bca3e986a`.

- `src/Paramore.Brighter/InMemorySchedulerFactory.cs:61`: all three creation
  methods construct a new scheduler.
- `src/Paramore.Brighter/InMemoryScheduler.cs:58`: each scheduler owns its timer
  dictionary and generation counter.
- `src/Paramore.Brighter/InMemoryScheduler.cs:263`: scheduling inserts into that
  dictionary; rescheduling at `:152` and cancellation at `:162` and `:255` search
  only that dictionary.
- `src/Paramore.Brighter/CommandProcessor.cs:344`, `:427`, `:535`, `:657`, `:717`,
  and `:783`: scheduled request operations call the factory for each invocation.
- `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:198`:
  singleton factories are shared, but scheduler registrations invoke separate
  factory calls at `:205`, `:213`, and `:219`.
- `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:734`:
  the command processor receives the factory, not the registered scheduler.

## Root-Cause Hypothesis

**Confirmed by code trace; diagnosis approved.** Scheduling through
the command processor creates scheduler A, while cancellation or rescheduling
through DI uses scheduler B. The returned ID exists only in A's timer dictionary.
B therefore cannot dispose or change the timer, even when both schedulers use the
same factory and command processor.

The issue suggests either reusing a scheduler per command processor or sharing
timer state across instances created by the factory. Reuse within one factory
and command processor addresses the cause without separating the timer registry
from its generation counter. Sharing only the dictionary would be incomplete.

## Confirmed Root Cause

The factory always creates a new scheduler, even when given the same command
processor. Scheduling through the command processor registers a timer in one
instance; the DI scheduler registration creates another. Cancellation and
rescheduling search the latter instance's private dictionary, which has no entry
for that ID. The singleton factory and command processor registrations do not
make the scheduler objects identical.

## Evidence

- Code trace: `CommandProcessor.cs:427` calls `CreateAsync(this)` before scheduling
  a request. `InMemorySchedulerFactory.cs:69` creates a new scheduler, whose
  private dictionary is initialized at `InMemoryScheduler.cs:58`. Scheduling adds
  the timer through `ScheduleTimer` at `:263`.
- The registered async request scheduler is independently created at
  `ServiceCollectionExtensions.cs:219`. Its `CancelAsync` at
  `InMemoryScheduler.cs:255` cannot find the ID; `ReScheduler` at `:152` returns
  `false`. Sync, Publish, and Post paths use the same creation pattern.
- Existing cancellation coverage at
  `tests/Paramore.Brighter.InMemory.Tests/Scheduler/When_scheduling_a_request_async.cs:353`
  schedules and cancels through the same scheduler object. It does not exercise
  the command-processor-to-DI boundary.
- Executable reproduction: all 104 regression cases failed before the fix on
  both .NET 9 and .NET 10. The failures were assertion failures for the confirmed
  behavior, not compilation or setup failures. Cancellation delivered two jobs
  instead of only the uncancelled control; rescheduling returned `false`.

## Scope Notes

- Cover cancellation and rescheduling for Send, Publish, and Post, sync and async,
  using both relative and absolute times. Include request and message scheduler
  interfaces when created by the same factory for the same processor.
- Proposed direction: reuse one scheduler per factory and processor reference
  across `Create`, `CreateSync`, and `CreateAsync`. Do not use global timer state
  or bind every processor to the first processor supplied to the factory.
- Preserve isolation between factories and between processors. Verify concurrent
  creation, duplicate-ID conflict behavior, and disposal of pending timers.
- Timer generations and the registry must have the same ownership boundary.
  Generation allocation at `InMemoryScheduler.cs:270`, `:286`, and `:292` protects
  cleanup at `:338`. Sharing only the dictionary would permit generation values
  from independent counters to collide and old callbacks to remove replacements.
- Review factory configuration timing and disposal semantics before implementing
  instance reuse: configuration would be captured on first creation for a
  processor, and disposing any alias would drain that processor's shared
  scheduler. Keep direct construction ownership unchanged. Avoid retaining
  processors indefinitely in a strong-reference cache. Do not change other
  scheduler backends or unrelated timer races.

## Regression Test

Diagnosis and test execution/implementation were approved on 2026-09-28.
All 104 cases failed before the fix and passed afterwards on each target framework.

`tests/Paramore.Brighter.Extensions.Tests/When_cancelling_a_command_processor_schedule_should_prevent_delivery.cs`
contains the shared DI and in-memory transport setup for `ScheduledRequestControlTests`:

- 48 cancellation cases cover Send, Publish, and Post; synchronous/asynchronous
  scheduling; relative/absolute times; and all four registered request/message
  scheduler interfaces. An uncancelled control must still execute exactly once.
- 48 rescheduling cases cover the same combinations. After advancing the clock
  five seconds, move delivery to twenty seconds from that point. Nothing may
  execute at the original deadline or just before the new deadline; execution
  must occur once at the new deadline, and the completed ID must be absent.
- Eight additional cases cover duplicate-ID rejection, overwrite behavior,
  processor and factory isolation, repeated sync/async disposal, and concurrent
  scheduling followed by cancellation through the registered message scheduler.

Four dedicated `TestDoubles/SchedulerControlEvent*.cs` files provide separate
sync/async request types and handlers, avoiding collisions with other scanned
handlers. The Extensions test project has no logging `Initializer.cs` to extend.
All timing uses `FakeTimeProvider`; no sleeps or external services are required.

Targeted command (repeat with `-f net9.0`):

```bash
dotnet test tests/Paramore.Brighter.Extensions.Tests/Paramore.Brighter.Extensions.Tests.csproj \
  -f net10.0 --filter FullyQualifiedName~ScheduledRequestControlTests
```

Results are stored under `/private/tmp/brighter-4437-test-results`: the
`4437-red-net9.trx` and `4437-red-net10.trx` files record the failures, and
`4437-green_net9.0_20260928011826.trx` and
`4437-green_net10.0_20260928011826.trx` record the passing runs.

The initial sandboxed invocation failed because MSBuild could not create its
local sockets. That process was stopped; the results above are from approved
unsandboxed runs. Existing nullable and analyzer warnings appear in unchanged
test files.

## Fix

`InMemorySchedulerFactory` retains one scheduler per command processor reference
in a factory-owned `ConditionalWeakTable`. `Create`, `CreateSync`, and
`CreateAsync` use the same lookup, keeping the timer dictionary and generation
counter together. No command processor, scheduler execution logic, or other
backend changes are required.

The weak-key table does not keep otherwise unreachable processors alive, even
though the scheduler references its processor. Its atomic lookup returns the
stored value under concurrent access. The value callback may allocate unused
schedulers concurrently, but construction creates no timers or disposable
resources. See the [.NET table documentation](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.conditionalweaktable-2)
and [GetValue contract](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.conditionalweaktable-2.getvalue).

The factory's XML documentation explains first-creation configuration capture
and shared disposal. Directly constructed schedulers retain their existing
ownership semantics. No public signatures or dependencies change.

Verification:

- Targeted regression tests: 104 passed, zero failures or skips per framework.
- Core library builds across all configured targets with zero warnings/errors.
- Full Extensions suite: 532 passed on .NET 9; 529 passed on .NET 10. No failures
  or skips. These totals include the 104 new regression cases on each target.
- Full Core suite: 1,474 passed and seven existing skips per framework; no failures.
- Full InMemory suite: 153 passed per framework; no failures or skips.
- `git diff --check` passed.

Full-suite TRX files are in the same results directory, prefixed
`4437-full-extensions`, `4437-full-core`, and `4437-full-inmemory`. No full-solution
or external broker/database suite was run; this change affects only the
in-memory scheduler factory.
