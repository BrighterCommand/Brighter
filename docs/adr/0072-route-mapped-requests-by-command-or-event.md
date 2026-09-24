---
id: 0072-route-mapped-requests-by-command-or-event
title: "Route mapped requests by command or event"
status: Proposed
author:
  - "Irakli Gabisonia"
created: 2026-09-23
summary: "Route mapped commands through Send and mapped events through Publish, and deprecate MessageHeader.MessageType while retaining its wire representation and pump control signals."
tags:
  - "reactor"
  - "proactor"
  - "transports"
---

# 72. Route mapped requests by command or event

Date: 2026-09-23

## Status

Proposed

## Context

The message pumps choose Send or Publish from the transport message type header.
Messages from third-party producers commonly default to MT_EVENT, even when the
consumer maps them to commands. This permits command fan-out or prevents an event
from reaching multiple handlers. See [issue #4266](https://github.com/BrighterCommand/Brighter/issues/4266).

MessageHeader.MessageType also carries MT_NONE, MT_UNACCEPTABLE and MT_QUIT control
signals. Transports and outboxes persist the field, so removing it would affect
existing messages and mixed-version deployments.

## Decision

Both pumps dispatch mapped ICommand requests through Send and mapped IEvent
requests through Publish, including direct implementations of these interfaces.
The Command and Event base classes remain supported through their marker interfaces.

Subscription validation reports an error when the configured request type implements
neither interface. The pumps reject such a mapped request as unacceptable at runtime,
covering subscriptions that choose the request type dynamically.

Mark MessageHeader.MessageType and the protected header/type mismatch check obsolete.
Preserve the public property's accessors, serialization and stored values. Keep control
signal handling before mapping and leave transport defaults unchanged: the mapped
request determines application routing even when a producer omits the header.

Retained compatibility accesses use scoped CS0618 suppressions, following the existing
deprecation convention. Deprecation warnings remain visible to application callers.

## Consequences

Consumers can interpret third-party events as commands without changing the producer.
Commands retain the single-handler constraint; events can reach multiple handlers.
Applications using bare IRequest implementations must implement ICommand or IEvent.

Existing compiled callers and stored messages retain the header API and wire format.
Recompiling code that accesses MessageHeader.MessageType emits CS0618, which fails
builds that treat warnings as errors. Application routing should use request types;
transport adapters that still need the field can suppress the warning locally.

Removing the property requires a separate migration for transport persistence and
pump control signals.
