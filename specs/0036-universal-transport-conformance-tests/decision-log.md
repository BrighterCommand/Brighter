# Decision log — spec 0036, universal transport conformance tests

Why identifiers were retired and why decisions were reversed. **This file is the record of
deliberation.** `requirements.md` states what must be true; the ADRs state how and why the design
works; neither carries withdrawal narration. Readers wanting only the specification can ignore this
file entirely.

Retired identifiers are never reused, so a gap in the numbering is deliberate. Each is explained
below.

---

## Retired: OOS-6 — the exclusion of unwired gateway transports

**Retired 2026-07-19, by spec-owner instruction.** In scope: all twelve gateway transports. FR-20
onboards the three that lack generator wiring.

OOS-6 excluded AzureServiceBus, MQTT and RMQ.Sync from the spec because they declare no
`test-configuration.json` and are therefore not generated for today.

**How it arose.** Review round 4 found that FR-13's phrase "every gateway configuration the
generator targets" was undefined. The definition written to close that finding was *descriptive* —
it named the nine projects the generator wires today — and that description then hardened into a
normative scope boundary without anyone deciding it on the merits. Round 5 added RMQ.Sync and MQTT
to the exclusion; round 6 observed that Azure Service Bus belonged in the same class and it was
added too. Three review rounds refined an exclusion none of them questioned, because each round was
told the target set was settled.

**Why it was wrong.** It repeats the error the spec exists to correct. The capability flags
described what was *being tested* and were treated as what a transport *owed*; a missing
`test-configuration.json` describes what the generator *covers* and was likewise treated as what
needs covering. Absence of wiring is a gap to close, not a scope boundary.

The three are not bare gateways: RMQ.Sync carries 31 hand-written gateway tests, MQTT 18,
AzureServiceBus 8. MQTT has its own dead-letter ADR (`0043-mqtt-dlq-brighter-managed`), and both
MQTT and RMQ.Sync implement `IAmAChannelFactoryWithScheduler`.

**Consequence accepted.** Onboarding needs a config, provider implementations and CI infrastructure
per transport; ASB is a cloud service with no container story in this repo. FR-13's deferral rule
governs any that cannot complete in-spec — a named, linked, signed-off ledger row, never silent
absence. ADR 0067 records the expectation that one or more lands as `Deferred` at flip time.

---

## Retired: FR-18, AC-19 — requeue-count exhaustion as a canonical behaviour

**Retired 2026-07-19, after review round 5.** Replaced by FR-19: delete the template.

FR-18 asserted that requeuing a message `Subscription.RequeueCount` times routes it to the DLQ, as a
universal channel obligation.

**How it arose.** Review round 4 found that
`When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue.cs.liquid` would be
silently ungated by FR-10, because `SkipTest` matches filename substrings and that name matches both
`dead_letter_queue` and `requeuing`. Adopting it as canonical was chosen over leaving it
unaccounted.

**Why it was wrong.** The supporting claim — "`RequeueCount` is a property of `Subscription` for
every transport, so this is a universal obligation" — is a non-sequitur, and was asserted without
being checked. `RequeueCount` existing says nothing about whether the *channel* enforces it, and it
does not:

- `Message.HandledCountReached` has exactly two callers, `Reactor.cs:498` and `Proactor.cs:504` —
  both in the message pump, which OOS-5 excludes.
- `Channel.Requeue` / `ChannelAsync.RequeueAsync` forward straight to `_messageConsumer.Requeue` and
  count nothing.
- Where the template passes today it proves the *transport's* native redrive: the AWS provider pairs
  `requeueCount: 3` with `redrivePolicy: new RedrivePolicy(dlqName, 3)`, so SQS does the counting.
  NFR-3 and OOS-1 exclude native-mechanism assertions.

It is the same defect that withdrew FR-3. Deleting the template loses no channel-owned coverage: the
plain-requeue half is the FR-10-ungated plain-requeue template, and the DLQ-arrival half is FR-4.

---

## Retired: FR-3, FR-1(4), NFR-4, AC-3 — scheduler-delegation testing

**Retired 2026-07-18, by ADR 0066.** Folded into the mechanism-agnostic FR-2.

FR-3 asserted that a delayed requeue is delegated to the producer's scheduler when the transport has
no native delay; FR-1(4) required a provider member exposing a scheduler-carrying producer; NFR-4
constrained that test to an in-memory scheduler or spy; AC-3 was FR-3's criterion.

**Why withdrawn.** Two reasons. It is a *mechanism* assertion, which NFR-3 and OOS-1 forbid —
asserting a requeue went via the scheduler rather than via native delay tests *how* a transport
achieves the behaviour. And the seam does not exist for most transports:
`IAmAChannelFactoryWithScheduler` is implemented by six gateways (Kafka, MQTT, MsSql, Redis,
RMQ.Async, RMQ.Sync); the other consumers take no scheduler at all, and giving them one would be a
public runtime API change that C-1 forbids.

The observable behaviour FR-3 cared about — a delayed requeue redelivers after the delay — is
covered by FR-2 uniformly. Scheduler-delegation testing for the six gateways that have the seam is
supplementary work under OOS-2.

---

## Narrowed: FR-12 / AC-12 — the no-delay-requeue prohibition

**Narrowed 2026-07-19, after review round 4.** Not a retirement; the identifiers remain live.

FR-12 originally read "after deletion, no messaging-gateway template may call `Requeue` or
`RequeueAsync` without a non-null `TimeSpan`". That was self-contradictory: FR-10 preserves the
plain-requeue template, which calls `Requeue` with no argument, and FR-15 *requires* a test calling
`Requeue(M, null)`. AC-12 was unsatisfiable as written. The prohibition is now scoped to templates
that exercise *delayed* requeue, with those two exempt.
