# Specification: Outbox Metrics

**Feature Name**: Outbox Metrics
**Spec ID**: 0037
**Created**: 2026-09-18
**Status**: Requirements Draft

## Overview

Brighter emits trace spans when messages are written to an Outbox, when an Outbox is cleared, and
when the sweeper checks for outstanding messages. Those spans are the only observability signal for
the Outbox, and because they are not sampled in production there is effectively no visibility into
Outbox behaviour: how many messages are written, how many are dispatched inline versus left for the
sweeper to pick up, and how long a message waits between being written to the Outbox and actually
reaching the broker.

This spec adds OpenTelemetry metrics for the Outbox so that an application using a Brighter Outbox
can answer those questions from metrics rather than from sampled traces.

## Workflow Status

- [x] Requirements defined
- [x] Requirements approved
- [x] ADR created
- [ ] ADR approved
- [ ] Adversarial review
- [ ] Tasks approved
- [ ] Implementation complete
- [ ] Tests passing
- [ ] PR submitted

## Files

- `requirements.md` — user requirements and problem statement
- [`docs/adr/0072-outbox-metrics.md`](../../docs/adr/0072-outbox-metrics.md) — the design decision

## Scope

### Affected (provisional, pending design)

- `src/Paramore.Brighter/Observability/` — meters, semantic conventions, metrics-from-traces processor
- `src/Paramore.Brighter/OutboxProducerMediator.cs` — the add / clear / dispatch paths
- `src/Paramore.Brighter/OutboxSweeper.cs`
- `src/Paramore.Brighter.Extensions.Diagnostics/` — registration of any new meter

### Not affected (provisional, pending design)

- Individual Outbox implementations (`Paramore.Brighter.Outbox.*`) — instrumentation is expected to
  sit above the Outbox abstraction in the mediator.

## Related Issues

- Jira: RLOI-4185 — Add outbox metrics to Brighter
- Jira: RLOI-4186 — [OrderDispatchWorker] Improve outbox observability (blocked by this)
- Jira: PI-36093 — production incident that motivated the ticket (events emitted after significant lag)

## Dependencies

- Metric names and instrument types need agreement with the Brighter maintainers
  (`#interest-oss-brighter`). Until that lands, the ADR records the proposal and the open questions.
