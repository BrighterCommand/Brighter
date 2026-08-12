# The readability programme — plan

The plan that answers [`readability-review.md`](readability-review.md), the owner's manual review of
ADRs 0070–0076. Working notes and the earlier scope-only discussion are in
[`context-restructure.md`](context-restructure.md); this file is the plan of record.

**Status: Phase 0 complete. Phase 1 is next.** No ADR and no instruction file has been edited.
`PROMPT.md` §20 is the live phase tracker.

---

## 1. What the review changes

`readability-review.md` is not another findings bucket. Its item 15 — *"a review and fix technique
that corrects individual statements needs occasional re-read of the whole document as statements
become contradictory or unnecessary in context"* — is a statement about **method**, and it is the
load-bearing item in the file.

Six review rounds of statement-level correction for exacting language are what produced the prose
now being objected to: 490- and 498-word `Scope` paragraphs, a bold run every 52 to 66 words, arguments
recorded where decisions belong. More statement-level patching cannot fix that. So the readability
work is a **whole-document rewrite, one ADR per session**, against a house style that is written
down first.

Scale, for sizing: the seven ADRs are **~90,000 words** and carry **1,557 bold runs** between them.

| ADR | words | bold runs | one bold run per |
| --- | --- | --- | --- |
| 0070 | 15,568 | 288 | 54 words |
| 0071 | 12,325 | 207 | 60 words |
| 0072 | 15,275 | 296 | 52 words |
| 0073 | 9,701 | 172 | 56 words |
| 0074 | 15,710 | 244 | 64 words |
| 0075 | 10,452 | 189 | 55 words |
| 0076 | 10,620 | 161 | 66 words |

## 2. The four buckets

Every item in `readability-review.md` falls into one of four, and **the buckets never share a
commit**. That rule is not new: round 5 mixed a substantive correction into a mechanical pass and
it produced both of round 6's Criticals.

| Bucket | What it is | Where it lands |
| --- | --- | --- |
| **H** | house style, repo-wide | `.agent_instructions/documentation.md`, `.agent_instructions/design_principles.md` — **Phase 1**, one commit, no ADR touched |
| **R** | per-ADR readability | absorbed into that ADR's rewrite — **Phase 3** |
| **M** | content that does not belong in an ADR | relocation to `PROMPT.md` or to the requirements true-up; rides the rewrite of its ADR |
| **S** | substantive — changes a decision, a fact, or a design | **Phase 2**, one commit each, *before* the rewrite. Six findings resolve to **four calls** — S1+S2 are one question, S4 is settled by principle |

## 3. Phase 0 — locate every item against current source ✅ COMPLETE

**40 bullets · 2 non-actionable** (one praise at 0070's *"The mechanism, end to end — good work"*;
one caveat, item 9) **· 38 actionable**, resolving to **10 house-style rules (H)**, **17 per-ADR
readability items (R)**, **2 relocations (M)**, **6 substantive (S)**, and 3 bullets that flag the
same sentence.

### What drifted — four items, and the owner's item 9 predicted it

The review was written by hand while round 6 was still landing.

| Review item | Filed as | Actually |
| --- | --- | --- |
| *"a `Dispatcher` started from inside a live request"* | ADR 0073 | ⚠ **THIS ROW WAS WRONG AND S1's LANDING (`d6502deb5`) CORRECTED IT.** The quoted sentence is at **`0073:209` verbatim** — the review filed it correctly — and `0073:211` is the *"next paragraph"* the item objects to being preceded by it. `0075:286` is a **paraphrase twin**, restated `0075:326`, `0075:349`. Five sites moved, in two ADRs, not three in one |
| *"This is a re-reading of FR-27.1's own words"* | ADR 0071 | **`0072:104`** |
| *"Two kinds of flow reach that line already suppressed"* | ADR 0071 | **`0072:112`** |
| *"Raising those **five** was rejected"* | quoted as five | **`0070:335` now says seven** — a round-6 commit changed the count |

And one item has a twin the review could not have known about: *"a handle this factory does not
recognise is ignored, not rejected"* — the most-objected-to sentence in the set, flagged three times —
is at **`0071:237` and `0072:238`**. Fix one end only and the set contradicts itself again.

### The located items, at `f5613099e`

| Review item | Bucket | Location |
| --- | --- | --- |
| `Scope` should lead with Defect 1b, `IAmAScope` and FR-13 as the core | R | `0070:30` and `:34`; Defect 1b is defined at `0070:72` |
| *"How the set treats non-functional requirements…"* → PROMPT.md | M | `0070:32` |
| NFR-8 `IAmALifetime` distinction buried in an NFR note | R | `0070:91` |
| *"the single internal exception"* — promote the exception to public | **S4** | `0070:276` |
| *"What this cache does and does not give"* → belongs in Decision | R | `0070:285` |
| *"surfaces its inner disposal failure"* — needs a sequence diagram | R | `0070:348` |
| argument-as-record, not decision (the *"Raising those … was rejected"* pattern) | R | **9 lines across six ADRs**: `0070:335`, `0070:407`, `0071:365`, `0073:209`, `0073:221`, `0073:244`, `0074:401`, `0076:263`, `0076:345` |
| move more argument into `## Alternatives Considered` bullets | R | `0070:481` is that section's head |
| *"FR-13 divides by family rather than by clause"* | R | `0071:30`, and `0070:32`, `0070:34` |
| *"`Transient` is not only `Scoped`'s poor relation"* — wants a diagram | R | `0071:104` |
| NFR-4 — what the `ConcurrentDictionary` buys; convention over restriction | R | `0071:108` |
| *"The member's **shape** is ADR 0070's"* — unreadable | R | `0071:209` |
| *"is not asserted over this property"* — hard to read | R | `0071:234` |
| *"is not asserted over this property"* — tracking, not a decision → PROMPT.md | M | `0071:234` (same passage, different objection) |
| *"ignored, not rejected"* — flagged three times | R | `0071:237` **and `0072:238`** (it has a twin) |
| *"The dictionary survives as the no-handle path"* — speculative | **S3** | ✅ **ANSWERED AND LANDED — `bd44be1ed`.** Raised at `0071:280` (anchor exact); **fourteen sites moved, in FOUR ADRs** — 0071 (13), `0072:494`, `0073:287` and 0070 step 7a's ledger count, plus `docs/adr/index.md` |
| *"AC-33 is that rule's regression guard"* — the cross-reference pattern | R | `0071:295` |
| *"a re-reading of FR-27.1's own words"* — argument, not decision | R | `0072:104` |
| *"Two kinds of flow reach that line"* — argument, not assertion | R | `0072:112` |
| `ScopeAffinityPolicy`'s role is unclear | R | `0072:131`, `:167`, `:242`, `:247`, `:249`, `:332`, `:336` (**19 mentions across the set**) |
| *"`AmbientScopeProbe` — what the probe is…"* — hard to parse | R | `0072:410` |
| consumer transform/mapper pipelines are never ambient | **S2** | **nowhere — see below.** The nearest statement is `0072:112` |
| a `Dispatcher` is never started from inside a live request | **S1** | `0075:286`, `:326`, `:349` |
| *"Why the rules are not `ISpecification<T>` families"* | **S5** | ✅ **ANSWERED AND LANDED — `bbb04d688`.** All seven anchors — `0074:140`, `:249`, `:367`, `:379`, `:388`, `:389`, `:502` — were verified exact and on-subject, the first row in the programme whose anchors had not drifted at all. The answer moved far more than them: **0074 throughout plus `0070:405` and the index** |
| registration must resolve from a borrowed ambient scope | **S6** | **one line in the set** — `0072:480` |

### ⚠ Phase 0's substantive result: S1 and S2 are one call, not two

`0072:112` reads: *"**A consumer pipeline is suppressed too, for its whole life**, by that ADR's third
bracket in `Performer.Run()`."* So **the set already reaches the owner's conclusion** — a consumer
pipeline never adopts an ambient scope. It reaches it by a **runtime bracket**, justified by the
scenario S1 says is a usage error.

That makes the two items halves of one question:

> **If a `Dispatcher` inside a live request is erroneous use rather than a configuration, is the
> pump-flow bracket defending against anything?** If consumer pipelines are structurally incapable of
> being ambient — a `Dispatcher` is not hosted inside an ASP.NET scope — then the bracket may be
> unnecessary, and if it is kept, its justification has to be rewritten in three places.

Raising them separately risks answering one and landing half. They are **one call, S1+S2**, and it
reopens round-6 decisions 5 and 9 (`679ff229f`).

### S6 has almost no footing in the set

`0072:480` is the only line that touches it: *"A provider that passes both tests and still cannot
resolve Brighter's artefacts fails loudly, not quietly … `Create` returns `null`, and the builder's
existing guard raises `ConfigurationException` (`PipelineBuilder.cs:193`)."* That is a **detection**
claim — it says the failure is loud. The owner's question is a **design** one: will Brighter's
registered services, many of them factory functions, still resolve from a scope owned by a
non-Brighter parent? Nothing in the set answers it, which is why it reads as a requirement rather
than an ADR fix.

## 4. The phases, and why in this order

| Phase | Work | Sessions |
| --- | --- | --- |
| **0** | locate every item against current source; complete the table in section 3 | ✅ **done** |
| **1** | the ten house-style rules into the two instruction files — **no ADR touched** | 1 |
| **2** | the substantive calls, one at a time, each landed in its own commit — **four calls, not six**: S1+S2 merged, S4 settled by principle | ~4 |
| **3** | the rewrite, **one ADR per session**, worst-first: 0072, 0071, 0070, then 0074, 0075, 0076, 0073 | ~7 |
| **4** | design review round 7, grading against the new house style | per §19's shape |

**Why house style first.** The rewrite is graded against `documentation.md`, and so is round 7 —
`/spec:review`'s *Structure and Readability* criteria read that file. Writing the rules after the
rewrite would mean the rewrite conformed to nothing.

**Why the substantive calls before the rewrite.** S3 and S5 could delete or replace whole sections;
polishing prose that then disappears is wasted work. S1 changes text in three places in 0075 and
touches a landed round-6 decision. Correcting first, then rewriting, means the rewrite carries the
correction — and it keeps a substantive change out of a diff where nobody would see it.

**Why round 7 last.** Your review found a usage error that six machine rounds and 478 findings
missed. Running round 7 against the old prose and the old house style would spend eight reviewers on
text about to be rewritten.

## 5. Phase 1 — the concrete diffs

Ten rules. Three already exist in some form and **one of those is mis-specified against the
principles file** — so Phase 1 corrects the house style before any ADR is asked to follow it.

### `.agent_instructions/documentation.md`

**D1 · New row in the ADR skeleton table, between `## Context` (`:73`) and `### Where this ADR sits`
(`:74`).** There is currently no row for this block at all, though 46 ADRs carry it.

> \| `### Scope` \| what the ADR covers, as lists rather than narrative. **Parent requirement** — the link. **In scope** — one bullet per FR/NFR it discharges, each naming the mechanism that makes the requirement true, plus a bullet for any scope no tagged requirement carries. **Out of scope** — one bullet per boundary a reader could reasonably mistake, each naming the ADR that does cover it. Where this ADR contributes to a requirement another ADR discharges, say so on the bullet and name the owner \|

**D2 · Rewrite the `### Key Components` row (`:80`).** It currently mandates
*"Role / Type / Stereotype (**knowing**, **doing**, **deciding**) / Responsibility"* — which
contradicts `design_principles.md:8` (knowing/doing/deciding are **responsibilities**) and `:15`
(stereotypes are *information holder, structurer, service provider, coordinator, controller,
interfacer*). The ADRs are faithfully following a wrong row. Replace with:

> \| `### Key Components` \| opens with `#### The roles, and what each is responsible for` — a table of **Role** / **Type** / **Responsibilities** / **Responsibility classifier** / **Collaborators**. *Role* is one phrase saying what the type does; a type needing more than one phrase has too many responsibilities. *Responsibilities* may be several. *Responsibility classifier* is **knowing**, **doing** or **deciding**, and one type may carry more than one. *Collaborators* are the types it works with to meet them. Then each significant type with a contract table (Member / Input / Output / Error conditions), then `#### Where each type is touched` (Assembly / Type / Change), closing with what is deliberately **unchanged** \|

**D3 · New sub-block in `### ADR readability` — sentence construction.** Nothing anywhere in the
repo currently constrains a sentence. The agreed form is a named subset of
[Simplified Technical English](https://www.asd-europe.org/standards-specifications/simplified-technical-english/),
not the full ASD-STE100: the value is in the writing rules, and the controlled dictionary would
fight this domain's vocabulary (*ambient*, *affinity*, *discharge*, *borrow*, *bracket*).

> **Sentence construction.** Follow these rules from Simplified Technical English, so that a
> non-native reader and a reader in a hurry get the same meaning:
>
> - **One idea per sentence, and no more than about 25 words.** A sentence carrying three
>   cross-references is two sentences and a list.
> - **Active voice with a named actor.** *"The factory disposes the scope"*, not *"the scope is
>   disposed"*.
> - **No ambiguous `this`, `it` or `that`.** Repeat the noun. A reader should never have to search
>   backwards for a referent.
> - **One term per concept, every time.** Do not vary the wording for elegance; vary it only when
>   the thing itself differs.
> - **No noun stacks.** *"the pipeline scope handle release ordering rule"* is a sentence pretending
>   to be a phrase.
> - **State the decision, not the argument that reached it.** *"Raising those five was rejected"*
>   records a deliberation; the ADR records what is true. Rejected options belong in
>   `## Alternatives Considered`, with their reasons.

**D4 · Widen the *Concentrate the citations* bullet (`:105-106`)** from `file:line` to requirement
IDs, which is the general form of the AC-33 example:

> - **Concentrate the citations — `file:line` and requirement IDs alike.** Both are load-bearing for
>   a reviewer cross-checking coverage and pure noise inside an argument. State the design point in
>   prose, then list the FRs and ACs it satisfies as bullets beneath it. A reader following the
>   design skims the list; a reviewer checking coverage reads only the list. Never thread three
>   requirement IDs through one sentence. At most one `file:line` per forces or Consequences bullet.
>   **Prefer a slightly longer document to a terse one:** a paragraph plus a list is more readable
>   than one dense sentence, and it serves both readers instead of neither.

**D5 · New bullet in `### ADR readability` — emphasis.**

> - **Emphasis is a symptom.** Bold marks the one sentence a section turns on; the Decision's single
>   bold sentence is the model. If a paragraph needs bold to make its point findable, the paragraph
>   is wrong — split it, or lead with the point. A section with bold in every paragraph has
>   emphasis in none of them.

**D6 · New row in the diagram-form table (`:120-125`), plus an encouragement after it.**

> \| the types a decision introduces, and how they relate — implements, holds, creates \| `classDiagram` \|
>
> Reach for a diagram sooner than feels necessary. If a paragraph needs three cross-references
> before it makes sense, draw it: a class diagram for how types relate, a sequence diagram for who
> calls whom and in what order.

**D7 · New check under `### Before an ADR is committed` (`:127`).**

> **Re-read the whole document.** Correcting statements one at a time produces a document that
> contradicts itself: a fix lands, and a sentence three sections away that depended on the old
> wording is now false, redundant, or an argument for something no longer in the ADR. After any
> round of statement-level edits, read the ADR start to finish and fix what the fixes broke. This is
> a separate pass from the edits themselves, and it is where near-duplicate paragraphs, stale
> summaries and orphaned rationale are found.

### `.agent_instructions/design_principles.md`

**P1 · Extend *Objects have roles* (`:14-15`)** — the vocabulary is already right, the useful part
is missing:

> - Objects have roles.
>     - A role is the collection of an object's responsibilities, expressed as one phrase saying
>       what the object does.
>     - If you cannot express it in one phrase, the object has too many responsibilities.
>       Responsibility-driven design restates the single responsibility principle as the **single
>       role principle**.
>     - An object may hold several responsibilities, and they may be of different kinds — knowing,
>       doing and deciding.
>     - Common roles are stereotypes: information holder, structurer, service provider, coordinator,
>       controller, interfacer.
>     - Name an object's **collaborators** — the objects it works with to meet its responsibilities.
>       Describing a role without them is half the picture, and a responsibility with no
>       collaborator is either self-contained or in the wrong object.

### What Phase 1 does not do

It does not touch an ADR. **D2's consequence is a rename in eight ADRs** — our seven plus
`0060-multi-tenancy-migration-history-scope`, which is approved and merged. The seven take it in
Phase 3. Whether 0060 is corrected, and under what commit, is an open decision (section 7).

## 6. Phase 3 — the instrument a whole-document rewrite needs

Diff-reading verifies a patch. It cannot verify a rewritten document, because every line has moved.
So each Phase 3 session brackets its rewrite with a mechanical **claim inventory**:

1. Before the rewrite, extract from the ADR, as sorted sets:
   - every requirement token — `FR-n`, `FR-n.m`, `NFR-n`, `AC-n`, `C-n`, `D-n`, `OOS-n`
   - every `file:line` citation
   - every sibling-ADR reference
   - every type, member and namespace name in backticks
   - every numeral used as a count, with the noun it counts
2. Rewrite.
3. Extract again and diff the sets.
4. **Every token that disappears must be deliberate and listed in the commit message.** A token that
   disappears silently is a fact lost.

Additions are expected and need no ceremony; deletions are the risk. This is the only way
*"no fact changed"* is a checkable claim rather than an assertion, and it is cheap — six greps.

Each Phase 3 session also keeps the §19.7 three-branch discipline, which is now the general rule
rather than a rule about the `Scope` section:

1. **The fact is fine and only its position or wording changes** → rewrite it. The ordinary case.
2. **The rewrite cannot preserve the fact without distorting it** → keep the longer form for that
   sentence and say why in the commit message. A readable ADR that is wrong is worse than an
   unreadable one that is right.
3. **The fact looks wrong, unsupported or contradicted once isolated** → stop. It becomes an owner
   call and lands in its own commit, never inside the rewrite.

Report the branch-2 and branch-3 list with every session, **even when it is empty**.

## 7. Decisions still owed

| # | Decision | Recommendation |
| --- | --- | --- |
| 1 | Rewrite all seven, or rewrite the worst three whole (0072, 0071, 0070) and apply targeted fixes to the rest? | all seven — the house style has to be uniform across a set that is read in order, and 0074 is the second-largest file |
| 2 | Does approved-and-merged `0060` get D2's column rename? | leave it; note the divergence in the commit message. It is a record, not a live document |
| 3 | S4 — is *"promote the exception to public"* your decision already, or a call to raise? | it is `design_principles.md:22-28` applied. `:23` puts a type on the boundary if it is used or tested from outside; `:27` prefers a public type with an internal constructor to an internal type. The exception is what a caller catches, so it belongs on the boundary — I read this as settled, not owed |
| 4 | ✅ **ANSWERED — `d6502deb5`.** **S1+S2** — does the pump-flow bracket survive once its stated scenario is a usage error, given `0072:112` already says a consumer pipeline never adopts? | **KEEP the bracket, RE-GROUND its justification.** No mechanical change; the usage error is named as one and is not defended as a configuration. The three grounds that survive: a mixed host asks `JoinAmbient` with nobody choosing it for consumers (one `ConsumersOptions` instance, 0076's residue); a pump inherits its start flow and there is more than one start site, so the guarantee cannot be a property of the start site; NFR-7 — a provider Brighter has never heard of need not key on `HttpContext`. ⚠ **The control plane's `ConfigurationCommandHandler` is NOT a legitimate live-request start** (owner's correction while the call was open) — it is the API of a process that already hosts a `Dispatcher` |
| 6 | ✅ **ANSWERED — `bd44be1ed`.** **S3** — is the no-handle dictionary path speculative code to drop, given the ADR concedes Brighter's own paths never take it? | **DROP IT**, on the owner's principle: ***tests that exercise non-production paths lack value, and we should re-write to exercise the production path***. `_lifetimeScopes`, `GetOrCreateLifetimeScope` and `ReleaseLifetimeScope` go; a non-`Singleton` `Create` with no usable handle throws `ConfigurationException`; both `Release` overloads keep their signatures and lose their bodies. ⚠ **The item's premise was half wrong and it did not change the answer**: *"if no path calls it"* is true of `src/` and false of `tests/` — six files, 26 facts, 21 on the fallback, and **AC-14 names two and requires them unchanged**. Those 21 become tests of a dead path, so they move; §19.9 row 1 is amended. The principle is recorded repo-wide in `.agent_instructions/testing.md` by `9fcfa2856`, a **separate** commit under the four-bucket rule |
| 7 | ✅ **ANSWERED — `bbb04d688`.** **S5** — can the rules be `ISpecification<T>` families the core validator pulls in, via an `IAmAValidationProvider` discovered by reflection? | **The rules already travel; the ENTITY TYPE cannot.** `Paramore.Brighter.ServiceActivator` ships four `ISpecification<Subscription>` rules that `ValidatePipelines()` harvests with `GetServices` into `PipelineValidator`'s `consumerSpecs` parameter — so contributing rules across assemblies is a solved problem. What blocks it here is that core *declares the collection*, `ISpecification<TData>` admits no variance and has no non-generic base, and every `T` in the repo is a core type; this ADR's entities carry `ServiceLifetime` and cannot be. ⚠ **So `:367`'s rejection was RIGHT but argued the wrong thing.** The owner's call: **take Alternative 1** — the pull moves up from specifications to **validators**. `ScopeConfigurationValidator` is registered alongside the core one, both hosts resolve `IEnumerable<IAmAPipelineValidator>` and `Combine`. Chosen on **open/closed**: a decorator makes every later contributor wrap the last |
| 5 | S6 — is the borrowed-scope registration question an ADR fix or a new requirement? | raise as a call. `0072:480` covers only *detection*, not whether resolution works, so nothing in the set answers it — that reads as a requirement, and requirements changes go to the §18.8 true-up, not into an ADR |

**Phase 2 is therefore four calls: S1+S2, S3, S5, S6** — with S4 confirmed against
`design_principles.md:22-28` rather than argued. ✅ **S1+S2 is DONE (`d6502deb5`), S3 is DONE
(`bd44be1ed`, plus `9fcfa2856` for the principle) and S5 is DONE (`bbb04d688`); ONE remains — S6.**

## 8. Tracking

`PROMPT.md` **§20** is the live tracker: phase status, what each session landed, and the running
branch-2 / branch-3 list. `PROMPT.md`'s resume block points at §20 rather than at §19.7, whose
`## Context` restructure is now Phase 1's D1 plus Phase 3's per-ADR work.
