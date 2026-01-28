# Current Work: Universal Scheduler Delay

**Branch:** `universal_delay`
**Spec:** `specs/0002-universal_scheduler_delay/`
**Status:** Requirements phase - awaiting approval

## Quick Context

We're adding universal scheduler support for delayed message delivery across all Brighter transports. The goal is to make all producers use the configurable `IAmAMessageScheduler` system (defaulting to `InMemoryScheduler`) instead of the current inconsistent mix of native delays, direct timers, and scheduler integration.

## Current State

### What's Done
- [x] Created spec directory `specs/0002-universal_scheduler_delay/`
- [x] Set as current spec in `specs/.current-spec`
- [x] Created `requirements.md` with full requirements analysis

### What's Next
1. Review and approve requirements (`/spec:approve requirements`)
2. Create technical design ADR (`/spec:design`) - will be `docs/adr/0037-universal-scheduler-delay.md`
3. Break down into tasks (`/spec:tasks`)
4. Implement using TDD (`/spec:implement`)

## Problem Summary

| Transport | Native Delay | Current Scheduler Use |
|-----------|-------------|----------------------|
| RabbitMQ | Yes (DLX/TTL) | Yes - fallback when native not deployed |
| Azure Service Bus | Yes | No - only uses native |
| AWS SQS | Partial (15min max) | Yes - for delays > 15min |
| Kafka | No | Yes |
| Redis | No | Yes |
| GCP Pub/Sub | No | Partial - throws if not configured |
| **InMemoryProducer** | No | **No - uses direct timer** |
| **InMemoryConsumer** | N/A | **No - uses direct timer for requeue** |

The in-memory transport is the primary focus - it needs to use the scheduler system like other transports.

## Key Files

| Purpose | Location |
|---------|----------|
| Requirements | `specs/0002-universal_scheduler_delay/requirements.md` |
| Scheduler Interfaces | `src/Paramore.Brighter/IAmAMessageScheduler*.cs` |
| InMemoryScheduler | `src/Paramore.Brighter/InMemoryScheduler.cs` |
| InMemoryProducer | `src/Paramore.Brighter/InMemoryMessageProducer.cs` |
| InMemoryConsumer | `src/Paramore.Brighter/InMemoryMessageConsumer.cs` |
| Configuration | `src/Paramore.Brighter/ProducersConfiguration.cs` |
| RMQ Producer (reference) | `src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessageProducer.cs` |

## Reference Pattern

The RMQ producer shows the target pattern for scheduler integration:

```csharp
if (delay == TimeSpan.Zero || DelaySupported || Scheduler == null)
{
    PublishMessage(message, delay);
}
else if (Scheduler is IAmAMessageSchedulerAsync schedulerAsync)
{
    await schedulerAsync.ScheduleAsync(message, delay, cancellationToken);
}
else if (Scheduler is IAmAMessageSchedulerSync schedulerSync)
{
    schedulerSync.Schedule(message, delay);
}
```

## Commands Reference

```bash
# Check spec status
/spec:status

# Approve requirements and move to design
/spec:approve requirements

# Create technical design (ADR)
/spec:design

# Create implementation tasks
/spec:tasks

# Start TDD implementation
/spec:implement
```

## Build & Test

```bash
# Build
dotnet build Brighter.sln

# Run all tests
dotnet test Brighter.sln

# Run specific test project
dotnet test tests/Paramore.Brighter.Core.Tests/Paramore.Brighter.Core.Tests.csproj
```
