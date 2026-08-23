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
| `Scope` should lead with Defect 1b, `IAmAScope` and FR-13 as the core | R | ✅ **CLOSED, `fb27c81bb`** — the `### Scope` lead now names all three as the core in terms, ahead of the In-scope list |
| *"How the set treats non-functional requirements…"* → PROMPT.md | M | ✅ **CLOSED, `fb27c81bb`** — REMOVED, and it needed no owner call, unlike 0071's M item: a grep over the other six found no citation of it. The concrete facts it carried stay (C-19 and C-8's disposal half are Scope bullets), and the five tokens that went with it — AC-22, FR-15, FR-17, NFR-2, NFR-9 — were each verified as carried by their owning ADR |
| NFR-8 `IAmALifetime` distinction buried in an NFR note | R | ✅ **CLOSED, `fb27c81bb`** — promoted into its own bolded question in *Technology Choices*, with the three reasons as bullets; the forces bullet keeps NFR-8 and points at it |
| *"the single internal exception"* — promote the exception to public | **S4** | ✅ **ANSWERED — NO DESIGN CHANGE, so no commit of its own; the prose change rode `fb27c81bb`.** ⚠ **§7 row 3's justification MISREAD the quote** — *"the single internal exception"* is the one class in the DI package that is not public, not an exception type, so *"the exception is what a caller catches"* answers a different question. The owner ruled the decision stands: nothing outside the package consumes `ServiceProviderLifetimeScope`, no test names it, the solution has no `InternalsVisibleTo`, and `0074:187` already ruled the identical CS0051 question the same way. Only the ARGUMENT moved, to alternative 10 |
| *"What this cache does and does not give"* → belongs in Decision | R | ✅ **CLOSED, `fb27c81bb`** — moved under `## Decision` as `### What one scope per pipeline gives, and what it does not`. No sibling cites the old heading; checked before renaming |
| *"surfaces its inner disposal failure"* — needs a sequence diagram | R | ✅ **CLOSED, `fb27c81bb`** — a `sequenceDiagram` in step 4b showing the surfacing path against the terminal-teardown swallow it does not inherit, plus the 400-word paragraph split into four |
| argument-as-record, not decision (the *"Raising those … was rejected"* pattern) | R | ⚠ **`0070:335` and `0070:407` CLOSED in `fb27c81bb`**; **`0074:401` CLOSED in `e3ed130b0`** — the *"The alternative … was rejected"* paragraph in step 5a now states the decision, and its rejection is alternative 12. ⚠ **The `0074` anchor had DRIFTED to `:421`** — S5 (`bbb04d688`) rewrote the file after Phase 0 located it. **Originally 9 lines across six ADRs**: `0070:335`, `0070:407`, `0071:365`, `0073:209`, `0073:221`, `0073:244`, `0074:401`, `0076:263`, `0076:345`. ⚠ **`0076:263` and `0076:345` CLOSED in `cc2b8b216`** — the `InternalsVisibleTo` rejection and the *"was considered and is wrong twice over"* paragraph both became numbered alternatives (8 and 9), leaving decision statements that point at them. ✅ **`0073:209`, `0073:221` and `0073:244` CLOSED in `e1a15733c`** — the visibility argument, the `TryGetAmbient` rejection and the namespace-candidates paragraph became alternatives 9, 8 and a pointer to 5 and 6. **One remains, in `0071`** |
| move more argument into `## Alternatives Considered` bullets | R | ✅ **CLOSED for `0073`, `e1a15733c`** — four arguments become numbered rejections, **APPENDED as 8 to 11**, because `review-design.md:1930` cites *"0073's alternatives 4, 5 and 6"* by number and `0074:707` pins *"ADR 0073 step 5"*. ⚠ **The pin was found only on a second sweep: the plural *"alternatives 4, 5 and 6"* defeated a grep anchored on the singular.** ✅ **CLOSED for `0070`, `fb27c81bb`** — FOUR alternatives added, **10 to 13**, all APPENDED so `0072`'s citation of *"ADR 0070's Alternative 2"* still resolves. ✅ **CLOSED for `0074`, `e3ed130b0`** — the two alternatives the round-6 finding said were *"hidden in the gap between 3 and 2"* are now numbered and each rejected, plus two more lifted out of prose. **APPENDED as 9 to 12**, because round 6's record cites *"0074 alternative 5"* by number. ✅ **CLOSED for `0076`, `cc2b8b216`** — four arguments become numbered rejections, **APPENDED as 8 to 11**: `review-design.md` cites *"0076 alternative 6"*, *"0076's alternatives 3, 4 and 7"* and *"0076's own alternative 2"* by number, and `0074:393`/`0074:707` pin the *step* numbers as well, so neither sequence was renumbered. ✅ **CLOSED for `0075`, `992537bcc`** — four arguments carried in the body become numbered rejections, **APPENDED as 6 to 9**: `review-design.md` cites *"0075 alternative 3a"* four times, *"alternative 5"* three times and *"its alternative 4"*, and **`0073:84` cites *"ADR 0075's third alternative"***, so 1–5 and 3a keep their numbers |
| *"FR-13 divides by family rather than by clause"* | R | ✅ **`0071:30` CLOSED in `3537c68cd`** — restated in the reviewer's own words. ✅ **`0070:32` and `0070:34` CLOSED in `fb27c81bb`** — restated to match 0071's wording, so the row is now fully discharged across both ADRs |
| *"`Transient` is not only `Scoped`'s poor relation"* — wants a diagram | R | ✅ **CLOSED, `3537c68cd`** — a `flowchart` with one subgraph per configured lifetime, under a new `#### What a Transient handler pipeline gets` |
| NFR-4 — what the `ConcurrentDictionary` buys; convention over restriction | R | ✅ **CLOSED, `3537c68cd`** — promoted out of a 224-word forces bullet into its own `#### What replaces the dictionary's atomicity`, as the review asked |
| *"The member's **shape** is ADR 0070's"* — unreadable | R | ✅ **CLOSED, `3537c68cd`** — split into what transfers from 0070 and what does not |
| *"is not asserted over this property"* — hard to read | R | ✅ **CLOSED, `3537c68cd`** — contract cell cut to two sentences |
| *"is not asserted over this property"* — tracking, not a decision → PROMPT.md | M | ✅ **SETTLED BY OWNER RULING, `3537c68cd`** — **SPLIT**, not relocated: deleting it outright would have falsified `0072:161` and orphaned §19.9 row 4's anchors. Design facts stay in the cell; the amendment record moves beneath the table. Both ends in one commit |
| *"ignored, not rejected"* — flagged three times | R | ✅ **CLOSED AT BOTH ENDS** — `0072:238` in `f30358c5e`, `0071:237` in `3537c68cd`. ⚠ **They are NOT twins and were NOT harmonised**; each now names the other as deciding a different question about a different object |
| *"The dictionary survives as the no-handle path"* — speculative | **S3** | ✅ **ANSWERED AND LANDED — `bd44be1ed`.** Raised at `0071:280` (anchor exact); **fourteen sites moved, in FOUR ADRs** — 0071 (13), `0072:494`, `0073:287` and 0070 step 7a's ledger count, plus `docs/adr/index.md` |
| *"AC-33 is that rule's regression guard"* — the cross-reference pattern | R | ✅ **CLOSED, `3537c68cd`** — the review's worked example. Now: the rule in prose, then a three-bullet list of criteria, AC-7 included as the one that is *not* the criterion |
| *"a re-reading of FR-27.1's own words"* — argument, not decision | R | `0072:104` |
| *"Two kinds of flow reach that line"* — argument, not assertion | R | `0072:112` |
| `ScopeAffinityPolicy`'s role is unclear | R | `0072:131`, `:167`, `:242`, `:247`, `:249`, `:332`, `:336` (**19 mentions across the set**) |
| *"`AmbientScopeProbe` — what the probe is…"* — hard to parse | R | `0072:410` |
| consumer transform/mapper pipelines are never ambient | **S2** | **nowhere — see below.** The nearest statement is `0072:112` |
| a `Dispatcher` is never started from inside a live request | **S1** | `0075:286`, `:326`, `:349` |
| *"Why the rules are not `ISpecification<T>` families"* | **S5** | ✅ **ANSWERED AND LANDED — `bbb04d688`.** All seven anchors — `0074:140`, `:249`, `:367`, `:379`, `:388`, `:389`, `:502` — were verified exact and on-subject, the first row in the programme whose anchors had not drifted at all. The answer moved far more than them: **0074 throughout plus `0070:405` and the index** |
| registration must resolve from a borrowed ambient scope | **S6** | ✅ **ANSWERED AND LANDED — `fa937a739`.** `0072:480` was exact, but **"the only line in the set" was WRONG** — `0072:410`, `:416` and `:420` all bear on it and `:416` largely answers it. The answer moved `0072` (two new passages + a Positive bullet + References) **and** `requirements.md` (FR-16(c), C-21, AC-52, revision 21) |

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

### S6 has almost no footing in the set — ⚠ **THIS SECTION'S PREMISE WAS FALSIFIED; see §7 row 5**

`0072:480` is the only line that touches it: *"A provider that passes both tests and still cannot
resolve Brighter's artefacts fails loudly, not quietly … `Create` returns `null`, and the builder's
existing guard raises `ConfigurationException` (`PipelineBuilder.cs:193`)."* That is a **detection**
claim — it says the failure is loud. The owner's question is a **design** one: will Brighter's
registered services, many of them factory functions, still resolve from a scope owned by a
non-Brighter parent? Nothing in the set answers it, which is why it reads as a requirement rather
than an ADR fix.

⚠ **BOTH SENTENCES ABOVE ARE WRONG AND ARE KEPT FOR THE LESSON.** `0072:480` is *not* the only line
that touches the item: `0072:416` answers the design question outright — Brighter's registrations
went into the same `IServiceCollection` the borrowed scope's container was populated from — and
`0072:410` and `:420` bear on it too. **A Phase 0 row saying "nothing in the set answers this" is a
hypothesis, and the cheapest way to test it is to go looking for the answer under words the item
does not use.** The item's own words — *registration*, *factory function*, *borrowed* — appear
nowhere near `:416`, which discusses **container provenance**. What S6 actually exposed was a
different gap entirely, in territory the item never named: the **transaction** consequence. See §7
row 5.

## 4. The phases, and why in this order

| Phase | Work | Sessions |
| --- | --- | --- |
| **0** | locate every item against current source; complete the table in section 3 | ✅ **done** |
| **1** | the ten house-style rules into the two instruction files — **no ADR touched** | 1 |
| **2** | the substantive calls, one at a time, each landed in its own commit — **four calls, not six**: S1+S2 merged, S4 settled by principle | ~4 |
| **3** | the rewrite, **one ADR per session**, worst-first: 0072, 0071, 0070, then 0074, 0075, 0076, 0073 | ✅ **COMPLETE — 7 of 7.** ✅ **0072 done, `f30358c5e`**; ✅ **0071 done, `3537c68cd`**; ✅ **0070 done, `fb27c81bb`**; ✅ **0074 done, `e3ed130b0`**; ✅ **0075 done, `992537bcc`** (preceded by `0583c5af8`, the X1 sweep); ✅ **0076 done, `cc2b8b216`** (preceded by `9bcbeb6a8`, the branch-3 summary correction); ✅ **0073 done, `e1a15733c`** — the last, and the only session owing **no** commit ahead of it and **no** `index.md` diff |
| **4** | design review round 7, grading against the new house style — ⚠ **preceded by D8 (§5a), so the round's fixes cannot undo Phase 3** | per §19's shape |

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

## 5a. D8 — an eleventh rule, added after Phase 1 and ahead of round 7

⚠ **This rule was not in Phase 1. The owner added it when Phase 3 closed**, on the ground that the
programme protects nothing if round 7's fixes undo it. It is bucket **H** and it landed the way
D1–D7 did: the instruction file only, no ADR touched.

**Why it is needed now, and not before.** Item 15 of the review says statement-level correction is
what produced the prose being objected to. Phase 3 fixed the *documents*; it did not fix the
*method*, and round 7 is the method's next run. Eight reviewers will return on the order of a
hundred findings, and almost every one of them can be closed by adding a qualifier, a
cross-reference or a bolded caveat to a sentence that is otherwise fine. Each such fix is small and
locally correct. A hundred of them is 0072's 498-word `Scope` paragraph again.

**D8 · New subsection `### Correcting an ADR`, between `### ADR readability` and
`### Diagrams in ADRs`.** Six rules plus a measurement:

> - **Replace, do not append.** A sentence that over-claims is answered by rewriting that sentence,
>   not by adding the qualifier that makes it true.
> - **Every qualifier a fix adds is a claim**, and it can contradict a section the fix never looked
>   at. Prefer the narrow statement to the broad statement plus its exception.
> - **Correct the source, not only the statement derived from it.**
> - **A fix that needs bold to be found is in the wrong place.**
> - **The finding's argument is not the fix.** A rejected reading belongs in
>   `## Alternatives Considered`, not as a warning inside the prose.
> - **When a finding can only be closed by making the document worse, stop and say so.**
>
> **Measure a batch of corrections, not only a rewrite** — blocks over 150 words, blocks over 200
> words, and bold runs in prose and at bullet leads, before and after. A batch that raises any of
> them owes an explanation in its commit message.

**Three of the six are Phase 3's own lessons turned around to face a fix rather than a rewrite.**
*Every qualifier a fix adds is a claim* is session 7's, where the rewrite's added *"without
discharging any of them"* contradicted the same ADR's *"that is the whole of NFR-2"* three sections
away. *Correct the source, not the derived statement* is session 6's, where the frontmatter summary
and the sentence it compressed were one defect at two distances. *A fix that needs bold* is D5 —
emphasis is a symptom — applied at the moment the emphasis gets added.

**The measurement is the part that makes it checkable rather than asserted**, in the same way the
claim inventory made *"no fact changed"* checkable. Phase 3 reported those counts for every session;
round 7's fix batches now report them too.

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
| 3 | ✅ **ANSWERED — NO DESIGN CHANGE (`fb27c81bb` carries only the prose).** S4 — is *"promote the exception to public"* your decision already, or a call to raise? | ⚠ **THE RECOMMENDATION BELOW WAS WRONG ABOUT WHAT THE ITEM REFERS TO, AND IS KEPT FOR THE LESSON.** *"The single internal exception"* is the one CLASS in the DI package that is not `public` — `ServiceProviderLifetimeScope` — not an exception type, so *"the exception is what a caller catches"* answers a question the review never asked. **What the review actually asks is whether that class should be widened so `ServiceProviderPipelineScope`'s constructor need not be `internal`. The owner ruled it should not**: nothing outside the DI package consumes it (its callers are the five factories in its own assembly), no test names it, the solution contains no `InternalsVisibleTo`, `design_principles.md` makes `internal` correct on exactly that test and prefers a public type with an internal constructor, and **`0074:187` already ruled the identical CS0051 question the same way**. The decision stands and only the ARGUMENT moved, into alternative 10. **Original recommendation, falsified:** *it is `design_principles.md:22-28` applied… the exception is what a caller catches, so it belongs on the boundary — I read this as settled, not owed* |
| 4 | ✅ **ANSWERED — `d6502deb5`.** **S1+S2** — does the pump-flow bracket survive once its stated scenario is a usage error, given `0072:112` already says a consumer pipeline never adopts? | **KEEP the bracket, RE-GROUND its justification.** No mechanical change; the usage error is named as one and is not defended as a configuration. The three grounds that survive: a mixed host asks `JoinAmbient` with nobody choosing it for consumers (one `ConsumersOptions` instance, 0076's residue); a pump inherits its start flow and there is more than one start site, so the guarantee cannot be a property of the start site; NFR-7 — a provider Brighter has never heard of need not key on `HttpContext`. ⚠ **The control plane's `ConfigurationCommandHandler` is NOT a legitimate live-request start** (owner's correction while the call was open) — it is the API of a process that already hosts a `Dispatcher` |
| 6 | ✅ **ANSWERED — `bd44be1ed`.** **S3** — is the no-handle dictionary path speculative code to drop, given the ADR concedes Brighter's own paths never take it? | **DROP IT**, on the owner's principle: ***tests that exercise non-production paths lack value, and we should re-write to exercise the production path***. `_lifetimeScopes`, `GetOrCreateLifetimeScope` and `ReleaseLifetimeScope` go; a non-`Singleton` `Create` with no usable handle throws `ConfigurationException`; both `Release` overloads keep their signatures and lose their bodies. ⚠ **The item's premise was half wrong and it did not change the answer**: *"if no path calls it"* is true of `src/` and false of `tests/` — six files, 26 facts, 21 on the fallback, and **AC-14 names two and requires them unchanged**. Those 21 become tests of a dead path, so they move; §19.9 row 1 is amended. The principle is recorded repo-wide in `.agent_instructions/testing.md` by `9fcfa2856`, a **separate** commit under the four-bucket rule |
| 7 | ✅ **ANSWERED — `bbb04d688`.** **S5** — can the rules be `ISpecification<T>` families the core validator pulls in, via an `IAmAValidationProvider` discovered by reflection? | **The rules already travel; the ENTITY TYPE cannot.** `Paramore.Brighter.ServiceActivator` ships four `ISpecification<Subscription>` rules that `ValidatePipelines()` harvests with `GetServices` into `PipelineValidator`'s `consumerSpecs` parameter — so contributing rules across assemblies is a solved problem. What blocks it here is that core *declares the collection*, `ISpecification<TData>` admits no variance and has no non-generic base, and every `T` in the repo is a core type; this ADR's entities carry `ServiceLifetime` and cannot be. ⚠ **So `:367`'s rejection was RIGHT but argued the wrong thing.** The owner's call: **take Alternative 1** — the pull moves up from specifications to **validators**. `ScopeConfigurationValidator` is registered alongside the core one, both hosts resolve `IEnumerable<IAmAPipelineValidator>` and `Combine`. Chosen on **open/closed**: a decorator makes every later contributor wrap the last |
| 5 | S6 — is the borrowed-scope registration question an ADR fix or a new requirement? | raise as a call. `0072:480` covers only *detection*, not whether resolution works, so nothing in the set answers it — that reads as a requirement, and requirements changes go to the §18.8 true-up, not into an ADR |

**Phase 2 is therefore four calls: S1+S2, S3, S5, S6** — with S4 confirmed against
`design_principles.md:22-28` rather than argued. ✅ **ALL FOUR ARE DONE: S1+S2 `d6502deb5`, S3
`bd44be1ed` (plus `9fcfa2856` for the principle), S5 `bbb04d688` and S6 `fa937a739` (preceded by the
`6883f589f` bookkeeping commit). PHASE 2 IS CLOSED.**

✅ **Phase 3, session 6: `0076` is rewritten — `cc2b8b216`** — preceded by `9bcbeb6a8`, a **branch-3 correction the owner ruled on**: the frontmatter `summary` said the options object does not exist yet on **two** of the four registration paths, where `## Context` and *The mechanism, end to end* both say **three** and source agrees with the body. `### Scope` became a statement of scope, Key Components took D2's rename and P1's `Collaborators` column, and a fourth diagram was added — a `classDiagram` carrying the two inheritance facts the prose had made a reader assemble. A new table replaces the 200-word "what defeats the write-through" paragraph with two placements by five columns. Alternatives APPENDED as **8 to 11**. Blocks over 200 words 4 → 0, over 150 words 13 → 3.
⚠ **MID-PROSE BOLD ROSE, 86 → 93, AND IT IS THE FIRST SESSION TO REPORT A RISE.** Bullet-lead bold rose 74 → 95, so emphasis did move into structure — but four new alternatives each carry a `**Rejected...**` run and two new tables carry cell-lead bold, and that is the honest account rather than a win. The one decorative block was removed: an eleven-token *"It serves"* requirement list that had been bolded token by token.
⚠ **THE NUMERALS GREP FOUND THE SESSION'S ONLY LOSSES FOR THE FIFTH TIME IN SIX — THREE OF THEM, AND ALL THREE WERE RESTORED RATHER THAN ACCEPTED.** *"and the same `GetService` in the other four"* (one citation standing for five identical call sites), *"fails silently and totally on three of the four"* (FR-17's own phrase), and ADR 0072's machinery in one sentence. **A compressed `## Context` is where counts go to die: all three losses came from the same two paragraphs.**
⚠ **THE BRANCH-3 ITEM AND A RE-READ FINDING WERE THE SAME DEFECT AT TWO DISTANCES.** The summary's wrong count was a lossy compression of a *Technology Choices* sentence that says the object *"does not exist yet"* on two paths and that *"on the fourth the `IOptions` pipeline produces it"* — which reads as though the fourth had it at registration time. Correcting the summary alone would have left the generator in place, so the re-read restated the source sentence too. **When a derived statement is wrong, go and read what it was derived from.**
⚠ **A COUNT I INTRODUCED WAS WRONG WITHIN THREE PARAGRAPHS OF WRITING IT.** Making an ambiguous *"alternative 2 records both"* explicit produced *"the three costs"*, and alternative 2 lists four. **Every numeral a rewrite adds is a claim about the document it is being added to, and the re-read is the only thing that checks it.**

✅ **Phase 3, session 5: `0075` is rewritten — `992537bcc`** — preceded by `0583c5af8`, the **X1 sweep**, which is not a branch-3 item but the last owed non-rewrite item in the programme: all seven sibling lists and all seven *Where this ADR sits* tables described 0075 as suppression *"for a `Publish` subscriber"* only, and the pump bracket had made that incomplete. Thirteen lines, seven files, one commit. `### Scope` became a statement of scope, the Key Components table took D2's rename and P1's `Collaborators` column, and a third diagram was added — the pump-flow `sequenceDiagram` the first diagram had said in terms was *"not drawn here"*. Blocks over 200 words 6 → 0; mid-prose bold 116 → 113 while bullet-lead bold rose 73 → 101.
⚠ **THE FIRST REWRITE THAT OWED NO `index.md` REGENERATION, AND IT WAS CHECKED RATHER THAN ASSUMED.** The four before it all changed their frontmatter `summary`; this one did not, and the index was verified to carry the summary verbatim already. **A frontmatter edit owes the index diff — the absence of one owes the check.**
⚠ **THE NUMERALS GREP FOUND THE SESSION'S ONLY LOSS FOR THE FOURTH TIME IN FIVE**, with the other five greps completely clean: an analogy sentence — *"in the same shape ADR 0074 owns FR-25 while this ADR supplies two of its clauses"*. Deliberate, because the new Scope states both arrangements as adjacent bullets under one heading. ⚠ **But the rule that made it safe is session 3's: read what the passage was DOING with the token.** The *count* it carried survives in step 7 and in `0074:48`, and that was verified rather than assumed.
⚠ **EVERY CITATION WAS EXACT — 19 file-qualified and 18 bare — INCLUDING THE ONES A RECENT COMMIT WROTE.** S1+S2 (`d6502deb5`) had edited this file, and session 4's lesson says to open what a recent commit wrote. Opened; clean. **A clean result is a result, and it does not retire the check.**
⚠ **THE RE-READ CAUGHT A POINTER NO GREP CAN SEE, AND IT IS SESSION 4's DEFECT IN A NEW SHAPE.** A bullet said its subject was *"the next paragraph"*; restructuring put a second bullet in between, so the pointer aimed at the wrong block. Session 4's version was a promoted heading orphaning the paragraph beneath it. **Both are relative pointers falsified by a structural edit — so after restructuring, re-read every *"next"*, *"above"* and *"below"*.**

✅ **Phase 3, session 3: `0070` is rewritten — `fb27c81bb`.** The first Phase 3 session with **no branch-3 commit ahead of it**, because its one bucket-S item resolved to no design change. Seven R items and the M item closed. Two diagrams added — a `classDiagram` for the handle hierarchy and a `sequenceDiagram` for step 4b's surfacing disposal path, which is the review's own request. Paragraphs over 200 words 10 → 0 (the worst was 417); inline bold 233 → 152 while list-lead bold rose 54 → 78. `index.md` regenerated in the same commit for a rewritten `summary`.
⚠ **THE NUMERALS GREP EARNED ITS KEEP A THIRD TIME, AND AGAIN NOTHING ELSE REPORTED THE LOSS**: the S4 trim dropped *"the **two** type tests that do name the class — step 6 and ADR 0071 step 4"*, a count plus a cross-ADR anchor. Bare citations and sibling references both diffed completely clean.
⚠ **A PHASE 0 BUCKET LABEL CAN BE RIGHT WHILE ITS JUSTIFICATION IS WRONG.** S4 really was substantive enough to raise, and the answer really was "settled" — but §7 row 3's reason for saying so had misread *exception* as an exception type. **Read the review's quoted sentence against the ADR before trusting the plan's gloss of it**, which is §20.6 row 3's lesson in a new place.
⚠ **AND THE M ITEM NEEDED NO OWNER CALL, WHERE 0071's DID.** The test that separated them is one grep: 0071's M item was cited by `0072:161` and anchored §19.9 row 4, so deleting it would have falsified a sibling; 0070's was cited by nobody. **Grep the set for a citation of the passage before deciding whether an M item is a deletion or a call.**

✅ **Phase 3, session 2: `0071` is rewritten — `3537c68cd`** — preceded by `a12e93f9a`, the branch-3 correction the owner ruled on (0072's cache-supply decline is ladder row 9, so *"the other two"*). Seven R items closed and the M item settled by an owner ruling: the FR-27.1/AC-46 amendment **splits**, design facts staying in the contract cell and the amendment record moving to a paragraph beneath the table. That reached `0072:161`, so both ends went in the one commit. Two diagrams added — a `flowchart` for how `Transient` scopes resolution (the review's own request) and a `classDiagram` for the type hierarchy. No paragraph now exceeds 200 words, where ten did; inline bold fell 195 → 166 while list-lead bold rose 25 → 59.
⚠ **The claim inventory earned its keep a second time, and in a new way: FOUR of the six greps came back COMPLETELY clean and the numerals grep still found a real loss** — the reciprocal of `0070:30`'s *"FR-7 is served here, not discharged here… its owning ADR is ADR 0071"*. **A clean run on the other five is not evidence.**
⚠ **`0070`'s session inherits one item from this one**: the *"FR-13 divides by family rather than by clause"* R row names `0070:32` and `0070:34` as well, and they were deliberately left alone.

✅ **Phase 3 has begun. `0072` is rewritten — `f30358c5e`** — preceded by a branch-3 correction in
its own commit, `dd83163e1` (ADR 0075 owns three brackets, not two; it reached 0074 as well). The
five R items Phase 0 filed against 0072 are closed, D1's `### Scope` heading and D2's column rename
are taken, and two diagrams were added — a `sequenceDiagram` for the hand-off and a `classDiagram`
for the type hierarchy. The claim inventory lost nothing undeliberate: three `FR-16a`/`b`/`c`
spellings normalised to the parenthesised form the requirements themselves use, and one numeral
neutralised. ⚠ **The inventory diff caught two REAL losses the writer had missed** — `ConsumersOptions`
and `AC-14` — **which is the argument for running it rather than trusting a careful read.**
✅ **§20.6 row 6 is RULED and CLOSED (`a12e93f9a`); row 7, raised and ruled in session 2, is CLOSED too. No branch-3 row is open.**

## 8. Tracking

`PROMPT.md` **§20** is the live tracker: phase status, what each session landed, and the running
branch-2 / branch-3 list. `PROMPT.md`'s resume block points at §20 rather than at §19.7, whose
`## Context` restructure is now Phase 1's D1 plus Phase 3's per-ADR work.
