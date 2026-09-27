# Adding a New Canonical Behaviour

The conformance suite proves twelve behaviours across 24 transport configurations. This guide is
for adding a thirteenth — a consumer or producer behaviour that every transport should honour, and
that should therefore be written once and generated everywhere.

Read [transport-conformance-getting-started.md](transport-conformance-getting-started.md) first,
especially *"What the suite deliberately does NOT prove"*. Its companion for the other direction is
[transport-conformance-new-transport.md](transport-conformance-new-transport.md).

> **Quick reference for AI agents**:
> [`.agent_instructions/generated_tests.md`](../../.agent_instructions/generated_tests.md).

## Is it actually a canonical behaviour?

Three tests, all of which must pass, before you write a line:

1. **Is it Brighter's behaviour, or the broker's?** The suite exists to prove that *Brighter* gives
   an application the same semantics over any transport. "The broker redelivers in order" is not a
   canonical behaviour; "a nacked message is redelivered" is.
2. **Can it be asserted without asserting mechanism?** You may assert only that the observable
   behaviour holds, against the channel and producer surfaces. If the only way to state it is "and
   it did so natively" or "and it used the scheduler", it is out of scope. The same bar rules out
   asserting relative delivery order, which is a transport mechanism.
3. **Does every transport have to honour it?** A canonical behaviour is generated into all 24
   configurations. A transport that cannot do it gets a `Deferred` ledger cell with a signed-off
   issue — not an absent test. If you find yourself wanting a flag to opt transports out, you have a
   transport-specific test, not a canonical behaviour.

## Everything a new behaviour touches

This is the whole list, in order. None of it is optional, and nothing else in the repository has to
change:

| # | Touch | Detail |
|---|---|---|
| 1 | **Two templates** | `Templates/MessagingGateway/Reactor/When_….cs.liquid` and the matching `Proactor/` one. Same behaviour, sync and async surfaces |
| 2 | **`CanonicalBehaviours.cs`** | **Both** dictionaries: `TEMPLATE_FR_COLUMNS` (template base name → ledger column) and `FR_COLUMN_BEHAVIOURS` (ledger column → the human-readable label that ends up in the `Skip` string) |
| 3 | **A new ledger column** | A column in the matrix in [`conformance-status.md`](../../specs/0036-universal-transport-conformance-tests/conformance-status.md), and a value in **all 24 rows** |
| 4 | **A generator test** | Under `tests/Paramore.Brighter.Test.Generator.Tests/CanonicalTemplates/`, pinning what the two templates must emit — broker-free, runs in the `build` job in milliseconds |
| 5 | **The hard-coded name lists** | See the warning below |
| 6 | **Regeneration** | `./generate-test.sh`, then commit the ~48 new generated files (24 configurations × 2 variants) |
| 7 | **The requirements document** | A new `FR-n` and its acceptance criterion in `requirements.md` — the ledger column is a reference into it |

### ⚠️ Two lists shadow `CanonicalBehaviours` and will not fail loudly

`CanonicalBehaviours.cs` is the authority, but two generator tests keep their own hard-coded copies
of the canonical set:

- `CanonicalTemplates/When_reconciling_canonical_set_should_cover_kafka_reference_surface.cs`
- `CanonicalTemplates/When_generating_everywhere_should_emit_skipped_canonical_suite_in_all_wired_projects.cs`

Both currently hold **eleven** names where `CanonicalBehaviours.TEMPLATE_FR_COLUMNS` holds twelve —
FR-23 is in the authority and in neither copy. Some of that is by design (the first is scoped to the
*Kafka reference surface*, a fixed historical list, and FR-23 is not part of it) and some is drift.

The consequence for you: a behaviour you add to `CanonicalBehaviours` but not to the second file is
**silently not checked** by the everywhere-generation test. It does not fail; it just covers less.
Decide deliberately which lists your behaviour belongs in, and prefer reading `CanonicalBehaviours`
over adding a third copy.

## How a ledger column starts life: `Unknown`

A new behaviour is not conformant anywhere on the day you write it, and the ledger has a value for
exactly that: **`Unknown`**.

Fill all 24 cells of your new column with `Unknown` and the generated suite is **skipped everywhere
and named everywhere** — each test carrying a placeholder `Deferred: #NNNN` marker. That is the
honest "nothing passes yet" state, it is legal, and it does not fail the build.

What it *does* block is the final cleanup merge: the rule is that no cell may remain `Unknown` when
the gate-retirement change merges. That rule is prose in the ledger header enforced by a maintainer,
not a compile error — so it constrains when your work can be *finished*, not when it can be *done*.

From there you drive each cell exactly as a new transport does: `Pass`, `Fixed (#PR)`, or
`Deferred -> #NNNN (sign-off: @handle)` with a real issue, a real maintainer and prose saying why.
Regenerate after every ledger edit; a cell and its generated `Skip` must agree at that exact
intersection, and an audit checks it.

## Developing the behaviour before you extract the template

The natural instinct — build it against something in-process, extract the template, then watch it
skip everywhere until you fill it in — is right, and the `Unknown` mechanic above is what supports
the last part of it.

Two honest caveats about the first part:

- **`InMemoryMessageConsumer` already implements the whole canonical surface** — `Acknowledge`,
  `Nack`, `Reject(Message, MessageRejectionReason?)`, `Requeue(Message, TimeSpan?)` and their async
  forms, plus `InMemoryMessageProducer.SendWithDelay[Async]`. It is a legitimate place to work out
  what the behaviour *is* before committing to a template.
- **It is not wired to the generator.** `tests/Paramore.Brighter.InMemory.Tests/` has no
  `test-configuration.json`, so it has no ledger row and no generated suite. Today's reference
  surface is Kafka, and Kafka needs a broker. Wiring InMemory as a broker-free conformance row is a
  live proposal, not something you can lean on yet.
- **InMemory conforming proves nothing about a broker.** See the worked example below — that is not
  a theoretical caveat.

## ⭐ A worked example: how a real defect got in and got caught

[`bugfixes/0021-canonical-two-message-arms-assume-ordering/bugfix.md`](../../bugfixes/0021-canonical-two-message-arms-assume-ordering/bugfix.md)
is a complete, real loop through this machinery, and is worth reading in full. The short version:

| Stage | What happened |
|---|---|
| A latent assumption | Two canonical templates paired a named sent message with *whichever arrived first* — invisible while both messages were byte-identical |
| Exposed by a fix | Giving each message its own id, to close an unrelated blindness, turned the assumption into a failure |
| Caught only by a live broker | Real AWS. The mock, the generator suite and the full solution build were **all green** — the emulator serialises delivery, so it could not see it |
| A wrong first diagnosis | Triage said "the spec never requires ordering". It did — two acceptance criteria mandated it normatively. Catching that changed *who decides* |
| Root cause above the code | An ***Example*** in the spec (*"send M1 then M2 …"*) had quietly narrowed an obligation that was already order-free |
| Fixed at the right level | Four templates, not 96 generated files, pinned by a **broker-free** generator test that catches the regression in `build` in ~20 ms |
| Proved, then verified by name | Green with every arm confirmed **RUN** — zero skipped — because a green tick over a skipped suite proves nothing |

Three lessons, each earned there, each of which applies directly to writing a new behaviour:

1. **An illustration in a spec gets implemented.** Write your `*Example:*` as carefully as your
   obligation. A worked example that shows one ordering, one count, or one timing will be encoded
   into the template as a requirement you never meant to impose.
2. **Local green does not mean conformant.** The generator suite and the solution build cannot see a
   behaviour that only a real broker exhibits. They are necessary and they are not sufficient.
3. **Check that a conformance pass actually RAN.** The ledger can skip a whole arm. A green job is
   not evidence until you have confirmed the arm by name.

## Writing the templates

Copy an existing pair rather than starting blank —
`When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue` is the most complete
example in the set. The conventions the audits and reviewers expect:

- **Bounded retry loops, never a fixed sleep and a single receive.** 500 ms poll interval, 30 s
  ceiling for a channel arrival, 60 s for a rejection destination. The ceiling lives in the test;
  provider helpers do a single bounded receive.
- **Identify messages by id, never by arrival position**, and state assertions over the *set* of ids
  seen. Tolerate repeat receipts — transports are at-least-once — so no exact-count assertion over
  received messages.
- **Where an arm sends two messages, give each its own id and body.** A shared builder whose id is a
  field initialiser produces two copies of one message, and the assertion can then no longer tell
  "the second arrived" from "the first was redelivered" — which is precisely the outcome such a test
  exists to forbid.
- **Prove a redelivery by seeing an id a second time *after* the release**, never by its position.
- **No mechanism terms.** The reconciliation test fails the build on `IAmAMessageScheduler` and its
  siblings appearing in a canonical template body.
- **Honour the ledger's skip** in the `[Fact]` attribute — copy the existing spelling verbatim:
  `[Fact{%- if Skip != empty -%}(Skip = "{{ Skip }}"){%- endif -%}]`. Omit it and your behaviour cannot
  be deferred on a transport that fails it.
- **Name it `When_…_should_…`**, matching the hand-written convention.

## Checklist

- [ ] The behaviour passes all three "is it canonical?" tests above.
- [ ] `requirements.md` has the new `FR-n` obligation and its acceptance criterion, and the
      `*Example:*` has been read back for assumptions it silently adds.
- [ ] Reactor and Proactor templates written, following the conventions above.
- [ ] `CanonicalBehaviours.TEMPLATE_FR_COLUMNS` **and** `FR_COLUMN_BEHAVIOURS` both updated.
- [ ] A `CanonicalTemplates/` generator test pins what the templates must emit — broker-free.
- [ ] Both hard-coded name lists reviewed, and inclusion or exclusion decided deliberately.
- [ ] New ledger column added with all 24 cells `Unknown`.
- [ ] `./generate-test.sh` run; the ~48 new files committed; `GeneratedTreeAudit` green.
- [ ] Cells driven off `Unknown`, each `Pass` confirmed to have **run** by name.
- [ ] `dotnet test tests/Paramore.Brighter.Test.Generator.Tests` green.

## Reference

- [transport-conformance-getting-started.md](transport-conformance-getting-started.md) — the scope
  rules this guide's "is it canonical?" test comes from.
- [transport-conformance-new-transport.md](transport-conformance-new-transport.md) — the audits, in
  full, and the provider contract.
- [`.agent_instructions/generated_tests.md`](../../.agent_instructions/generated_tests.md) — every
  template, configuration key and feature flag.
- [ADR 0035](../adr/0035-generated-test.md) — why tests are generated.
- [ADR 0066](../adr/0066-conformance-test-provider-and-ungating.md) — why canonical behaviours are
  ungated by construction.
- [ADR 0067](../adr/0067-conformance-rollout-and-deferral-governance.md) — the ledger vocabulary and
  deferral governance.
