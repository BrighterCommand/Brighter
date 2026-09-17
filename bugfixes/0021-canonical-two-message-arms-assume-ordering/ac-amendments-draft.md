# Draft AC amendments — bug 0021, order-dependent FR-7 / FR-16 arms

**Status: ✅ APPROVED BY THE USER AND APPLIED to `requirements.md` (2026-09-17).** This file is kept
as the rationale record — the spec carries the wording now. ⭐ **One addition the user made on
approval, folded into NFR-4: a scheduler-mediated delay de-orders even on an ordered transport** (see
"Edit 5"). ⛔ The companion ordering table was NOT pasted — only 5 of its rows are verified; NFR-4
instead carries an explicit "Owed" note naming what is verified and what is not.
Target file: `specs/0036-universal-transport-conformance-tests/requirements.md`.

Five edits. Four are replacements of existing text; one is new. Line numbers verified in this
worktree at the time of drafting — re-check before applying, they drift.

⚠️ **Correction to `bugfix.md`:** AC-7 is at **`:634-636`**, not `:635-637`. AC-17 (`:698-701`),
FR-7's Example (`:183-184`) and NFR-2 (`:514-522`) are as recorded.

---

## The principle these edits encode

The suite's premise is that these behaviours are **Brighter's**, not the broker's (ADR 0066, and the
spec summary). **Delivery order is the broker's.** An AC that mandates order therefore tests the
transport's ordering guarantee, not Brighter's behaviour — which is why it silently converted a
correct implementation into a red test the moment the messages became distinguishable.

⭐ **The amendments do not weaken what is proven. They relocate the proof of redelivery from
*position* to *recurrence*:** an id seen **again, after the `Nack`**, is a redelivery by definition,
whatever order it arrives in. That is strictly what FR-16 claims, and it is order-free.

---

## Edit 1 — AC-7 (`:634-636`) — REPLACE

**Current:**

> - **AC-7 (FR-7).** *Given* a channel with neither DLQ nor invalid channel and two queued messages
>   `M1` and `M2`, *when* `channel.Reject(M1, DeliveryError)` is called, *then* it returns `true` and
>   the next receive yields `M2`.

**Proposed:**

> - **AC-7 (FR-7).** *Given* a channel with neither DLQ nor invalid channel and two queued messages
>   `M1` and `M2`, *when* the channel receives one of them — whichever the transport delivers first,
>   call it `MR` — and `channel.Reject(MR, DeliveryError)` is called, *then* it returns `true`; and a
>   bounded receive-retry loop (NFR-2) yields the **other** message within the ceiling, with `MR`'s id
>   absent from every receipt in that window. **Which of the two is delivered first is not asserted:
>   ordering is a transport property, not a Brighter behaviour (NFR-4).**

**Why.** `Reject` with no channels deletes the message (`SqsMessageConsumer.cs:263-272`), so on an
unordered transport a wrong-order first receive destroys the evidence the terminal assertion needs.
Rejecting *whichever arrived* is a faithful instantiation of FR-7 — the FR says nothing about which
message is rejected.

⚠️ **Judgement call, yours.** The clause *"with `MR`'s id absent from every receipt in that window"*
preserves FR-7's "removed rather than redelivered" claim, but **SQS Standard is also at-least-once**:
a pre-existing duplicate copy of `MR` has its own receipt handle and cannot be deleted by the arm, so
this clause carries a residual flake. **Recommend keeping it.** Reordering is a coin flip (~50 %);
SQS duplicates are rare. Keeping the clause converts a frequent flake into a rare one and retains the
assertion. The alternative — drop the absence check and rest on `Assert.True(rejected)` alone — is
duplicate-proof but stops proving the FR's own words.

---

## Edit 2 — AC-17 (`:698-701`) — REPLACE

**Current:**

> - **AC-17 (FR-16).** *Given* a received message `M`, *when* `channel.Nack(M)` or `NackAsync(M)` is
>   called, *then* a subsequent receive within the bounded retry loop yields a message with `M`'s id
>   and body; and, given a second queued message `M2`, after nacking `M` the redelivered `M` is
>   received and then `M2` is received.

**Proposed:**

> - **AC-17 (FR-16).** *Given* a received message `M`, *when* `channel.Nack(M)` or `NackAsync(M)` is
>   called, *then* a subsequent receive within the bounded retry loop yields a message with `M`'s id
>   and body. *And*, given two queued messages `M1` and `M2`, *when* the channel receives one of them
>   — whichever the transport delivers first, call it `MN` — and nacks it, *then* a bounded
>   receive-retry loop (NFR-2), **acknowledging each message it receives**, observes **both** ids
>   within the ceiling: a **second** receipt of `MN`'s id, which is a redelivery by definition
>   because `MN` was already received once and released; and a receipt of the other message's id,
>   proving it was not blocked behind the redelivery. **The two may arrive in either order: ordering
>   is a transport property, not a Brighter behaviour (NFR-4).** Repeat receipts beyond the first of
>   each id are ignored, so an at-least-once transport does not fail the arm.

**Why this still forbids what `2e650c36e` was written to forbid.** A naive *"both messages were
eventually received"* would be satisfied by an implementation that **never redelivers at all** — the
nacked message's first arrival plus the other message's arrival already makes two ids. ⛔ **That is
exactly the blindness the commit removed, and it must not be reintroduced.** The amended AC counts
receipts **after the `Nack`** only, so an implementation that does not redeliver never produces
`MN`'s id in that window and times out. Both halves stay non-vacuous.

⭐ The "acknowledging each message it receives" clause is load-bearing, not housekeeping: without it
a head-of-line-blocking transport (FIFO, whose group is blocked while a message is in flight) can
never deliver the second id, and the arm would fail on the transports that are *most* correct.

---

## Edit 3 — FR-7's Example (`:183-184`) — REPLACE

**Current:**

> *Example:* send `M1` then `M2`; receive `M1`; `Reject(M1, DeliveryError)` returns `true`; the next
> receive yields `M2`.

**Proposed:**

> *Example:* send `M1` and `M2`; receive whichever arrives first, call it `MR`;
> `Reject(MR, DeliveryError)` returns `true`; a bounded retry loop then yields the other message, and
> `MR` does not reappear. On an ordered transport `MR` is `M1`, but the test does not require it.

**Why.** This Example is the proximate cause: FR-7's obligation prose is already order-free, and the
Example is what re-imported the order into the template. ⚠️ **An illustration in a spec gets
implemented.** Worth a moment's thought about whether other Examples in this document carry the same
risk — that is a broader sweep, not part of this fix.

---

## Edit 4 — NFR-2 (`:514-522`) — AMEND ONE CLAUSE

**Current clause:**

> …returning as soon as a message arrives and failing if the ceiling is reached with none.

**Proposed:**

> …returning as soon as a message arrives and failing if the ceiling is reached with none. **Where an
> acceptance criterion names a target rather than any arrival (AC-7's "the other message", AC-17's
> "both ids"), the loop returns as soon as that target is satisfied, acknowledging non-matching
> receipts so a head-of-line-blocking transport can make progress; the ceiling and poll interval are
> unchanged.**

**Why.** Without this clause a drain-until-target loop reads as violating NFR-2's "as soon as a
message arrives", and the rewritten arms would be spec-non-compliant on their face.

---

## Edit 5 — NEW: NFR-4 (Ordering neutrality) — INSERT after NFR-3 (`:539-542`)

⭐ **This is the root defect, not a consequence of it.** The spec nowhere states which configurations
guarantee ordering, which is what allowed an order-dependent AC to be written for a suite that
targets unordered transports. Without this, the same class of bug recurs the next time someone writes
a multi-message arm.

**Proposed:**

> - **NFR-4 (Ordering neutrality).** The suite MUST NOT assert the **relative delivery order** of two
>   messages, and MUST NOT assume it. Ordering is a property of the transport, not a Brighter
>   behaviour, and the targeted transports do not share one: some guarantee FIFO per queue or
>   partition, some guarantee it only within a declared group or ordering key, and some — SNS/SQS
>   Standard and GCP Pub/Sub without an ordering key — guarantee nothing. A multi-message arm MUST
>   identify each received message **by id** against the messages it sent, and state its assertions
>   over the **set** of ids observed within the NFR-2 bound. Where an arm must prove a *redelivery*,
>   it does so by observing an id **a second time after** the release (`Nack`/`Requeue`), never by
>   its position in the sequence. Transports are also at-least-once unless they state otherwise, so
>   arms MUST tolerate repeat receipts; an exact-count assertion over received messages is forbidden.
>   This is a special case of NFR-3: delivery order is a transport mechanism, and the suite does not
>   assert mechanism.

### Companion table — ⛔ NOT READY TO PASTE. Only five rows are verified.

The new NFR needs a statement of which configurations offer what. **I verified five rows; the rest
are inference from transport documentation and MUST be checked against the provider code before this
table enters the spec.** Publishing unverified guarantees would repeat the original mistake in a more
authoritative place.

| Configuration | Ordering | Evidence |
|---|---|---|
| `AWS{,.V4} / SnsStandard`, `SqsStandard` | ❌ **none** | ✅ **verified — this bug.** CI run 35220513183 |
| `AWS{,.V4} / SnsFifo`, `SqsFifo` | ⚠️ **per group — harness-induced** | ✅ **verified.** `FifoMessageBuilder.cs:43-46` stamps one partition key per *builder instance*; each test class holds one builder, so both messages share a `MessageGroupId`. **Not a property of the configuration — give the messages different groups and FIFO reorders too.** |
| `GCP / Pull`, `GCP / Stream` | ❌ **none** | ✅ **verified.** `GcpPullMessageGatewayProvider.cs:125-131` sets no `EnableMessageOrdering`; `tests/Paramore.Brighter.Gcp.Tests/test-configuration.json:4-13` gives `Pull` no `MessageBuilder` |
| `GCP / PullOrdering`, `StreamOrdering` | ✅ **per ordering key** | ✅ **verified.** `GcpPullOrderingMessageGatewayProvider.cs:139` `EnableMessageOrdering = true`; `test-configuration.json:19,42` set `FifoMessageBuilder` |
| `Kafka / *` | ✅ per partition *(expected)* | ⛔ **NOT VERIFIED** — confirm the three configurations pin a single partition or a constant key |
| `RMQ.Async / *`, `RMQ.Sync / *` | ✅ per queue *(expected)* | ⛔ **NOT VERIFIED** |
| `Redis`, `Postgres`, `MSSQL` | ✅ *(expected)* | ⛔ **NOT VERIFIED** — all three read a single ordered structure, but confirm |
| `AzureServiceBus` | ✅ per queue/subscription *(expected)* | ⛔ **NOT VERIFIED** |
| `RocketMQ` | ❌ **none expected** on a normal topic | ⛔ **NOT VERIFIED** — and its FR-7/FR-16 cells read `Fixed (#4240)`, so this is the same latent exposure as `GCP / Pull` |
| `MQTT` | ✅ *(expected)* — QoS-1, one topic, one client | ⛔ **NOT VERIFIED** |

---

## Two decisions the amendments do not make for you

**1. The generated method name.** The FR-16 two-message arm is
`When_nacking_first_of_two_messages_should_redeliver_nacked_then_receive_second`. ⚠️ **The `_then_`
encodes the order the amendment removes**, so the name becomes a lie. Suggested:
`When_nacking_first_of_two_messages_should_redeliver_nacked_and_receive_second`.
✅ **Low risk, verified:** the method name appears **only** in the two `.liquid` templates (plus
`bin/` copies). No golden test, no audit and no NFR-1 example references it. The template *file*
names do not change, so `GeneratedTreeAudit` sees no missing/orphan.

**2. The four falsified ledger cells** (`conformance-status.md:819,821,823,825` — `AWS{,.V4} /
Sns|SqsStandard`, FR-7 and FR-16 both `Pass`). Those Passes were earned by the pre-`2e650c36e`
blindness. Once the arms are order-free they should return to `Pass` **on their own merits** — so the
cleanest route is to leave them and let the re-run prove them, rather than churn them to `Deferred`
and back. ⛔ **But do not simply assume the re-run is green: nothing has yet demonstrated these two
behaviours hold on an unordered transport, because no test has ever been able to tell.** If the
order-free arms go red, that is a genuine transport finding and the cells must change.

---

## Order of operations once the wording is settled

1. Amend `requirements.md` (these five edits).
2. `/bugfix:test` — the failing regression test is written against the **amended** ACs.
3. Rewrite the four `.liquid` templates; update the four golden facts at
   `When_generating_nack_test_should_emit_redelivery_and_two_message_variant_both_variants.cs:262,285`
   and `When_generating_no_channels_reject_should_emit_ack_and_continue_both_variants.cs:214,237`
   **in the same commit**.
4. `./generate-test.sh` — 96 generated files across 24 configurations.
5. Re-run `aws-ci`; reconcile ledger cells only if the re-run says so.
