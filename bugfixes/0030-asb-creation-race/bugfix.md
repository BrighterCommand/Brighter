# Bugfix: Tolerate an Azure Service Bus entity creation race

**Linked Issue**: #4312
**Status**: Verified

## Symptom

Concurrent first sends can fail when another producer or consumer creates the
topic or queue after the producer's existence check. Both producer types rethrow
the creation conflict rather than recognizing that the required entity exists.

## Suspected Location

- `src/Paramore.Brighter.MessagingGateway.AzureServiceBus/AzureServiceBusTopicMessageProducer.cs:62-89`
- `src/Paramore.Brighter.MessagingGateway.AzureServiceBus/AzureServiceBusQueueMessageProducer.cs:62-89`

## Root-Cause Hypothesis

The triage hypothesis was that the check-then-create sequence treats a lost creation race as a
failure, while the corresponding consumers recognize
`ServiceBusFailureReason.MessagingEntityAlreadyExists` as success.

## Confirmed Root Cause

Independent tracing confirms the hypothesis. Both producers check existence and
create in separate awaited calls. If another participant creates the entity
between them, the SDK's specific already-exists creation response is rethrown
before the destination is cached or the sender is acquired. The diagnosis and
scope are approved.

## Evidence

References describe base commit `4fecf11f0` and are relative to
`src/Paramore.Brighter.MessagingGateway.AzureServiceBus/`.

- `AzureServiceBusMessageProducer.cs:125-127` routes public async sends through
  delayed send; sync entry points at lines 115-117 and 199 reach the same path.
- Sender acquisition awaits channel creation at line 248, before the sender
  acquisition retry policy and before the actual send's error handling.
- Both producers check existence at line 69, create at line 80, and cache success
  at line 81. Their catches at lines 83-88 reset and rethrow every exception.
- `AzureServiceBusWrappers/AdministrationClientWrapper.cs:75-84` and `:146-155`
  preserve SDK creation exceptions with bare rethrows.
- Azure.Messaging.ServiceBus 7.20.2, pinned in `Directory.Packages.props:37`,
  documents `ServiceBusException` with reason `MessagingEntityAlreadyExists`
  for duplicate queue and topic creation. The installed SDK XML documentation
  confirms this contract.
- The deterministic regression tests reproduced six race failures on each of
  .NET 9 and .NET 10 before the fix. The same cases pass after the fix.
- Live Azure HTTP-conflict verification has not been performed. The tests model
  the documented SDK exception using in-memory administration and sender clients.

## Scope Notes

Cover both topic and queue producers. Do not suppress unrelated failures or all
HTTP 409 responses. Preserve `OnMissingChannel.Assume` and `Validate` behavior.
Keep this work independent of the SQS scheduler fix in #4356.

The already-exists handling must surround the create operation only. An error
from an existence check must not mark the destination available. Successful
race recovery should cache availability; other failures must preserve the
existing reset/rethrow behavior. An instance-local lock cannot protect against
other producers, consumers, or processes and is not required for the narrow fix.

The shared channel-creation path also serves delayed and bulk sends. No separate
dispatch change is indicated. Consumer subscription-creation behavior is outside
this producer fix.

## Regression Test

Tests are in
`tests/Paramore.Brighter.AzureServiceBus.Tests/MessagingGateway/When_another_caller_creates_the_destination_should_send_and_cache_its_availability.cs`.
They use an in-memory administration implementation and the existing sender
substitutes to exercise the public producer APIs without Azure credentials.

The 50 cases cover:

- An external caller creating the entity after the check, for both producer
  types and both send APIs, followed by another send to verify cached availability.
- Two independent async producers observing absence before either creates the
  entity, then both delivering successfully after one loses the race.
- Timeouts, generic HTTP 409 conflicts, permission errors, and cancellation from
  both existence checks and creation calls. These must propagate unchanged,
  preserve reset behavior, and leave a later retry able to create and send.
- An already-exists exception from an existence check remaining a failure.
- `Assume` and `Validate` retaining their existing behavior without creation.

The concurrent test uses a bounded completion barrier, not timing sleeps.
The test-first review was approved before the production fix was applied.

## Fix

Both producers catch `ServiceBusException` with reason
`MessagingEntityAlreadyExists` around the create call only, log the race at debug
level, and cache the destination as available. All other exceptions retain the
existing reset/rethrow behavior. No locking, public API, or dispatch changes are
required.

The build job runs the broker-free creation-race tests explicitly so fork pull
requests receive regression coverage without Azure secrets.

## Verification

- Before the fix: 6 failed and 44 passed on each of .NET 9 and .NET 10. All six
  failures were the lost-creation-race scenarios.
- After the fix: all 50 regression cases passed on each framework.
- The exact CI command also passed all 50 cases on each framework in Release,
  with the GitHub Actions logger and without restoring or rebuilding.
- The broader broker-free suite passed in Release: 157 passed, 0 failed, and
  0 skipped on each framework, using `Category!=ASB&Category!=AzureServiceBus`.
  This conservative filter excludes all tests carrying either live-broker category.
- The gateway Release build succeeded for .NET Standard 2.0 and .NET 8, 9, and 10
  with 0 warnings and 0 errors. `git diff --check` passed.
- Live Azure integration tests were not run because Azure test credentials were
  not configured. No Azure resources were created or modified.

Commands (from the repository root):

```sh
dotnet test tests/Paramore.Brighter.AzureServiceBus.Tests/Paramore.Brighter.AzureServiceBus.Tests.csproj --filter "FullyQualifiedName~AzureServiceBusProducerCreationRaceTests"
dotnet test tests/Paramore.Brighter.AzureServiceBus.Tests/Paramore.Brighter.AzureServiceBus.Tests.csproj --filter "Category!=ASB&Category!=AzureServiceBus" -c Release --no-restore
dotnet build src/Paramore.Brighter.MessagingGateway.AzureServiceBus/Paramore.Brighter.MessagingGateway.AzureServiceBus.csproj -c Release --no-restore
git diff --check
```
