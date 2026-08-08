# Spec 0034 — Surface Failed Delivery Context from Confirmation-Based Producers

**Created:** 2026-06-10
**Branch:** `issue-4179-failed-delivery-context`
**Tracking issue:** [#4179](https://github.com/BrighterCommand/Brighter/issues/4179)

## Summary

Confirmation-based producers (Kafka, RabbitMQ) report publish success/failure asynchronously via
`ISupportPublishConfirmation.OnMessagePublished`, which routes to `OutboxProducerMediator`. Today the
failure case (`success == false`) is silently dropped: the publisher swallows the `ProduceException`
without logging, the failed message id is discarded, the circuit breaker is never tripped, and nothing
is recorded on the OpenTelemetry span. The message remains un-dispatched (correctly left for the sweeper),
but operators get no signal that delivery failed.

This spec makes failed delivery observable without changing the "don't bubble, let the sweeper retry"
design choice.

## Goals

- Log a **Warning** (not Error) when a confirmation-based publish fails, identifying the message id.
- Propagate the failed message id through `PublishResults` → `OnMessagePublished(false, id)`.
- Enrich OpenTelemetry spans on failure (span-lifetime trade-off to be resolved in the ADR).
- Trip the circuit breaker for the topic on confirmation failure, matching the non-confirmation path.

## Out of Scope (initial)

- Changing the sweeper-retry semantics or bubbling exceptions to callers.
- Fixing the wrong-typed `ProduceException<string, string>` catch clauses (note in design; low risk).

## Key Open Question

**Span lifetime for the async confirmation callback.** Synchronous failure points still have a live
producer span; the async `OnMessagePublished` callback fires after `DispatchAsync` has ended its spans.
The ADR must choose: keep the span open until confirmation, attribute to an active parent span, or emit
a standalone span linked via message id / propagated trace context.

## Status Checklist

- [ ] Requirements (`/spec:requirements`)
- [ ] Design / ADR (`/spec:design`)
- [ ] Adversarial review (multiple rounds)
- [ ] Tasks (`/spec:tasks`)
- [ ] Implementation (`/spec:implement`)
- [ ] Verify
- [ ] Review

## Affected Code (from issue triage)

- `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessagePublisher.cs:52-62` — silent swallow
- `src/Paramore.Brighter.MessagingGateway.Kafka/KafkaMessageProducer.cs:364-384` — `PublishResults` discards id on failure
- `src/Paramore.Brighter/OutboxProducerMediator.cs:737-784` — callback configurers ignore `success == false`
- `src/Paramore.Brighter/OutboxProducerMediator.cs:950-1009` — `DispatchAsync`; breaker not tripped for confirmation producers
- `src/Paramore.Brighter/Observability/BrighterTracer.cs:73` — `AddExceptionToSpan` available for span enrichment
