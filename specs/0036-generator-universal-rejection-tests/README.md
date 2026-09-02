# Specification: Generator-Owned Rejection, DLQ and Delay Conformance Tests

**Feature Name**: Generator-Owned Rejection, DLQ and Delay Conformance Tests
**Spec ID**: 0036
**Created**: 2026-08-29
**Status**: Requirements Draft

## Overview

The messaging gateway test generator treats `HasSupportToDelayedMessages` and
`HasSupportToDeadLetterQueue` as native-capability switches. They actually gate behaviour that
Brighter provides *for* a transport — scheduler-backed delay, and a Brighter-provisioned dead
letter / invalid message channel driven by `Reject`. Because the generator cannot express those
behaviours, contributors hand-wrote the same tests once per transport, and the flags were set to
whatever kept the generated suite green.

This spec makes the Brighter-provided rejection and delay behaviours generator-owned and on by
default, and re-scopes the two flags to describe *native* broker capability only.

## Workflow Status

- [x] Requirements defined
- [ ] Requirements approved
- [x] ADR created
- [ ] ADR approved
- [ ] Adversarial review
- [ ] Tasks approved
- [ ] Implementation complete
- [ ] Tests passing
- [ ] PR submitted

## Files

- `requirements.md` — user requirements and problem statement
- `tasks.md` — task breakdown (draft, pending ADR approval)

## ADRs

- [ADR 0070: Generator-Owned Rejection and Delay Conformance](../../docs/adr/0070-generator-owned-rejection-and-delay-conformance.md) — **this design**
- [ADR 0037: Add Messaging Gateway Generated Tests](../../docs/adr/0037-add-messaging-gateway-generated-test.md) — the generator this extends
- [ADR 0045: Provide a Dead Letter Channel Where Native Support is Missing](../../docs/adr/0045-provide-dlq-where-missing.md)
- [ADR 0047: Message Rejection Routing Strategy](../../docs/adr/0047-message-rejection-routing-strategy.md) — the ladder under test
- [ADR 0037: Universal Scheduler Delay Support](../../docs/adr/0037-universal-scheduler-delay.md)
- [ADR 0039: Transport Channel Factory Scheduler Wiring](../../docs/adr/0039-transport-scheduler-wiring.md)

## Scope

### Affected

- `tools/Paramore.Brighter.Test.Generator/` — configuration, `MessagingGatewayGenerator.SkipTest`, templates
- `tests/*/test-configuration.json` — 21 messaging gateway configurations across 9 test projects
- `tests/*/MessagingGateway/*MessageGatewayProvider.cs` — 21 provider implementations
- Hand-written rejection/requeue tests in the transport test projects (retired as templates replace them)

### Not affected

- `src/` production code. This spec adds no transport behaviour; it only tests behaviour that
  already exists. Conformance gaps it exposes are recorded as waivers and tracked separately.

## Related Issues

- Parent: #4240
- Siblings: #4238 (single Outbox async-only), #4239 (`CollectionName` ignored by sync outbox templates)
