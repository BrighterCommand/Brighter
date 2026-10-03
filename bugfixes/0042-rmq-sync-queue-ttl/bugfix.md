# Bugfix: RMQ.Sync queue TTL uses total milliseconds

**Linked Issue**: #4472
**Status**: Verified — full .NET 10 sync suite and targeted .NET 9 regression tests pass

## Symptom

A subscription TTL of 30 seconds declares a RabbitMQ queue with `x-message-ttl` equal to zero.
A TTL of 1.5 seconds becomes 500 milliseconds. The queue should receive the full configured duration in milliseconds.

## Suspected Location

- `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageConsumerFactory.cs:73` passes the subscription TTL to the consumer.
- `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageConsumer.cs:506` declares the queue using `SetQueueArguments()`.
- `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/RmqMessageConsumer.cs:580` converts the TTL to the queue argument.

## Root-Cause Hypothesis

The sync consumer uses `TimeSpan.Milliseconds`, which returns the sub-second component rather than the full duration.
The issue's suggested conversion to total milliseconds was confirmed by code inspection and a failing broker regression test.

## Confirmed Root Cause

The subscription's `Ttl` reaches the consumer unchanged, but `SetQueueArguments()` discards whole seconds by reading `Milliseconds`.
RabbitMQ receives the truncated value in the queue declaration. The async gateway already uses `Convert.ToInt32(TotalMilliseconds)`.

## Evidence

- The factory forwards `rmqSubscription.Ttl` directly to the consumer constructor.
- The consumer stores the duration in `_ttl` and passes `SetQueueArguments()` to `QueueDeclare`.
- Before the fix, RabbitMQ rejected a redeclaration with the expected TTL: `received '30000' but current is '0'`.
- The 1,500 ms case failed with `received '1500' but current is '500'`.
- The `int.MaxValue` case also failed. Zero and 500 ms passed as controls: two passed and three failed before the fix.
- After the fix, all five cases passed on both .NET 9 and .NET 10.

## Scope Notes

The change corrects the sync gateway's conversion and matches the async gateway's integer conversion behavior.
`Convert.ToInt32` rejects overflow rather than silently truncating the duration; the regression covers the largest supported integer value, but does not exercise overflow.
There are no public API, dependency, or default changes. No additional defect was found in the subscription-to-queue path.

RabbitMQ checks queue argument equivalence on redeclaration. An existing queue declared with the truncated TTL can therefore reject the corrected declaration.
Deployments using affected TTLs need to plan queue recreation or migration; this fix does not mutate existing queue arguments.

## Regression Test

`tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway/Reactor/When_configuring_queue_ttl_should_use_total_milliseconds.cs`
contains `RmqQueueTtlTests` with cases for 0, 500, 1,500, 30,000, and 2,147,483,647 milliseconds.

Each case creates a subscription and obtains the consumer through the public factory. `Purge()` creates the queue.
A passive declaration first verifies that the queue exists. Redeclaring it with the expected TTL then checks RabbitMQ's argument equivalence contract.
The test uses unique queue and exchange names and removes both in `finally`. It does not inspect private state or mock the transport.

## Fix

Replace `_ttl.Value.Milliseconds` with `Convert.ToInt32(_ttl.Value.TotalMilliseconds)` in `SetQueueArguments()`.
This is the same conversion used by `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageConsumer.cs:683`.

## Verification

Local tests used RabbitMQ `3.13-management` in temporary containers, including a separate TLS listener for the acceptance tests.
Both containers were stopped and removed after verification.

| Run | Passed | Failed | Skipped |
| --- | --- | --- | --- |
| .NET 10 regression before the fix | 2 | 3 | 0 |
| .NET 10 regression after the fix | 5 | 0 | 0 |
| .NET 9 regression after the fix | 5 | 0 | 0 |
| Full .NET 10 sync test project | 121 | 0 | 1 |

The existing skip is `RMQMessageConsumerRetryDLQTests.When_retry_limits_force_a_message_onto_the_dlq`.
No skip attributes were added or changed. The full .NET 9 suite and the entire solution were not run.
Both test-project target builds succeeded with compiler/analyzer warnings. `git diff --check` passed.
The test runs above preceded the move to the dedicated branch. After moving to upstream `master` at `2461094a6`,
the production fix and regression test were verified unchanged and the .NET 10 test-project build passed with 332 warnings and zero errors.
On the final branch, all five TTL regression cases passed again on each of .NET 9 and .NET 10 against RabbitMQ 3.13.
The full suite was not repeated after the branch move. Final regression evidence is `/tmp/brighter-4472-final-regression.log`;
the build log is `/tmp/brighter-4472-branch-build.log`.

Reproduction command, with RabbitMQ available locally:

```sh
dotnet test tests/Paramore.Brighter.RMQ.Sync.Tests/Paramore.Brighter.RMQ.Sync.Tests.csproj -f net10.0 --filter FullyQualifiedName~RmqQueueTtlTests
```

Remove the filter to run the full project, including tests requiring the repository's test certificates and a TLS broker on port 5671.
Use `-f net9.0` for the other target framework.

Local evidence is in `/tmp/brighter-4472-red.log`, `/tmp/brighter-4472-green.log`,
`/tmp/brighter-4472-green-net9.log`, and `/tmp/brighter-4472-suite-net10.log`.

## Workflow Record

The work used the standalone test-first path: the diagnosis and proposed conversion were presented, the regression test was prepared, and implementation followed explicit local-only approval.
The initial test run exposed a missing request type in the fixture; that setup error was corrected before the broker failures recorded above.
The bugfix tracking files were added retrospectively during the PR-readiness check. A separate `/bugfix:confirm` command was not run.

## PR Preparation

Suggested title: `fix: preserve total milliseconds in RMQ sync queue TTL`

The PR should target `master`, describe the root cause and broker evidence above, and include `Fixes #4472`.
Use `.github/PULL_REQUEST_TEMPLATE.md`. No new capability or architectural decision is introduced, so no ADR is needed.
The contributor and bugfix instructions do not require a release-notes entry for this change.
Request a merge commit, as required by `CONTRIBUTING.md`, to preserve branch commit identities.

Implementation branch: `bugfix/4472-rmq-sync-queue-ttl`, based on upstream `master` at `2461094a6`.
Only the TTL fix, regression test, and this bug's tracking files were carried onto the branch.
The original relational configuration branch remains intact. The local active-bug pointer selects this bug.
