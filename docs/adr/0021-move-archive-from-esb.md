# 20. Move Archive Methods from External Service Bus to Outbox Archiver to Reduce Complexity

Date: 2024-10-28

## Status

Adopted

## Context

The `ExternalServiceBus` class is a mediator between producer and outbox. It suffers from complexity, see ADR 0020.

The `ExternalServiceBus` class has a number of methods that are related to archiving messages. These methods are:
 - Archive
 - ArchiveAsync

The OutboxArchiver and the TimedOuboxArchiver are the only callers of these ExternalBus Archive methods. The TimedOutboxArchiver provides both a timer that fires the archiver at a given periodic interval, and uses the global distributed lock to ensure only one archiver runs.

## Decision

Whilst the `Archive` methods were not called out by CodeScene analysis, they do add to the overall set of responsibilities of `ExternalServiceBus`. As they have different reasons to change to `ExternalServiceBus` they should be moved to a separate class.

We will move the implementation from `ExternalServiceBus` into `OutboxArchiver`. We will call `OutboxArchiver` from  `TimedOutboxArchiver`.

## Consequences

One, we have moved these functions, it makes sense to rename the `ExternalServiceBus` class to 'OutboxProducerMediator' as this better describes its role within our codebase.





