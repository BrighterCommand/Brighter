# Review: design — 0036-scoped-lifetime-per-pipeline

**Date**: 2026-08-23
**Threshold**: 60
**Verdict**: NEEDS WORK

95 findings at or above threshold 60. Address these before approving.

**Round 7.** Eleven reviewers. **Eight blind** to `PROMPT.md`, to every earlier round's findings file
and to the readability review and plan — one per ADR (0070–0076) plus one whose only remit was
set-level properties — each with its own scratchpad subdirectory. Then **three gap-coverage runs**,
redirected onto the gaps the set-level reviewer declared: `requirements.md` reviewed as a document in
its own right (gap A), the set's decision tables checked row by row as executable specifications
(gap B), and the seven `Alternatives Considered` and `Consequences` sections read as one corpus
(gap C). Reviewed at HEAD `789a5bb05`.

This is the first round graded against a house style that exists. All seven ADRs were rewritten whole
against `.agent_instructions/documentation.md` before it ran, and D8 (`f7eb6483a`) added
`### Correcting an ADR` so that this round's *fixes* cannot undo that rewrite. Two amendments to the
round's shape follow from D8 and both were applied: every reviewer was required to supply a **fix
shape that does not cost readability**, and the set-level reviewer carried a **readability-count
remit** whose table is below.

## Counts

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | 0 |
| 70-89 (High) | 29 |
| 50-69 (Medium) | 100 |
| 0-49 (Low) | 30 |

**Total findings**: 159
**Findings at or above threshold (60)**: 95

Per reviewer — 0070: 14 (6) · 0071: 12 (5) · 0072: 13 (8) · 0073: 13 (9) · 0074: 12 (7) ·
0075: 13 (9) · 0076: 12 (7) · set-level: 18 (14) · gap A: 25 (13) · gap B: 13 (7) · gap C: 14 (10).

⚠ **Two reviewers' own at-or-above-threshold counts were wrong and are corrected here.** 0071
reported 8 and its scores give **5** (63, 60, 60, 60, 60; the next three are 58, 58, 57). 0072
reported 8 and its scores give 8 — correct. Every per-reviewer figure above was recomputed from the
scores rather than taken from the reviewer's header. **A reviewer's summary line is a claim like any
other; recount it.**

**The at-or-above-60 trend is 63 → 71 → 39 → 63 → 45 → 53 → 95.** It rose, and rose more than any
previous round. ⚠ **The rise is not like-for-like and must not be read as a quality collapse.**
Round 6 ran nine reviewers over the seven ADRs; round 7 ran eleven, and **three of them worked
remits no round has ever covered** — `requirements.md` had never been reviewed as a document, the
decision tables had never been enumerated, and the alternatives had never been read as a corpus.
Those three account for **30 of the 95**. Over the like-for-like eight, the count is **65** against
round 6's 53, which is still a rise.

## Where the findings landed

**The ADRs are in better shape than the count suggests, and the requirements are in worse shape than
anyone had established.** No Critical was filed by any reviewer. The two 80s that are genuine design
gaps rather than wording defects are named below; almost everything else is a false statement of fact
that a narrower sentence fixes.

- **The FR-19 mechanism split (three-way convergence, 80/80/68).** `0076:47` gives FR-19's mechanism
  as "the pump publishing no per-message ambient", which `0072:40` rejects **by name** as "not what
  makes this true and is not offered as the reason". 0072 and 0075 are the correct end — the
  mechanism is 0075's `Performer.Run()` bracket — so this is one clause in one ADR, not a
  three-ADR change.
- **Keyed `ServiceDescriptor`s (two-way convergence, 80/80).** ADR 0074 reads the registration
  collection in four places as "the last descriptor for that service type", and a probe shows MS DI
  resolves the last **unkeyed** one. FR-22.4 as written raises a startup-failing `Error` against a
  host that did nothing wrong — and **ADR 0076 already protects that host's registration**, spending
  a paragraph on the `ServiceKey` clause for exactly this reason. Nothing protects the rule. This is
  a design gap.
- **`requirements.md` had never been reviewed** and returned 25 findings, 13 at threshold — including
  a **five-site** `PipelineBuilder` citation drift, an NFR-1(b) enumeration that undercounts both its
  halves and omits an entire assembly, and **AC-20, which is unsatisfiable by a conforming
  implementation**.
- **Six of the ten NFRs are in no ADR's `In scope` list** (set #3). Not closable by editing a
  sentence.
- **The readability programme's own habits reasserted (five-way convergence).** Five reviewers
  independently filed mid-prose bold density against five different ADRs. **This is D8's premise
  confirmed by measurement**, and it is why the baseline below matters.

## Independent convergences

Nine, two of them three-way. The reviewers were blind to each other, and the three gap runs were
blind to the eight.

| Convergence | Reviewers |
| --- | --- |
| `0076:47` gives FR-19 the mechanism `0072:40` rejects by name | 0076 #1, set #1, 0072 #3 |
| Mid-prose bold density against the house style | 0071 #5, 0072 #8, 0075 #9, 0074 #9, 0070 #10 |
| Frontmatter summaries breach the one-or-two-sentence schema | set #7, 0073 #6, 0075 #8 |
| FR-19's two-entry log bound is stale under the delivered bracket | 0076 #5, set #1, gap A #3 |
| "Last descriptor wins" is false where a keyed descriptor exists | 0074 #1, gap B #1 |
| `0076`'s `InternalsVisibleTo` ground diverges from the rule four siblings state | 0076 #7, gap C #4 |
| The pseudo-code calls row 2's return `OWNED`, the word the ladder reserves | 0072 #7, gap B #6 |
| 0074's captive-dependency funnel is not exhaustive over its own failure modes | 0074 #4, gap B #3 |
| `requirements.md`'s `PipelineBuilder` build-loop citations have drifted | 0075 #10, gap A #1 |

## The readability baseline — D8 amendment 2

Counted blind by the set-level reviewer, with its script and its stated definitions. **This table is
what every round-7 fix batch measures against**, and a batch that raises any column owes an
explanation in its commit message.

| ADR | blocks | >150 words | >200 words | bold in prose | bold at bullet leads | diagrams |
|---|---:|---:|---:|---:|---:|---:|
| 0070 | 261 | 3 | 0 | 117 | 78 | 4 |
| 0071 | 210 | 5 | 0 | 138 | 59 | 5 |
| 0072 | 227 | 10 | 0 | 95 | 58 | 3 |
| 0073 | 164 | 2 | 0 | 128 | 53 | 3 |
| 0074 | 270 | 4 | 1 | 128 | 71 | 4 |
| 0075 | 180 | 1 | 0 | 138 | 49 | 3 |
| 0076 | 156 | 2 | 0 | 120 | 32 | 4 |
| **total** | **1468** | **27** | **1** | **864** | **400** | **26** |

Definitions, so the numbers reproduce: frontmatter, all fenced blocks including their fences, ATX
heading lines and table lines are stripped first; a **block** is a maximal run of lines bounded by a
blank line, split further at every list-item lead; a **bold run** is one non-greedy `\*\*(.+?)\*\*`
match; a **bullet lead** is the first bold run of a list-item block that begins with `**`, and every
other bold run counts as prose. The two bold columns are disjoint. Script:
`count_readability.py` in the set-level reviewer's scratchpad.

⚠ **The per-ADR reviewers' own counts differ from this table by definition, not by disagreement, and
they reconcile exactly.** 0071's reviewer reported 91 mid-prose bold runs against this table's 138 —
the difference is 47 paragraph-opening bolds, which that reviewer separated out and this script
counts as prose. 0072's two independent counts agree outright (95 prose bold; 10 blocks over 150
against the reviewer's 11, a single boundary case). **Use this table.** The per-ADR figures are
corroboration, not competing numbers.

The single block over 200 words is `0074`'s Alternative 1 ("A decorating validator"), at 207.

## Two artefacts this round produced that are worth as much as the findings

1. **The FR → ADR ownership table**, re-derived from scratch off today's `Scope` paragraphs (set-level
   section below). Every one of the 27 FRs is owned; FR-13, FR-15, FR-17 and FR-24.3 are split, and
   in each case both ends describe the split identically. **Six NFRs are owned by nobody.**
2. **The full enumeration of ADR 0072's decision ladder** over its real input space (gap B section
   below) — every combination of lifetime, suppression, affinity and provider behaviour mapped to the
   row that fires. Every combination lands on exactly one row. **Two land on outcomes the ADRs do not
   describe**, and both are findings.

## Ranked index — findings at or above threshold

| Score | Finding |
|---|---|
| 80 | **0074 #1** — keyed descriptors falsify "last descriptor wins"; FR-22.4 raises a false startup `Error` |
| 80 | **0076 #1** — Scope gives FR-19 the mechanism ADR 0072 rejects by name |
| 80 | **set #1** — the same FR-19 contradiction, found independently |
| 80 | **gap B #1** — the same keyed-descriptor gap, found independently, with three probes |
| 78 | **0072 #1** — the translated `ConfigurationException` *is* wrapped twice, on four of six catch sites |
| 78 | **gap A #1** — FR-9's build-loop citations drifted seven lines, repeated at four further sites |
| 78 | **gap C #1** — 0070's "No hidden state" *Positive* is falsified by 0075's three `AsyncLocal` brackets |
| 76 | **0074 #2** — three of four inputs the registration snippet passes do not exist at that point |
| 76 | **gap C #2** — 0072's "degrades on every failure path" is denied by its own ladder row 4 |
| 74 | **0070 #1** — the `Warning`-message ledger says eight; there are ten, two in a type this ADR changes |
| 74 | **0073 #1** — the `netstandard2.0` rejection rests on "end-of-life 2.2.x"; a serviced 2.3.x line builds clean |
| 74 | **gap A #2** — NFR-1(b) undercounts both halves and omits `Paramore.Brighter.ServiceActivator` |
| 74 | **gap C #3** — 0073 alternative 9's fourth ground is false of ADR 0070, which says the opposite |
| 72 | **0070 #2** — nothing says how the scope reaches `TransformPipelineDrain`; the table denies the change |
| 72 | **0074 #3** — FR-22.3's rule is given no collaborator that can supply the lifetime it evaluates |
| 72 | **0075 #1** — the mixed-host mechanism contradicts C-12 |
| 72 | **0075 #2** — title, slug and the Decision sentence all exclude the consumer-pump bracket |
| 72 | **set #2** — AC-45 named as FR-15's guard; AC-45 asserts nothing about the default |
| 72 | **gap C #4** — the `InternalsVisibleTo` rule stated nine ways; 0076 alone treats it as available |
| 70 | **0070 #3** — "every non-container implementation returns `null`" excludes `MessageMapperRegistry` |
| 70 | **0072 #2** — the Decision's central bold property is false of ladder rows 1, 2 and 4 |
| 70 | **0073 #2** — the new test project is put on TFMs no test project in the repository uses |
| 70 | **0075 #3** — "three files, three assemblies"; the five brackets sit in two |
| 70 | **set #3** — six of the ten NFRs are in no ADR's `In scope` list |
| 70 | **gap A #3** — FR-19/AC-20/C-14: AC-20 is unsatisfiable by a conforming implementation |
| 70 | **gap A #4** — AC-52's negative control turns into a second positive under D18 |
| 70 | **gap B #2** — 0075's NFR-9 row family is false for `Singleton` and `Transient` |
| 70 | **gap C #5** — 0073 alternative 9's third ground is stale against 0074 in the same release |
| 70 | **gap C #6** — six rejected alternatives are argued in `## Consequences` |
| 68 | **0070 #4** — the `Create` contract row promises a fallback step 9 abolishes |
| 68 | **0072 #3** — ADR 0076 gives a different mechanism for FR-19 and cites 0072 for it |
| 68 | **0075 #4** — "no bracket is ever established outside a publish", contradicted three paragraphs later |
| 68 | **0076 #2** — "step 7a enumerates the whole entry" describes five of its thirteen items |
| 68 | **set #4** — FR-10 names three types; the string `FR-10` appears in neither 0070 nor 0071 |
| 68 | **set #5** — 0072 claims NFR-8 though it declares neither seam interface |
| 68 | **gap A #5** — `PipelineBuilder.cs:528`/`:539` cited twice for `HandlerLifetimeScope` creation |
| 68 | **gap A #6** — AC-14's spy clause is vacuous in every project able to run its When |
| 68 | **gap B #3** — the captive-dependency funnel has no branch for three of its own failure modes |
| 68 | **gap C #7** — Risks *Mitigation* cells that are arguments; 0076's second row runs 238 words |
| 66 | **0070 #5** — the AC-24 arithmetic declares the numeral referentless on a reading the ADR contradicts |
| 66 | **0073 #3** — `Scope`'s "It serves …" line: eight IDs, no mechanism, no owner |
| 66 | **0075 #5** — `Dispatcher.cs:484` attributed to `Receive()`; it is in `Start()` |
| 66 | **0076 #3** — probe falsifies the null-`optionsFunc` contract row |
| 66 | **set #6** — 0070 claims NFR-5 and NFR-6 unqualified |
| 66 | **gap A #7** — FR-11(b) has no acceptance criterion |
| 65 | **gap A #8** — AC-46's "no pipeline scope taken" is asserted through an instrument that cannot see scopes |
| 64 | **0073 #4** — "the only one an author has to touch anything to use" is false of the set |
| 64 | **0073 #5** — alternative 9's "decisive" ground contradicts the section that examines it |
| 64 | **0074 #4** — the funnel omits the open-generic stage the prose orders against |
| 64 | **0075 #6** — the 248-byte bracket cost is not a constant (measured 216 B) and AC-23 cannot see it |
| 64 | **0075 #7** — NFR-4's suppression clauses are discharged here and owned nowhere |
| 64 | **0076 #4** — a 252-word Risks cell that argues about test coverage |
| 64 | **0076 #5** — the FR-19 log bound 0076 restates is the one two siblings record as superseded |
| 64 | **set #7** — all seven frontmatter summaries breach the one-or-two-sentence schema |
| 64 | **gap A #9** — AC-24's "six factory interfaces" has no referent and contradicts NFR-1(c) |
| 64 | **gap B #4** — an ambient naming the **root** provider passes all three probe tests and is borrowed from |
| 64 | **gap C #8** — 0074 records no alternative for "rely on the container's own `ValidateScopes`" |
| 63 | **0071 #1** — "no shipped factory's `Release` touches the handler"; three dispose it |
| 63 | **0072 #4** — "everything `AddBrighter` and `AddConsumers` register is `Singleton` or `Transient`" is false |
| 63 | **0076 #6** — Scope claims NFR-4; *Technology Choices* disclaims it |
| 63 | **gap A #10** — FR-22.3 says the probe class has "no constructor"; it has one, inside the cited range |
| 62 | **0070 #6** — the roles table names the wrong caller for four of six interfaces |
| 62 | **0072 #5** — the transaction consequence is argued at full length in two sections |
| 62 | **0073 #6** — the frontmatter summary's first sentence is 106 words |
| 62 | **0073 #7** — FR-19 is a consumer-side requirement, cited twice as a whole-host log budget |
| 62 | **0073 #8** — both length claims in the rejected-candidates table are wrong (27 vs 27) |
| 62 | **0074 #5** — "no single triple can serve a `Singleton` mapper and a `Singleton` transform" is false |
| 62 | **0074 #6** — FR-22.4's message needs a value the ADR says is unreadable |
| 62 | **0075 #8** — the frontmatter summary is one 83-word sentence, republished in `index.md` |
| 62 | **set #8** — 0071's `handler` tag is not in the controlled vocabulary |
| 62 | **set #9** — the uniform preamble says "one decision each"; 0072 and 0076 open "two things" |
| 62 | **set #10** — 0074's dependency-order sentence omits 0075 |
| 62 | **set #11** — 0072's "the first two having only closed defects" |
| 62 | **gap A #11** — C-20's heading says two bounds; the body lists four |
| 62 | **gap A #12** — C-2's heading claims a scope broader than its body, and 0075's bracket sits in the gap |
| 62 | **gap B #5** — 0074:491's `Singleton` mapper/transform claim is false |
| 62 | **gap B #6** — the pseudo-code uses `OWNED`, the word the ladder reserves |
| 62 | **gap C #9** — 0070 alternative 8's rejection ground is circular |
| 61 | **0072 #6** — `TryAddScoped` is justified by a reason that does not hold |
| 60 | **0071 #2** — the "today" walkthrough names four of the six threading methods, both sync-only |
| 60 | **0071 #3** — step 6's AC-33 guard names an instrument the criterion excludes |
| 60 | **0071 #4** — a counterfactual compares against a state in which the compared object cannot exist |
| 60 | **0071 #5** — 91 mid-prose bold runs, ~30 on bare numerals and criterion IDs |
| 60 | **0072 #7** — row 2 is `OWNED` in the pseudo-code and explicitly not `OWNED` in the table |
| 60 | **0072 #8** — step 2 is a 90-line sub-document with twelve bolded lead-ins |
| 60 | **0073 #9** — the Decision carries a third bold-led paragraph on a topic with its own section |
| 60 | **0074 #7** — "every `T` in the repository is a core type" is false |
| 60 | **0075 #9** — five ⚠-plus-bold caveats against zero in four siblings |
| 60 | **0076 #7** — alternative 8 rejects `InternalsVisibleTo` on a design ground three siblings call a rule |
| 60 | **set #12** — 0072's Scope gives `AmbientScopeDiagnostics` two of its three latches |
| 60 | **set #13** — FR-6 claimed unqualified by 0070 and "for the handler family" by 0071 |
| 60 | **set #14** — C-3 and AC-21 cited only by 0070 where a second ADR binds |
| 60 | **gap A #13** — AC-30 exercises one of FR-24.1's three entry points |
| 60 | **gap B #7** — a `Scoped` transformer with the v9 null factory asks nothing, falsifying 0072's iff-rule |
| 60 | **gap C #10** — 0070 alternative 1 rejects an option for a cost the set pays four more times |

## ▶ Triage under D8 — the second sort, by what closing a finding costs the prose

D8's rule is that **the review cannot lower readability but the fixes can**. Every finding at or
above threshold is sorted here a second time, by fix shape. The counts are what matter for planning:

| Shape | Count (≥60) | What it costs | How to close it |
| --- | ---: | --- | --- |
| **wrong token** — bad AC, bad `file:line`, bad count, bad slug | 31 | nothing | fix it; D8 does not touch these |
| **over-claim** — the sentence says more than is true | 34 | ⚠ the qualifier trap | **rewrite the sentence narrower.** Do not append "except where…" |
| **omission** — the ADR does not state X | 9 | ⚠ the insertion trap | lead, table or `Alternatives Considered` first; mid-paragraph sentence last |
| **contradiction with a sibling** | 13 | usually nothing | one end is right; cite it, edit the other |
| **the reviewer objects to the prose itself** | 8 | — | **bucket R.** Rides that ADR's own pass; never joins a batch of factual corrections |

**The over-claim column is the round's story.** Thirty-four findings at threshold are a true sentence
that says slightly more than it should — exactly the population D8 was written for, and exactly the
population that six earlier rounds closed by appending a qualifier. Every one of them has a
reviewer-supplied narrower replacement, and **in every case the replacement is the same length or
shorter than what it replaces**. Three reviewers said so explicitly of their own finding sets.

⚠ **Nine findings are owner calls, because closing them costs something the review cannot spend.**
They are their own commits, exactly like a branch-3 item:

| Finding | Why it is a call |
| --- | --- |
| **0074 #1 / gap B #1** (80) | widening the rules to keyed descriptors is a **design change**, not a wording fix |
| **set #3** (70) | six unowned NFRs is a **coverage gap**; no sentence closes it |
| **0075 #2** (72) | the **slug** rename costs seven files plus `index.md`; C-16 makes slugs the citation key. Retitling without renaming is a legitimate outcome |
| **0074 #2** (76) | the ADR must **pick one of two shapes** for the exclusion-set inputs; the review cannot pick for it |
| **0074 #6** (62) | FR-22.4's message on the delegate path — report with a hole, or drop the rule |
| **0073 #2** (70) | whether to add an ASP.NET Core 8 **test leg that exists nowhere else** in the repository |
| **gap B #4** (64) | the probe **cannot** detect a root provider; the residue can only be stated |
| **set #16** (50) | ADR 0067's `Terms` block is **Accepted**; closable only by touching it or caveating seven places. Filed by its reviewer as a make-it-worse case |
| **gap C #6 / #7** (70/68) | moving six deliberations and rewriting the Risks cells is a **structural** pass over four ADRs |

⚠ **The requirements findings do not belong in any ADR batch.** Gap A's 25 findings — 13 at
threshold — are tagged **[REQ]** (the document is wrong) or **[ADR]** (the document is right and an
ADR misreads it). Twenty-one are [REQ] and go to **the end-of-phase requirements true-up**, joining
the rows §19.9 already carries. Four are [ADR] and are the amendments the ADRs themselves say are
owed — gap A assessed each on its merits and **found all four correct**, with one addition the ADRs
had missed: FR-18's enumeration names a case its own `Warning` clause can no longer reach.

## What the round proved clean — do not re-derive

Recorded because it is the expensive half of the work and the next round should not repeat it.

- **`docs/adr/index.md` is in sync.** Regenerated to a scratchpad path and diffed: zero difference.
- **All 26 mermaid blocks render.** 26/26 through `mmdc`, with the largest of each ADR rendered to
  PNG and looked at. Three reviewers independently confirmed their ADR's diagrams match the prose
  that reads off them.
- **Zero escaped entities** across all seven, no unbalanced backticks outside fences, no broken table
  pipes, and every `<see cref>` inside a code fence where it belongs.
- **The seven *Where this ADR sits* tables are byte-identical**, the unifying sentence is verbatim in
  all seven, every `## References` sibling one-liner matches its table cell, and every cited external
  ADR's status is correct.
- **All seven follow the template skeleton exactly**, and frontmatter matches the body in all seven
  (0075's title differs from its H1 by backticks only).
- **Every one of the 27 FRs, 10 NFRs and 52 ACs is cited by at least one ADR.** No orphan.
- **The nine/eight/thirteen interface-count trio is correctly scoped and is NOT a contradiction** —
  nine across the set, eight across 0070+0071, thirteen items in the ledger. Two reviewers checked it
  independently. AC-24 has exactly four `Then` clauses.
- **Probes that confirmed rather than falsified**, and which should not be re-run: the whole
  `ScopedArtefactCache` concurrency contract; `AmbientScopeProbe`'s design on both MS DI and Autofac;
  `FrameworkReference` transitivity through a NuGet package; MS DI's abandoned-scope non-disposal;
  `IOptions<T>.Value` reference-inequality with `IOptionsMonitor`/`IOptionsSnapshot`; the whole of
  0075's `ExecutionContext` restore table, confirmed **on the real `CommandProcessor`**, including
  that bracket 1's synchronous restore is the load-bearing one and bracket 2's is defence in depth;
  `Task.Factory.StartNew(LongRunning)` capturing across two nested hops; C-1's root-parented scope
  factory; AC-33's non-swallowed disposal exception; AC-42's ambiguous-constructor message.
- **Counts recounted and correct**, in bulk: 125 test files registering `IBrighterOptions`; 824:91
  public-to-internal in `src/`; 17:1 in the DI package; 69 `PipelineBuilder` constructions splitting
  48/21; 37 test projects, none referencing ASP.NET Core; 12 classes and 70 test doubles for 0070's
  factory family; 21 implementations and 22 test files for 0071's; five container-backed factories;
  six builder catch clauses; ten new DI-package types.

---

## Findings

### ADR 0070 — `per-pipeline-di-scope-for-mapper-and-transform-factories`

**14 findings, 6 at or above the threshold of 60. 0 Critical, 6 High, 5 Medium, 3 Low.**

| # | Finding | Score |
|---|---|---|
| 1 | Step 4a's `Warning`-message ledger ("eight… seven are about releasing a mapper or a transform") omits two release messages, in a type this ADR changes | 74 |
| 2 | The ADR never says how the scope reaches `TransformPipelineDrain`, and the touched table denies the signature changes | 72 |
| 3 | Step 2 and the Risks table say every non-container implementation returns `null`; `MessageMapperRegistry` forwards (step 6) | 70 |
| 4 | The `Create` contract row promises a fallback to "exactly its current behaviour", which step 9 abolishes | 68 |
| 5 | The AC-24 arithmetic declares the criterion's numeral referentless on a reading the ADR itself contradicts and ADR 0075 does not share | 66 |
| 6 | The roles table names the builders as the four factory interfaces' "only caller"; the ADR's own forces bullet and step 9 name others | 62 |
| 7 | 47 sentences of 40+ words against the house style's ~25-word rule | 56 |
| 8 | Step 4b calls `ServiceProviderLifetimeScope.DisposeAsync()` "existing"; step 6 adds it | 52 |
| 9 | "the break is theirs alone" vs "binary-breaking… caller and implementer alike" | 50 |
| 10 | *Implementation Approach* has a bold-led sentence in most of its paragraphs | 48 |
| 11 | *Where the pieces live* summary claims FR-3 for a mechanism confined elsewhere to `{Scoped, Scoped}` | 46 |
| 12 | Alternative 10's "no test names it" is false of the repository | 38 |
| 13 | FR-19 and FR-21 cited in the body, absent from the References roll-up | 34 |
| 14 | The class diagram omits the members steps 4b and 6 add to `ServiceProviderLifetimeScope` | 30 |

---

##### 1. The `Warning`-message ledger omits two release messages, in a type this ADR itself changes (74)

Step 4a states a closed count and then makes an explicit completeness claim about it. Two more `Warning` messages meet the description exactly, both emitted from a `catch` around `factory.Release(lease)` on the transform-pipeline build path, in the two internal helpers this ADR changes. The family is ten, of which nine are mapper-or-transform releases. The numeral is load-bearing twice.

**Evidence**: `0070:446` — "**Eight messages exist and all of them log at `Warning`.** Seven are about releasing a mapper or a transform rather than about disposing a DI scope". `0070:452` — "Any enumeration of this family that stops at seven is incomplete in the one place that matters." `0070:692` — "**11. Raise the seven existing `Warning` messages to `Error`…**". Against `src/Paramore.Brighter/TransformerFactory.cs:68-69` — `[LoggerMessage(LogLevel.Warning, "Failed to release a transformer after its initialization failed; …")] public static partial void FailedToReleaseTransformerAfterInitFailure(...)`, emitted at `:60`; and `src/Paramore.Brighter/TransformerFactoryAsync.cs:68-69`, identical. `0070:354` lists both types as touched. A whole-repo scan of `LoggerMessage(LogLevel.Warning` under `src/` found no further release-or-dispose message.

**Recommendation**: change the two numerals and extend the existing bullet list — do not add a paragraph. Lead becomes *"Ten messages exist and all of them log at `Warning`. Nine are about releasing a mapper or a transform…"*; the third bullet gains `FailedToReleaseTransformerAfterInitFailure` (`TransformerFactory.cs:69`, `TransformerFactoryAsync.cs:69`). Alternative 11's first phrase becomes "the nine existing `Warning` messages"; its argument is unchanged. The completeness sentence at `:452` then stands as written.

##### 2. Nothing says how the pipeline scope reaches `TransformPipelineDrain`, and the touched table denies the signature changes (72)

`TransformPipelineDrain` is `internal static` and holds no state — it receives everything it acts on as delegates. A third step that releases the `IAmAScope` must therefore be handed a third delegate. The touched table's **Change** column states the opposite; step 5 states a third thing again. No sentence anywhere gives the parameter. This is the mechanism FR-6 and FR-13's lead clause rest on.

**Evidence**: `0070:357` — "a third drain step… **Both parameter lists are unchanged**, because the third step runs on the explicit-dispose and finalizer paths alike". Against `0070:512` — "`Drain` and `DrainAsync` keep the parameter lists step 5 gives them". Against `src/Paramore.Brighter/TransformPipelineDrain.cs:38` (`internal static class`), `:46` (`internal static void Drain(Action disposeScope, Action releaseMapper)`), `:85` (`DrainAsync(Func<ValueTask>, Func<ValueTask>)`) — no field, no state (whole file read). Step 5 at `0070:508` says only that the methods "gain a third step".

**Recommendation**: the omission belongs at step 5's lead sentence, where the signatures are already quoted. Replace "gain a third step, which runs in a `finally`…" with "gain a third delegate and a third step: `Drain(Action disposeScope, Action releaseMapper, Action releaseScope)` and `DrainAsync(Func<ValueTask>, Func<ValueTask>, Func<ValueTask>)`, with the third running in a `finally` around the first two." Rewrite the table clause to the narrower truth — "Each list gains one delegate and nothing else: the third step runs on both paths, so no parameter tells them apart" — and drop the now-redundant sentence at `:512`.

##### 3. Step 2 and the Risks table exclude `MessageMapperRegistry` from the forwarding rule step 6 gives it (70)

Step 2 states an unqualified universal that covers `MessageMapperRegistry` — a class it enumerates — and the Risks table turns it into a count. There are five classes with a non-trivial body, not four. The consequence is functional: the builder asks the registry first and never calls a mapper factory directly, so a registry answering `null` would leave a `{Scoped mapper, Singleton transformer}` pipeline with no scope.

**Evidence**: `0070:408` — "Every non-container implementation gets the same two-line treatment: return `null`, ignore the parameter." `0070:663` — "the four container-backed factories are the only ones whose bodies are not `return null;` / ignore-the-parameter". Against `0070:540` — "`MessageMapperRegistry` forwards both members… `Get<T>`/`GetAsync<T>` pass the scope straight through", and `0070:351` — "implements both new members by forwarding to the factories it owns". Step 6 is the correct one, confirmed against `0070:115` and `src/Paramore.Brighter/TransformPipelineBuilder.cs:332` — `var messageMapperLease = _mapperRegistry.Get<TRequest>();`.

**Recommendation**: narrow both, do not append exceptions. Step 2: "Every implementation except `MessageMapperRegistry` and the four container-backed factories gets the same two-line treatment…". Risks cell: "the four container-backed factories and `MessageMapperRegistry` are the only ones whose bodies are not `return null;` / ignore-the-parameter, and a test asserts each of the four factories returns a scope under `Scoped` and `null` otherwise".

##### 4. The `Create` contract row promises a fallback step 9 abolishes (68)

The contract table is normative. For a `Scoped` factory, "its current behaviour" is the factory-wide cache built in the constructor — exactly what step 9 removes, on the ground that one factory must not carry two behaviours.

**Evidence**: `0070:331` — "a scope this implementation does not recognise is **ignored**, not rejected: the implementation falls back to exactly its current behaviour, and must not throw". Against `0070:537` — "When the lifetime is `Scoped` and no handle is supplied it resolves fresh and caches nothing, so the factory-wide `Scoped` cache goes…", and `0070:572` — "resolves a fresh artefact **where today it returns the one the factory has held since its constructor ran**". Steps 6 and 9 are the design verified against `ServiceProviderMapperFactory.cs:46` and `ServiceProviderLifetimeScope.cs:163-178`.

**Recommendation**: one clause for one, in the row: "…is **ignored**, not rejected: the implementation behaves as it does when handed no scope at all — step 6's rule for that lifetime — and must not throw."

##### 5. The AC-24 arithmetic declares the numeral referentless on a reading the ADR itself contradicts (66)

Five paragraphs argue AC-24's "six factory interfaces whose signature changed" has no referent, and conclude an amendment to the requirement is owed. The argument turns on the ADR's own choice to treat a member added to a shared base as not changing the twins' signature. But AC-24's clause asks for "what changed and **how a hand-rolled implementation is migrated**", and on that test all six of NFR-1's factory interfaces changed — after ADR 0071 a hand-rolled `IAmAHandlerFactorySync` no longer compiles. The numeral is then exactly six. The passage also records a deliberation rather than a decision.

**Evidence**: `0070:571` — "So the numeral in AC-24's clause has no referent under either count… An amendment to AC-24's last clause is owed". `0070:568` concedes the counter-reading ("change through the shared base ADR 0071 puts `CreatePipelineScope()` on"). Against `requirements.md:354` (NFR-1's six) and AC-24's last clause. Against `0075:324` — "That criterion enumerates… the six factory interfaces whose signatures changed", used without difficulty. Verified that 0071 puts the member on the base (`0071:337`; `IAmAHandlerFactory.cs:7`), so both twins' implementers must move.

**Recommendation**: delete `0070:567-571` and replace with one sentence: "AC-24's six are NFR-1's six factory interfaces, and every one of them requires a hand-rolled implementation to add `CreatePipelineScope()` — four in their own text, the two handler twins through the base ADR 0071 changes." The superset claim already lives at `:593`. Hedging the argument would be the wrong shape; the fix is to remove it.

##### 6. The roles table's Collaborators cell names the wrong caller for four of six interfaces (62)

The *Scope offerer* row covers the four factory interfaces and the two registry interfaces together and asserts one caller for all six. True of the registries' `Get`/`GetAsync`; false of everything else.

**Evidence**: `0070:269` — "`TransformPipelineBuilder[Async]`, their only caller in Brighter's own code". Against `0070:115` — "Neither builder calls a mapper factory directly"; `0070:599` — "the only callers of the transformer factories' `Create` are `TransformerFactory<TRequest>` (`:42`) and `TransformerFactoryAsync<TRequest>` (`:40`)"; and `src/Paramore.Brighter/TransformPipeline.cs:71` — `releaseMapper: () => _mapperRegistry?.Release(MapperLease)`. Both counter-statements verified at source.

**Recommendation**: split the row — one phrase cannot carry two families. Registry row: collaborators `TransformPipelineBuilder[Async]` and `TransformPipeline[Async]`. Factory row: "the registry and `TransformerFactory[Async]`, which call them; the `IAmAScope` they offer and are handed". Role and Responsibilities text is unchanged.

##### 7. Sentence length (56)

47 sentences of 40+ words in the prose (code fences, tables, headings excluded), against `.agent_instructions/documentation.md:125-126`'s "no more than about 25 words". Worst is `0070:517` at 63 words with two em-dash asides. Also `:622` (56w), `:593` (56), `:595` (52), `:383` (52).

**Recommendation**: take the worst ten only; `### Correcting an ADR` warns that this is where readability is lost. For `:517`: "Its `SynchronizationContext` suppression is a no-op on the finalizer thread, and the method's own remarks say so (`ServiceProviderLifetimeScope.cs:391`). The deadlock that suppression prevents needs the blocked thread to be the pump thread whose captured context would run the continuation, so it cannot arise there." Two sentences, no words added. Measure blocks over 150 and bold runs before and after.

##### 8. `DisposeAsync()` described as existing (52)

**Evidence**: `0070:480` — "The existing `Dispose()` and `DisposeAsync()` entry points keep today's swallow-and-log behaviour exactly". Against `ServiceProviderLifetimeScope.cs:42` — `internal sealed partial class ServiceProviderLifetimeScope : IDisposable` (verified; only whole-object teardown is `Dispose()` at `:462`). Against the ADR's own `0070:363` and `0070:530` ("Today it is `IDisposable` alone (`:42`)").

**Recommendation**: "The **terminal** `Dispose()` and `DisposeAsync()` entry points swallow and log, because their callers are terminal teardown and a throw there would strand the remaining scopes."

##### 9. Contradictory statements of who the break falls on (50)

**Evidence**: `0070:336` — "the break is theirs alone" (implementers). Against `0070:637` — "binary-breaking for anyone not recompiled, caller and implementer alike". The second is correct.
**Recommendation**: "…so the **source** break is theirs alone." The *Negative* bullet already carries the binary half.

##### 10. Bold-led paragraphs saturate *Implementation Approach* (48)

231 bold runs, 80 mid-prose. In step 5 alone (`0070:506-525`), five of eight paragraphs open with a fully bolded sentence (`:510`, `:512`, `:521`, `:523`), on top of the bolded step headings. Against `.agent_instructions/documentation.md:117-119`.
**Recommendation**: closable only by removing emphasis. Unbold the paragraph leads inside numbered steps; keep bold on the step heading. Where a paragraph loses findability, move its point to the step's lead — step 4b's "Without this, none of step 4a fires" is the model.

##### 11. *Where the pieces live* summary over-claims FR-3 (46)

**Evidence**: `0070:208` — "One `IServiceScope` per transform pipeline, reached by every participating factory… That is FR-1, FR-2 and FR-3 in one mechanism". Against `0070:553` and `0070:622` ("Defect 1b is closed **where FR-3 asks it to be** — with… both `Scoped`").
**Recommendation**: "One `IServiceScope` per transform pipeline, offered to every participating factory and resolved from by every `Scoped` one, disposed exactly once when the pipeline is released. That is FR-1 and FR-2, and FR-3 wherever both lifetimes are `Scoped`."

##### 12. "no test names it" is false (38)

**Evidence**: `0070:688`. Against `tests/Paramore.Brighter.Extensions.Tests/When_creating_a_request_from_a_reply_message_on_the_pump_context_it_should_not_deadlock.cs:41` and `.../When_releasing_an_async_disposable_mapper_on_the_pump_context_it_should_not_deadlock.cs:61` — both name the type in comments. The other two conjuncts verified true.
**Recommendation**: replace the middle conjunct with the stronger one: "no test **can** name it, because the package grants `InternalsVisibleTo` to nothing" — two conjuncts collapse into one.

##### 13. FR-19 and FR-21 missing from the References roll-up (34)

Cited at `0070:50`; absent from `0070:709`. Derived by extracting every requirement ID from lines 1–707 and differencing against the roll-up; no other genuine gap.
**Recommendation**: add both to the list between `FR-16` and `FR-20`. No prose change.

##### 14. Class diagram omits `ServiceProviderLifetimeScope.DisposeAsync()` (30)

`0070:252-255` renders it with `GetOrCreate` and `Dispose` only, while step 6 gives it `IAsyncDisposable` and a whole-scope `DisposeAsync()` and step 4b the surfacing path.
**Recommendation**: add `+DisposeAsync()` to the class body and nothing else; the surfacing path is a mode and belongs only in the 4b sequence diagram, where it already is.

---

#### Verified CLEAN — do not re-derive

**Every `file:line` citation in the ADR that was opened was correct**, including these, checked at the line and against the declaration rather than the attribute: the six interface `Create`/`Release` lines (`IAmAHandlerFactorySync/Async :44 :51`; `IAmAMessageMapperFactory :45 :60`; `…Async :46 :62`; `IAmAMessageTransformerFactory :44 :50`; `…Async :45 :54`); `IAmAMessageMapperRegistry.cs:34`; `MessageMapperRegistry.cs:41`; `ClaimCheckTransformer.cs:62`; `ServiceProviderHandlerFactory.cs:102-107, 127-131, 133-137`; `ServiceProviderMapperFactory.cs:44-46, 61-65, 78`; `ServiceProviderTransformerFactory.cs:46`; `ServiceProviderLifetimeScope.cs:42, 110, 118-123, 126, 132-142, 151-157, 163-178, 259-261, 320, 346-350, 367, 384-391, 400-402, 406, 422-436, 449, 462-501, 522`; `TransformPipelineBuilder.cs:51, 93, 95-97, 104, 106, 108, 111, 116-125, 122-123, 124, 134, 157-166, 163-164, 165, 172, 174, 180, 193, 215-223, 221-222, 231, 244, 330, 332, 409, 412`; `TransformPipelineBuilderAsync.cs:50, 52, 93, 116-125, 134, 157-166, 172, 174, 231, 239, 253, 255, 318, 321` — and the ADR's claim that the two builders are **line-identical** through the guarded region is true; `TransformPipeline.cs:16, 21, 24, 37-72, 65, 69`; `TransformPipelineAsync.cs:22, 96-118`; `TransformPipelineDrain.cs:38, 46, 67-72, 76, 85`; `TransformerFactory.cs:32, 42`; `TransformerFactoryAsync.cs:30, 40`; `WrapPipeline.cs:53`, `UnwrapPipeline.cs:45`, `WrapPipelineAsync.cs:57`, `UnwrapPipelineAsync.cs:47` — and all six pipeline types are `public` with `public` concrete constructors; `ControlBusMessageMapperFactory.cs:31`; `BrighterOptions.cs:20, 37, 52, 69`; `Paramore.Brighter.csproj:24` (with the stated comment); `IAmATransformLifetimeAsync.cs` (`internal interface … : IDisposable, IAsyncDisposable`); `ServiceCollectionExtensions.cs:807, 808, 945, 957`; `OutboxProducerMediator.cs:569, 587, 1248, 1258, 1269-1279, 1312, 1321, 1449`; `Reactor.cs:531, 638`; `Proactor.cs:239, 241, 538, 652`; `FactoryLifetimeTests.cs:36, 154` (both are handler-family, both assert within-lifetime identity, as claimed); `requirements.md:714`.

**Every count recounted was right, except the one in finding 1:**
- **12 classes in `src/`** — a multi-line class-declaration scan (primary constructors and wrapped base lists included) across `src/`, `tests/` and `samples/` returns exactly 12 files/classes: 4 container-backed + 6 core factories + `ControlBusMessageMapperFactory` + `MessageMapperRegistry`. ✔
- **70 test doubles / 64 factory doubles across 37 files / 6 registry doubles in 3 files, one carrying no factory double / 38 test files in all** — all four numbers verified exactly by the same scan. ✔
- **82 implementations** (Risks) = 12 + 70. ✔
- **Thirteen release-note items = five + eight from five siblings**, and **nine not reached by AC-24** (13 − 4) — all recounted and correct; AC-24 does have exactly four `Then` clauses.
- **Nine interfaces the set breaks** (0070's six + 0071's two + 0076's one) and **eight across 0070/0071, three of them not factories** — internally consistent and consistent with `0076:455`.
- **Seventeen of the eighteen classes in the DI package are public, `ServiceProviderLifetimeScope` the single internal exception** — enumerated top-level types across all 21 files: exactly 17 public classes + 1 internal. ✔
- **Five container-backed factories all read `IBrighterOptions` in their constructor** — verified in all five.
- **Four tests encode the old contract, all in `tests/Paramore.Brighter.Extensions.Tests/`** — all four files exist; a repo-wide search for tests combining `ServiceLifetime.Scoped` with `ServiceProvider*Factory` returns exactly those four. (`DispatcherResolutionScopedDependencyTests.cs` sets `Scoped` lifetimes but asserts nothing about caching, so it is not a fifth.)
- **`ClaimCheckTransformer` is the one Brighter-shipped transform with constructor dependencies** — every other in-core and out-of-core Brighter transform is parameterless. ✔
- **Six build-and-release call sites, four of them in `OutboxProducerMediator`, all releasing in a `finally`** — verified at all six.
- **Readability**: no prose block or bullet exceeds 200 words and only three exceed 150 (alternatives 1 and 9, and the NFR-1 forces bullet). This document is *not* block-bloated; finding 7 is about sentences, not blocks.

**All four mermaid blocks render** with `mmdc` (SVG for all four; PNG at `-w 1600 -b white` for the sequence and class diagrams, both inspected). The class diagram is legible and not a candidate for conversion to a decision-ladder table. The sequence diagram's release ordering (transform leases → mapper lease → scope) matches both the prose and `TransformPipelineDrain.Drain`'s actual step order. The flowchart's "Reading the edges" prose is accurate: both solid arrows crossing the package boundary run DI → core.

**Markup checks pass**: `grep -c '&lt;\|&gt;\|&amp;\|&nbsp;'` returns **0**; backticks balance on every non-fence line; the single `<see cref>` is inside a C# code block, which is correct; no broken table pipes.

**Probes — all three confirmed the ADR rather than falsifying it** (`net10.0`, `Microsoft.Extensions.DependencyInjection` 9.0.0):
- The abandoned-scope claim at `0070:516` is **exactly right**: a `Scoped` `IDisposable` in an abandoned scope sees **0** `Dispose()` calls after three forced Gen-2 collections with `WaitForPendingFinalizers`, against **1** on the control path.
- `ServiceProviderEngineScope` and `ServiceProvider` **declare no `Finalize`** (reflection over the shipped assembly).
- **CS0051** is the exact error for a public constructor taking an `internal sealed` parameter type, as *Technology Choices* and alternative 10 claim.

**Sibling cross-checks that hold**: the *Where this ADR sits* table is row-identical across all seven ADRs; the unifying sentence "the per-pipeline object carries the DI scope" is repeated verbatim in all six siblings; 0071 confirms `IAmAHandlerFactory` gains `CreatePipelineScope()` and `IAmALifetime` gains `PipelineScope`, that `HandlerLifetimeScope.Log` gains `FailedToDisposePipelineScope` at `Error` guarded by AC-33, that it depends on 0070 step 4b, and that `ServiceProviderPipelineScope` is configured by its creator's lifetime; 0072 confirms step 1b, `AmbientScopeSourceException`, the AC-30 coverage statement (stated identically from both ends), the `IAmAServiceProviderScope` role type-test, the `_scopedInstances` relocation and the faulted-`Lazy` eviction on both paths with `Singleton` untouched; 0074 confirms the `ScopeConfigurationValidator` public-type/internal-constructor answer and its two internal entity types, and carries the two items 0070 attributes to it; 0075 confirms the `isolateSubscribers` binary break and states it inside its *Negative* section (as a `####` sub-heading rather than a bullet — 0070's pointer is correct); 0076 confirms `IBrighterOptions.DefaultScopeAffinity` and draws the FR-19/FR-21 served-not-discharged distinction 0070 cites. **The ADR's heading skeleton matches the template verbatim and in order.**

#### Gaps

- **`release_notes.md` was not inspected.** Step 7a is a specification of a document that does not yet exist, so only its internal arithmetic and its pointers into siblings could be checked, not whether the entry it describes is achievable.
- **`docs/guides/lifetimes-and-scoping.md` (FR-25) does not exist yet**, so the Risks-table mitigation that leans on it is unverifiable.
- **The solution was not built.** The claim that the 82-implementation edit "lands as one commit or the build is broken in between" is untested; a compile of the proposed signatures against the 70 test doubles would be the next probe.
- **Requirements coverage across the whole set was not audited** — only the FRs, NFRs, constraints, decisions and ACs ADR 0070 itself cites.
- **`0070:535`'s claim that `CreateRequestFromMessage` is the `Call` reply path** was confirmed only to the extent that its sole caller is `CommandProcessor.cs:1472`; that line was not verified to sit inside `Call`.


### ADR 0071 — `pipeline-scope-handle-for-handler-pipelines`

**12 findings, 8 at or above threshold. 0 Critical, 0 High, 8 Medium, 4 Low.**

| # | Score | Finding |
|---|---|---|
| 1 | 63 | *Key Components* says no shipped factory's `Release` touches the handler; three do |
| 2 | 60 | The "today" walkthrough names 4 of 6 threading methods, and both named Push/Append are sync-only |
| 3 | 60 | Step 6's AC-33 guard names an instrument the criterion excludes |
| 4 | 60 | A counterfactual compares against a state in which the compared object cannot exist |
| 5 | 60 | 91 mid-prose bold runs, ~30 on bare numerals and criterion IDs |
| 6 | 58 | The `CreatePipelineScope()` contract row sits under the `IAmALifetime` heading |
| 7 | 58 | "ADR 0070's second failure mode" is one 0070 says is not part of its contract |
| 8 | 57 | *Scope* claims a throwing `Release` can skip reclamation today; no shipped factory can |
| 9 | 52 | FR-5 cited directly for a rule FR-5's own text does not carry |
| 10 | 48 | The lifetime flowchart renders in reverse of the order its prose reads it |
| 11 | 45 | FR-11(b) cited in the body, missing from `## References` |
| 12 | 42 | Class-diagram realization edge targets the base, not the twins |

---

##### 1. *Key Components* says no shipped factory's `Release` touches the handler; three shipped factories dispose it (63)

The ordering section justifies pinning the release-order rule with a test rather than a criterion by asserting the only `Release` it could protect is one Brighter does not ship. Three factories in `src/Paramore.Brighter` dispose the handler inside `Release`, and the same ADR says so 200 lines later. The real reason the ordering is unobservable for them is different — they return `null` from `CreatePipelineScope()`, so there is no handle to order against — and that reason is never given.

**Evidence**: `0071:397` — "**So what the ordering protects here is a factory this repository does not ship**: a third-party `Release` that resolves against a scope already dead, or that touches the handler instance … Brighter's own factory does neither". Against `src/Paramore.Brighter/SimpleHandlerFactory.cs:27-33` — `public void Release(IHandleRequests handler, IAmALifetime lifetime) { if (handler is IDisposable disposable) { disposable.Dispose(); } }` (async overload `:18-24`), plus `SimpleHandlerFactorySync.cs:40` and `SimpleHandlerFactoryAsync.cs:51`. Self-contradicted at `0071:596` — "`SimpleHandlerFactorySync.Release` calls `disposable?.Dispose()`".

**Recommendation**: replace the clause, do not qualify it — "The ordering is invisible in this repository: the container-backed factory's `Release` does nothing, and the factories whose `Release` disposes the handler offer no handle to order against." One sentence for one; removes the false universal instead of appending an exception.

##### 2. The "today" walkthrough names four of the six threading methods, and both are sync-only (60)

Step 2 of *How a handler pipeline reaches its DI scope today* enumerates the methods carrying `IAmALifetime` to each attribute decorator. It names `PushOntoPipeline` and `AppendToPipeline`, both of which take `IHandleRequests<TRequest>` and serve the sync path only. An async decorator reaches its factory through `PushOntoAsyncPipeline` and `AppendToAsyncPipeline`, omitted — though the sentence covers the async handler at `:236`. *Technology Choices* lists all six, so the ADR contradicts itself on the same set.

**Evidence**: `0071:109` — "each attribute decorator through `BuildPipeline` (`:272`), `BuildAsyncPipeline` (`:316`), `PushOntoPipeline` (`:499`) and `AppendToPipeline` (`:430`)". Against `src/Paramore.Brighter/PipelineBuilder.cs:499-500` (`PushOntoPipeline(… IHandleRequests<TRequest> lastInPipeline …)`), `:525-526` (`PushOntoAsyncPipeline`), `:430` / `:451-452`. Against `0071:434-436`, which lists six.

**Recommendation**: the list is the defect. Replace the four names with the six paired — "`BuildPipeline` (`:272`) and `BuildAsyncPipeline` (`:316`), then `PushOntoPipeline` (`:499`)/`AppendToPipeline` (`:430`) or their async twins (`:525`, `:451`)". No new sentence.

##### 3. Step 6's AC-33 guard names an instrument the criterion excludes (60)

AC-33 fixes its injection point explicitly and gives the reason. The ADR restates the test as "over a handle whose `Dispose()` throws", which a stub `IAmAScope` satisfies — bypassing `ServiceProviderPipelineScope` and therefore ADR 0070 step 4b, the dependency the ADR's own touched table names. An implementation can go green on the ADR's wording without satisfying the criterion.

**Evidence**: `0071:537` — "A `Send` whose handler completes normally, over a handle whose `Dispose()` throws…". Against `requirements.md:535` — "**as the stated injection point**, a container-`Scoped` dependency of the handler whose `Dispose()` throws `InvalidOperationException`: MS DI … does not swallow their exceptions, so releasing the owned pipeline scope throws." Against `0071:408` — "**The second depends on ADR 0070 step 4b** — without the surfacing disposal path … this member never fires".

**Recommendation**: replace the four words naming the instrument — "over a pipeline scope whose container-`Scoped` dependency throws from `Dispose()`". The rest stands.

##### 4. A counterfactual compares against a state in which the compared object cannot exist (60)

The foreign-handle paragraph prices the new behaviour against the old, but the call it calls "the same call" cannot be made before this ADR: `IAmALifetime` has no `PipelineScope` until this ADR adds it, so there is no unrecognised handle to pass and no "two DI scopes" to count.

**Evidence**: `0071:382` — "Before this ADR the same call produced **two** DI scopes for one pipeline — the unrecognised handle and Brighter's own, keyed in the dictionary". Against `src/Paramore.Brighter/IAmALifetime.cs:34-47`, whose entire member list is the two `Add` overloads; and `0071:406`, which records `IAmALifetime` as *gaining* `PipelineScope`.

**Recommendation**: state the narrower true thing — "A caller that already held a scope of its own got Brighter's dictionary scope as well, and leaked it whenever the handler was never tracked." Drop "the same call" and the numeral, which are what make it false.

##### 5. 91 mid-prose bold runs, most on bare numerals and criterion IDs (60)

Measured with fences and frontmatter stripped: 226 bold runs in 12,464 words — 29 in table cells, 59 at bullet leads, 47 opening a paragraph, **91 inside running prose**. Roughly a third carry no emphasis load: bare numerals (`**eight**`, `**six**`, `**32**`, `**26**`, `**Twenty-one**`, `**Four**`, `**One**`, `**two**`, `**three**`, `**four**`, `**five**`) and mid-sentence IDs (`**AC-9**`, `**AC-33**`, `**AC-51**`, `**AC-7**`, `**FR-12**`, `**FR-27.1 and AC-46**`).

**Evidence**: against `.agent_instructions/documentation.md:118-119` — "**Emphasis is a symptom.** … A section with bold in every paragraph has emphasis in none of them" — and `:106-110` — "requirement IDs … pure noise inside an argument."

**Recommendation**: unbold the numerals and mid-sentence IDs. ~30 four-character deletions, no rewording, no loss of findability; bullet-lead and paragraph-lead bolds untouched. **Block sizes need no work** — no prose block exceeds 200 words; only five sit between 151 and 200.

##### 6. The `IAmAHandlerFactory.CreatePipelineScope()` contract row lives under the `IAmALifetime` heading (58)

`#### IAmAHandlerFactory gains the offer` discusses the member for twenty lines then forwards to "the contract below" — a table sitting under the *next* `####` heading, about a different type.

**Evidence**: `0071:322` heading; `0071:344` — "so the contract below states one error condition"; `0071:346` `#### IAmALifetime gains the handle`; `0071:364-368` the `**Contract.**` table whose first row is `IAmAHandlerFactory.CreatePipelineScope()`. Against `.agent_instructions/documentation.md:81` — "then **each significant type** with a contract table".

**Recommendation**: move the one row, with its own `**Contract.**` label, under `#### IAmAHandlerFactory gains the offer`, and delete the now-unnecessary forward pointer at `:344`. The edit removes a sentence rather than adding one.

##### 7. "ADR 0070's second failure mode" is one 0070 says is not part of its contract (58)

**Evidence**: `0071:344` — "ADR 0070's **second** failure mode is not yet in play." Against `0070:330` — "**This ADR's contract has one failure.** … *Widened by ADR 0072, **not part of this contract**:* … both the type and the clause that discriminates them are 0072's to add". **0070 verified as correct** — it declares the contract.

**Recommendation**: rename the owner in place — "ADR 0072's widening of the contract is not yet in play." Nothing downstream repeats it.

##### 8. *Scope* claims a throwing `Release` can skip the reclamation today; no shipped factory can (57)

Today the reclamation *is* the container-backed factory's `Release`, and the only thing beneath it that could throw catches per-scope and logs. The factories whose `Release` can throw own no DI scope. The ADR's *Consequences* bullet states the real defect accurately (aborted loop, unreleased handlers, uncleared lists) and claims no stranded scope; the *Scope* bullet over-states it.

**Evidence**: `0071:41` — "where today a throwing `Release` can skip the reclamation entirely." Against `ServiceProviderHandlerFactory.cs:102-107` and `:133-137`, and `ServiceProviderLifetimeScope.cs:470-501`, where each disposal is `try { DisposeScope(scope); } catch (Exception e) { Log.FailedToDisposeScope(s_logger, e); }`. Against the accurate `0071:596`.

**Recommendation**: narrow the clause — "…where today a throwing `Release` aborts the loop and leaves the remaining handlers unreleased." Matches the *Consequences* bullet it derives from; drops the claim rather than hedging it.

##### 9. FR-5 is cited directly for a rule FR-5's own text does not carry (52)

FR-5 is scoped to a failed transform-pipeline *build*. Its "must not mask it" clause concerns the `ConfigurationException` such a build raises. The ADR cites FR-5 twice as the authority for a handler pipeline whose *handler* threw during execution. The reading is not invented — AC-51 makes the same attribution — but the authority is AC-51's gloss, not FR-5's text.

**Evidence**: `0071:480` (and `0071:40`). Against `requirements.md:187` — "**A failed pipeline build releases the *owned* pipeline scope.** When building a **transform pipeline** throws (the `CleanUpAfterFailedBuild` path, `TransformPipelineBuilderAsync.cs:122` and `:163`)…". Attributing text is `requirements.md:546` (AC-51).

**Recommendation**: cite the criterion — "AC-51's second clause requires that a release failure must not replace the caller's exception, and FR-6's example requires a throwing handler's exception to reach the caller unchanged." Same length; apply at `0071:40` too so the derived bullet does not regenerate it.

##### 10–12 (below threshold)

- **(48)** `mmdc` lays the three subgraphs out Singleton→Scoped→Transient; `0071:217`'s prose and step 5's table read Transient→Scoped→Singleton. Fix: reorder the three `subgraph` declarations (source-only), or reorder the sentence. Do not add a "read right to left" note.
- **(45)** `0071:378` cites FR-11(b); `0071:637`'s otherwise-exhaustive list, which has an explicit slot for routed requirements, omits FR-11. Fix: add `FR-11` beside `FR-12`.
- **(42)** `0071:305` — `ServiceProviderHandlerFactory ..|> IAmAHandlerFactory : implements both twins`. Against `ServiceProviderHandlerFactory.cs:34` — `: IAmAHandlerFactorySync, IAmAHandlerFactoryAsync`. Fix: two edges to the twins, drop the label; the transitive relation is already drawn.

---

#### Verified CLEAN — do not re-derive

**Every count in the document recounted and correct.** With a multi-line class-declaration scan: `IAmAHandlerFactory` family = **21** implementations (**5** in `src/` — `ServiceProviderHandlerFactory`, `ControlBusHandlerFactorySync`, `SimpleHandlerFactory`, `SimpleHandlerFactorySync`, `SimpleHandlerFactoryAsync`; **16** test doubles in **16** files, of which **11** are `QuickHandlerFactory`/`Async` across AWS, AWS.V4, Gcp, RMQ.Async, RMQ.Sync (sync only) and RocketMQ, and **5** in `Core.Tests`). `IAmALifetime` = **7** (one internal `src/` class + **6** `TestLifetimeScope` doubles in six files, all in `Extensions.Tests`, none carrying a factory double). **22** test files. **Six** classes in `src/`. **`DummyHandlerFactory`** is exactly `sealed class DummyHandlerFactory : IAmAHandlerFactory;` with no body. **Six** threading methods, **eight** on the resolution path, **32** `src/` declarations taking an `IAmALifetime` parameter (grep returns 35; 3 are the interface declaration and the two `GetXInstanceScope()` *returns*) and therefore **twenty-four** off it. **Six** test files, **26** test methods (25 `[Fact]` + 1 `[Theory]`), splitting **21** non-`Singleton` / **4** `Singleton` / **1** null-lifetime — all four splits confirmed method by method. **Four** existing `Debug` `Log` members. **Three** release sites, **eight** interfaces, **four**/**five**, **thirteen** items, **two** amendments — all agree with ADR 0070's own figures.

**Every `file:line` citation checked and correct**, including the ones most likely to drift: `IAmAHandlerFactory.cs:7`, `IAmALifetime.cs:34`, `HandlerLifetimeScope.cs:33`/`:74-93`/`:95`, `ServiceProviderHandlerFactory.cs:34`/`:40`/`:94-99`/`:102-107`/`:120-125`/`:127-131`/`:133-137`/`:129`/`:135`, `IAmAHandlerFactorySync.cs:32-34` (quotation accurate) and `:44`, `IAmAHandlerFactoryAsync.cs:36`, `PipelineBuilder.cs:47`/`:59`/`:76`/`:179-205`/`:190`/`:191`/`:192-193`/`:195`/`:202-205`/`:235`/`:236`/`:241`/`:248-251`/`:269-270`/`:272`/`:316`/`:430`/`:451`/`:499`/`:525`/`:567`/`:578`, `HandlerFactory.cs:44`/`:47`, `AsyncHandlerFactory.cs:42`/`:46`, `RequestHandler.cs:83-86` (non-virtual, does not store, `_successor` is `IHandleRequests<TRequest>?` at `:56` so the successor call *is* interface dispatch), `RequestHandlerAsync.cs:97-100`, `IHandleRequests.cs:71`, `IHandleRequestsAsync.cs:82`, `IAmAPipelineBuilder.cs:36` and `IAmAnAsyncPipelineBuilder.cs:37` (both `internal`, both `IDisposable`-only), `CommandProcessor.cs:317`/`:394`/`:472`/`:575` (all four `using var builder`), `BrighterOptions.cs:37`, `ServiceProviderTransformerFactory.cs:66-71`, `ServiceProviderLifetimeScope.cs:81`/`:346-350`/`:422-436` (inside `DisposeScope`, load-bearing-invariant comment immediately above)/`:462-501`/`:522` (the method declaration, not the attribute line), `SimpleHandlerFactory.cs:11` (public), `SimpleHandlerFactorySync.cs:33`, `SimpleHandlerFactoryAsync.cs:33`, `ControlBusHandlerFactory.cs:6`, `TransformPipelineDrain.cs:38`.

**Probed rather than argued**: `Paramore.Brighter.ServiceActivator.csproj` has exactly one `ProjectReference` and no `PackageReference` — the NFR-3 claim holds. `netstandard2.0` is in the TFM set. The `FailedToReleasePipeline` members in `Reactor.cs:637`, `Proactor.cs:651` and `OutboxProducerMediator.cs:1448` are all `LogLevel.Warning`. `TransformPipelineDrain.Drain`/`DrainAsync` do compose and throw (`AggregateException`, `ExceptionDispatchInfo`). The latent leak is real: `GetOrAdd` runs before `GetOrCreate`, `_trackedObjects` stays empty when `Create` returns null, so `Release` never runs and the entry survives. `HandlerLifetimeScope` is constructed at only two sites (`PipelineBuilder.cs:572`, `:583`), and the ADR's step-3 snippet raises no overload ambiguity.

**All five mermaid blocks render** with `mmdc` (lines 85-103, 152-173, 189-214, 236-256, 264-307). The class diagram and the lifetime flowchart were rendered to PNG at 1600px and viewed; both are legible and the classDiagram's content matches its prose. Escaped-entity grep returns **0**; no `&nbsp;`, no stray `<see cref>`, no broken table pipes, no unbalanced backticks outside fences.

**Set-level claims cross-checked and correct**: 0070 does name FR-7 as *served* (`0070:50`); 0070 does specify the handle over its creator's lifetime and names 0071's restatement (`0070:385`); 0070's step 7a arithmetic (eight interfaces, three non-factories, four/five, four clauses of thirteen) matches 0071 word for word; 0070's step-7a catalogue lists exactly three ADR-0071 items in the order 0071's "third contribution" numbering assumes; `TransformPipelineDrain`'s "third drain step" (`0070:357`, `:508`) and the "design-owed test" term (`0070:610`) are 0070's own; 0072 states the reject-vs-ignore reconciliation from its side (`0072:338`) and the FR-27.1/AC-46 amendment (`0072:161`, `:623`); 0073 records the AC-14 spy-clause split as the *second* amendment and 0071's as the first (`0073:423`); 0075's `isolateSubscribers` constructor and bracket lines (`0075:289`) match `0071:409` exactly. Frontmatter, body `# 71.` title, `## Status`, date, tags and the `docs/adr/index.md:109` row are all in sync. Every template heading is present in order.

#### Gaps

- Nothing was compiled or run. The interface-break counts, the constructor-overload analysis and the `netstandard2.0` default-interface-member reasoning are static; a build after the change is the only thing that settles whether all 22 test files were found.
- `IAmAScope`, `ServiceProviderPipelineScope` and `CreatePipelineScope` do not exist in `src/` yet (they are ADR 0070's). Every claim about their behaviour was checked against ADR 0070's text, not against code.
- Blinded per the brief: no review-round file, no `PROMPT.md`, no readability plan, no git history. A finding above may have been raised and consciously closed in an earlier round.
- ADRs 0070 and 0072 were read closely (they carry most of 0071's cross-claims); 0073/0074/0075/0076 only at the points 0071 cites them.


### ADR 0072 — `ambient-scope-adoption-seam`

**13 findings, 8 at or above threshold 60. 0 Critical · 2 High · 6 Medium · 5 Low.**

##### Ranked list

| # | Score | Finding |
|---|---|---|
| 1 | 78 H | Step 4's translated `ConfigurationException` **is** wrapped a second time on the four transform-builder catches |
| 2 | 70 H | The Decision's bold central property is false of ladder rows 1, 2 and 4 |
| 3 | 68 M | ADR 0076 gives a different mechanism for FR-19 and cites ADR 0072 for it |
| 4 | 63 M | "Everything `AddBrighter` and `AddConsumers` register is `Singleton` or `Transient`" is false |
| 5 | 62 M | The transaction consequence is argued at full length in both step 2 and *Positive* |
| 6 | 61 M | `TryAddScoped` is justified by a reason that does not hold; the one that does is missing |
| 7 | 60 M | Row 2 is `OWNED` in the pseudo-code and explicitly not `OWNED` in the table |
| 8 | 60 M | Step 2 is a 90-line sub-document with 12 bolded lead-ins, one of them step 5's subject |
| 9 | 50 L | "both `PipelineBuilder` filters now also exclude the new type" prescribes a dead edit |
| 10 | 48 L | Six requirement IDs bolded in `## References` with no key |
| 11 | 45 L | The `ScopeAffinityPolicy` ```csharp block is not compilable C# |
| 12 | 45 L | `_scopedInstances` placed on the handle rather than on `ServiceProviderLifetimeScope` |
| 13 | 40 L | Risks row drops ADR 0071's non-`Singleton` qualifier |

---

#### 1. Step 4's translated `ConfigurationException` is wrapped a second time — on four of the six catch sites (78)

Step 4 says the borrowed-`Create` path translates an `ObjectDisposedException` into a `ConfigurationException` naming the cause, that one site covers every caller because all five factories reach the borrowed provider through the handle, and that it reaches the caller as thrown. That holds only for the handler family. The four transform-builder catches have **no exception filter at all**, so a `ConfigurationException` raised inside a `Post`'s build is caught by `catch (Exception e)` and re-thrown wrapped in a second `ConfigurationException`. The message naming the disposed ambient reaches a `Post` caller only as `InnerException.Message` — the degradation the translation exists to avoid, on the family where `ScopedArtefactCache` and FR-16(a) live.

**Evidence**: `0072:639` — "Both `PipelineBuilder` `catch` filters exclude `ConfigurationException`, so it reaches the caller as thrown rather than being wrapped a second time", one sentence after "One site covers every caller, because all five factories reach the borrowed provider through this handle". Against `TransformPipelineBuilder.cs:116` — `catch (Exception e)`, no filter — and `:124` `throw new ConfigurationException("Error building wrap pipeline for outgoing message, see inner exception for details", e);`, plus `:157`/`:165`, and the identical lines `116-125` and `157-166` in `TransformPipelineBuilderAsync.cs`. All four opened. The ADR contradicts itself: `0072:484` says the transform builders wrap "without even a filter".

**Recommendation**: replace the sentence, do not qualify it. `0072:639`: *"`PipelineBuilder`'s two filters exclude `ConfigurationException`, so a `Send` caller sees it as thrown. The four transform-builder catches carry no filter, so a `Post` caller sees it as the inner exception of the builder's own `ConfigurationException`."* If that outcome is unwanted, the fix belongs at step 1b instead — the transform catches need a `ConfigurationException` exclusion in the same commit as the `AmbientScopeSourceException` clause — and `:639` then becomes true as written. Either way correct step 1b, which is the source that generates the claim.

#### 2. The Decision's central property is false of three of the ten rows it is read off (70)

Row 1 returns `null` and creates no scope. Row 2 returns a handle the ADR insists is "not an FR-27 pipeline scope". Row 4 rethrows and produces no scope. Only rows 3 and 5–9 end at create-and-own. The following sentence gets it right — it enumerates exactly six failures — so the bold sentence is broader than its own gloss.

**Evidence**: `0072:155` — "**Every path that is not borrowed ends at create and own a scope, which is exactly today's behaviour.**" Against `0072:142` row 1 ("`null`: this factory offers nothing"), `0072:143` row 2 ("**a handle, but not an FR-27 pipeline scope**"), `0072:145` row 4 ("the **original** is rethrown **unwrapped**").

**Recommendation**: *"**Every row that declines an ambient ends at create and own a scope, which is exactly today's behaviour.**"* Leave the six-failure sentence unchanged. No qualifier, no second sentence.

#### 3. ADR 0076 gives a different mechanism for FR-19 and attributes it to this ADR (68)

**Evidence**: `0072:40` — "The pump publishing no per-message ambient (D0b, OOS-1) is not what makes this true and is not offered as the reason: it would leave a `Dispatcher` started from inside a live request free to inherit an `HttpContext`". Against `0076:47` — "**FR-19 …** The mechanism is the pump publishing no per-message ambient (D0b, C-2, **ADR 0072**)." **The one I verified as correct is 0072**: `0075:47` says "The pump-flow bracket of step 4a is the mechanism that makes it true. **ADR 0072 discharges FR-19** and names this bracket"; `requirements.md:388` (C-14) concedes it is "an assumption, not a verified invariant"; `0073:89` and `0073:301` route the consumer case through 0075's bracket.

**Recommendation**: the edit is in ADR 0076. Replace `0076:47`'s first sentence with *"The mechanism is ADR 0075's pump-flow bracket, which ADR 0072 names as what discharges FR-19."* Nothing in 0072 changes.

#### 4. "Everything `AddBrighter` and `AddConsumers` register is `Singleton` or `Transient`" is false (63)

`AddProducers`, in the same package, takes `ServiceLifetime serviceLifetime = Transient` and uses it verbatim for four descriptors; an application passing `Scoped` gets container-`Scoped` transaction- and connection-provider registrations made by Brighter's own extension. `UsePublicationFinder<T>` is the same shape. The conclusion survives; the premise does not, and `0072:592` already knows better ("registered `Transient` **by default**").

**Evidence**: `0072:587` — "**Everything `AddBrighter` and `AddConsumers` register is `Singleton` or `Transient`**, and neither package registers a single container-`Scoped` service today". Against `ServiceCollectionExtensions.cs:250` and `:386` consumed at `:289`, `:290`, `:425`, `:835`, `:840`; and `:513`/`:516`. `requirements.md:390` (C-21) cites the same two defaults.

**Recommendation**: *"**Brighter registers no container-`Scoped` service of its own; `ScopedArtefactCache` is the first.** Every registration `AddBrighter` and `AddConsumers` make is `Singleton` or `Transient`, and the transaction- and connection-provider lifetimes `AddProducers` takes as an argument default to `Transient`."*

#### 5. The transaction consequence is argued at full length in two sections (62)

**Evidence**: `0072:594` — "Under `AlwaysNew` a handler resolves from a scope Brighter created … silently, since the deposit succeeds and only atomicity is lost. … That is FR-16(c), the mechanism is C-21, and AC-52 measures it through a rollback with an `AlwaysNew` negative control." Against `0072:654` — "Under `AlwaysNew` it does not, and nothing says so — the deposit succeeds and only atomicity is lost, which is why C-21 records the silence and AC-52 pins it with a rollback and an `AlwaysNew` negative control." 90 and 150 words; neither summarises the other.

**Recommendation**: keep the *Positive* bullet whole and cut `0072:592-596` to *"Brighter's relational transaction providers wrap a `DbContext` taken by constructor injection (`MsSqlEntityFrameworkCoreTransactionProvider.cs:18`), and the provider is `Transient` while the `DbContext` is `Scoped`, so the borrowed scope decides which transaction a handler joins. *Consequences* states what that is worth."* The outbox-boundary paragraph at `:596` restates C-21(i)–(iii) and goes with it.

#### 6. `TryAddScoped` is justified by a reason that does not hold, and the reason that does is missing (61)

With plain `AddScoped`, an application registering `ScopedArtefactCache` as a `Singleton` afterwards wins resolution as the last descriptor, so AC-37 clause 2's control works either way. The real reason is that `BrighterHandlerBuilder` runs **twice** in a mixed `AddBrighter` + `AddConsumers` host, which is why every other registration in that method is a `TryAdd`.

**Evidence**: `0072:407` — "`TryAddScoped` rather than `AddScoped` is what lets AC-37 clause 2's positive control register the same type `Singleton`". Against `requirements.md:742`, which fixes no ordering and permits "or replaced by a test double that never releases"; and `ServiceCollectionExtensions.cs:149-215` (all `TryAdd*`), reached twice via `ServiceActivator…/ServiceCollectionExtensions.cs:64` and `:131`.

**Recommendation**: *"`TryAddScoped` matches every other registration in `BrighterHandlerBuilder`, which runs once per registration entry point and therefore twice in a host that calls both `AddBrighter` and `AddConsumers`."* The AC-37 point already lives in the *Negative* bullet at `0072:663`.

#### 7. Row 2 is `OWNED` in the pseudo-code and explicitly not `OWNED` in the table (60)

**Evidence**: `0072:143` — "**a handle, but not an FR-27 pipeline scope**"; `0072:159` — "rows 3–10's `OWNED` is reserved for one that is". Against `0072:530-531` — "`2. if Scoped does not participate in this pipeline  -> return an OWNED handle, … make NO ask [FR-27.1]`". `0071:369` takes the table's side. An implementor coding from the pseudo-code produces the conflation the prose warns against, and the FR-27.1/AC-46 amendment turns on it.

**Recommendation**: change the pseudo-code only. `0072:530` → `-> return ADR 0067's per-resolution handle,` / `   make NO ask   [FR-27.1]`.

#### 8. Step 2 is a ninety-line sub-document with twelve bolded lead-ins, one of them step 5's subject (60)

**Evidence**: `0072:516-605`; bolded lead-ins at `:516`, `:564`, `:569`, `:575`, `:581`, `:587`, `:588`, `:598`, `:600`, `:602`, `:604` — eleven inside one numbered step, against one each for steps 1, 1a, 5, 6. `0072:604` ("**The registration model for `IAmAScopeProvider`.**") against `0072:641` ("**5. Registration.**"). Measured: *Implementation Approach* carries 29 of the document's 95 mid-prose bold runs in 140 of 729 lines.

**Recommendation**: sub-number, do not rewrite. `2` keeps the protocol; `2a` the probe (`:564-573`); `2b` rows 8/9 and provenance (`:575-583`); `2c` what borrowing does to registrations (`:585-596`); `2d` the residue (`:598-602`). Move `:604` whole into step 5. No sentence changes and no bold is added; eight lead-ins become headings and stop being emphasis.

#### 9. "Both `PipelineBuilder` filters now also exclude the new type" prescribes a dead edit (50)

A clause placed **ahead** matches first, and an exception thrown from inside a catch block leaves the whole `try` rather than entering a sibling clause. **Evidence**: `0072:512` against `0072:510` ("**Ahead of** each existing wrapping `catch`") and `PipelineBuilder.cs:202`/`:248`. **Recommendation**: delete the clause from `:512`; if wanted as defence against reordering, that is a reason and belongs in *Alternatives Considered*.

#### 10. Six requirement IDs bolded in `## References` with no key (48)

**Evidence**: `0072:713` — "FR-16/FR-16(a)/**FR-16(c)** … **C-14**, C-15, **C-17**, **C-21** … **AC-20** … **AC-52**". Nothing says what the bold means. **Recommendation**: remove the six bold runs.

#### 11. The `ScopeAffinityPolicy` block is labelled `csharp` and is not compilable C# (45)

**Evidence**: `0072:355-361` declares a `class` whose constructor and two methods end in `;` — `CS0501`. The other two ```csharp blocks (`:276-286`, `:316-319`) are complete interfaces. **Recommendation**: give the members `=> throw new NotImplementedException();` bodies, or drop the block — the contract table at `:368-372` carries every fact in it.

#### 12. `_scopedInstances` placed on the handle rather than on `ServiceProviderLifetimeScope` (45)

**Evidence**: `0072:400` — "a private field of the handle (`ServiceProviderLifetimeScope.cs:49`)". Against `ServiceProviderLifetimeScope.cs:42` and `:49`. `0072:466` gets it right. **Recommendation**: "…a private field of the `ServiceProviderLifetimeScope` the handle owns".

#### 13. Risks row drops ADR 0071's non-`Singleton` qualifier (40)

**Evidence**: `0072:683` — "it is rejected at `Create`". Against `0071:8` — "a **non-Singleton** `Create` that is handed no usable handle throws `ConfigurationException`" — and `ServiceProviderHandlerFactory.cs:66`/`:84`, where the `Singleton` arm resolves from `_singletonScope`. **Recommendation**: "…a non-`Singleton` `Create` rejects it."

---

#### Verified CLEAN — do not re-derive

**Probes run (four `dotnet` projects; all confirmed the ADR rather than falsifying it):**

- **`netstandard2.0` API surface.** `ConcurrentDictionary<Type, Lazy<object?>>.TryRemove(KeyValuePair<…>)` **fails to compile** on `netstandard2.0` (`CS7036`) and compiles on net8/9/10 — the ADR's claim at `0072:428` is exactly right. The `((ICollection<KeyValuePair<Type, Lazy<object?>>>)_cache).Remove(pair)` form the ADR specifies **builds clean on all four targets with no `#if`**.
- **`src/Directory.Build.props:43`** is `<BrighterTargetFrameworks>netstandard2.0;net8.0;net9.0;net10.0</BrighterTargetFrameworks>` and the DI csproj uses it — "one of the DI package's four targets" is correct. `ServiceProviderLifetimeScope.cs:507-508` really does document an equivalent `netstandard2.0` gap (`ReferenceEqualityComparer`).
- **`ScopedArtefactCache` concurrency contract — every clause confirmed.** Default-mode `Lazy` caches the fault and rethrows on every read. `EqualityComparer<Lazy<object?>>.Default` is reference equality (`Equals(object)` is `System.Object`'s), so pair removal evicts only the observed instance and a losing waiter's repeat is a no-op. Key-only `TryRemove` **does** delete a healthy replacement (reproduced). Eight concurrent waiters each observe the fault and would each attempt eviction. `GetOrAdd`+`Lazy` yields exactly one factory run under 16 concurrent first-resolvers.
- **`AmbientScopeProbe`'s design works, on MS DI and on Autofac.** On a live scope, `GetService(IServiceScopeFactory)` is non-null with no descriptor and `ScopedArtefactCache` resolves once per scope. On a **disposed** scope both throw `ObjectDisposedException` — FR-23's case is detectable. On a container `AddBrighter` never registered into, `IServiceScopeFactory` resolves but `ScopedArtefactCache` is **null** — the cache test discriminates registrations exactly as claimed. **`0072:583`'s Autofac claim is confirmed by probe**: an `AutofacServiceProviderFactory`-built provider passes both tests including the cache test, gives one cache per scope, and throws `ObjectDisposedException` after disposal.

**Citations opened and correct** (every one in the remit): `BrighterOptions.cs:20/:37/:52/:69/:72`; `ServiceProviderMapperFactory.cs:44-45`/`:45`; `ServiceProviderMapperFactoryAsync.cs:45-46`; `ServiceProviderTransformerFactory.cs:44-45`; `ServiceProviderTransformerFactoryAsync.cs:45-46`; `ServiceProviderHandlerFactory.cs:49-50`/`:50`/`:67-68`/`:85-86`; `ServiceProviderLifetimeScope.cs:49`/`:152`/`:163-178`/`:185`/`:507-508`; `PipelineBuilder.cs:187`/`:190`/`:193`/`:202`/`:202-205`/`:232`/`:235`/`:248`/`:248-251`/`:269-270`; `TransformPipelineBuilder.cs:116-125`/`:157-166`/`:180`; `TransformPipelineBuilderAsync.cs:116-125`/`:157-166` — **the "at identical lines in both files" claim is literally true**; `CommandProcessor.cs:481`/`:601`/`:795`; `ServiceCollectionExtensions.cs:119`/`:142`/`:201-220`/`:410`/`:420`/`:428`/`:431`/`:484`/`:487`/`:648`/`:700`; `MsSqlEntityFrameworkCoreTransactionProvider.cs:18`; `src/Directory.Build.props:43`. Step 1a's filter-spelling difference (`e is not ConfigurationException` at `:202` vs `!(e is ConfigurationException)` at `:248`) is real.

**Counts recounted and correct**: five container-backed factories (source-enumerated); four transform factories + the handler factory; six builder catch blocks / six unwrap sites; three new core types; three latches; six converging failures (as scoped); four registration entry points, all routing through `BrighterHandlerBuilder` with `:119` forwarding to `:142`; four `using var builder` dispatch sites in `CommandProcessor.cs` (`:317`, `:394`, `:472`, `:575`); "five further invariants" is five; the four EF Core sibling providers (MySql, PostgreSql, Sqlite, MongoDb) all take `T context` by constructor injection. ADR 0070's "seventeen of the eighteen classes are public" — recounted by multi-line scan: 17 public + 1 internal (`ServiceProviderLifetimeScope`), correct.

**Diagrams**: all three mermaid blocks render with `mmdc` (`:113-136` sequence, `:179-215` flowchart, `:221-249` classDiagram). The flowchart rendered to PNG at 1600px and read — legible, one subgraph per assembly, and it does draw the `lifescope → cache` edge that `0072:633` reads off it. No `;` in message text, no `<`/`>` in labels.

**Markup**: `grep -c '&lt;\|&gt;\|&amp;'` = 0. No `<see cref>`/`<c>` left in prose. Every backtick balanced. All fourteen tables have consistent pipe counts row-to-row.

**Cross-ADR claims verified as consistent**: 0070's `CleanUpQuietly` lift and first-non-null routing; 0070 step 7a carries the #4260 break as a one-line *Behavioural, ADR 0072* pointer, exactly as `0072:666` says; 0071 records the FR-27.1/AC-46 amendment beneath its contract table (`0071:369-372`) on the same footing as the AC-14 change (`0071:595`), exactly as `0072:161` says; 0071 removes the handler-factory dictionary rather than keeping it; 0075 owns `AmbientScopeSuppression`, the three brackets and `Performer.Run()` step 4a, and states the affinity line **verbatim** identically to `0072:165`; 0073 confirms its provider returns nothing on an `AlwaysNew` ask and keeps `GetAmbient`; 0076 supplies `DefaultScopeAffinity`, its `AlwaysNew` default and `ConsumersOptions` inheritance.

**Requirement attributions checked and accurate** (not adding force the requirement lacks): FR-10's D11/D16/D17 division; FR-11(a)/(b); FR-12; FR-13's borrowed-scope routing to FR-12; FR-16(b)/(c); FR-18; FR-21/AC-26; FR-23; FR-24.1–24.4 and the FR-24.4 → FR-23 → FR-24.2 order; FR-26; FR-27.1/27.2/27.3; NFR-2, NFR-4, NFR-6 ("budgets DI scopes" — correct), NFR-7, NFR-8; C-1, C-7, C-14, C-17, C-19, C-21; OOS-1/3/4/7/10; AC-5, AC-8, AC-11, AC-13, AC-17, AC-22.3, AC-29, AC-30, AC-31, AC-32, AC-35, AC-37, AC-38, AC-46, AC-52. `ScopeAffinityPolicy`'s two contract rows are exact restatements of FR-27.2 over the two participating sets.

**Readability measured** (D8 baseline for this ADR): **0 blocks over 200 words**; **11 blocks 151–200 words** (largest 197, at `:409`); **95 mid-prose bold runs**, **58 bold at bullet leads**; 729 lines. The two dense spots are `### Implementation Approach` (29 prose-bold) and `### Scope` (18 bullet-lead bold).

**Deliberate, disclosed divergences from the requirements — filed as clean, not as findings.** The ADR explicitly owes and names three amendments rather than hiding them: FR-27.1/AC-46's instrument (`0072:161`), FR-19/AC-20/C-14's count going from one warning to zero under 0075's pump bracket (`0072:171`), and AC-30's missing `Post` branch (`0072:514`). Each states the substance and the reason. Checked all three against `requirements.md:250`, `:289`, `:388`, `:515`, `:670`, `:809`; the ADR's characterisations are accurate.

#### Gaps this remit could not cover

- **ADR 0074's half of FR-24.3.** 0072 fixes the registration model and routes the evaluation site to 0074; 0072's and 0073's ends were read, 0074's site/message/AC-32 discharge were not audited.
- **ADR 0075's three brackets as a mechanism.** 0072's single line and 0075's are word-identical and 0075 owns `Performer.Run()`, but whether `AsyncLocal` suppression survives `Parallel.ForEach`'s per-worker `ExecutionContext` restore (NFR-4) was not tested — 0075's reviewer owns it and it is load-bearing under both.
- **ADR 0076's write-through.** Finding 3's fix lands in 0076; the rest of that ADR was not reviewed.
- **The `AmbientScopeSourceException`/`ExceptionDispatchInfo` path end to end.** The six catch sites and the filters were verified by reading, and finding 1 by reading; no harness was built that actually throws through a `PipelineBuilder` and a `TransformPipelineBuilder`, because neither `IAmAScope` nor `CreatePipelineScope()` exists in `src/` yet — every type this ADR touches is still Proposed. Re-run finding 1 as an executable test on an implementation branch.
- **Guidance-page obligations (FR-25, NFR-8).** The ADR says NFR-8 "is discharged where this package documents its types" without naming an owner; which sibling claims it was not chased.


### ADR 0073 — `aspnet-core-request-scope-package`

**13 findings, 9 at or above threshold. 0 Critical, 2 High, 7 Medium, 4 Low.**

| # | Score | Finding |
|---|---|---|
| 1 | 74 High | The `netstandard2.0` rejection rests on a false fact: the ASP.NET HTTP packages ship a serviced **2.3.x** line, not "the end-of-life 2.2.x line" (stated twice) |
| 2 | 70 High | Step 4a puts the new test project on `net8.0;net9.0;net10.0`; **no test project in the repository targets `net8.0`**, and the departure is unnamed |
| 3 | 66 Med | `Scope`'s `It serves …` line: eight IDs, no mechanism, no owner — mixes sibling-discharged, self-discharged and non-preclusion, and includes FR-23 which the ADR says does not arise here |
| 4 | 64 Med | "the only one an application author has to touch anything to use" is contradicted by this ADR's own `Positive` bullet and by AC-35 |
| 5 | 64 Med | Alternative 9's "decisive" ground contradicts the section headed *Why no criterion forces the two types to be public* |
| 6 | 62 Med | Frontmatter summary's first sentence is **106 words** — longest in the set by 23 |
| 7 | 62 Med | FR-19 is a *consumer-side* requirement, cited twice as a whole-host log budget for a web host |
| 8 | 62 Med | Both length claims in the rejected-candidates table are wrong (27 vs 27 characters) |
| 9 | 60 Med | The Decision carries a third, bold-led, 122-word paragraph on a topic that already has its own section and alternative |
| 10 | 55 Low | "all under `tests/Paramore.Brighter.Extensions.Tests/`" over-reaches AC-14's whole-suite half |
| 11 | 52 Low | The class diagram drops the `?` from `GetAmbient`'s return, which the whole mechanism turns on |
| 12 | 50 Low | Prose gives one of ADR 0076's **two** registrar obligations; the constructed-instance half is only in the code sample |
| 13 | 45 Low | "The entry appears in the project's `runtimeconfig.json`" — a class library produces none |

---

###### 1. The `netstandard2.0` rejection rests on a false fact about the ecosystem (Score: 74)

*Technology Choices* states that on `netstandard2.0` the extension "would need `PackageReference`s to **both** `Microsoft.AspNetCore.Http.Abstractions` … and `Microsoft.AspNetCore.Http` …, and the only shippable versions of either are the end-of-life 2.2.x line." Both packages have shipped **2.3.0 through 2.3.12** — a servicing line Microsoft still maintains for `netstandard2.0` consumers. The same false fact appears again in `Consequences → Negative`, so it is one defect at two distances. The *decision* survives; the sentence a reader is asked to accept does not, and the phrase "the only implementable choice rather than a preference" three lines above rests on it.

**Evidence**: `0073:383` — "the only shippable versions of either are the end-of-life 2.2.x line". `0073:454` — "The alternative was a dependency on the end-of-life `Microsoft.AspNetCore.Http.Abstractions` 2.2.x line, which is worse." Against `api.nuget.org/v3-flatcontainer/microsoft.aspnetcore.http.abstractions/index.json`, tail `['2.2.0','2.3.0','2.3.8','2.3.9','2.3.10','2.3.11','2.3.12']`; same tail for `microsoft.aspnetcore.http`. **Probe**: a `netstandard2.0` class library with `PackageReference`s to both at `2.3.12` **builds clean** and compiles `services.AddHttpContextAccessor()` and `accessor.HttpContext?.RequestServices`. Both restored packages carry `lib/netstandard2.0`; `Microsoft.AspNetCore.Http 2.3.12` depends on `System.Text.Encodings.Web 8.0.0`.

**Recommendation**: rewrite the bullet's second sentence — *"Both packages are still serviced on a 2.3.x line that targets `netstandard2.0`, but that line is the ASP.NET Core 2.x branch, and taking two dependencies on it to serve a target no ASP.NET Core application uses is a cost with no beneficiary."* Correct the `Negative` bullet at the same time: "end-of-life … 2.2.x line" → "legacy … 2.3.x line". No footnote; the sentence carries the whole claim.

###### 2. Step 4a puts the new test project on TFMs no test project in the repository uses (Score: 70)

Step 4a says the test project is "on the same TFMs as the package" — `net8.0;net9.0;net10.0`. The repository's test convention is a *different* property, `$(BrighterTestTargetFrameworks)` = `net9.0;net10.0`, and **no test project targets `net8.0`**. The ADR devotes a whole bullet with a 24-project precedent to justifying the *package's* departure from `BrighterTargetFrameworks`, then silently prescribes a second, unremarked departure for the test project — adding an ASP.NET Core 8 test leg that exists nowhere else.

**Evidence**: `0073:413` — "on the same TFMs as the package". Against `tests/Directory.Build.props:4` — `<BrighterTestTargetFrameworks>net9.0;net10.0</BrighterTestTargetFrameworks>`. Recount of every `<TargetFrameworks>` under `tests/`: 34 × `$(BrighterTestTargetFrameworks)`, 2 × `$(BrighterTestNineOnlyTargetFrameworks)` (`net9.0`), 1 × `net10.0`; `grep -rn 'net8.0' tests --include='*.csproj'` returns nothing.

**Recommendation**: replace with "on `$(BrighterTestTargetFrameworks)`, as every test project in the repository is". The package's `net8.0` leg is then covered by compilation only — if the owner wants a host on it, that belongs as a named cost in `Consequences → Negative`, not as a silent TFM choice in `Implementation Approach`.

###### 3. `Scope`'s `It serves …` line names eight requirements with no mechanism and no owner (Score: 66)

The house style requires **In scope** to carry "one bullet per FR/NFR it discharges, each naming the mechanism". `0073` gives three bullets, then a bare list of eight IDs conflating: five a *sibling* discharges (FR-10, FR-12, FR-16, FR-18, FR-23 — all five are In-scope bullets of ADR 0072, with mechanisms); two this ADR **itself** discharges and says so in its own body (NFR-2, FR-25.11's three-gestures clause); and one non-preclusion clause (NFR-7). ADR 0076 has the identical construction and names the owners. FR-23 also sits in a list headed "It serves" while `0073:320` says "FR-23's condition does not arise for this package".

**Evidence**: `0073:51`. Against `0076:43` — "It serves … **Each of those is discharged elsewhere, by the mechanism that makes it true**: FR-16, FR-18, FR-19 and FR-23 by ADR 0072, and FR-20 by ADR 0070." Against `0072`'s `Scope`, which carries FR-10, FR-12, FR-16, FR-18, FR-23 with mechanisms — **0072 verified as correct**. Against `0073:178` ("That is the whole of NFR-2") and `0076:495` ("The three gestures themselves are ADR 0073's").

**Recommendation**: promote the two into `In scope` bullets with mechanisms — *"**NFR-2 — the DI package gains no ASP.NET dependency.** Every edge from this package runs downward, to the DI package and to core."* and *"**FR-25.11's three-gestures clause** — the guidance page states the three gestures; step 5 gives them."* Then give the residue 0076's closing clause verbatim. Naming 0072 as FR-23's owner is what removes the clash with `0073:320` — no qualifier needed.

###### 4. "the only one an application author has to touch anything to use" is false of the set (Score: 64)

ADR 0076 adds `ScopeAffinity DefaultScopeAffinity` to the public `IBrighterOptions`, and an application opting in **without** this package must set it — AC-35's console host. ADR 0072 delivers `IAmAScopeProvider`, which AC-35's host implements and registers itself. This ADR says as much two hundred lines later.

**Evidence**: `0073:57`. Against `0073:447` — "another package registers its own `IAmAScopeProvider` and its own `ScopeAffinityOverride` in exactly the same two lines". Against `requirements.md:693` (AC-35) — "a test-assembly `IAmAScopeProvider` … registered in a console host … and **the affinity option `JoinAmbient`**". Against `0076:257` — the property on a public options type.

**Recommendation**: "This is the fourth, and the only one that ships an application-facing registration gesture." Same length, true of the set.

###### 5. The "decisive" ground for public visibility contradicts the section that examines it (Score: 64)

The ADR devotes a section to establishing that **nothing requires** the two types to be public: AC-19's log assertion "a test satisfies with `typeof(HttpContextScopeProvider)` **because it can, not because it must**"; AC-18's recorder "builds against an internal provider without difficulty"; AC-29 is not one of this ADR's criteria; NFR-7 is non-preclusion. Alternative 9 then rejects `internal` "on four grounds, and **the first is decisive**" — the first being test-project reach. The ADR names no test that would fail. The Decision's third paragraph carries the same claim.

**Evidence**: `0073:317`, `:319`, `:321` against `0073:489` and `0073:108`.

**Recommendation**: demote the first ground. "Rejected on four grounds, and the second carries the weight", with ground 1 rewritten as *"`internal` costs the package's own unit tests the ability to construct either type directly, and no assembly in this solution grants `InternalsVisibleTo`."* Fix the Decision at the same distance — or delete it there per finding 9.

###### 6. The frontmatter summary's first sentence is 106 words (Score: 62)

The *Sentence construction* rules are not scoped to the body: "One idea per sentence, and no more than about 25 words." The sentence carries at least six ideas, two em-dash asides and a three-clause "so that" tail. Four siblings split their summaries into six or seven sentences whose longest runs 29–44 words.

**Evidence**: `0073:8`. Measured across the set: 0070 141w/6/33w · 0071 179w/7/44w · 0072 100w/4/38w · **0073 124w/2/106w** · 0074 86w/4/29w · 0075 120w/2/83w · 0076 174w/7/36w.

**Recommendation**: split into four sentences — what ships and where; targets and the `FrameworkReference`; the one gesture and its signature; what the reference alone does and what a host with no `HttpContext` gets. The accessibility aside belongs in the Decision, not in a compression of it.

###### 7. FR-19 is a consumer-side requirement, cited twice as a whole-host log budget (Score: 62)

FR-19 is titled "The flag is inert on **the consumer side**" and its bound is "the difference between the two settings … for the life of the host, **however many messages are consumed**". This ADR applies it to an ASP.NET web host's hosted service, background thread and startup, and converts "the difference between the two settings" into "in total" — a different claim that FR-24.4's diagnostic would falsify in a host with a non-conforming third-party provider. What actually bounds the count is FR-24's diagnostic model and D19's per-container-per-provider-type latch, which the ADR cites correctly a sentence earlier.

**Evidence**: `0073:89` and `0073:446`. Against `requirements.md:289` (FR-19).

**Recommendation**: at both distances cite what bounds the count and drop FR-19. `0073:89` becomes: "The only difference in this case is one latched `Warning` — once per Brighter container per provider implementation type (FR-24's diagnostic model, D19). FR-23's *ambient offered but unusable* entry does not arise here." Shorter than what it replaces.

###### 8. Both length claims in the rejected-candidates table are wrong (Score: 62)

`AddBrighterHttpRequestScope` is **27** characters and `AddBrighterAspNetCoreScopes` is **27** — exactly equal, not "one character shorter", and therefore joint-longest, not "longest of the candidates". The rationale survives (and is strengthened), but two checkable facts in a table are false.

**Evidence**: `0073:348` and `0073:347`. Recounted: 27 · 27 · `AddBrighterScopeAffinity` 24 · `AddBrighterAmbientScope` 23 · `UseBrighterRequestScope` 23 · `AddBrighterRequestScope` 23.

**Recommendation**: two cell edits — "exactly as long as the name being replaced, so it does not fix the complaint that prompted the rename", and "joint-longest of the candidates".

###### 9. The Decision carries a third, bold-led, 122-word paragraph on a topic with its own section (Score: 60)

The house style fixes `## Decision` as "the decision in **one bold sentence**, then one short paragraph on the shape it takes", and names it as the model for the whole emphasis rule. `0073`'s Decision is the longest in the set at 228 words and is the only one of the seven whose third paragraph *opens* with a bold sentence that is not the decision. It argues type accessibility, which already has `#### Why no criterion forces the two types to be public` and `Alternatives Considered` 9 — both of which it points at.

**Evidence**: `0073:102-108`. Measured: 0070 80w/2 ¶ · 0071 68w/2 · 0072 134w/2 · **0073 228w/3** · 0074 176w/3 · 0075 168w/3 · 0076 131w/2. `0073` and `0074` are the only two with more than one bold run, and `0074`'s second is mid-sentence, not a paragraph lead. Against `.agent_instructions/documentation.md:78` and `:118`.

**Recommendation**: delete the paragraph. Everything in it is already in the later section or in Alternative 9. A removal, not a rewrite — and it closes finding 5's second distance too.

###### 10. "all under `tests/Paramore.Brighter.Extensions.Tests/`" over-reaches AC-14's whole-suite half (Score: 55)

On the natural reading the trailing modifier scopes the whole list, and the existing suite for `Send`/`Publish`/`Post` lives in `Paramore.Brighter.Core.Tests` and elsewhere. The plural "those projects" two clauses earlier and again two sentences later makes the singular directory incoherent with its own surroundings.

**Evidence**: `0073:417` against `requirements.md:512-513`. All five named files *do* live in that project (`FactoryLifetimeTests.cs:36` and `:154`, plus the three `When_…` files) — the ADR is correct about those five; only the scope of "all" is wrong.

**Recommendation**: "…with its named exclusions and its named non-excluded pair, all five of which sit in `tests/Paramore.Brighter.Extensions.Tests/`."

###### 11. The class diagram drops the nullability the mechanism turns on (Score: 52)

The prose contract is `IAmAScope? GetAmbient(ScopeAffinity affinity)` and the sequence diagram's three `null` returns are what the section's three invariants are read off. The class diagram renders `+GetAmbient(ScopeAffinity) IAmAScope` on both `IAmAScopeProvider` and `HttpContextScopeProvider`.

**Evidence**: `0073:203`, `:207` against `0073:97` and `0073:144`. Both diagrams render (`mmdc` exit 0; PNGs produced and read) — this is content, not a render failure.

**Recommendation**: two label edits to `IAmAScope?`. Mermaid renders a trailing `?` without escaping.

###### 12. The prose gives one of ADR 0076's two registrar obligations (Score: 50)

`0073` says "plain `AddSingleton`" and Alternative 11 rejects `TryAddSingleton`. ADR 0076 states **two** obligations: plain `AddSingleton`, **and** of a constructed instance rather than a factory delegate — because ADR 0074's rule reads affinity values off `ImplementationInstance` without resolving, so `sp => new ScopeAffinityOverride(a)` works at run time and is invisible to validation. `0073:255` does the right thing; the prose does not say why.

**Evidence**: `0073:305` (both reasons given are about `TryAdd`) against `0076:338` — **0076 verified as correct**; 0073 is incomplete rather than wrong.

**Recommendation**: put it at the section lead, not mid-paragraph. "**Two calls to the extension resolve to the last one, a conflicting repeat is reported, and both depend on the override being a plain `AddSingleton` of a constructed instance.**" Then add "a factory delegate would leave the value unreadable to ADR 0074's rule" to the list already there.

###### 13. "The entry appears in the project's `runtimeconfig.json`" (Score: 45)

The bullet says the reference flows to "any project", then that the entry appears in "the project's" `runtimeconfig.json`. A class library produces none; the entry appears in the *application's*. The concluding example (worker service, console producer) is an executable, so the deployment consequence holds — the intermediate step does not.

**Evidence**: `0073:453`. **Probe**: the packed library's nuspec carries `<frameworkReferences>` for all three TFMs, and a console EXE referencing it emits `Microsoft.AspNetCore.App` `10.0.0` into `app.runtimeconfig.json` — the transitivity claim is confirmed. The library's own `bin/.../net10.0/` holds `lib.dll`, `lib.pdb`, `lib.deps.json` and **no `.runtimeconfig.json`**.

**Recommendation**: one word — "The entry appears in the **application's** `runtimeconfig.json`".

---

#### Verified and CLEAN — do not re-derive

**Probes that confirmed rather than falsified**:
- A `Microsoft.NET.Sdk` class library on `net8.0;net9.0;net10.0` with `<FrameworkReference Include="Microsoft.AspNetCore.App"/>` builds and reaches both types. **`IHttpContextAccessor` is in `Microsoft.AspNetCore.Http.Abstractions`; `AddHttpContextAccessor` (`HttpServiceCollectionExtensions`) is in `Microsoft.AspNetCore.Http`** — exactly as `0073:389` claims.
- **`FrameworkReference` flows transitively through a NuGet package**: the nuspec carries `<frameworkReferences>` per TFM and no `<dependencies>`; a consuming console EXE gets `Microsoft.AspNetCore.App` in its `runtimeconfig.json`. The *Negative* bullet's core claim is true.
- **`new DefaultHttpContext().RequestServices` returns `null`** (Alternative 10, step 1) ✓. **`HttpContext.RequestServices` has a public setter** ✓. Setting `IServiceProvidersFeature` to null also yields `null` ✓.
- **`AddHttpContextAccessor()` is idempotent**: three calls → one `Singleton` descriptor for `HttpContextAccessor` (`0073:295`) ✓.
- **`Task.Factory.StartNew(…, LongRunning, TaskScheduler.Default)` captures `ExecutionContext`**: a pump-shaped task inherits a live `HttpContext` and its `RequestServices` is reference-equal to the request's scope — the C-14 paragraph is correct. `ExecutionContext.SuppressFlow()` blocks it. Resolving from a disposed scope throws `ObjectDisposedException` (0072's case).

**Citations opened and correct**: `src/Directory.Build.props:43` (`BrighterTargetFrameworks`) and `:45` (`BrighterCoreTargetFrameworks`) · `ServiceCollectionExtensions.cs:65-66` (AddBrighter's `ArgumentNullException` guard — exactly those two lines) · `BrighterOptions.cs:20`, `:37`, `:52`, `:69` · `ServiceCollectionExtensions.cs:119` and `:142` (both `BrighterHandlerBuilder` overload declarations) · `FactoryLifetimeTests.cs:36` and `:154` · `Performer.Run()` and `Dispatcher.Receive()`→`Start()` both reach `Task.Factory.StartNew`.

**Counts recounted and correct**: **24** src projects on `$(BrighterCoreTargetFrameworks)` · **37** test projects under `tests/` (= 37 slnx entries), **none** referencing `Microsoft.AspNetCore.*`, `Mvc.Testing` or `WebApplicationFactory` · **824 public to 91 internal** classes in `src/` (multi-line declaration scan; the 91 is explicit `internal` — there is additionally 1 `file`-scoped and 1 implicit) · **seventeen public to one internal** in the DI package, the one being `ServiceProviderLifetimeScope` (`:42`, `internal sealed partial`) · **eight** ACs needing an ASP.NET host (AC-15/16/17/18/19/34/48/49 — correct *against the stated scope* "this ADR cites or discharges") · **five** constraints, **four** constraints, **three** invariants, **three** things, **four** grounds, **eleven** alternatives, **five** rejected-candidate rows, **six** validation rules — all recounted correct.

**Repository-convention claims verified true**: no `InternalsVisibleTo` attribute anywhere (only a comment at `SpannerBoxMigrationRunner.cs:131`) · no `namespace Microsoft.*` in `src/` · every `Use*` extension in `src/` extends `IBrighterBuilder` (multi-line scan found 12, all of them; the ADR's eight all exist) · `AddBrighter`/`AddConsumers` on `IServiceCollection`, `AddProducers`/`AddControl` on `IBrighterBuilder` · `Paramore.Brighter.DynamoDb` declares `Paramore.Brighter.Outbox.DynamoDB`, `Paramore.Brighter.Archive.Azure` sets `RootNamespace` `Paramore.Brighter.Storage.Azure` (the only `RootNamespace` in `src/`) · `Control.Api` is `Sdk="Microsoft.NET.Sdk.Web"` + `OutputType=Library` + `IsPackable=true` on `$(BrighterCoreTargetFrameworks)`, and no `FrameworkReference` or `AspNetCore` string appears in any `src/*.csproj` · CPM is on and no `Mvc.Testing` entry exists.

**Set-level**: all seven *Where this ADR sits* tables are **byte-identical** in their seven row texts. 0073's `## References` sibling list matches those rows. 0071's `Consequences → Negative` does record the first AC-14 amendment and cross-references 0073's spy split (`0071:595`). 0070 step 7a's ledger holds thirteen breaks = five of 0070's own + eight from **five** siblings — consistent with 0073 contributing none.

**Markup/diagrams**: zero HTML entities; no unbalanced backticks outside fences; no ragged table pipes; the only `<see cref>` is inside the C# fence. All three mermaid blocks render (`mmdc` exit 0); the sequence diagram and class diagram were rendered to PNG and read — both legible, and the sequence diagram's three `null` branches match the prose's three invariants.

**Readability**: 0073 measures **best-in-set** on block length — 2 prose blocks over 150 words, **0 over 200** (0072 has 10 over 150; 0074 has 1 over 200) — and carries the fewest bold runs of the seven (198; mid-prose 145).

#### Gaps

- **NuGet publish dates for the 2.3.x line** could not be retrieved — the registration API host is blocked in this sandbox; only the flat-container version index and the package payloads were reachable. Finding 1 therefore rests on *existence* of 2.3.0–2.3.12 with `netstandard2.0` assets and a successful compile, not on how recently they shipped.
- **No end-to-end ASP.NET host probe.** No `WebApplicationFactory` was stood up to observe a real request scope, so claims about the built-in `HttpContextAccessor` clearing itself at end of request (`0073:320`) are taken from FR-23's own parenthesis rather than independently re-measured.
- **The seven ADRs' `Alternatives Considered` were not reviewed for cross-set duplication** beyond the four that 0073 names.


### ADR 0074 — `lifetime-validation-evaluation-site`

**12 findings, 7 at or above threshold 60. 0 Critical · 3 High · 7 Medium · 2 Low.**

Ranked: 1) keyed descriptors falsify "last descriptor wins" — **80** · 2) exclusion-set inputs don't exist at the registration site — **76** · 3) FR-22.3's rule has no path to the snapshot — **72** · 4) funnel omits the open-generic stage the prose orders against — **64** · 5) `ArtefactExclusionSet` conjunction has no owner for its prefix half — **62** · 6) FR-22.4's message needs a value the ADR says is unreadable — **62** · 7) "Every `T` in the repository is a core type" is false — **60** · 8) keyed registrations missing from the enumerated failure modes — **56** · 9) "three things follow", five bolded — **55** · 10) Contract table covers 2 of 10 types — **50** · 11) `ents` node is a third copy of the type list — **46** · 12) Scope guard list omits AC-40/AC-41 — **44**.

---

##### 1. "The last descriptor for that service type" is not what Microsoft resolves, and a sibling gets it right (80)

0074 states four times that the descriptor Microsoft resolves for a service type is the **last** one registered. A probe falsifies that whenever a **keyed** descriptor exists for that type: `GetService<T>()` resolves the last *unkeyed* descriptor and ignores keyed ones. FR-22.4's second conjunct *is* this reading, so a host with a keyed `IBrighterOptions` registered after `AddBrighter` yields "the effective descriptor is not Brighter's" and a startup-failing `Error` — the direction 0074's own risk table calls "the worst direction for a rule to be wrong in".

**Evidence**: `0074:445` — "the `IBrighterOptions` descriptor Microsoft's container will resolve, which is the **last** one for that service type". Same reading at `0074:307`, `:415`, `:521`. Against `0076:365-369` — `if (services.Any(d => d.ServiceType == typeof(IBrighterOptions) && d.ServiceKey is null)) return;` and `0076:409` — "**The `ServiceKey` clause is load-bearing and must not be simplified away.**", `0076:413` — "A host with a keyed `IBrighterOptions` — a multi-tenant registration, a test fixture — works today". **0076 verified correct.** Probe (MEDI 10.0.0, net9.0): unkeyed `IOpts` first, keyed `IOpts` last ⇒ `descriptors = 2; LAST is keyed=True`, `GetService<IOpts>() resolves => brighter` (the first). `grep ServiceKey` over 0074 returns zero hits.

**Recommendation**: correct the generator at `:307` first — "…taking the last **unkeyed** descriptor where there is more than one, which matches Microsoft's resolution" — then the same one-word narrowing at `:415`, `:445`, `:521`. A narrowing, not an appended exception; the added qualifier is one 0076 already states.

##### 2. Three of the four inputs the registration snippet hands the scope validator do not exist at that point (76)

The `AddSingleton` snippet passes `pipelineBuilder`, `publications`, `subscriptions` to `ArtefactExclusionSet.Build`. All three are **locals of the core validator's `TryAddSingleton` delegate**, and each derives from *that* delegate's `sp` — so unlike `snapshot` none can be "captured above the delegate". The ADR invented `ValidationMapperRegistry` (its tenth type) precisely because two validators must not each build a `MessageMapperRegistry`; the identical question for `PipelineBuilder<IRequest>` is never asked, so the ADR neither authorises a second builder nor arranges a shared one.

**Evidence**: `0074:336-343` — "`ArtefactExclusionSet.Build(pipelineBuilder, sp.GetRequiredService<ValidationMapperRegistry>().Value, publications, subscriptions)`" with only `snapshot` annotated "captured from builder.Services, above the delegate". Against `BrighterPipelineValidationExtensions.cs:71-94`: `pipelineBuilder` at `:75`, `publications` at `:77`, `subscriptions` at `:78`, all inside the lambda opened at `:71`; `ResolvePublications`/`ResolveSubscriptions` are `private static` taking `IServiceProvider` (`:135`, `:144`). And `0074:660` — "an *instance* method on the builder the validation delegate already constructs (`BrighterPipelineValidationExtensions.cs:75`)", which is the **core** validator's delegate. (Verified by opening the file.)

**Recommendation**: an owner call between two shapes; the ADR must pick one rather than leave the snippet naming free variables. Either (a) state in step 5a that the scope validator's delegate builds its own `PipelineBuilder<IRequest>` from `sp` and calls the same two private helpers — the second `Describe()` pass is already priced in the *Negative* startup-cost bullet — and replace the three bare identifiers with those expressions; or (b) add an eleventh holder beside `ValidationMapperRegistry` and say so in the *Negative* bullet that already counts the tenth. The snippet's comment column and step 5a's existing bullet are the two homes; no mid-paragraph sentence.

##### 3. FR-22.3's rule is given no path to the one input the rule is about (72)

The captive-dependency rule's whole question is each constructor parameter's *registration* lifetime, from `ContainerRegistrationSnapshot.EffectiveLifetimeOf`. But the entity it evaluates carries only type, kind and configured lifetime, and neither the roles table nor the class diagram gives `ScopeConfigurationRules` the snapshot as a collaborator. `design_principles.md` makes this first-order: "a responsibility with no collaborator is either self-contained or in the wrong object."

**Evidence**: `0074:521` — "**Each parameter's lifetime** is the `ServiceLifetime` of its descriptor in the snapshot"; `0074:307` lists it as one of the snapshot's three questions "(FR-22.3)". Against `0074:301` (`ScopeConfigurationRules` collaborators: "…the two entity families; `ArtefactConstructorSelector` and `ArtefactExclusionSet`" — no snapshot), `0074:297` (`ArtefactRegistration`: "its type, its `ArtefactKind`, and the configured lifetime that kind selects"), `0074:298` (`ContainerRegistrationSnapshot` collaborators: "`builder.Services` … `DescriptorRecord` and `ArtefactRegistration`, which it yields"). Confirmed visually in the rendered class diagram: `ContainerRegistrationSnapshot` has exactly one incoming edge, from `ScopeConfigurationValidator`, and none to or from the rules.

**Recommendation**: two cell-level replacements, no new prose. End the `ScopeConfigurationRules` Collaborators cell "…`ArtefactConstructorSelector`, `ArtefactExclusionSet`, and `ContainerRegistrationSnapshot`, which it asks for each parameter's registered lifetime", and add `ScopeConfigurationRules ..> ContainerRegistrationSnapshot : asks each parameter's registered lifetime` to the class diagram.

##### 4. The captive-dependency funnel has no open-generic stage, and the prose orders the exclusion against one (64)

The funnel is the artefact the section reads off, and `0074:511` argues an *ordering* — exclusion before the open-generic skip — against a stage the funnel does not contain. The forward reference "the open-generic rule below" points at nothing: the ADR states no such rule.

**Evidence**: `0074:468-481`, whose complete node set is `d → k(marker interface?) → reg → gov(Singleton?) → excl(Brighter's own?) → ctor → parm → warn`. Against `0074:511` — "would also be skipped by the open-generic rule below … The exclusion is applied first … The ordering is chosen so that the rule stays correct if one ever does." The only "below" is `0074:530`, a *failure-mode* row recording an outcome. `grep "open generic\|open-generic"` returns exactly line 511. The ADR's premise about the source is correct and re-derived: `ServiceCollectionBrighterBuilder.cs:254-261` registers open generics via `EnsureHandlerIsRegistered`, and `ExceptionPolicyHandlerAsync<>` is in core (`src/Paramore.Brighter/Policies/Handlers/`), reached because `RegisterHandlersFromAssembly` concatenates `typeof(IHandleRequestsAsync<>).Assembly` (`:104`).

**Recommendation**: add the node to the funnel where the ordering requires — after `excl` — as `gen{"is the implementation type an open generic?"}` with a `yes` branch to `skip["not inspected"]`; change `:511`'s "below" to "above". The funnel is the home; do not restate the rule as prose.

##### 5. `ArtefactExclusionSet` is given the whole conjunction in one place and half of it in another (62)

FR-22.3's exclusion is *attribute-returned* **and** *`Paramore.Brighter`-prefixed assembly*. The roles table gives the type a set built from the attribute halves alone and then has it answer the whole question; the assembly flowchart calls the same type "the attribute half". Nothing says which type applies the prefix test. The risk table names the failure mode but the wrong half of it — as specified the mechanism drops the *prefix* test, not the attribute test.

**Evidence**: `0074:300` — "Holds the set of artefact types returned by a `RequestHandlerAttribute` or `TransformAttribute` `GetHandlerType()`. Answers one question — is this type one Brighter put in the pipeline itself". Against `0074:197` — "ArtefactExclusionSet, the attribute half of FR-22.3's conjunction"; and `0074:475` — `excl{"is the type Brighter's own?<br/>attribute-returned AND Paramore.Brighter assembly"}`. Its only member is `+Contains(artefactType)` (`0074:265-267`). `0074:754` risks "the exclusion is implemented as 'assembly prefix' alone". The requirement is `requirements.md:312`.

**Recommendation**: rewrite the roles cell to state the conjunction the type applies ("Holds the artefact types that are both returned by a `RequestHandlerAttribute` or `TransformAttribute` `GetHandlerType()` **and** defined in an assembly named `Paramore.Brighter` or beginning `Paramore.Brighter.`…"), and replace the flowchart label with "FR-22.3's exclusion conjunction". Two label replacements.

##### 6. FR-22.4's message must name a value the ADR itself says is unreadable on a reachable path (62)

The ADR settles the delegate-registered override for FR-17 and declares that path reachable, but does not settle it for FR-22.4, whose message row requires "the affinity the override carries". On that path FR-22.4's *condition* is still satisfiable (presence is readable) but its message is not fillable — leaving an implementer to choose between an error with a hole and dropping the one error that exists to break a silent total loss of the opt-in.

**Evidence**: `0074:414` — "| FR-22.4 | the affinity the override carries; …". Against `0074:436` — "The uncomparable path is reachable. … **an override registered by factory delegate cannot be compared, and therefore cannot be reported.**" — a paragraph scoped to FR-17. Requirement `requirements.md:317` — "The message must name the affinity the extension registered". `0076:338` makes the instance form an obligation on any registrar, so this is the residual case.

**Recommendation**: FR-22.4 turns on registration shape and is forbidden from comparing values (`0074:449`), so make the value optional rather than gating the rule. Replace the message cell's first clause with "that an affinity override is registered, and its value where the descriptor supplies one"; and replace the closing sentence of `0074:436` so it closes both rules — "FR-17 cannot report such an override, because its key is a value; FR-22.4 still reports it, because its key is the registration."

##### 7. "Every `T` in the repository today is a core type" is false (60)

Offered as evidence in *Technology Choices*. The narrower argument survives, but the sentence is falsified by a shipping package.

**Evidence**: `0074:598` — "Every `T` in the repository today is a core type for that reason: `HandlerPipelineDescription`, `Publication` and `Subscription`…". Against `src/Paramore.Brighter.Validation.Specification/SpecificationRequestHandler.cs:68` — `(ISpecification<TRequest>?)serviceProvider.GetService(typeof(ISpecification<TRequest>))`, where `TRequest` is the application's request type. Recount of closed instantiations in `src`: `HandlerPipelineDescription` 6, `Publication` 4, `Subscription` 10, `TRequest` 1 — the list of three is one short. The surrounding argument at `:597` is correct: `ISpecification<TData>` (`Specification.cs:35`) puts `TData` in both argument and return position.

**Recommendation**: replace with the narrower true statement the argument needs — "Every `T` a core signature names is a core type: `HandlerPipelineDescription`, `Publication` and `Subscription` all live in core, whoever writes the rules over them."

##### 8. Keyed registrations are an unenumerated failure mode of the candidate scan (56 — below threshold)

**Evidence**: `0074:527-538`'s nine rows name no keyed case; the closest, `:531` ("no statically known implementation type"), does not cover a keyed registration, whose implementation type *is* statically known. Probe: with Abstractions 10.0.0 on net8/9/10, `AddKeyedSingleton<IKeyed, KeyedImpl>("k1")` gives `ImplementationType=null, KeyedImplementationType=KeyedImpl`; with 8.0.0 the same read **throws** `InvalidOperationException: This service descriptor is keyed…`. Pinned version is 10.0.10 (`Directory.Packages.props:90`), so the null reading ships.

**Recommendation**: one row in the table that exists for it — "| Artefact registered under a service key | not a candidate — a keyed descriptor reads `null` for `ImplementationType` | Accepted; not a shape Brighter's own registration builders produce |".

##### 9. The FR-22.4 subsection announces three consequences and bolds five (55 — below threshold)

**Evidence**: `0074:447` "Three things follow…", then bold-led paragraphs at `:449`, `:451`, `:453`, `:455`, `:457`. Whole document: 268 bold runs, 64 at bullet leads, **204 in prose**. House style: "A section with bold in every paragraph has emphasis in none of them."

**Recommendation**: keep the bold on `:449`/`:451`/`:453` (the announced three) and remove it from `:455`/`:457`. Lowers the bold count; adds no text.

##### 10. The Contract table covers two of ten types, omitting the one other sections turn on (50 — below threshold)

**Evidence**: `0074:375-378` has only `ScopeConfigurationValidator.Validate()` and `ValidationMapperRegistry.Dispose()`. Against `0074:298` ("the only role in this table that reads the service collection"), the three members at `0074:258-261`, and `0074:532` ("Parameter with no descriptor … not a finding"), which depends on `EffectiveLifetimeOf`'s undocumented no-descriptor answer.

**Recommendation**: three rows in the existing Contract table, one per query.

##### 11. The `ents` node is a nine-item inventory as one box, and the third copy of that list (46 — Low)

**Evidence**: `0074:197`; rendered to PNG it is a sixteen-line column taking roughly a third of the diagram's height. The same nine types appear at `0074:583` and individually at `0074:293-303`.

**Recommendation**: reduce the node body to "the entities, all NEW and internal" and let the roles table carry the list. Shortens the document.

##### 12. The Scope bullet's FR-22 guard list omits AC-40 and AC-41 (44 — Low)

**Evidence**: `0074:38` names AC-27, AC-28, AC-42, AC-50. Against `requirements.md:625` (AC-40) and `:637` (AC-41), both cited later in this ADR at `:367`, `:399`, `:617`.

**Recommendation**: extend the existing list in place — "**AC-27** and **AC-40** for the inert opt-in, **AC-28** and **AC-41** for the mixed triple, …".

---

#### Verified CLEAN — do not re-derive

**Citations opened and correct**: `PipelineValidator.cs:54, :58, :69-71, :85, :92-93, :139, :152, :45-51`. `BrighterPipelineValidationExtensions.cs:58, :64-66, :68-69, :71, :75, :79, :85-88, :91-93, :135-142`. `BrighterValidationHostedService.cs:47, :60, :71, :73, :76, :80, :90-93` (`:84` is the `foreach`, `LogError` at `:86` — the same one-line drift as `ServiceActivatorHostedService:61`, consistent in both). `ServiceActivatorHostedService.cs:45-71, :50, :50-53, :57, :67-70`. `PipelineValidationResult.cs:45, :52, :64`. `Specification.cs:35`; three `Specification<T>` constructors incl. the collapsed one at `:107`; `LastResults` is the only `internal` member (`:125`); rule-body exceptions → `ValidationSeverity.Error` at `:161-166`/`:182-187`; `ValidationResultCollector<TData>` is public. `ValidationSeverity` = exactly `Error=0, Warning=1`. `BrighterPipelineValidationOptions.cs:47`. `ServiceCollectionTransformerResolvabilityProbe.cs:40-56` (a `HashSet<Type>` and a `Contains`, no ctor/lifetime inspection). `ServiceProviderMapperFactory.cs:44`. `MessageMapperRegistry.cs:360-362` (and its remarks do say the guard exists so an owner and the container can both dispose). `RequestHandlerAttribute.cs:91` (`public abstract`). `TransformAttributeBase.cs:5`/`:17`. `PipelineBuilder.cs:151`, `:146-162`. `DescribeTransforms` `:270` (`public static`, `includeAsync`) and the two-arg overload `:255` defaulting `false`. `ClaimCheckTransformer.cs:62`. `ServiceCollectionBrighterBuilder.cs:118-122`. `JustSayingCompressionTransform.cs:34`, `MassTransitTransform:40` — both classes, both genuinely with no declared constructor. All seven `ServiceCollectionSubscriberRegistry` lines (63, 76, 90, 116, 130, 146, 160), all six `ServiceCollectionMessageMapperRegistryBuilder` lines (80, 99, 116, 117, 127, 137), `ServiceCollectionTransformerRegistry.cs:56` — every one registers the artefact as its own service type at `Transient`. `ServiceCollectionExtensions.cs:74, :97`; `ServiceActivator…/ServiceCollectionExtensions.cs:38, :60, :88, :89-90, :127, :199, :201-228`.

**Counts recounted and correct**: **nine** `PipelineValidator` constructor arguments, and the nine named are the nine passed. **Four** `ISpecification<Subscription>` rules in `ConsumerValidationRules` (`:46, :99, :114, :142`), four `AddSingleton` calls (`:201, :207, :213, :215`). **Ten** new types, `ValidationMapperRegistry` genuinely the tenth. **Six** rules = FR-22's four + FR-24.3 + FR-17, three errors / three warnings, matching `requirements.md:347`. FR-25's **eleven** clauses, all mapped. `docs/adr` holds exactly **three** 0053s, **two** 0054s, **two** 0064s. **125** test files register `IBrighterOptions` themselves. AC-22.3's source scan returns **zero** matches today. **No** `InternalsVisibleTo` anywhere (only a comment in `SpannerBoxMigrationRunner.cs:131`). `PipelineValidationResult.Combine` defined at `:64`, called nowhere in `src`. The "**four** bounds" match C-20's four items — note C-20's own *heading* still says "two ways" while its body says "All four"; the ADR follows the body and is right. Six rows = six host combinations.

**Diagrams**: all four mermaid blocks render, `mmdc` exit 0. Class diagram and assembly flowchart rendered to PNG and read; the class diagram is legible and matches the roles table (finding 3 confirmed visually). No `;` in the sequenceDiagram, no `<`/`>` in labels except the valid `<<interface>>`, zero HTML entities, no `<see cref>` in prose, no broken table pipes.

**Probes that confirmed the ADR**: `TryAddSingleton` then `AddSingleton` ⇒ `GetServices` returns both in registration order, `GetService` returns the last — the ADR is right that a plain `AddSingleton` alone would silently lose the core validator's findings. An application registration before `ValidatePipelines()` ⇒ the application's validator + the scope validator, core suppressed by the `TryAdd` — exactly the break at `:371`. An `IEnumerable<T>` parameter with nothing registered injects an empty sequence. A factory-produced singleton is disposed by the container; a disposable created *inside* a factory delegate is not. MS DI's superset failure raises exactly `InvalidOperationException: Unable to activate type 'X'. The following constructors are ambiguous:` as quoted; a widest constructor with an unregistered parameter really does fall back to a narrower one (so "would warn wrongly" is correct); two equal-count constructors really are unactivatable (AC-42's final clause and Alternative 6's fourth count hold). `ValidateScopes` raises `Cannot consume scoped service … from singleton …` (Alternative 6's second count). `IServiceProvider`, `IServiceScopeFactory`, `IEnumerable<T>` all resolve from an empty collection (C-20(i)'s always-resolvable set).

**Set-level**: the *Where this ADR sits* table is identical across all seven ADRs, correctly bolded. `## References` names all six siblings. FR-25/NFR-9 ownership is 0074's alone and `0075:46` explicitly defers to it. 0072 confirms the plain-`AddSingleton` model for `IAmAScopeProvider` and that `AmbientScopeDiagnostics` is its own. `0073:255` confirms the override is registered as an **instance** under plain `AddSingleton`. 0076 confirms `BrighterOptionsRegistration` is internal, carries the descriptor by reference, and is an instance registration. Frontmatter is internally consistent and matches `docs/adr/index.md:112`. Forces bullets carry at most one `file:line` each; Positive/Negative bullets carry none — both house-style rules satisfied.

#### Gaps

- The proposed types were not compiled (they do not exist), so finding 2 is argued from the enclosing method's source rather than a build error.
- `docs/guides/lifetimes-and-scoping.md` and `release_notes.md` do not exist, so step 7's deliverables are unverifiable.
- No real ASP.NET host was exercised; FR-22.4's orderings are reasoned from 0076's mechanism plus container probes.
- 0075 was read only for its FR-25/NFR-9 deferral, and 0070/0071 only for the sibling map and the solid/dotted edge convention.


### ADR 0075 — `publish-subscriber-scope-suppression`

**13 findings; 9 at or above the 60 threshold. 0 Critical · 3 High · 6 Medium · 4 Low.**

| # | Score | Finding |
|---|---|---|
| 1 | 72 | Mixed-host mechanism is false — with `AddBrighter` first, `IBrighterOptions` and `IAmConsumerOptions` do **not** name one `ConsumersOptions` instance (C-12 says so) |
| 2 | 72 | Title, slug and the Decision's single bold sentence all exclude the consumer-pump bracket that all six siblings say this ADR decides |
| 3 | 70 | "three files, **three assemblies** and five shapes" — the five brackets sit in **two** assemblies |
| 4 | 68 | "No bracket is ever established outside a publish" is contradicted three paragraphs later by the pump-flow bracket |
| 5 | 66 | `Dispatcher.cs:484` attributed to `Dispatcher.Receive()`; it is in `private void Start()` and starts the control loop, not a pump |
| 6 | 64 | The 248-byte bracket cost is not a constant (measured 216 B), and AC-23 cannot observe an allocation |
| 7 | 64 | NFR-4's suppression clauses are discharged here and owned nowhere in the set |
| 8 | 62 | Frontmatter `summary` is one 83-word sentence — 3× the house limit, 2× any sibling's worst — and `index.md` republishes it |
| 9 | 60 | Five ⚠-plus-bold caveats against zero in four siblings; highest mid-prose bold density in the set |
| 10 | 58 | Requirements FR-9/AC-11 cite drifted `PipelineBuilder` lines (the ADR's own citations are right) — owed to the true-up |
| 11 | 56 | NFR-3 / AC-22.2 not named as unchanged, though this ADR is the only one touching `Paramore.Brighter.ServiceActivator` |
| 12 | 46 | Pump sequence diagram compresses the real start chain and drops `Consumer` |
| 13 | 44 | "so NFR-5 and NFR-6 hold" attaches an allocation argument to two requirements about scopes |

---

##### 1. Mixed-host mechanism is false (72)

Step 4a's *Why configuration cannot do this and a flow property can* rests on one object carrying both roles' affinity. In the ordering C-12 mandates for every mixed-host criterion — `AddBrighter` before `AddConsumers(Action<ConsumersOptions>)` — `AddBrighter` wins the `TryAddSingleton<IBrighterOptions>` and `AddConsumers`'s own is a no-op. ADR 0076's residue is scoped to the *consumer* `Action` path, not to a mixed host. The conclusion survives; the mechanism does not.

**Evidence**: `0075:378` — "In a mixed host on the `Action` overload, `IBrighterOptions` and `IAmConsumerOptions` name **the same `ConsumersOptions` instance**, which is ADR 0076's stated residue." Against `requirements.md:381` (C-12) — "`IBrighterOptions` and `IAmConsumerOptions` then resolve to *different* objects". Verified in source: `Extensions.DependencyInjection/ServiceCollectionExtensions.cs:74` runs first; `ServiceActivator.Extensions.DependencyInjection/ServiceCollectionExtensions.cs:38` is then inert. `0076:424` scopes its residue to the consumer-only path. AC-20 (`requirements.md:668`) requires the `AddBrighter`-first ordering.

**Recommendation**: replace with the statement true in every ordering — *"Brighter's factories read one `IBrighterOptions` for the whole host, and nothing on it says which side is asking. In a mixed host that object is whichever side won the `TryAddSingleton` (C-12), so the consumer side reads an affinity chosen for the producer."* Correct the source too: the bullet at `0075:375` ("because one `ConsumersOptions` instance carries both roles' affinity") becomes "because both roles read one `IBrighterOptions`". Drop the 0076 residue attribution.

##### 2. Title, slug and Decision sentence exclude the pump bracket (72)

**Evidence**: `0075:3` `title: "Suppressing ambient scope adoption beneath a Publish subscriber"`; `0075:16` same H1; `0075:106` the bold Decision ends "…bracketed twice per subscriber", the pump bracket arriving only in the unbolded `0075:108`. Against this ADR's own row `0075:70` — "how a `Publish` subscriber and the consumer pump **suppress** adoption" — repeated verbatim at `0070:79`, `0071:71`, `0072:73`, `0073:66`, `0074:71`, `0076:70`. `0072:40` and `0072:169` make the pump bracket load-bearing for FR-19. The contradiction is republished at `docs/adr/index.md:113`.

**Recommendation**: minimum fix at both distances — frontmatter `title` and H1 become *"Suppressing ambient scope adoption for `Publish` subscribers and the consumer pump"*; regenerate `index.md` in the same commit. The bold Decision sentence must name three brackets: replace "bracketed twice per subscriber" with *"bracketed three times — once around each subscriber's resolution, once around its own `Handle`/`HandleAsync`, and once around the consumer pump's own flow — with the restore written explicitly on each"*, then delete the now-duplicated first sentence of `0075:108`. The **slug is an owner call**: C-16 makes slugs the citation key and six siblings link it, so renaming costs seven files plus `index.md`. Leaving the slug narrow while the title is correct is a legitimate outcome and should be recorded as one.

##### 3. "three assemblies" is two (70)

**Evidence**: `0075:474` — "They sit in three files, three assemblies and five shapes". The five rows beneath (`0075:478-482`) are `PipelineBuilder.Build`, `PipelineBuilder.BuildAsync`, `CommandProcessor.Publish`, `CommandProcessor.PublishAsync`, `Performer.Run`. On disk: `src/Paramore.Brighter/PipelineBuilder.cs`, `src/Paramore.Brighter/CommandProcessor.cs`, `src/Paramore.Brighter.ServiceActivator/Performer.cs` — two assemblies. Contradicted internally by the flowchart at `0075:203-227` (both `pb` and `cp` inside one `core` subgraph) and by `0075:288-292` (two `Paramore.Brighter` rows). The DI package holds a *read*, not a bracket.

**Recommendation**: *"They sit in three files, two assemblies and five shapes."*

##### 4. "No bracket is ever established outside a publish" (68)

**Evidence**: `0075:445` against `0075:451` — "one bracket per pump thread for the life of the process" — and against the Decision at `0075:108`.

**Recommendation**: *"On the producer side no bracket is established outside a publish."*

##### 5. `Dispatcher.cs:484` (66)

`Dispatcher.Receive()` is at `:403`. Line 484 is inside `private void Start()` (`:477`) and starts `RunControlLoop`, not a pump. The real link to `Performer.Run()` is `Consumer.Open()` (`Consumer.cs:112`), never named. The enumeration is also incomplete in a way that *helps* the ADR: on the second start site it names — `ConfigurationCommandHandler.cs:85` → `Dispatcher.Open(Subscription)` in `DS_RUNNING` (`Dispatcher.cs:375-380`) — `consumer.Open()` runs on the handler's own flow with **no** intervening `StartNew` at all.

**Evidence**: `0075:367`. Against `Dispatcher.cs:403`, `:477`, `:484-488`, `:528`, `:375-380`; `Consumer.cs:112`. Repeated without a line number at `ADR 0073:297` — one defect at two distances.

**Recommendation**: *"`Consumer.Open()` (`Consumer.cs:112`) starts every pump through `Performer.Run()`, whose `Task.Factory.StartNew` captures `ExecutionContext` (`Performer.cs:62-69`). Where a `Dispatcher` is opened from a live request the call reaches `Performer.Run()` on the caller's own flow, so the pump inherits it."* Fix `0073:297` too.

##### 6. The 248-byte figure (64)

**Evidence**: `0075:447`. Probe, .NET 10.0.11, `GC.GetAllocatedBytesForCurrentThread` over 200k iterations: 0 other `AsyncLocal`s live → **216.0 B/bracket** (432/subscriber); 1 → **248.0** (496); 2 → 280.0; 6 → 472.0. So 248 holds only under one unstated condition, and it *under*-states the figure in a traced consumer, where `Activity.Current` is `AsyncLocal`-backed and Brighter creates a span per publish. Separately AC-23 (`requirements.md:687`) counts pipeline scopes begun/released and cannot see an allocation, yet is cited as the guard at `0075:447` and `0075:525`.

**Recommendation**: *"A bracket costs two `ExecutionContext` allocations, so a subscriber pays four. Each allocation is proportional to the number of `AsyncLocal` values already live on the flow — roughly 216 bytes per bracket with none, and more in a traced host, because `Activity.Current` is one of them."* Delete "the hot path AC-23 measures" from both sites. A measured guard is a new criterion for the true-up, and the Risks table is where it should be named as owed.

##### 7. NFR-4's suppression half has no owner (64)

**Evidence**: `0075:42` — "It serves **NFR-4**: once either publish returns, nothing is left on the caller's flow." Against `requirements.md:357`, whose NFR-4 also requires that "establishing and clearing ambient suppression … be safe under concurrent pipelines … with no cross-pipeline interference and no torn or shared state". Against `0074:380`, which disclaims NFR-4 in exactly the terms that point here. Against `.agent_instructions/documentation.md:74` — a contribution bullet must name the owner; there is none.

**Recommendation**: move it into *In scope*, replacing the "It serves" line — *"**NFR-4's suppression half — concurrent subscribers do not interfere, and nothing is left on the caller's flow.** `AsyncLocal` confines the bit to one logical flow, and every bracket restores explicitly. The guards are **AC-12** and **AC-39**."* No new prose elsewhere; the mechanism is already in *Key Components* and step 5.

##### 8. Frontmatter summary (62)

**Evidence**: `0075:8` is 120 words in two sentences, the first of **83**. Longest summary sentence across the set: 0070 = 33, 0071 = 44, 0072 = 38, 0073 = 106, 0074 = 29, 0076 = 36. Republished verbatim at `index.md:113`.

**Recommendation**: split at the joints and drop the causal tail the body carries — *"A `Publish` subscriber suppresses ambient DI scope adoption for its own pipeline and for every pipeline created beneath it. The mechanism is a public, `AsyncLocal`-backed `AmbientScopeSuppression` flag in `Paramore.Brighter`. Each subscriber is bracketed twice — once around its resolution inside `PipelineBuilder`'s build loop, once around its own `Handle`/`HandleAsync` — and both restores are written rather than inherited from `ExecutionContext`. A third bracket in `Performer.Run` suppresses the consumer pump's own flow, so a consumer pipeline owns its scope unconditionally."* Land with finding 2; they touch the same lines.

##### 9. Five ⚠-plus-bold caveats (60)

**Evidence**: `0075:388`, `:449`, `:464`, `:493`, `:519`. Glyph counts: 0070 = 0, 0071 = 0, 0072 = 0, 0073 = 6, 0074 = 1, **0075 = 5**, 0076 = 0. Mid-prose bold runs per 100 non-blank lines: 0075 is highest in the set at **41.2** (0070 21.7, 0072 22.3, 0074 31.9, 0073 37.1, 0071 39.1, 0076 40.5). `### Correcting an ADR` names this shape directly.

**Recommendation**: no new text — relocate. `:388` becomes the lead of step 4a's *What this makes true*. `:449` becomes the lead of *What suppression costs* (it is that paragraph's point). `:464` becomes the lead of *Core gains a public static with a public mutator*. `:493` already sits in a Risks mitigation cell whose job is to say this — delete glyph and bold, keep the sentence. `:519` is a mis-reading argument already inside Alternative 5 — delete glyph and bold.

##### 10. Drifted requirements citations (58)

**Evidence**: `requirements.md:199` — "(`PipelineBuilder.cs:183` scope, `:184` handler, `:187` decorators; async twin `:228` scope, `:229` handler, `:230-231` decorators)". Actual: `:183` is `new Pipelines<TRequest>()`, `:184` blank, `:187` the `Each(` header; the real lines are `:190`, `:191`, `:194`, and async `:235`, `:236`, `:239`. `requirements.md:488` (AC-11) points at the same drifted `:228-229`. The ADR's own `:187-198` / `:232-244` are correct.

**Recommendation**: nothing in the ADR. Add the three corrections to the requirements true-up alongside the FR-19/AC-20/C-14 correction the ADR already records at `0075:388`.

##### 11. NFR-3 / AC-22.2 not named as unchanged (56)

**Evidence**: `0075:457` states the new ServiceActivator coupling; the References list at `0075:540` names NFR-4, NFR-7, NFR-8 and AC-22.3 but neither NFR-3 (`requirements.md:356`) nor AC-22.2 (`requirements.md:684`).

**Recommendation**: one bullet on the existing *Unchanged* list — *"`Paramore.Brighter.ServiceActivator`'s project file, which still holds one `ProjectReference` and no `PackageReference` — the bracket uses a type from the assembly it already references (NFR-3, AC-22.2)."* Add both IDs to References.

##### 12. Pump diagram fidelity (46)

**Evidence**: `0075:174-177` draws `Disp->>Perf: Run()` with a note about one `StartNew`; the real chain has two captures and a `Consumer` between them.

**Recommendation**: add `participant Cons as Consumer` with `Disp->>Cons: Open()` and `Cons->>Perf: Run()`. Land with finding 5.

##### 13. NFR-5/NFR-6 (44)

**Evidence**: `0075:449` — "so NFR-5 and NFR-6 hold". NFR-5 (`requirements.md:358`) bounds Brighter-created *scopes*; NFR-6 (`:359`) bounds scope begin/release per pipeline. Suppression creates no scope.

**Recommendation**: drop the clause; the preceding half-sentence is the whole true statement.

---

#### Verified CLEAN — do not re-derive

**Every runtime claim in the ADR that could be probed was confirmed**, including three on the real `CommandProcessor` (.NET 10.0.11):

- `Parallel.ForEach` restores EC **per worker/replica, not per body** — an unrestored write leaks from one body to the next. Confirmed synthetically (59/64 bodies) *and on the real `CommandProcessor.Publish`* with three subscribers (`S3` entered with the flag already `true`).
- An unrestored write **inside** a `Parallel.ForEach` body does **not** reach the caller — including where every body is inlined on the calling thread (512/512 in one run). Confirmed on the real `Publish`.
- An `async` method is an EC boundary: **no** `AsyncLocal` write anywhere inside `PublishAsync` reaches its caller, restored or not. Confirmed on the real `PublishAsync` with three handlers writing and never restoring. The plain `void` `Publish` has no such boundary, and `Each.cs:39-45`'s plain `foreach` exposes the caller — so bracket 1's sync restore really is the load-bearing one and bracket 2's really is defence in depth. The step 5a table is correct as drawn.
- **Alternative 5 is exactly right**: three subscribers started unbracketed and awaited under one bracket see `IsSuppressed == false` in their synchronous prefix *and after every subsequent await*, while the caller inside the bracket sees `true`. "No subscriber at any point in its life" is literally true.
- The **invocation-only** bracket works: prefix and every continuation suppressed, caller restored immediately (confirmed on the real `PublishAsync`).
- `Task.Factory.StartNew(LongRunning)` captures EC, **including across two nested hops** (Dispatcher control task → pump), and a bracket taken inside the started task does not touch the starter's flow.
- **Bracket 3 covers the Proactor.** An `AsyncLocal` set in `Performer`'s task body survives into `BrighterAsyncContext.Run(Action)`, into `Run(Func<Task>)`, and across `Task.Yield()` and `Task.Delay()` inside it. The ADR never states this and it holds.
- Thread pool restores a fresh EC per work item — nothing survives onto unrelated work (0/256 items, both `QueueUserWorkItem` and `UnsafeQueueUserWorkItem`).
- **Out-of-order disposal** behaves exactly as the contract table describes: outer-first clears suppression early, inner-second leaves the flow suppressed with every bracket disposed.
- A set writing **the value already held still allocates** (96 B); a read allocates 0 B. The ⚠ at `0075:449` is substantively correct.
- **The binary break is real**: adding a defaulted parameter to a public constructor and swapping the DLL without recompiling gives `MissingMethodException: Method not found: 'Void L.PipelineBuilder..ctor(System.String, System.String)'`.

**Citations opened and correct**: `CommandProcessor.cs` `:317`, `:394`, `:458`, `:472`, `:481`, `:481-497`, `:489`, `:559`, `:575`, `:596`, `:601`; `PipelineBuilder.cs` `:37`, `:59`, `:76`, `:92`, `:187-198`, `:232-244`, `:269-270`; `IAmAPipelineBuilder.cs:36`; `IAmAnAsyncPipelineBuilder.cs:37`; `Performer.cs:31-32`, `:62-69`; `Reactor.cs:95`, `:406`; `Proactor.cs:95`, `:130`; `RequestContext.cs:61`; `Extensions/Each.cs:39-45`; `BrighterPipelineValidationExtensions.cs:75`, `:116` (both genuinely the describe-only constructor); `ConfigurationCommandHandler.cs:73` (`_dispatcher.Receive()`) and `:85` (`Open`, which does reach `Performer.Run()` on the handler's flow); `SpannerBoxMigrationRunner.cs:131` — the sole `InternalsVisibleTo` hit in the repo *is* a comment, so Alternative 3's rule claim holds.

**Counts recounted and correct**: 69 `PipelineBuilder` constructions in `tests/`; 48 use the two dispatch constructors; 21 use the describe-only one (verified with a brace-matching argument-arity scan, not a one-line regex). Four dispatch construction sites in `CommandProcessor`. Exactly five container-backed `ServiceProvider*Factory` types in the DI package. Four `AsyncLocal` writes per subscriber. Five rows in the *five places to get wrong* table. "Nine interface signatures" reconciles with `0070:114` (0070's six, 0071's two, 0076's one), and AC-24's "six factory interfaces" is quoted accurately.

**Set consistency verified**: 0072's ladder rows 3/5/6/7 match every row number this ADR reads off them; 0072's affinity-computation line (`0072:165`) is character-identical to `0075:193`; 0072 discharges FR-19 and names this bracket (`0072:40`, `:169`); 0073 owns C-14 and records the closure (`0073:297`, `:301`); 0074 owns FR-25 and NFR-9 and confirms 0075 supplies two row families without being a second owner (`0074:48`, `:699`, `:701`); 0070 step 7a carries the `PipelineBuilder` constructor break (`0070:582`); AC-13's five decisions, AC-11's two `AlwaysNew` asks, AC-47's two branches, OOS-2's D6 amendment, OOS-10 and OOS-14 all say what the ADR says they say.

**Markup and diagrams**: zero HTML-escaped entities; backticks balanced; no `<see cref>` leakage outside the deliberate XML-doc code block; no `;` or `<`/`>` in mermaid labels; all labels with commas quoted. All three mermaid blocks render with `mmdc` (SVG), and diagram 1 rendered to PNG at 1800px and inspected — it is legible, the three invariants the prose reads off it are genuinely drawn, and only `await Task.WhenAll` falls outside a `loop for each subscriber`. Frontmatter is in sync with `docs/adr/index.md:113`. No paragraph exceeds 200 words; the longest is 157.

#### Gaps

- The Brighter test suite was not run; the probes model the brackets rather than execute a proposed implementation (none exists yet).
- No real ASP.NET host, `IHttpContextAccessor` or ASP.NET provider was exercised — FR-18/FR-23 interactions with suppression are ADR 0072's and 0073's remit and were checked only for consistency with this ADR's statements.
- No attempt was made to reproduce the 248-byte figure under the author's original conditions; only that it is not reproducible as a bare constant, and the one configuration that yields it.
- The `## Alternatives Considered` rejection rationales were checked for internal and sibling consistency, but alternatives 6, 8 and 9 were not independently re-derived against the source.


### ADR 0076 — `scope-affinity-option-and-write-through`

**12 findings, 7 at or above the threshold of 60. 0 Critical · 1 High · 6 Medium · 5 Low.**

| # | Score | Finding |
|---|---|---|
| 1 | 80 | Scope names, as FR-19's mechanism, the exact reason ADR 0072 rejects by name |
| 2 | 68 | "Step 7a enumerates the whole entry" describes five of its thirteen items |
| 3 | 66 | Probe falsifies the null-`optionsFunc` contract row — the guard does not preserve the failure shape |
| 4 | 64 | A 252-word Risks cell that argues about test coverage instead of stating a mitigation |
| 5 | 64 | The FR-19 log bound 0076 restates is the one two siblings record as superseded |
| 6 | 63 | Scope claims to serve NFR-4; *Technology Choices* disclaims it |
| 7 | 60 | Alternative 8 rejects `InternalsVisibleTo` on a design ground; three siblings rule it out as unavailable |
| 8 | 58 | NFR-2 claimed whole in the body, absent from the Scope ledger |
| 9 | 58 | "`IOptions<BrighterOptions>.Value` … a different object on three of them" — attributed to C-12a, wrong on one path |
| 10 | 52 | "MS DI freezes the collection when the provider is built" is false for `BuildServiceProvider()` |
| 11 | 50 | "Exactly one implementation of `IBrighterOptions` in `src/`" contradicted two paragraphs later |
| 12 | 42 | `optionsFunc` "documented at `:140`" — line 140 is an empty `<param>` tag |

---

#### 1. The Scope names, as FR-19's mechanism, the exact reason ADR 0072 rejects by name (Score: 80)

0076's `Scope` says FR-19's consumer-side inertness is made true by "the pump publishing no per-message ambient", and credits ADR 0072. ADR 0072 says the opposite in as many words: the mechanism is **ADR 0075's** pump-flow bracket in `Performer.Run()`, and D0b's no-per-message-ambient "is **not** what makes this true and is not offered as the reason". ADR 0075 concurs. 0076 is the only ADR of the three that never mentions the bracket, and its own `Where this ADR sits` table already credits 0075 with the consumer pump — so the ADR contradicts a sibling and itself.

**Evidence**: `0076:47` — "**FR-19 — the flag is inert on the consumer side.** The mechanism is the pump publishing no per-message ambient (D0b, C-2, **ADR 0072**)." Against `0072:40` — "ADR 0075's pump-flow bracket in `Performer.Run()` suppresses the pump's own flow… ADR 0075 owns the mechanism and states the site, and ADR 0076 supplies the property and its inheritance onto `ConsumersOptions`; this ADR discharges the requirement. The pump publishing no per-message ambient (D0b, OOS-1) is not what makes this true and is not offered as the reason: it would leave a `Dispatcher` started from inside a live request free to inherit an `HttpContext`, which C-14 assumes away rather than prevents". And `0075:47` — "The pump-flow bracket of step 4a is the mechanism that makes it true. **ADR 0072 discharges FR-19** and names this bracket as what makes it hold." **0072 verified as the correct one**: 0075 independently states the same division, and 0072's reason for rejecting D0b is spelled out again at `0075:367-371`. The error repeats at `0076:457` — "inert on the consumer side (FR-19, D0b)".

**Recommendation**: replace the mechanism clause, do not qualify it. `0076:47` becomes: "**FR-19 — the flag is inert on the consumer side.** The mechanism is ADR 0075's pump-flow bracket in `Performer.Run()`, and **ADR 0072** discharges the requirement. What this ADR contributes is that `ConsumersOptions` inherits the property and can set it, so the inertness is a property of a flag that was *set* rather than of one nobody could reach. It also contributes the documentation obligation FR-25.11 places on the guidance page." Correct the derived statement in the same commit: `0076:457`'s parenthesis becomes `(FR-19, ADR 0075)`. `0076:43`'s serves-list already reads "FR-19 … by ADR 0072", which stays true and needs no edit.

#### 2. "Step 7a enumerates the whole entry" describes five of its thirteen items (Score: 68)

0076 tells a reader that ADR 0070's single release-note entry consists of this ADR's break, FR-20's behavioural break, FR-22.2's compatibility break and the eight interface signatures. Step 7a says the entry "holds **thirteen**" breaks, and 0076's four items reach only five of them. The eight step 7a entries 0076 does not account for are the factory-level cache removal, the six transform-pipeline constructors, the disposal log-level change, `HandlerLifetimeScope.Dispose()`, the handler factory's dictionary removal, the faulted-`Lazy` eviction, `PipelineBuilder`'s two constructors and `IAmAPipelineValidator`. Step 7a is emphatic that this shortfall matters — "nine of the thirteen breaks would ship with nothing checking that they were written down".

**Evidence**: `0076:287` — "Step 7a enumerates the whole entry: this break, FR-20's behavioural break, FR-22.2's compatibility break, and the eight factory, registry and handler interface signatures ADRs 0070 and 0071 change (C-18, NFR-1(c), AC-24)." Against `0070:560` — "that entry holds **thirteen** of them. Five are this ADR's own… The other eight arrive with five siblings" — and `0070:562-582`, enumerated item by item. Verified correct in the same passage: "the eight … interface signatures" is right (`0070:578`, `0071:563`), and "AC-24, whose four clauses do not reach an options-interface member" is right (`requirements.md:697-703`). The same compression repeats at `0076:514`.

**Recommendation**: the sentence's job is to say where this break is recorded, not to inventory the entry. `0076:287`: "Step 7a catalogues the whole release-note entry — thirteen breaks across six ADRs — and carries this one as a pointer to the *Consequences* bullet below." `0076:514`: "This is one more item for the single `release_notes.md` entry ADR 0070 step 7a catalogues." Both are shorter than what they replace.

#### 3. A probe falsifies the null-`optionsFunc` contract row (Score: 66)

The contract table says a null return from `optionsFunc` gets "the same exception type, at the same point" as today. That is true of `GetRequiredService` and false of `GetService` — which is how all five container-backed factories read `IBrighterOptions`, as this ADR itself says two sections later. Today a null-returning factory leaves `GetService<IBrighterOptions>()` returning `null`, and each factory falls back to `ServiceLifetime.Singleton`. Under the proposed `?? throw`, which runs unconditionally rather than only when an override is present, those same five calls throw `InvalidOperationException`.

**Evidence**: `0076:402` — "today MS DI raises its own error on a null-returning factory, but this delegate would dereference first whenever an override is registered… The guard restores the failure *shape* — the same exception type, at the same point, on resolution." Against `0076:481` and `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderMapperFactory.cs:44-45`: `var options = (IBrighterOptions?)serviceProvider.GetService(typeof(IBrighterOptions)); var lifetime = options?.MapperLifetime ?? ServiceLifetime.Singleton;`. Probe (net9.0, M.E.DI 10.0.10): `AddSingleton<IOpts>(sp => null!)` then `GetService<IOpts>()` → **null, no throw**; `GetRequiredService<IOpts>()` → `InvalidOperationException`; with the ADR's `?? throw` in the delegate, `GetService<IOpts>()` → `InvalidOperationException`.

**Recommendation**: replace the general claim with the narrower truth: "Today a null return surfaces as `InvalidOperationException` from the three `GetRequiredService<IBrighterOptions>` sites and as a silent `null` from the five factories' `GetService`. The guard raises `InvalidOperationException` at the first resolution on both routes, which is a behavioural change on the `GetService` route and the price of not turning the error into a `NullReferenceException` from inside Brighter."

#### 4. A 252-word Risks cell that argues about test coverage instead of stating a mitigation (Score: 64)

The second Risks row is the only unit in the document over 200 words — 252, with five bold runs — and its last third is a review argument rather than a mitigation: which criterion pins what, why a cross-product would be redundant, and the closing "identical code exercised four times is an argument, not a test". The house style puts a deliberation in `Alternatives Considered` and says a fix needing bold to be found is in the wrong place.

**Evidence**: `0076:528`, measured mechanically across the document (218 units; three over 150 words; one over 200 — this cell at 252, against 191 for the next largest and 162 after that). Everything in the cell checked is *true* — AC-48's second clause is the before-ordering on `AddBrighter(Action<BrighterOptions>)` alone (`requirements.md:768,771`), and AC-45's `Given` fixes no placement for the extension call (`requirements.md:759`) — so this is a placement and length defect, not a truth defect.

**Recommendation**: cut to the mitigation. "The mechanism has no ordering: the override is a service rather than a mutation of a descriptor, and the pick-up runs inside the producer at first resolution. AC-48's second clause pins the before-ordering on `AddBrighter(Action<BrighterOptions>)`; the other three paths hold by construction, because all four funnel through `RegisterBrighterOptions`." The coverage argument, if kept at all, belongs as one sentence at the lead of *Why the single funnel, and not four call sites* (`0076:387`).

#### 5. The FR-19 log bound 0076 restates is the one two siblings record as superseded (Score: 64)

0076 gives FR-19's permitted difference as "at most two latched `Warning` entries for the life of the host". Both ADRs that own the consumer side record that their mechanism makes the true count **zero**, that this is stronger than FR-19 as drafted, and that a correction is owed to FR-19, AC-20 and C-14. 0076 is the only one of the three that repeats the drafted bound without the correction.

**Evidence**: `0076:283`. Against `0075:388` — "⚠ **The bracket is stronger than FR-19 as currently written.** FR-19 permits up to two log entries on the consumer side… Under this bracket a conforming provider emits **none**… FR-19, AC-20 and C-14 are owed the corresponding correction" — and `0072:171`, which says the same. **0072 and 0075 verified as the correct pair**; FR-19 as drafted does say two, so 0076's sentence is true of the requirement and false of the delivered behaviour.

**Recommendation**: the paragraph does not need the number — its point is that the property is settable on the consumer side and inert there. "FR-19 makes that setting inert on the consumer side: every consumer pipeline creates and owns its scope. ADR 0075's pump-flow bracket is what makes that hold, and ADR 0072 records the diagnostic count it produces." That removes the stale numeral rather than qualifying it, and closes finding 1 in the same edit.

#### 6. The Scope claims to serve NFR-4, and *Technology Choices* disclaims it (Score: 63)

NFR-4 is in the serves-list. The only thread-safety statement the ADR makes then says, in bold, that it is not NFR-4's guarantee — and nothing else in the document begins or releases a pipeline scope or establishes ambient suppression, which is all NFR-4 governs.

**Evidence**: `0076:43` — "It serves … NFR-1, NFR-4 and NFR-7." Against `0076:483` — "**It is not NFR-4's guarantee**, which is about pipeline scopes and ambient suppression under concurrent pipelines." And NFR-4 (`requirements.md:357`): "Beginning and releasing pipeline scopes, and establishing and clearing ambient suppression, must be safe under concurrent pipelines…".

**Recommendation**: strike `NFR-4` from the serves-list at `0076:43` and from the requirement line at `0076:581`. Leave `0076:483` exactly as it stands — it is the sentence that made the error findable, and it is correct.

#### 7. Alternative 8 rejects `InternalsVisibleTo` on a design ground; three siblings rule it out as unavailable (Score: 60)

0076 treats `InternalsVisibleTo` as a real option that NFR-7 outweighs. ADRs 0073, 0074 and 0075 all state that this repository uses the attribute nowhere and treat that as a rule, and 0075 makes it decisive ahead of NFR-7. The fact was re-derived.

**Evidence**: `0076:562` — "**Rejected on NFR-7.**… ADR 0075 rejects `InternalsVisibleTo` for suppression on the same ground." Against `0075:501` — "**Rejected on a rule rather than a preference: this repository does not use `InternalsVisibleTo`, anywhere, without exception.** Even if it did…". Also `0073:489` and `0074:635`. Verified: `grep -rn "InternalsVisibleTo" src tests samples` → one hit, `src/Paramore.Brighter.BoxProvisioning.Spanner/SpannerBoxMigrationRunner.cs:131`, and it is a comment.

**Recommendation**: lead with the rule, which shortens the alternative. "**Rejected: this repository grants `InternalsVisibleTo` nowhere, and ADR 0075 states that as a rule.** Even where it did, NFR-7 requires the mechanism to be usable by a package Brighter does not ship, and an `InternalsVisibleTo` list can only name packages Brighter knows about." Correct `0076:340` in the same edit — "`InternalsVisibleTo` would serve the first caller and no other" asserts the same availability.

#### 8. NFR-2 is claimed whole in the body and appears nowhere in the Scope ledger (Score: 58)

**Evidence**: `0076:181` — "The dependency direction is fixed, and it is the whole of NFR-2". Against `0076:36-48`, whose in-scope bullets and serves-list name eleven requirements without NFR-2, and `0076:581`, which does list it.

**Recommendation**: change `0076:181` to "…neither of the lower two ever depends upward. Nothing here gives the DI package an ASP.NET reference (NFR-2)." Then add `NFR-2` to the serves-list at `0076:43`.

#### 9. "`IOptions<BrighterOptions>.Value` … a different object on three of them" (Score: 58)

C-12a says only that those three run no configuration pipeline; it says nothing about what `IOptions<BrighterOptions>.Value` is. On the two `AddConsumers` paths the claim holds by accident, because `AddConsumers` calls `services.Configure<BrighterPipelineValidationOptions>` (`:60`, `:127`) and that registers the open generic. On `AddBrighter(Func<…>)` alone it is false: `IOptions<BrighterOptions>` is not resolvable at all.

**Evidence**: `0076:96`. Against `requirements.md:378` (C-12a). Probe: after `Configure<OtherOpts>` alone, `GetService<IOptions<BOpts>>()` is non-null with a fresh default `.Value`; with neither `AddOptions` nor any `Configure`, it returns **null**.

**Recommendation**: drop the added claim. "**AC-45 asserts the value on the *resolved* `IBrighterOptions`, on all four paths**, and not on `IOptions<BrighterOptions>.Value`, which only one path routes that object through (C-12a)."

#### 10. "MS DI freezes the collection when the provider is built" (Score: 52)

**Evidence**: `0076:542`. Probe: after `sc.BuildServiceProvider()`, `sc.IsReadOnly` is `False` and `sc.AddSingleton<B>()` succeeds; after `Host.CreateApplicationBuilder().Build()`, `IsReadOnly` is `True` and the same `Add` throws. The 125 self-registering test hosts all take the bare route. The second ground — no callback between the last registration and the build — is true on both paths and carries the rejection alone.

**Recommendation**: "A variant that registers a marker and rewrites on first resolution is not available either: the provider fixes its descriptors when it is built, and there is no callback between the last registration and that build."

#### 11. "Exactly one implementation of `IBrighterOptions` in `src/`" (Score: 50)

**Evidence**: `0076:285` against `0076:281` and `ConsumersOptions.cs:10` (`public class ConsumersOptions : BrighterOptions, IAmConsumerOptions`). A base-list scan of `src` returns exactly one *declaration* naming the interface, so the search is right and the word "implementations" is wrong.

**Recommendation**: "A repository-wide search for types that **declare** `IBrighterOptions` in their base list finds **exactly one** in `src/` … `ConsumersOptions` inherits the member and needs no change."

#### 12. `optionsFunc` "documented at `:140`" (Score: 42)

**Evidence**: `0076:389`. `ServiceCollectionExtensions.cs:140` is `/// <param name="optionsFunc"></param>` — a tag with no text. `:144` and "referenced nowhere in the body" are both correct.

**Recommendation**: "the parameter is declared at `:144` and referenced nowhere in the body."

---

#### Verified CLEAN — do not re-derive

**Every `file:line` citation, opened at the line.** DI `ServiceCollectionExtensions.cs` `:61`, `:69`, `:71`, `:74`, `:77-79`, `:88`, `:97`, `:98-100`, `:119`, `:142`, `:144`, `:146`, `:161`, `:169`, `:708` — all correct, including `:161`/`:169`, which land on the `GetRequiredService<IBrighterOptions>()` calls rather than the `TryAddSingleton` lines above them. ServiceActivator `ServiceCollectionExtensions.cs` `:29`, `:36`, `:37`, `:38`, `:39`, `:45`, `:64`, `:78`, `:88`, `:89-90`, `:131-133` — all correct. `ConsumersOptions.cs:10`, `BrighterOptions.cs:9`, `BrighterOptions.cs:37`, `src/Paramore.Brighter/IAmConsumerOptions.cs:7`, `ServiceProviderMapperFactory.cs:44` — all correct.

**Counts, all recounted.** **125** test files register `IBrighterOptions` themselves — exactly. **Five** container-backed factories read it via `GetService`. `IAmConsumerOptions` has exactly **five** members. **Four** registration paths and only four — no fifth `IBrighterOptions` registration anywhere in `src`, and no `BrighterHandlerBuilder` caller outside those four plus the `:119`→`:142` internal forward, so `0076:391`'s "no such caller exists in `src/`, `tests/` or `samples/`" is correct. AC-24 has exactly **four** `Then` clauses. Alternative 2's "four costs" are four; alternative 4's "(i)–(iv)" are four. "The eight … interface signatures ADRs 0070 and 0071 change" is right at **eight**. All **eleven** internal `alternative N` cross-references point at the right alternative.

**Diagrams.** All four mermaid blocks render with `mmdc`; `d3` and `d4` rendered to PNG at 1600px and read. Both legible, neither contradicts the prose. The class diagram's `IBrighterOptions <|.. BrighterOptions`, `BrighterOptions <|-- ConsumersOptions`, `IAmConsumerOptions <|.. ConsumersOptions` and the deliberate absence of any `IAmConsumerOptions → IBrighterOptions` edge all match the source. `grep -c '&lt;\|&gt;\|&amp;'` → 0; no `<see cref>` in prose; no unbalanced backticks outside fences.

**Probes that confirmed rather than falsified.**
- `d.ServiceKey` and `ServiceDescriptor.Singleton<T>(factory)` **compile on `netstandard2.0`** against M.E.DI.Abstractions 10.0.10 (`Directory.Packages.props:90`), which the DI package's `netstandard2.0;net8.0;net9.0;net10.0` targets need. The guard is implementable as written.
- `TryAdd` **does** match on `ServiceType` *and* `ServiceKey`: with a keyed service present, `TryAddSingleton` still registers, and the ADR's `d.ServiceKey is null` guard correctly does not trip where a `ServiceType`-only guard does. `0076:393` and `0076:395`'s keyed-host argument are both correct.
- MS DI resolves an unkeyed service to the **last** descriptor — the rule `0076:435` and AC-50's after-ordering branch rely on.
- `IOptions<T>.Value` is **not** reference-equal to `IOptionsMonitor<T>.CurrentValue` or a scoped `IOptionsSnapshot<T>.Value`, and mutating `IOptions<T>.Value` is invisible to both while visible to every other `IOptions<T>` resolver. `0076:516`'s Negative bullet — the sharpest claim in the document — is exactly right.
- The container does **not** dispose an instance registration and **does** dispose a factory-returned `IDisposable` singleton, confirming `0076:418`.

**Requirement attributions checked against the requirement's own text.** AC-45's three clauses; AC-48's second clause; AC-50's branches; AC-20 as the only mixed-host criterion fixing one ordering and the `Action` overload (so `0076:449` is right); FR-22.4's two conjuncts; C-9's "name, type and default expression are open"; FR-25.11's three gestures; NFR-1's source-level clause; AC-22.3.

**Sibling claims verified correct.** 0072 owns `ScopeAffinity` in core with `AlwaysNew = 0` and states the positive-`JoinAmbient` contract 0076 relies on. 0072 does register `ScopedArtefactCache` and `AmbientScopeDiagnostics` through the same funnel. 0073 owns FR-15's package-inertness half and names the normative clause as 0076's. 0074 places `ScopeConfigurationValidator` in the DI package, reads `BrighterOptionsRegistration` as an `ImplementationInstance`, asks it whether the last descriptor is the recorded one, and agrees a factory-registered override is invisible to the repeat rule. The unifying sentence "**the per-pipeline object carries the DI scope**" appears verbatim in all seven. Every `IAmConsumerOptions` consumer in `src` and `tests` reads only members of that interface — no downcast anywhere — so `0076:426`'s residue argument holds.

**Readability baseline for any fix batch.** 218 units; **3 over 150 words, 1 over 200** (the 252-word Risks cell). 189 bold runs — 84 at a line/bullet lead, 105 mid-prose. Densest: `### Scope` 29, `#### RegisterBrighterOptions` 24, `## Alternatives Considered` 24. `RegisterBrighterOptions` opens nearly every paragraph with a bold lead, at a density where "a section with bold in every paragraph has emphasis in none of them" begins to bite (~54 on its own; not filed separately). **None of the fixes above adds a word, a bold run or a qualifier; findings 2, 4, 5, 9, 10 and 12 all shorten the text.**

#### Gaps

- ADR 0074's rule bodies and ADR 0073's extension signature were not audited beyond the points 0076 cites, so the far side of the FR-22.4 and FR-17-repeat contracts is unchecked from here.
- The solution was not built and no tests were run. The ADR proposes deleting four registration sites; whether that compiles and leaves the 125 self-registering test hosts green is unverified.
- The requirements true-up that 0072 and 0075 say is owed to FR-19, AC-20 and C-14 (finding 5) has not landed. If it lands before this ADR is corrected, finding 5's fix text changes.


### Set-level — spec 0036, round 7

**18 findings, 14 at or above the threshold of 60. 0 Critical, 3 High, 13 Medium, 2 Low.**

#### Ranked findings

| # | Score | Finding |
|---|---|---|
| 1 | 80 | **ADR 0076 gives FR-19 the very mechanism ADR 0072 rules out by name.** `0076:47` — "The mechanism is the pump publishing no per-message ambient (D0b, C-2, ADR 0072)". `0072:40` — "The pump publishing no per-message ambient (D0b, OOS-1) **is not what makes this true and is not offered as the reason**". 0072/0075 verified correct (the mechanism is 0075's `Performer.Run()` bracket). Repeated at `0076:457`. 0076 also states the two-entry log bound at `:283` without the FR-19/AC-20/C-14 correction both siblings record (`0072:171`, `0075:388`). |
| 2 | 72 | **0076 names AC-45 as FR-15's normative-clause guard; AC-45 asserts nothing about the default.** All four AC-45 hosts *call* the extension; AC-45 is tagged `(FR-17, FR-14)`. AC-14 — the criterion that does test the default — is cited nowhere in 0076. |
| 3 | 70 | **Six of the ten NFRs are in no ADR's `In scope` list** — NFR-2, NFR-3, NFR-4, NFR-7, NFR-8, NFR-10. NFR-1 is claimed by 0074 for one clause only; NFR-1(a)/(b)/(c) are discharged by 0070/0071 and recorded by neither. |
| 4 | 68 | **FR-10 names three types (D4); 0070 introduces `IAmAScope` and 0071 extends it, and the string `FR-10` appears in neither ADR** — not in Scope, not in References. 0072 claims FR-10 whole and offers only `IAmAScopeProvider`. |
| 5 | 68 | **0072 claims to discharge NFR-8 (`:80`) though it declares neither `IAmAScope` nor `IAmALifetime`; 0073 (`:71`) maps NFR-8 to "ADRs 0070 and 0072", omitting 0071 where the reciprocal doc is written (`0071:468`).** 0074's clause map (`:700`, 0070+0071) verified correct. |
| 6 | 66 | **0070 claims NFR-5 and NFR-6 unqualified**, though NFR-5 names handler lifetimes and 0070 declines FR-7 on exactly the "does not touch handler pipelines" ground; 0071 claims neither. |
| 7 | 64 | **All seven frontmatter summaries breach the "one or two sentences" schema** (86–179 words; 0071 = 179/7 sentences, 0076 = 174/7). `index.md` rows are generated from this field. |
| 8 | 62 | **0071's `handler` tag is not in the controlled vocabulary** (`adr_frontmatter.md:109-146`); 0071 is the only ADR in the repo using it. |
| 9 | 62 | **The uniform preamble in all seven says "one decision each"**, while 0072 (`:31`) and 0076 (`:34`) open "This ADR decides two things". Generated in a pass, so wrong in all seven at once. |
| 10 | 62 | **0074's dependency-order sentence (`:82`) omits 0075**, from which its own Scope (`:48`) says two families of FR-25 clause come. |
| 11 | 62 | **0072 (`:64`): "the first two having only closed defects"** — 0071's Scope names no defect and 0070's step 7a (`:579-580`) catalogues two behavioural changes as 0071's. |
| 12 | 62 | **C-8 covers three types**; 0070 (`:37`) reports it confirmed for "the seam types" while introducing one, and 0072, which puts the other two in core (`:506`), never cites C-8. |
| 13 | 60 | **0072's Scope gives `AmbientScopeDiagnostics` two of its three latches** (`:43`); its own body (`:263`, `:433`) and 0074 (`:54`) say three. FR-23's latch is left off `:42`. |
| 14 | 60 | **FR-6 is claimed unqualified by 0070 (`:41`) and "for the handler family" by 0071 (`:41`)** — the only split-in-fact requirement described as split at one end only. |
| 15 | 55 | **C-3 and AC-21 are cited only by 0070**, though 0072 owns FR-16(b) — C-3's single stated exception — and FR-19, which AC-21 also tags. |
| 16 | 50 | **ADR 0067's `Terms` block (`:49`, "`IAmALifetime` is the token identifying a single pipeline") is cited as authoritative by all seven**, and 0071 makes it also carry the scope without any ADR noting the definition is now partial. Closable only by touching an Accepted ADR or caveating seven places — filed as a "can only be closed by making it worse" case with a narrow one-clause fix in 0071 alone. |
| 17 | 40 | OOS-6 and OOS-11 are cited by no ADR (covered in substance by C-5/D0c; recommendation is to leave them). |
| 18 | 35 | 0075's frontmatter title drops the backticks its H1 carries (recommendation: leave it). |

#### The FR → ADR ownership table (re-derived from today's `Scope` paragraphs)

**Functional requirements (27) — every one is owned.**

| Req | Discharged by | Contributes / serves | Verdict |
|---|---|---|---|
| FR-1, FR-2, FR-3, FR-4, FR-5 | 0070 | 0072 (cache relocation) | clean |
| FR-6 | 0070 *(unqualified)* + 0071 *(handler family)* | — | **finding 14** |
| FR-7 | 0071 | 0070 explicitly *serves*, names 0071 | clean, both ends agree |
| FR-8, FR-9 | 0075 | 0072 honours in the ladder | clean |
| FR-10 | 0072 (`IAmAScopeProvider` only) | 0073 serves | **finding 4** — `IAmAScope` half unclaimed |
| FR-11, FR-12 | 0072 | 0070/0071 route FR-13's carve-out here | clean |
| FR-13 | **split**: 0070 transform family (both clauses) / 0071 handler family (both clauses) / borrowed carve-out → FR-12 → 0072 | — | clean — all three ends say "no ADR claims the whole of FR-13" |
| FR-14 | 0076 | — | clean |
| FR-15 | **split**: package-inertness → 0073 / default `AlwaysNew` → 0076 | — | split described identically; **guard wrong (finding 2)** |
| FR-16 (a,b,c) | 0072 | 0073, 0076 | clean |
| FR-17 | **split three ways**: gesture → 0073 / write-through+precedence → 0076 / repeated-call site → 0074 | 0072 | clean — all three ends state the same three-way split |
| FR-18 | 0072 | 0073, 0076 | clean |
| FR-19 | 0072 | 0075 supplies bracket; 0076 contributes | **finding 1 — the two ends give incompatible mechanisms** |
| FR-20 | 0070 | 0076 serves | clean |
| FR-21 | 0072 | 0076 supplies the property + default | clean |
| FR-22 (all four rules) | 0074 | 0073, 0076 | clean |
| FR-23 | 0072 | 0073, 0076 | clean |
| FR-24 | 0072 (.1, .2, .4); **.3 split** registration model → 0072 / site → 0074 | 0073, 0075 | clean |
| FR-25 (all 11 clauses) | 0074 | 0075 supplies .5 + subscriber truth-table rows; 0076 supplies .11 | clean — 0075 disclaims ownership, 0074 records both families |
| FR-26 | 0072 | — | clean |
| FR-27 | .1 + .2 → 0072; .3 → 0075 | 0070, 0071 route .1 to 0072 | clean |

**Non-functional (10)**: discharged in a Scope list — NFR-1 (0074, one clause), NFR-5, NFR-6 (0070, over-broadly), NFR-9 (0074). **Unclaimed: NFR-2, NFR-3, NFR-4, NFR-7, NFR-8, NFR-10.**

**Constraints / decisions / out-of-scope sweep**: all 22 decisions (D0, D0b, D0c, D1–D19) cited. Every C-N is cited somewhere; C-3, C-8 and C-19 are cited by only one ADR where a second one binds (findings 12, 15). OOS-6 and OOS-11 uncited. **No citation misstates its item.** All 52 ACs are cited by at least one ADR; the only unsupported AC citation is AC-45 for FR-15 (finding 2).

#### The readability table (data, not findings)

Definitions, in full, so the numbers reproduce: YAML frontmatter, **all fenced blocks including their fences**, ATX heading lines, and markdown table lines (stripped line starts with `|`) are removed first. A **block** is then a maximal run of lines bounded by a blank line, *split further at every list-item lead* (`^([-*+]|\d+[.)])\s`) — so one paragraph is one block and each bullet is its own block with its continuation lines; word count is `len(text.split())`. A **bold run** is one `\*\*(.+?)\*\*` match (non-greedy, DOTALL within the block). A **bullet lead** is the *first* bold run of a list-item block whose content begins `**`; every other bold run is **prose**. The two bold columns are disjoint. A **diagram** is one ```mermaid fence. Script: `count_readability.py`.

| ADR | blocks | >150 words | >200 words | bold in prose | bold at bullet leads | diagrams |
|---|---:|---:|---:|---:|---:|---:|
| 0070 | 261 | 3 | 0 | 117 | 78 | 4 |
| 0071 | 210 | 5 | 0 | 138 | 59 | 5 |
| 0072 | 227 | 10 | 0 | 95 | 58 | 3 |
| 0073 | 164 | 2 | 0 | 128 | 53 | 3 |
| 0074 | 270 | 4 | 1 | 128 | 71 | 4 |
| 0075 | 180 | 1 | 0 | 138 | 49 | 3 |
| 0076 | 156 | 2 | 0 | 120 | 32 | 4 |
| **total** | **1468** | **27** | **1** | **864** | **400** | **26** |

The single >200-word block is `0074`, 207 words, Alternative 1 ("A decorating validator").

#### Verified CLEAN — do not re-derive

- **`docs/adr/index.md` is in sync.** Regenerated with `awk -f .claude/commands/adr/generate_adr_index.awk docs/adr/[0-9]*.md` to a scratchpad path and diffed: **zero difference**. Repository copy untouched.
- **All 26 mermaid blocks render.** Extracted and run through `mmdc` v11: 26/26 succeed, no parse errors. The largest (`0074-3`, the validator class diagram, 62 lines) rendered to PNG at 1600px and visually inspected — readable grid, no overlapping edges.
- **Zero escaped entities** in any of the seven. No semicolons in `sequenceDiagram` message text. No stray `<`/`>` in mermaid labels. The four `<see cref>` hits are all inside XML-doc code fences, not prose.
- **The seven "Where this ADR sits" tables are byte-identical** apart from the bold/(this one) marker — diffed pairwise.
- **The unifying-rule sentence is repeated verbatim in all seven**: "the per-pipeline object carries the DI scope."
- **Every `## References` sibling one-liner matches its table cell in all seven**, and every sibling link resolves.
- **Every cited external ADR's status is correct**: 0053 [Accepted], 0064-validate-pipeline-assembly-and-provider-registration [Accepted], 0054-roslyn-analyzer… [Proposed], 0066/0067/0068/0069/0014/0064-pipeline-cache-type-key/0007/0005 [Accepted], 0033/0039-scoping…/0004 [Proposed]. 0074's C-16 file-count claim ("three files numbered 0053, two 0054, two 0064") is **correct**, as is C-16's four-file 0039 list.
- **ADR 0067's `Terms` block genuinely says what all seven say it says**, and `0067:38` does name "ADRs 0070–0076" as 0075 claims.
- **All seven follow the template skeleton exactly.**
- **Frontmatter**: `id` = filename stem in all seven; `status: Proposed` matches every body `## Status`; `created` matches every body `Date:`; `title` matches the H1 in six of seven (0075 differs by backticks only); tag counts 3–4 in all seven, all tags in-taxonomy except 0071's `handler`.
- **Recounted and correct**: "five container-backed factories"; "four registration paths" and every line citation in 0076's registration table (`:61`, `:74`, `:69`, `:71`, `:88`, `:97`, `:29`, `:38`, `:36`, `:37`, `:78`, `:88`, `ConsumersOptions.cs:10`) — all exact; all four route through `BrighterHandlerBuilder` (`:142`, forwarded from `:119`); "there is no `AddServiceActivator`" — correct.
- **The nine/eight/thirteen interface-count trio is correctly scoped and NOT a contradiction**: 0070 says the set breaks **nine** interfaces (its six + 0071's two + 0076's one); 0071 says 0070's step 7a names **eight** *"across this ADR and 0070"*; the entry holds **thirteen** items and both ADRs agree AC-24's four `Then` clauses reach four of them. AC-24 does have exactly four `Then` clauses and its verifier line is at `requirements.md:714`.
- **AC-22's three numbered clauses exist**; 0073's `AC-22.2` and 0074's `AC-22.3` are both correct references.
- **`$(BrighterCoreTargetFrameworks)` = `net8.0;net9.0;net10.0`** (`src/Directory.Build.props:45`), so 0073's frontmatter target-framework claim is correct.
- **All 52 ACs are cited somewhere in the set**; no orphan.

#### Gaps the set-level remit did not cover — a further reviewer should be redirected onto these

1. **The internal argument of any single ADR.** All seven were read, but only the `Scope`, `Where this ADR sits`, `References`, frontmatter and the passages a set-level claim turned on. No ADR's Decision, mechanism, Key Components, Technology Choices, Implementation Approach, Consequences or Alternatives was audited on its own merits.
2. **`file:line` citations into `src/` and `tests/`.** Only the set-wide ones were verified. The hundreds of other `src/` citations are unverified from this remit.
3. **All implementation counts** — "12 classes in `src/` and 70 test doubles" (0070), "21 implementations…" (0071), "seventeen of the eighteen classes" (0070), "eight methods that carry `IAmALifetime`" (0071), "six builder `catch` clauses" (0072), "nine constructor arguments" (0074) — **none recounted here.**
4. **No runtime or compile-time probe was run** from this remit.
5. **The ladder in 0072 and the truth table in 0074** were read for their Scope-level consequences only; not checked row-by-row for exhaustiveness, evaluation order or mutual exclusivity beyond finding 13.
6. **`docs/guides/lifetimes-and-scoping.md` does not exist yet**; nothing about FR-25's eleven clauses was checked beyond who owns them.
7. **The requirements themselves** were read as the authority and not reviewed. Two corrections the ADRs say are owed (FR-19/AC-20/C-14 under the pump bracket; AC-14's spy clause and AC-24's two amendments) are recorded by the ADRs but unassessed — true-up items, not findings.
8. **Readability judgement.** The table is a blind mechanical baseline. No view is offered on whether 0072's ten >150-word blocks or the set's 864 prose bold runs are too many, and no diff against any earlier count was made.


### Gap coverage A — `requirements.md` reviewed as a document

**25 findings, 13 at or above threshold. 0 Critical, 4 High, 15 Medium, 6 Low.**

Every finding is tagged **[REQ]** (the document is wrong) or **[ADR]** (the document is right).

##### 1. FR-9's `PipelineBuilder` build-loop citations are drifted seven lines, and four other sites repeat the drift (Score: 78) — **[REQ]**

FR-9 cites the sync build as `PipelineBuilder.cs:183` scope, `:184` handler, `:187` decorators, and the async twin as `:228` scope, `:229` handler, `:230-231` decorators. Opened: `:183` is `var pipelines = new Pipelines<TRequest>();`, `:184` is blank, `:187` is `observerTypes.Each(observer =>`. The real lines are **`:190`** (`var instanceScope = GetSyncInstanceScope();`), **`:191`** (`_syncHandlerFactory.Create(observer, instanceScope)`) and **`:194`** (`BuildPipeline(handler, context, instanceScope)`); the async twin is **`:235`**, **`:236`**, **`:239-240`**. The prose is correct; only the numerals are wrong.

The same drift is repeated at four further sites: **AC-11**'s *"Why this is a resolution-time assertion"* (`requirements.md:488`, `PipelineBuilder.cs:228-229` → `:235-236`); Design notes *Highest-risk area* (`:861`, `PipelineBuilder.cs:183-187` → `:190-194`); the Terms table's **pipeline** row (`:36`, `PipelineBuilder.cs:167` for `Build` returning `Pipelines<TRequest>` — `:167` is a `<param>` doc line, the declaration is `:174`).

**Recommendation**: substitute the correct numerals at all five sites in one edit. A five-site single defect, not five defects; correcting only FR-9 leaves AC-11 and the Design notes wrong.

##### 2. NFR-1(b)'s "every implementation moves together" enumeration undercounts both halves and omits an entire assembly (Score: 74) — **[REQ]**

NFR-1(b): *"Every implementation in this repository moves together — **the four container-backed factories**, **the six core factories** … and every test double."* A multi-line class-declaration scan over `src/` for implementations of NFR-1's six interfaces finds **sixteen**, not ten:

- `Paramore.Brighter.Extensions.DependencyInjection` — **five**, not four: the four mapper/transformer ones **plus `ServiceProviderHandlerFactory`** (`ServiceProviderHandlerFactory.cs:34`, `: IAmAHandlerFactorySync, IAmAHandlerFactoryAsync`).
- `Paramore.Brighter` — **nine**, not six: the six named **plus `SimpleHandlerFactory`**, **`SimpleHandlerFactorySync`** (`:33`) and **`SimpleHandlerFactoryAsync`** (`:33`).
- `Paramore.Brighter.ServiceActivator` — **two, named nowhere**: `ControlBusMessageMapperFactory` (`:31`) and `ControlBusHandlerFactorySync` (`Ports/ControlBusHandlerFactory.cs:6`).

The omission matters because NFR-3 says ServiceActivator "keeps its current dependency set" and OOS-5 forbids changing five of its types — a reader taking NFR-1(b) at its word would not expect the freeze withdrawal to reach that assembly at all.

**Recommendation**: replace the two counted phrases with the assemblies rather than with larger numerals, which would drift again — *"every implementation in `src/` of the six interfaces, in `Paramore.Brighter`, `Paramore.Brighter.Extensions.DependencyInjection` and `Paramore.Brighter.ServiceActivator`, and every test double"*. Shorter than what it replaces.

##### 3. FR-19 / AC-20 / C-14: the delivered mechanism makes the consumer-side count zero, where FR-19 states two and AC-20 asserts one (Score: 70) — **[ADR: the ADRs are right; requirements edit owed]**

ADR 0075 (`:388`) and ADR 0072 (`:171`) both record this and defer it to the true-up. Assessed on its merits, the claim is **correct**. ADR 0075's third bracket suppresses the ambient on the flow `Performer.Run()` starts, so a consumer pipeline's affinity is `AlwaysNew` unconditionally; FR-24.2's own carve-out (`requirements.md:235`) says an `AlwaysNew` ask returning nothing is *"never"* logged, and FR-23 (`:286`) is likewise `JoinAmbient`-only. So a conforming host emits **zero**. FR-19 (`:289`) says *"there are exactly two, and both are bounded"*; AC-20 (`:670`) asserts the `JoinAmbient` run *"records **exactly one**"*. AC-20 is therefore **unsatisfiable by a conforming implementation** — the same defect shape revision 18 fixed for AC-29.

**The ADRs' list is incomplete in one place.** FR-18 (`:283`) enumerates *"a consumer running in the same host"* among the no-`HttpContext` cases and then states the `Warning` positively. Under the bracket that case never carries `JoinAmbient`, so FR-18's enumeration points at a case its own `Warning` clause can no longer reach.

**Recommendation**: FR-19 — replace *"there are exactly two…"* with the one true bound. AC-20 — change *"exactly one"* to *"zero"* in the `JoinAmbient` run and drop the FR-23 sentence, keeping the per-message clause that is the criterion's real content. C-14 — restate as a mechanism rather than an assumption, deleting the *"FR-23 governs"* routing clause. FR-18 — strike *"a consumer running in the same host"* from the enumeration. Do not append an "except on the consumer" qualifier to FR-19.

##### 4. AC-52's negative control does not say how `AlwaysNew` is selected, and the obvious reading makes it a false negative (Score: 70) — **[REQ]**

AC-52's branch reads *"**And given** the identical host and identical application code with the affinity set to **`AlwaysNew`** — the negative control"* (`requirements.md:560`), over a Given that is *"an opted-in ASP.NET host"*. Opting in means calling `AddBrighterRequestScope()`, and under **D18** the extension's argument wins unconditionally over any affinity assigned on the options object. An implementer who reads *"the affinity set to `AlwaysNew`"* as an option assignment gets `JoinAmbient` at run time, and the negative control silently becomes a second positive. AC-20 and AC-26 were both amended (revisions 8 and 16) to say this explicitly; AC-52, added at revision 21, was not swept.

**Evidence**: AC-26 (`:611`) — *"selected by the argument passed to that host's `AddBrighterRequestScope(...)` call, which is the value under FR-17/**D18**, rather than by assigning the option alongside it"*.

**Recommendation**: replace *"with the affinity set to `AlwaysNew`"* with *"calling `AddBrighterRequestScope(ScopeAffinity.AlwaysNew)`"*. Shorter than the phrase it replaces and needs no cross-reference.

##### 5. `PipelineBuilder.cs:528`/`:539` is cited twice for `HandlerLifetimeScope` creation and points at unrelated code (Score: 68) — **[REQ]**

The lifetime-model table's `Scoped` row (`requirements.md:104`) and the *What is already correct* Design note (`:857`) both cite `PipelineBuilder.cs:528`, `:539`. `:528` is the opening brace of `PushOntoAsyncPipeline`; `:539` is `lastInPipeline = decorator;`. The real construction sites are **`:572`** and **`:583`**. The claim is true; the drift is 44 lines, so it is a different edit from finding 1.

**Recommendation**: `:572`, `:583` at both sites.

##### 6. AC-14's spy clause is vacuous in every project able to run its **When** (Score: 68) — **[ADR: correct; requirements edit owed]**

ADR 0073 (`:419`, `:423`) records this. Verified independently: `grep -rn IHttpContextAccessor src tests --include='*.cs'` returns **zero** matches; **zero** of the **37** test `.csproj` files reference `Microsoft.AspNetCore.*`; and AC-22.2 forbids the DI package any such reference. AC-14's *"the spy records zero accesses"* (`requirements.md:511`) cannot fail in any project that can run its When. It is also the only criterion FR-15's package-inertness half has.

**Recommendation**: a **split, not a qualifier**. Leave AC-14's whole-suite regression half where it is and lift the spy clause into its own lettered branch whose Given is a project that references the ASP.NET package and never calls the extension.

##### 7. FR-11(b) has no acceptance criterion, and AC-14 — the only AC tagged FR-11 — tests only FR-11(a) (Score: 66) — **[REQ]**

FR-11 states *"Two distinct requirements"*: (a) no adoption, and (b) *"A real owned pipeline scope is still created…"* (`requirements.md:222`). C-17 makes (b) load-bearing. AC-14 is the only AC naming FR-11, and its Then is *"it passes, and the spy records zero accesses"*. The existing suite runs with the three lifetimes at their `Transient` defaults (`BrighterOptions.cs:20`, `:52`, `:69`), so it cannot exercise a `Scoped` pipeline's scope at all — and AC-14's own exclusion list removes the three tests that do touch `Scoped` mappers. Nothing in the document asserts (b).

**Recommendation**: (b) needs a criterion, not a sentence. The smallest one that discriminates is a Then on an existing AC: add to **AC-9** a clause that the handler's container-`Scoped` dependency resolves without `InvalidOperationException` under a provider built with `ValidateScopes = true`.

##### 8. AC-46's and AC-13's "no pipeline scope taken" is asserted through an instrument that cannot see scopes (Score: 65) — **[ADR: correct; requirements edit owed]**

ADR 0072 (`:161`) and ADR 0071 (`:372`) both record this. Verified from the document alone: AC-46's first Then asserts *"**zero** adoption decisions and no pipeline scope taken"* (`requirements.md:809`) with a Given of *"the recording `IAmAScopeProvider` of AC-13"* — and AC-13's own note (`:506`) says the fake *"asserts nothing about scope release, which the fake cannot observe."* FR-27.1's rule 1 has the same wording (`:248`). ADR 0071 additionally establishes that `IAmALifetime.PipelineScope` is *non-null* under `Transient`, so the nearest available instrument gives the wrong answer.

**Recommendation**: delete *"and no pipeline scope taken"* from AC-46's Then rather than adding a definition — FR-27.1 already makes the ask and the pipeline scope co-extensive, so the zero-ask assertion carries it.

##### 9. AC-24's "the six factory interfaces whose signature changed" has no referent, and contradicts NFR-1(c) (Score: 64) — **[ADR: correct; requirements edit owed]**

NFR-1(c) says *"naming **each interface whose signature changed** and stating the migration (AC-24)"* — open-ended. AC-24's last clause (`requirements.md:703`) turns that into *"for each of the **six factory interfaces whose signature changed**"*, adding a numeral and narrowing to *factory* interfaces. NFR-1's six are the interfaces whose **freeze was withdrawn**, a different set from the ones the design changes. `IAmAHandlerFactory` exists (`src/Paramore.Brighter/IAmAHandlerFactory.cs:7`), so ADR 0070's "five factory interfaces whose own signature changes" is a real count, and neither five nor NFR-1's six is what AC-24 names. Step 7a's ledger recounted: 5 + 8 = **thirteen**, AC-24's four Then clauses reach **four** — arithmetic correct.

**Recommendation**: replace AC-24's clause with NFR-1(c)'s own wording — *"for each interface whose signature this work changed"*. The second amendment (a general Then over the single release-note entry) belongs in AC-24's `*Verifier*` note at `:714`, not mid-criterion.

##### 10. FR-22.3 states that `ServiceCollectionTransformerResolvabilityProbe` has "no constructor"; it has one, inside the range cited (Score: 63) — **[REQ]**

FR-22.3 (`requirements.md:316`): *"the whole class is a `HashSet<Type>` of service types plus a `Contains` (`ServiceCollectionTransformerResolvabilityProbe.cs:40-56`), **with no constructor** and no lifetime inspection."* The file has a public constructor at **`:49-53`**, which null-checks and builds the snapshot at `:52`. It sits inside the cited range. The surrounding argument survives; the stated fact does not.

**Recommendation**: replace *"with no constructor and no lifetime inspection"* with *"whose constructor does nothing but snapshot service types"*. Same length.

##### 11. C-20's heading says the detection is bounded in two ways; the body lists four and closes "All four are accepted gaps" (Score: 62) — **[REQ]**

`requirements.md:386`. Items (iii) and (iv) were added at revisions 8 and 9 without the heading being swept. The constraint is cited by number twelve times and the heading is what a reader carries away.

**Recommendation**: *"bounded in four ways, all deliberate."* One word.

##### 12. C-2's heading claims a scope broader than its body, and ADR 0075's pump-flow bracket sits in the gap (Score: 62) — **[REQ]**

C-2 (`requirements.md:368`) is headed *"The message pump is untouched"*; its body names five types — `Reactor`, `Proactor`, `Dispatcher`, `DispatchBuilder`, `ConsumerFactory`. D0b's table entry repeats the broad form. ADR 0075 (`:380-382`) places a bracket in `Performer.Run()` and argues C-2 by its enumeration rather than its heading. Premises verified: `Performer.cs:31-32` is *"Abstracts the thread that runs a message pump"*, `Run()` is at `Reactor.cs:95` and `Proactor.cs:95`, `Performer` is constructed by `Consumer.cs:98`, and neither `Performer` nor `Consumer` is among C-2's five. The reading is defensible — but a load-bearing decision resting on the difference between a constraint's heading and its body needs the difference stated.

**Recommendation**: retitle C-2 *"Five message-pump types are untouched"* and make D0b's table cell match. Do not add an exemption clause for `Performer`.

##### 13. AC-30 exercises one of FR-24.1's three entry points, and the one whose path the new work does not run (Score: 60) — **[ADR: correct; requirements edit owed]**

Recorded at ADR 0070 (`:334`) and ADR 0072 (`:514`), and correct. FR-24.1 (`requirements.md:234`) states the rule over *"the caller of `Send`/`Publish`/`Post`"*; AC-30's When is `Send` only. The asymmetry is real in source: `PipelineBuilder`'s two catches (`:202`, `:248`) do nothing but wrap — `:202-205` is `throw new ConfigurationException(...)` with no cleanup — while `TransformPipelineBuilder.cs:116` and `:157` sit above `CleanUpAfterFailedBuild` calls at `:122` and `:163`. So AC-30's second conjunct is discharged on the tested path by the pre-existing `PipelineBuilder.Dispose()` (`:269-270`), not by anything this work adds.

**Recommendation**: add a `Post` branch to AC-30 — same provider, same Then. A lettered branch at the end, not a clause inside the existing Then.

##### 14–25 (below threshold)

- **(58) [REQ]** C-8 still asks the ADR to settle what FR-13, FR-24 and AC-8 already require. FR-10 (`:213`) *requires* the seam in `Paramore.Brighter`; FR-13 (`:230`), FR-24 (`:245`) and **AC-8** (`:462`) all name `DisposeAsync()` normatively. The document has a convention for this (C-10 *"CLOSED"*, C-11 *"Naming is settled"*). *Fix*: retitle C-8 *"CLOSED: home and disposal shape of the seam types"*.
- **(56) [REQ]** FR-11's heading over-claims against its own body — *"every pipeline"* vs *"every pipeline that has a `Scoped` participating factory"*. AC-46's first branch asserts the opposite of the heading. *Fix*: *"every `Scoped` pipeline"*. One word.
- **(55) [REQ]** AC-21 is tagged FR-19 and exercises nothing of it. AC-43 and AC-49 are both tagged NFR-10 and assert only that a message contains a literal string. *Fix*: drop the tags.
- **(55) [REQ]** AC-22 clause 1's *"The same test"* has no antecedent — residue of revision 14's deletion of the API-approval clause (`:880` explains it). *Fix*: delete the two words.
- **(52) [REQ]** Both Acceptance-Criteria preamble enumerations are stale: **AC-40** missing from the configuration-subject list, **AC-52** missing from the `Warning`-count list, and AC-52's *"the identical host"* should be *"a second host of the same shape"*, the wording revision 12 established.
- **(50) [REQ]** AC-32's *"the pipeline resolved via the last-registered provider"* has no instrument — the Given makes the two providers indistinguishable at the resolution the Then reads. Probe confirms the mechanism (`GetService` → last, `GetServices` → both). *Fix*: three words in the Given.
- **(48) [REQ]** Residual citation drift at three further sites: `BrighterPipelineValidationExtensions.cs:88-90` → `:91-93`; `GetOrCreateSingleton` cited as `:151-155` twice and `:152-157` once (the method is `:152-157`); FR-5 names only `TransformPipelineBuilderAsync.cs:122`/`:163` for a rule FR-2 requires of both builders.
- **(46) [REQ]** FR-22.3's out-of-core transform parenthetical: there are **three**, not two — `JustSayingCompressionTransform`, **`JustSayingTransform`**, `MassTransitTransform` — and MassTransit ships one type, not "transforms" plural. The universal claim (all parameterless) is **true**.
- **(42) [REQ]** AC-26's quoted exception message is not what MS DI produces. Probed: **"Cannot resolve 'HandlerLikeBrighter' from root provider because it requires scoped service 'Scoped1'."** The quoted form comes from resolving the scoped service itself from root. The note asserts nothing, so no Then breaks.
- **(40) [REQ]** The revision history's fourth count is undefined and does not reconcile. Recounted: **27 FRs, 10 NFRs, 52 ACs, 22 constraints, 14 out-of-scope items**. Under the only reading that makes revision 19's `122` right, revision 21 should read **125**, not 123. *Fix*: drop the fourth number, matching revisions 15–18 and the README.
- **(38) [REQ]** Section ordering: NFR-8, **NFR-10, NFR-9**; constraints run C-1 … C-11, **C-12a, C-12, C-19, C-18, C-17, C-20, C-13, C-14, C-15, C-21, C-16**. Residue of appending. No citation is affected.
- **(36) [REQ]** Three "Where it lands" entries in the decisions table point at nothing: **D6**'s cell names FR-9, which never cites D6; **D16**'s names FR-24; **D10**'s names FR-6. The column's disclaimer covers omissions, not wrong entries.

---

#### Verified CLEAN — do not re-derive

**Citations opened at the line and correct** — every `CommandProcessor.cs` citation in the document (`:317`, `:394`, `:472`, `:474`, `:481`, `:575`, `:581`, `:591-599`, `:601`, `:795`, `:1502`); `ServiceProviderMapperFactory.cs:44`, `:46`, `:55-59`, `:61-65`, `:68-73`, `:78`; `ServiceProviderTransformerFactory.cs:46`; `ServiceProviderLifetimeScope.cs:110`, `:118-123`, `:126`, `:132-142`, `:136`, `:163-178`, `:167`, `:259-261`, `:346-350`; `ServiceProviderHandlerFactory.cs:67-68`, `:85-86`, `:102-107`, `:104`, `:127`, `:127-131`, `:133-137`; `BrighterOptions.cs:20`, `:37`, `:52`, `:69`; both `ServiceCollectionExtensions.cs` files at `:60`, `:69`, `:69-71`, `:74`, `:74-75`, `:97`, `:127`, `:250`, `:386`, `:431`, `:484`, `:487`, `:646`, `:648` and ServiceActivator's `:29`, `:36-38`, `:38`, `:38-39`, `:78`, `:88`, `:89-90`; `BrighterPipelineValidationExtensions.cs:47-52`, `:58`, `:64-69`; `BrighterValidationHostedService.cs:73`; `BrighterPipelineValidationOptions.cs:47`; `PipelineValidationResult.cs:52-56`; `IAmAPipelineValidator.cs:38`; `RequestHandlerAttribute.cs:91`; `TransformAttributeBase.cs:17`; `ClaimCheckTransformer.cs:62`; `JustSayingCompressionTransform.cs:34` and `JustSayingDecompressAttribute.cs:29`; `Proactor.cs:239` then `:241`; `TransformPipelineBuilderAsync.cs:122`, `:163`; all twelve C-17 registration sites, every one `ServiceLifetime.Transient`; all twelve C-19 factory-interface member lines; `MsSqlEntityFrameworkCoreTransactionProvider.cs:18`; `samples/WebAPI/WebAPI_EFCore/GreetingsWeb/Startup.cs:132`; `FactoryLifetimeTests.cs:36-55` and `:154`; the three AC-14 exclusion filenames.

**Counts recounted and correct** — the **six** factory interfaces; exactly **three** `GetService<IAmABoxTransactionProvider>` sites (`:431`, `:487`, `:648`) and both outbox services registered `Singleton` (`:454`, `:501`), so all three of C-21's bounds hold; the **four** registration entry points; the **six** validation messages, three errors and three warnings, consistent across FR-25.10, AC-25, AC-43 and AC-44; AC-13's **five** adoption decisions; the **thirteen**-item release-note ledger (5 + 8) and its 13 − 4 = **9** unreached items; ADR 0073's **37** test projects; every Brighter decorator type name FR-22.3 enumerates.

**Claims about the codebase, checked** — the *misnamed existing test* note (`:863`) is exactly right. C-20(iv)'s *"Brighter ships no mapper with constructor dependencies today"* — verified across all twelve Brighter-shipped mappers, every one parameterless. AC-42's `ClaimCheckTransformer` case is well-founded. `tests/Paramore.Brighter.Extensions.Tests` sets no `AssemblyName`, so AC-42's prefix case rests on a real assembly simple name. NFR-3 holds today (one `ProjectReference`, no `PackageReference`); NFR-1 clause 2 and NFR-2 hold today. AC-22.3's scan passes today: zero matches in `src/Paramore.Brighter/`.

**Coverage** — every one of the 27 FRs, 10 NFRs and 52 ACs is named by at least one of the seven ADRs' `## References` requirement lines. No orphan requirement and no orphan criterion.

**Markup** — no mermaid blocks, no HTML-escaped entities, no broken table pipes, no stray `<see cref>`. Every sub-clause reference resolves. The `#decisions-d0d19` anchor is correct.

**Probes that confirmed rather than falsified** (net9.0, MEDI 9.0.0):
- **C-1** — `IServiceScopeFactory` from a scoped provider is reference-equal to the one from root, and the scope it creates is root-parented. C-1 is true as written.
- **AC-33's stated premise** — MS DI does **not** swallow a tracked disposable's exception: `scope.Dispose()` threw `InvalidOperationException`.
- **FR-24.3 / FR-22.4's last-descriptor-wins** — `GetService` returned the second registration, `GetServices` returned both.
- **AC-42's tie-case parenthetical** — two same-count public constructors gave `InvalidOperationException: … The following constructors are ambiguous`. The superset pair resolves, so C-20(i)'s divergence is real.

#### Gaps

- No ASP.NET host was built, so **AC-26's** claim that `ValidateScopes` is on by default in a test host, and everything in AC-15/16/17/19/34/48/49/52 that depends on `WebApplicationFactory` semantics, is unverified by probe.
- No part of the Brighter test suite was run, so **AC-14's** *"it passes"* half is assessed from the document and the ADRs only.
- The spec `README.md` is stale against the document reviewed — it records the requirements as *"APPROVED at revision 15"* with *"Revision 16 … awaiting its own review round"*, while `requirements.md` is at revision 21 and PENDING. Not treated as in remit and not scored.
- Requirements-side prose readability against `### ADR readability` was not scored: that house-style section is written for ADRs, and no reviewer in this round was given the requirements' own readability as a remit.


### Gap coverage B — the decision tables as executable specifications

**13 findings. 7 at or above the threshold of 60. 0 Critical · 1 High · 6 Medium · 6 Low.**

| # | Score | One line |
|---|---|---|
| 1 | 80 | Keyed `ServiceDescriptor`s are invisible to 0074's snapshot and defeat its "last descriptor" reading — FR-22.4 can raise a false `Error` against a host 0076 explicitly protects |
| 2 | 70 | 0075's NFR-9 row-family statement ("for each of the three lifetimes … its own scope") is false for `Singleton` and `Transient`, and contradicts 0072's identity rule and ladder row 1 |
| 3 | 68 | 0074's captive-dependency funnel has no branch for "no constructor selected", none for open generics and none for a descriptor with no statically known implementation type — three of its own enumerated failure modes |
| 4 | 64 | A provider whose ambient names the **root** provider passes all three probe tests under `ValidateScopes: false` and is borrowed from — process-wide `Scoped` artefact identity, never disposed. Not in the two-part residue |
| 5 | 62 | 0074:491's "no single triple can serve a `Singleton` mapper and a `Singleton` transform at once" is false — `{Transient, Singleton, Singleton}` is FR-22.2-conformant and does exactly that |
| 6 | 62 | The pseudo-code calls step 2's return "an OWNED handle", the word the ladder paragraph reserves for rows 3–10, on the distinction the ADR itself says an implementor must not get wrong |
| 7 | 60 | Transform family with `Scoped` transformer and the v9 null transformer factory: zero asks, no scope — falsifying 0072's own iff-rule at `:615`, for the case `:610` goes out of its way to include |
| 8 | 56 | Ladder row 1 tests the handler factory **negatively** (`is Singleton`) where it tests mapper/transformer **positively** (`not Scoped`), so an out-of-enum `ServiceLifetime` makes an ask FR-27.1 forbids |
| 9 | 52 | `ScopeAffinityPolicy`'s "null options ⇒ `AlwaysNew` unconditionally" rule is unreachable: rows 1 and 2 read the same null fallbacks and return before step 3 |
| 10 | 50 | "Both host shapes, enumerated" claims six combinations but its two mixed rows name only `AddConsumers(Action)`; the `Func` overload is behaviourally distinct (C-12) and appears only in prose |
| 11 | 48 | Rows 8–10 omit the `JoinAmbient` guard rows 5–7 carry, so they are simultaneously satisfiable with row 5 and correct only via the ordering preamble; the pseudo-code makes it structural instead |
| 12 | 48 | `BrighterValidationHostedService` "logs errors at `:84`" and `ServiceActivatorHostedService` "at `:61`" point at the `foreach` headers, not the `LogError` calls (`:86`, `:63`) |
| 13 | 45 | "Three brackets, five places to get wrong" omits the sixth place 0075's own touched-types row puts in this ADR's commit — the single `IsSuppressed` read in the five factories |

---

#### Findings at or above threshold

##### 1. Keyed `ServiceDescriptor`s break 0074's "last descriptor" reading, and FR-22.4's failure direction is a false `Error` (Score: 80)

0074 reads the collection in three places and every one keys off descriptor position and `ImplementationType`: `EffectiveLifetimeOf` (`0074:307`); `DescriptorsFor` (`:309`); FR-22.4 (`0074:445`); and the funnel's first node (`0074:470`). None of the six rule rows, the funnel, or the *Failure modes, enumerated and accepted* table mentions a keyed descriptor.

**Evidence** (probed against MEDI 10.0.10, the pinned version, `Directory.Packages.props:89`):

- `AddKeyedSingleton<IOpt, AppOpt>("tenantA")` → `IsKeyedService = True`, **`ImplementationType = null`**, **`ImplementationInstance = null`**, `KeyedImplementationType = AppOpt`. Keyed artefact and keyed override registrations are silently dropped at the funnel's first node and contribute nothing to FR-17's distinctness set.
- `TryAddSingleton<IOpt, BrighterOpt>()` then `AddKeyedSingleton<IOpt, AppOpt>("tenantA")`: the **last descriptor for `IOpt` is the keyed one**, while unkeyed `GetService<IOpt>()` returns `BrighterOpt`. FR-22.4 as written reads the keyed descriptor, finds it is not Brighter's, and raises an `Error` that fails startup on a host where the write-through worked perfectly.
- `AddScoped<IDb, Db>()` then `AddKeyedSingleton<IDb, Db>("x")`: last-descriptor lifetime `Singleton`, unkeyed truth `Scoped` — FR-22.3 withholds a real captive-dependency warning; the reverse ordering warns wrongly.

Not hypothetical: `0076:393-395` names a keyed `IBrighterOptions` registration as a supported shape and spends a paragraph making `RegisterBrighterOptions` preserve it, precisely so that "ADR 0074's rule would additionally report an `Error` against an application that did nothing wrong" does not happen. The `ServiceKey` clause protects the registration; nothing protects the *rule*. 0074's own *Negative* calls a false `Error` "the worst direction for a rule to be wrong in" (`:736`).

**Recommendation**: narrow the three readings at their source rather than qualifying them. In *The three questions `ContainerRegistrationSnapshot` answers* (`0074:305-309`), make each bullet say "the last **unkeyed** descriptor" and add a fourth bullet naming keyed descriptors as out of the snapshot's subject; in the FR-22.4 conjunct (`:445`) write "the last unkeyed descriptor for that service type". Then add one row to the *Failure modes, enumerated* table — "A keyed registration of an artefact type, a provider, an override or a dependency | not read; the rules are stated over unkeyed descriptors, which is what unkeyed resolution reads | new" — since that table is the section that claims exhaustiveness.

##### 2. 0075's NFR-9 row family is false for `Singleton` and `Transient`, and contradicts 0072 (Score: 70)

0074's clause map assigns two row families of NFR-9's truth table to 0075 (`0074:699`). 0075 writes them as: "**For each of the three configured lifetimes and both affinities**, the source a subscriber's pipeline resolves from is **its own scope** and never the ambient" (`0075:427`, repeated at `:46`).

The second half is true. The first is false in two of the three lifetimes it quantifies over. 0072 says "`Singleton` sits outside both, resolving from the root provider" (`0072:502`), and ladder row 1 gives a `Singleton` handler factory no scope at all — `CreatePipelineScope()` returns `null` (`0072:142`; `0071:512`, `:368`). A `Singleton` subscriber pipeline has no "own scope". Under `Transient` the source is ADR 0067's *per-resolution* scope, one per resolution rather than one per pipeline, and 0072 row 2 is explicit that this handle "is not what FR-27 means by a pipeline scope" (`0072:159`). **0072 verified as correct**: `ServiceProviderHandlerFactory` resolves `Singleton` handlers through `_singletonScope`, built over the root provider (`ServiceProviderHandlerFactory.cs:52`).

Since the guidance page is written from these rows, the false row family propagates into the deliverable NFR-9 is discharged by.

**Recommendation**: replace with the narrower true statement, still one row-family sentence: "A subscriber's pipeline never resolves from the ambient, whatever the affinity. Under `Scoped` it resolves from a scope Brighter created for it; under `Transient` from ADR 0067's per-resolution scope; under `Singleton` from the root provider." Correct `0075:46`'s compressed restatement in the same edit.

##### 3. The captive-dependency funnel is not exhaustive over its own enumerated failure modes (Score: 68)

The section opens "FR-22.3 runs as a funnel, and each stage answers one question" (`0074:466`) — the flowchart is offered as the executable form of the rule. Rendered with `mmdc` and read, it has seven nodes and no branch for three cases the same section enumerates:

- **No constructor selected.** `ArtefactConstructorSelector`'s contract has three outcomes — "the public constructor with the most parameters, and on a tie, none" (`0074:299`), and "A type with no public constructor, or with only a parameterless one, also yields nothing" (`0074:515`). The `ctor` node flows unconditionally into `parm`, so the "none" outcome has nowhere to go. The failure-mode table has the row (`:534`) the diagram cannot express.
- **Open generics.** The table row says "Open generic artefact type → not inspected" (`:530`), and `0074:511` argues against "the open-generic rule **below**". There is no such rule anywhere in the ADR. Under the funnel as drawn, an open generic *is* inspected: an open generic type definition does report its marker interface, so it clears node `k`. The two statements disagree.
- **No statically known implementation type** (`:531`) is only implicit in node `k`.

**Recommendation**: add the one branch the diagram is actually missing — an edge from `ctor` labelled *"none — a tie, or no public constructor"* to `skip2`, which already exists as *not inspected*. For the other two, fix the source: replace `0074:511`'s "the open-generic rule below" with a pointer to the failure-mode row, and make that row say what actually happens ("not reported: a parameter typed by a type parameter has no descriptor") rather than "not inspected", which the funnel contradicts.

##### 4. An ambient naming the root provider passes the probe and is borrowed from (Score: 64)

*The residue is stated rather than claimed away, and there are two parts to it* (`0072:598`) names exactly two survivors. There is a third.

**Evidence** (probed): with `sc.AddScoped<Cache>()` and a plain `sc.BuildServiceProvider()` — no `validateScopes` — resolving `Cache` **from the root provider succeeds** and returns one process-wide instance; `GetService<IServiceScopeFactory>()` on the root is non-null. So an `IAmAServiceProviderScope` whose `Services` is the root provider passes all three positive tests including the `ScopedArtefactCache` one, and the pipeline borrows. The consequence is what `ScopedArtefactCache` exists to prevent: one artefact cache for the life of the process, disposed by nothing, falsifying FR-1 and FR-2 rather than FR-16(a). Under `BuildServiceProvider(validateScopes: true)` the same resolve throws `InvalidOperationException`, which probe outcome 4 catches — so the failure is silent on exactly the hosts that have scope validation off.

**Recommendation**: this belongs in the residue paragraph as a third part rather than as a fourth probe outcome — the probe cannot cheaply distinguish a root provider from a scoped one. One sentence: "*A provider that offers the root provider* passes all three tests where the container was built without `ValidateScopes`, and the borrow then gives process-wide artefact identity. The contract on `Services` forbids it and the probe cannot detect it."

##### 5. "No single triple can serve a `Singleton` mapper and a `Singleton` transform at once" is false (Score: 62)

`0074:491` offers this as a property of its own rule set, and as the reason AC-42 is shaped as it is.

`{HandlerLifetime = Transient, MapperLifetime = Singleton, TransformerLifetime = Singleton}` serves both at once, and it is FR-22.2-conformant by the ADR's own rule — discard the two `Singleton`s, the remainder `{Transient}` is uniform (`0074:104`, FR-22 rule 2). So is `{Singleton, Singleton, Singleton}` under `AlwaysNew`. The claim originates in AC-42's own text and 0074 restates it, but 0074 presents it as following from its rule table rather than citing the criterion, so it reads as a derived property and is not one.

**Recommendation**: rewrite to the true, narrower reason — the two kinds are governed by two different settings: "The kind selects the setting, so AC-42's mapper and transform cases vary `MapperLifetime` and `TransformerLifetime` independently." A correction is owed to AC-42 in the requirements true-up as the source of the error.

##### 6. The pseudo-code uses the word the ladder reserves (Score: 62)

Row 2 returns "**a handle, but not an FR-27 pipeline scope**" (`0072:143`), and "rows 3–10's `OWNED` is reserved for one that is. An implementation asserting AC-46's 'no pipeline scope taken' over the handle's nullness is testing the wrong thing" (`0072:159`). The pseudo-code then writes step 2 as `-> return an OWNED handle, make NO ask [FR-27.1]` (`0072:530-531`) — the reserved word for the one return the paragraph says it must not describe, in the artefact an implementor reads.

**Recommendation**: change step 2's right-hand side to `-> return ADR 0067's handle, make NO ask [FR-27.1]`, which is the ladder row's own wording and no longer.

##### 7. A `Scoped` transformer with no transformer factory asks nothing (Score: 60)

`0072:610` fixes the transform pipeline's participating set as `{MapperLifetime, TransformerLifetime}` — "both, always … and **whether or not a transformer factory instance exists at all** (`TransformPipelineBuilder.cs:180`'s v9 null path)". `0072:615` then states the rule: the pipeline "takes a pipeline scope and asks, exactly once, **if and only if** `Scoped` is in the set".

Those cannot both hold. ADR 0070 delivers the ask by first-non-null routing, and "the second ask is null-conditional" (`0070:436`). `TransformPipelineBuilder.cs:180` is `if (_messageTransformerFactory is null)`. So with `{MapperLifetime = Transient, TransformerLifetime = Scoped}` and no transformer factory, the mapper registry returns `null` at row 1 and there is no second participant to ask: zero asks, no pipeline scope, with `Scoped` in the set. The ladder has no row for it, and `0072:173`'s walk of exactly this lifetime pair is the case that does not happen. Reachability is bounded — `ServiceCollectionExtensions.cs:945` always supplies a `ServiceProviderTransformerFactory`, so this is the hand-built v9 shape — but 0072 chose to include that shape in the participating-set rule.

**Recommendation**: fix the participating-set table row, which is the source. Replace "whether or not a transformer factory instance exists at all" with the true narrower statement — the transformer counts as a participant whenever a transformer factory exists, and a pipeline built with none takes its scope from the mapper factory or takes none. Then `0072:615`'s iff-rule is true as written and needs no change.

---

#### Findings below threshold

**8 (56).** Row 1's handler clause is a negative test — "for the handler factory, is `Singleton`" (`0072:142`, matching `0071:512`) — where its mapper and transformer clauses are positive ("is not `Scoped`"). `HandlerLifetime` is a plain non-nullable `ServiceLifetime` on a public options interface (`BrighterOptions.cs:20`), so a cast integer falls through rows 1 and 2 and reaches the ask, which FR-27.1 forbids. 0072 argues the exactly analogous point for `ScopeAffinity` at `:378` and makes no equivalent statement here. *Fix*: state row 1's handler clause positively — "is not `Scoped` and not `Transient`".

**9 (52).** `ScopeAffinityPolicy`'s ctor contract row promises "`AlwaysNew` **unconditionally** when the options object is `null`" (`0072:370`). With null options, `ServiceProviderMapperFactory.cs:45` and its three siblings fall back to `Singleton` and `ServiceProviderHandlerFactory.cs:50` to `Transient` — so row 1 or row 2 fires for all five factories and step 3 is never reached. Defensive rather than wrong; the ADR does not say so.

**10 (50).** *Both host shapes, enumerated* (`0074:557-564`) is cited as walking "all six combinations" (`0074:132`), but rows 4 and 5 name only `AddConsumers(Action)`. C-12 and 0074's own prose (`:402`, `:574`) make the `Func` overload behaviourally distinct — in one ordering it throws `InvalidCastException` before any host starts. *Fix*: give the mixed rows a `Func` column or two more rows.

**11 (48).** Rows 8, 9 and 10 omit the `JoinAmbient` guard rows 5–7 carry, so row 5 and each of rows 8–10 are simultaneously satisfiable and correct only via the "first that matches decides" preamble (`0072:138`). The pseudo-code makes the exclusion structural (`0072:543`), so the two restatements are not row-for-row legible.

**12 (48).** `0074:549` — `:84` and `:61` are the `foreach` headers; the `LogError` calls are at `:86` and `:63`. The warning citations in the same sentence are ranges covering whole loops, so the style is inconsistent within one sentence.

**13 (45).** "Three brackets, **five** places to get wrong" (`0075:472-482`) counts bracket sites only. `0075:292` puts a sixth edit in this ADR's commit — the single `IsSuppressed` read in the five container-backed factories.

---

#### The enumeration

##### A. 0072's ladder, handler family (`CreatePipelineScope()` on `ServiceProviderHandlerFactory`)

| `HandlerLifetime` | Suppressed? | Affinity option | Provider | Row | Outcome | Diagnostic |
|---|---|---|---|---|---|---|
| `Singleton` | any | any | any | **1** | `null`; no next participant; no ask | none |
| `Transient` | any | any | any | **2** | ADR 0067 handle; **no ask** | none |
| out-of-enum | any | any | none | **3** ⚠ | OWNED; ask made where FR-27.1 forbids one (finding 8) | none |
| `Scoped` | yes | any | none | 3 | OWNED, no ask | none |
| `Scoped` | no | `AlwaysNew`/out-of-enum | none | 3 | OWNED, no ask | none |
| `Scoped` | no | `JoinAmbient` | none | 3 | OWNED, no ask | none |
| `Scoped` | any | any | throws | **4** | inner rethrown unwrapped through the builder `catch` | none |
| `Scoped` | yes | any | returns non-null | **5** | OWNED; ambient ignored unprobed, not disposed | *ignored for `AlwaysNew` ask* |
| `Scoped` | yes | any | returns `null` | **6** | OWNED | none |
| `Scoped` | no | `AlwaysNew`/out-of-enum | non-null | 5 | OWNED | *ignored for `AlwaysNew` ask* |
| `Scoped` | no | `AlwaysNew`/out-of-enum | `null` | 6 | OWNED | none |
| `Scoped` | no | `JoinAmbient` | `null` | **7** | OWNED | *no ambient offered* |
| `Scoped` | no | `JoinAmbient` | non-role `IAmAScope` | **8** | OWNED; declined, not disposed | *offered but unusable* |
| `Scoped` | no | `JoinAmbient` | role, probe fails | **9** | OWNED; declined, not disposed | *offered but unusable* |
| `Scoped` | no | `JoinAmbient` | role, probe passes | **10** | BORROWED | none |
| `Scoped` | no | `JoinAmbient` | role, probe passes, `Services` = **root** | 10 ⚠ | BORROWED over root — process-wide artefact identity (finding 4) | none |

Every combination lands on exactly one row; the two `⚠` rows are outcomes the ADRs do not describe. Rows are exhaustive **given** row 1's handler clause is read as "not `Scoped` and not `Transient`"; as written (`is Singleton`) the out-of-enum row leaks.

##### B. 0072's ladder, transform family — which participant is asked

| `MapperLifetime` | `TransformerLifetime` | Transformer factory present? | Who is asked | Affinity under option = `JoinAmbient`, unsuppressed |
|---|---|---|---|---|
| `Transient` | `Transient` | either | nobody (row 1 twice) | — no scope, no ask |
| `Transient` | `Scoped` | yes | transformer | `AlwaysNew` (a `Transient` participates) |
| `Transient` | `Scoped` | **no** | **nobody** ⚠ | — no scope, no ask, `Scoped` in the set (finding 7) |
| `Transient` | `Singleton` | either | nobody | — |
| `Scoped` | `Transient` | either | mapper | `AlwaysNew` |
| `Scoped` | `Scoped` | either | mapper | **`JoinAmbient`** |
| `Scoped` | `Singleton` | either | mapper | **`JoinAmbient`** (`0072:617`) |
| `Singleton` | `Transient` | either | nobody | — |
| `Singleton` | `Scoped` | yes | transformer | **`JoinAmbient`** |
| `Singleton` | `Scoped` | **no** | **nobody** ⚠ | — (finding 7) |
| `Singleton` | `Singleton` | either | nobody | — |

Suppression (0075) collapses every `JoinAmbient` cell to `AlwaysNew` at step 3, adding no row. `ForTransformPipeline`'s stated contract (`0072:372`) reproduces this matrix exactly; the two `⚠` cells are where the routing, not the policy, fails.

##### C. 0074's six rules — co-firing matrix

Rows 1 and 2 partition all 27 lifetime triples × 2 affinities:

| Affinity | FR-22.1 fires | FR-22.2 fires | Neither |
|---|---|---|---|
| `AlwaysNew` | 0 | 12 (≥1 `Transient` **and** ≥1 `Scoped`) | 15 |
| `JoinAmbient` | 8 (no `Scoped`) | 12 | **7** |

The 7 that pass under `JoinAmbient` are exactly the triples over `{Scoped, Singleton}` with at least one `Scoped` — the set `0074:709` derives for FR-25 clause 9. **Verified arithmetically; the derivation is correct.**

Pairwise co-firing: only the 22.1/22.2 cell is exclusive, and 0074's proof of it holds (FR-22.2 requires a `Scoped` in the remainder, FR-22.1 requires none anywhere). Every other pair can co-fire. No precedence is needed anywhere else and none is stated. **Clean.**

##### D. Composition — the 7 `JoinAmbient`-conformant triples through 0072's ladder

| (H, M, T) | Handler pipeline | Transform pipeline |
|---|---|---|
| S, S, S | row 10 — adopt | row 10 — adopt |
| S, S, Sg | row 10 — adopt | row 10 — adopt; `Singleton` transformer resolves from root |
| S, Sg, S | row 10 — adopt | row 10 via the transformer (⚠ finding 7 on the v9 null path) |
| S, Sg, Sg | row 10 — adopt | row 1 twice — no scope, no ask |
| Sg, S, S | row 1 — `null`; `Create` uses `_singletonScope` | row 10 — adopt |
| Sg, S, Sg | row 1 — `null` | row 10 — adopt |
| Sg, Sg, S | row 1 — `null` | row 10 via the transformer (⚠ finding 7) |

Every FR-22-conformant configuration is described by the two ADRs except the v9 null-transformer-factory variant. No configuration exists that 0074 reports on and 0072 never reaches.

---

#### Verified CLEAN — do not re-derive

**Probes that confirmed rather than falsified** (net10.0, MEDI 10.0.10):

- 0075's restore table, sync `Publish` / bracket 2: **confirmed** at 200 items and at 3 items (which forces the calling-thread replica). Caller reads `False` both times.
- `0075:390`'s cross-body leak on a shared worker: **confirmed and large** — 1994 of 2000 bodies saw a leaked `true` on entry.
- `0075:400`'s "bracket 1 sync is load-bearing": **confirmed**.
- `0075:402-404`'s "no `AsyncLocal` write inside `PublishAsync` can reach its caller's flow": **confirmed**.
- 0074's registration model: `TryAddSingleton` then `AddSingleton` → `GetServices` yields both in order, `GetService` yields the last. `0074:371`'s break claim **confirmed**. `GetServices` on an empty collection is an empty sequence, not null.
- `0074:424`'s idempotence claim: **confirmed for the singular resolution** the design uses.
- `0072:569`'s probe design: a scope of a foreign container returns **null** for a `TryAddScoped` concrete type but **non-null** for `IServiceScopeFactory` — the `ScopedArtefactCache` test is genuinely the discriminating one. A **disposed** scope throws `ObjectDisposedException` on both resolutions.

**Citations opened and correct**: `BrighterOptions.cs:20/:37/:52/:69`; the five factory constructor reads and both null fallbacks; `ServiceProviderLifetimeScope.cs:48/:49/:152/:163-178/:185`; `PipelineBuilder.cs:37/:59/:76/:92/:151/:187-198/:193/:202/:248/:248-251/:269-270`; `TransformPipelineBuilder.cs:116/:157/:180/:255/:270` **and identical line numbers in `TransformPipelineBuilderAsync`**; `CommandProcessor.cs:458/:472/:474/:481/:489/:559/:575/:581/:591-599/:596/:601`; `MessageMapperRegistry.cs:360-362`; `PipelineValidator.cs:45-51/:54/:85/:92-93/:139/:152`; `PipelineValidationResult.cs:45/:52/:64`; `BrighterPipelineValidationExtensions.cs:58/:64-66/:68-69/:71/:75/:79/:85-88/:91-93/:116/:135-142`; `BrighterValidationHostedService.cs:47/:60/:71/:73/:76/:80/:90-93`; `ServiceActivatorHostedService.cs:50-53/:57/:67-70/:74`; `ServiceActivator…/ServiceCollectionExtensions.cs:60/:127`; `src/Directory.Build.props:43`.

**Counts recounted and correct**: 125 test files registering `IBrighterOptions`; six builder `catch` clauses; ten new DI-package types with `ValidationMapperRegistry` listed last; four failed-probe outcomes; six converging failures (twice); five bracket sites.

**Diagrams**: all ten mermaid blocks across 0072, 0074 and 0075 render with `mmdc`. 0074's captive-dependency funnel rendered to PNG and read — it draws what the prose says except for the three missing branches in finding 3. 0072's ladder sequence diagram agrees with the ladder table; 0072's class diagram correctly shows `ServiceProviderPipelineScope` implementing `IAmAScope` only, not the role.

**Markup**: no HTML-escaped entities, no double-escaped generics, no `<see cref>` leakage into prose, no broken table pipes across all three ADRs.

#### Gaps

- The remit named "ADR 0075's suppression truth table". 0075 has no truth table — NFR-9's truth table is declared and owned by 0074 (`0074:42`, `:699`), and 0075 contributes two row families in prose (`0075:427`). 0075's three actual tables were reviewed instead: *What a subscriber must stop*, the restore table, and *five places to get wrong*.
- The Brighter solution was not built; no probe touched Brighter's own types, only Microsoft's container and the CLR.
- The *five places to get wrong* table's `Performer.cs:31-32` and `:62-69` citations, and 0075's `ConfigurationCommandHandler.cs:73/:85` and `Dispatcher.cs:484`, were not opened — the ServiceActivator half of 0075 sits outside the table remit.
- FR-22.3's exclusion-set inputs (`ArtefactExclusionSet.Build`'s four arguments, step 5a) are a plumbing question rather than a table question; only the two citations the funnel depends on were checked.
- 0073's and 0076's own tables were read only where 0074's and 0072's tables compose against them.


### Gap coverage C — `Alternatives Considered` and `Consequences` as one corpus

**14 findings. 10 at or above threshold (60).** 0 Critical · **4 High** · **6 Medium** · 4 Low.

| # | Score | One line |
|---|---|---|
| 1 | 78 | 0070's *Positive* "No hidden state… nothing is per-flow, per-thread or static" is falsified by 0075, which lands three `AsyncLocal` brackets on the same path |
| 2 | 76 | 0072's *Positive* "the seam degrades to today's behaviour on **every** failure path" is falsified by 0072's own ladder row 4 and by two of its own *Negative* bullets |
| 3 | 74 | 0073 alternative 9's fourth ground ("the same ground already carries `ServiceProviderPipelineScope` in ADR 0070") is false of 0070, which says the opposite |
| 4 | 72 | The `InternalsVisibleTo` rule is stated nine times in nine different sentences across five ADRs; 0076 alone treats the attribute as available, and misattributes 0075's ground |
| 5 | 70 | 0073 alternative 9's third ground ("that **one** internal…") is stale inside its own set — ADR 0074 adds ten types to that package, nine of them internal |
| 6 | 70 | Six rejected alternatives are argued in `## Consequences` instead of `## Alternatives Considered`, across four ADRs |
| 7 | 68 | Risks *Mitigation* cells that are arguments, not mitigations — 0076's second row runs 238 words against a corpus median of ~40 |
| 8 | 64 | 0074 records no alternative for "rely on the container's own `ValidateScopes`", the option its own *Negative* calls the complete check |
| 9 | 62 | 0070 alternative 8 is "Rejected by the settled decision on C-8", but C-8 delegates that decision *to the ADR* — the rejection is circular |
| 10 | 60 | 0070 alternative 1 rejects an option for "a second public core type"; the set adds four more core public types for other purposes |
| 11 | 55 | 0076 alternative 6's "the ban is FR-17's alone… nothing in FR-14 forecloses a nullable affinity" — FR-17 grounds the ban in FR-14 by name |
| 12 | 55 | One break, three names: 0070 calls it "D3… (OOS-8)", step 7a calls it "(FR-20)", 0074 and 0076 call it "FR-20's break" |
| 13 | 52 | The middleware alternative is written twice in different words (0072 alt 5, 0073 alt 2) where house style asks for one sentence repeated exactly |
| 14 | 48 | 0070 has no "do nothing" alternative; five siblings open with one and four call it "the honest alternative" — 0074's sits at #8 |

---

#### The inventory — 70 alternatives across seven ADRs

**0070 (13).** 1 additive capability role `IAmAPipelineScopeParticipant` found by type test — *rejected: needs per-flow state, optional at runtime, second core type*. 2 container-package-private `AsyncLocal` ambient — *invisible coupling, no explicit end, needs a core publication point anyway*. 3 shared collaborator at the four construction sites — *not per-pipeline; still needs a key*. 4 overloads not changed signatures — *an interface overload is still a required member*. 5 scope on `Release` too — *two factories, no single owner*. 6 release at end of build — *artefacts must outlive the build; pushes release to six call sites, C-2*. 7 `IAmAChainScope`/`IAmAPipelineScope`/`IAmAUnitOfWorkScope` — *rejected by D4*. 8 `IAmAScope : IDisposable` only — *rejected by "the settled decision on C-8"; Proactor stall*. 9 `Dispatcher` disposes the consumer factories (#4254) — *OOS-12; answers a different question; half the surface; C-2/OOS-5*. 10 widen `ServiceProviderLifetimeScope` to public — *design_principles rule; no outside consumer; no `InternalsVisibleTo`*. 11 raise the seven existing `Warning`s to `Error` — *changes level for non-opt-in apps; loses the discrimination AC-6 wants*. 12 a new AC with a `Then` per break — *step 7a's count keeps moving*. 13 keep the factory-level `Scoped` cache when no scope is passed — *two behaviours per factory; leaves the defect alive on a path*.

**0071 (6).** 1 do nothing, leave handler pipelines on the dictionary — *rejected but serious; pushes two adoption paths onto 0072*. 2 copy 0070's scope parameter — *a second parameter travelling all eight resolution methods forever*. 3 replace `IAmALifetime` with `IAmAScope` — *different jobs; `IAmALifetime` serves user-supplied factories*. 4 give the handler family a token+dictionary and make the transform family match — *cannot work; two factories, Defect 1b*. 5 a second list on `PipelineBuilder` — *index-aligned lists, ordering rule in the wrong object*. 6 `CreatePipelineScope()` on both twins — *`IAmAHandlerFactory` already exists; twins could answer differently*.

**0072 (7).** 1 do nothing, no adoption — *FR-16/FR-17 are the requirement*. 2 public borrowed constructor on `ServiceProviderPipelineScope` — *freezes the construction contract; does not generalise off MS DI*. 3 abstract provider base class — *spends the base class to save a property; wrong shape for both implementers*. 4 resolution member on `IAmAScope` — *ADR 0014; core would abstract a container*. 5 middleware `app.UseBrighterScope()` — *D1/OOS-4*. 6 `IsUsable` on the hand-off role — *the question is the DI package's, not the ambient owner's*. 7 per-pipeline artefact cache on the borrowed handle — *falsifies FR-16(a)/AC-17, contradicts D7*.

**0073 (11).** 1 do nothing, no ASP.NET package — *leaves the motivating case unreachable*. 2 middleware — *D1/OOS-4*. 3 ship the provider in the DI package — *NFR-2/D1, dependency direction*. 4 make it an `IBrighterBuilder` extension — *before-ordering unexpressible; AC-48*. 5 declare it in `Microsoft.Extensions.DependencyInjection` — *no `src/` type does; ungreppable*. 6 declare it in `Paramore.Brighter.Extensions.DependencyInjection` — *a package declaring another assembly's namespace*. 7 `Microsoft.NET.Sdk.Web` project — *a three-type class library with no web assets*. 8 spell it `TryGetAmbient`/`GetAmbientScope` — *`Try*` implies `out`+`bool`; the other is redundant*. 9 make the two types `internal` — *test project out of reach; no `InternalsVisibleTo`; the public/internal ratios; `ServiceProviderPipelineScope` precedent*. 10 hold the `HttpContext` and read `RequestServices` on demand — *breaks the non-null invariant*. 11 `TryAddSingleton` for provider and override — *first-wins vs last-wins split; nothing left to report*.

**0074 (12).** 1 a decorating validator — *closed to extension; cascade must be specified*. 2 a validation spec the core validator consumes — *entity type carries `ServiceLifetime`*. 3 rules in core with core-typed lifetime values — *ADR 0014/AC-22.3; a mirror enum is worse than a violation*. 4 a Roslyn analyzer — *most inputs are not statically visible*. 5 validate eagerly at `AddBrighter` time — *affinity not final; partial collection; OOS-13*. 6 resolve the artefacts and inspect instances — *runs app constructors; throws under `ValidateScopes`; AC-42's final clause*. 7 a separate hosted service — *two hosts, two throws, an ordering question*. 8 do nothing, document the six conditions — *FR-22/D5; silence is the likely outcome*. 9 a named question interface (`IAmATransformerResolvabilityProbe` shape) — *a bool cannot supply the message content*. 10 an opaque bag of pre-bound rules — *hides the collaboration in a closure*. 11 preserve the old escape hatch — *a registration that cancels the others*. 12 let each validator build its own `MessageMapperRegistry` — *a second registry with its own factories and DI scope*.

**0075 (10, incl. 3a).** 1 do nothing, let subscribers adopt — *FR-8; repeals ADR 0039 silently*. 2 suppression on the ambient query — *FR-27.3: a subscriber with no `Scoped` participant never asks*. 3 `internal` + `InternalsVisibleTo` — *repository rule; the list is uncontrollable*. 3a public read, `internal` write — *makes step 4a unimplementable across assemblies; tests out of reach; host write case is real*. 4 reuse `RequestContext.Bag` — *cannot reach; the context is optional*. 5 one bracket round the whole build loop and the whole dispatch — *FR-9(a) outright; async bracket reaches no subscriber at all (probe-confirmed)*. 6 an added `PipelineBuilder` overload — *nothing says which to pick*. 7 a bracket round each subscriber's own task — *cost for no observable gain*. 8 detect a bracket disposed on the wrong flow — *hot-path cost for a caller error Brighter cannot make*. 9 an injected suppression role — *the writers are not container-resolved*.

**0076 (11).** 1 do nothing, no opt-in property — *no configured affinity to consult*. 2 `bool AdoptAmbientScope` — *D13/D4 give one concept two spellings; forecloses a third affinity*. 3 descriptor rewriting — *AC-48's before-ordering kills it*. 4 bring all four paths onto `IOptions` + `PostConfigure` — *four concrete costs, each verified in source*. 5 factories read the override directly — *AC-45 clause 1; two sources of truth*. 6 a nullable sentinel — *FR-17's ban; a tri-state every reader must collapse*. 7 an ordering rule "call the extension last" — *C-10; works on one path of four*. 8 `internal` override + `InternalsVisibleTo` to the ASP.NET package — *NFR-7*. 9 plain `AddSingleton` for `IBrighterOptions` — *discards a deliberate registration; still loses the after-ordering*. 10 a bare `ScopeAffinity` service — *primitive obsession*. 11 route `IAmConsumerOptions` through `IBrighterOptions` — *imports an `InvalidCastException` to remove an unobservable state*.

---

#### The findings in detail

**1. (78) 0070's "No hidden state" is a *Positive* that 0075 records as a *Negative*, and 0070 carries no correction.**
0070 *Positive*: "**No hidden state.** The scope is an argument on the stack. Nothing is per-flow, per-thread or static, so there is no `ExecutionContext` behaviour to reason about, nothing a debugger cannot show you next to the `Create` call, and nothing for a future change to accidentally move across an `await`." 0075 *Negative*: "**The design is no longer free of per-flow state.** ADR 0070 removed all of it and listed 'no hidden state' as a positive, and this ADR puts one bit back… A reader now has `ExecutionContext` semantics to hold in mind on the `Publish` paths." 0075's flag is read *inside* `CreatePipelineScope()` — 0070's own mechanism as extended by 0072 — so after 0075 lands, "there is no `ExecutionContext` behaviour to reason about" is false of exactly the path 0070's bullet is about. **0075 verified as correct**: it ladders three brackets across three files. Corroborating: 0070 alternative 1's *first* rejection ground is "the scope has to reach `Create` by per-flow state… exactly the kind of invariant a later change breaks silently" — the cost the set then pays.
**Recommendation**: rewrite 0070's bullet to the narrower true thing and delete the per-flow clause, which is the sibling's to own: "**The scope is visible at the call site.** It is an argument on the stack — a debugger shows it next to the `Create` call, and no future change can move it across an `await`." Shorter than what it replaces. The generator is 0070 alternative 1's first ground, which should stop asserting that per-flow state is a shape the design refuses and say only that the shape cannot carry a *scope*.

**2. (76) 0072's *Positive* claims a degradation guarantee its own ladder row 4 and its own *Negative* deny.**
0072 *Positive*: "**The seam degrades to today's behaviour on every failure path.** No provider registered, provider returned nothing, ambient stale, ambient from a container this package cannot use, ambient offered for an `AlwaysNew` ask, suppression in force — all six converge on *create and own a scope*." The six are correct. But `0072:145`, ladder row 4, is a failure path that does not degrade: "the ambient source throws | the fault is wrapped in `AmbientScopeSourceException`… then the **original** is rethrown **unwrapped** — a misconfigured container is a startup-class fault, **never degraded to 'no ambient'**". Two *Negative* bullets say the same: "A provider that passes both tests and still cannot resolve Brighter's artefacts **fails loudly, not quietly**… raises `ConfigurationException`"; "A borrowed scope disposed after the probe fails loudly too." The ladder (ten rows) verified as correct; the *Positive* headline is the defect.
**Recommendation**: replace the bold lead with the count the bullet's own list supports, and leave the rest of the bullet unchanged: "**Six of the seam's outcomes converge on one behaviour — create and own a scope.**" No qualifier appended, one word shorter, and it no longer contradicts row 4.

**3. (74) 0073 alternative 9's fourth ground misattributes its own rule to ADR 0070.**
0073 alt 9 states the convention as "a type is public if it belongs on its package's boundary to be **used** or to be **tested** from outside the assembly", then closes "Fourth, the same ground already carries `ServiceProviderPipelineScope` in ADR 0070." 0070 says the opposite. 0070 alternative 10: "Nothing in this set requires that type to be public either, and this ADR does not claim otherwise… no type test in the set crosses a package boundary onto it. **The public shape is the package's default rather than a contract this design needs.**" And 0070 *Technology Choices*: "Public is the DI package's own convention: seventeen of the eighteen classes… are public." **0070 verified as correct** (its own alternative 10 and *Technology Choices* agree). The same conflation weakens 0073's second ground: the 824:91 and 17:1 ratios — both recounted and **correct** — are evidence of "public by default", not of "public because used or tested from outside".
**Recommendation**: 0073's own first ground is decisive by its own words ("and the first is decisive"). Delete the fourth ground rather than restate it, and rewrite the second to the convention 0070 actually records: "Second, public is this package family's default — seventeen of the eighteen classes in the DI package are public."

**4. (72) The `InternalsVisibleTo` rule is stated nine times in nine different sentences, and 0076 alone treats the attribute as available.**
`documentation.md:114` requires: "**State the unifying rule once, in one sentence**, and repeat that exact sentence in every sibling ADR that applies it." The nine statements: `0070:688`; `0073:489`; `0073:512`; `0074:635`; `0075:312`, `:332`; `0075:501`; `0075:505`; `0076:340`, `:562`. **0076 is the divergence**: alternative 8 is "**Rejected on NFR-7**… an `InternalsVisibleTo` list can only name packages Brighter knows about" — the attribute is available and merely insufficient — and `0076:340` says "`InternalsVisibleTo` would serve the first caller and no other". Under 0075's and 0073's statements the option is not available at all. 0076 alt 8 then adds a false cross-ADR attribution: "ADR 0075 rejects `InternalsVisibleTo` for suppression **on the same ground**." 0075 alternative 3's stated ground is the repository rule, with NFR-7 explicitly secondary. **0075 verified as correct on the underlying fact**: one hit repository-wide, a comment at `src/Paramore.Brighter.BoxProvisioning.Spanner/SpannerBoxMigrationRunner.cs:131`.
**Recommendation**: pick 0075 alternative 3's sentence as the one, and repeat it verbatim wherever the rule is used. In 0076 alternative 8, replace "**Rejected on NFR-7**" with the rule plus the sibling pointer, and drop the "on the same ground" clause: "**Rejected: this repository does not use `InternalsVisibleTo`, anywhere, without exception.** Even were it available, the list could only name packages Brighter knows about, and NFR-7 anticipates ones it does not." One sentence shorter than the current pair.

**5. (70) 0073 alternative 9's third ground is stale against ADR 0074, in the same release.**
0073 alt 9: "the DI package this one references is **seventeen public to one internal**. Third, **that one internal** — `ServiceProviderLifetimeScope` — is exactly the shape the exception is for." Both counts correct today (multi-line class-declaration scan: 17 public, 1 internal). But ADR 0074's *Negative* says "**Ten new types in the DI package**, and none in core. **They are internal apart from the validator**" — nine more internals in the same package, plus 0072's `ScopeAffinityPolicy` and `AmbientScopeProbe`, both "NEW, internal". The ground rests on a scarcity the set removes.
**Recommendation**: the third ground adds nothing the first two do not; delete it rather than date-stamp the ratio.

**6. (70) Six rejected alternatives are argued in `## Consequences`, against an explicit house-style rule.**
`documentation.md:135` — "**State the decision, not the argument that reached it.** … **Rejected options belong in `## Alternatives Considered`, with their reasons.**" Instances, all in *Negative* except the last:
- `0071` — `IAsyncDisposable` on `IAmALifetime`: "That trade was declined here."
- `0071` — keeping `_lifetimeScopes` as a fallback: "an unrun path is the one that rots." The transform-side twin of this deliberation **is** an alternative — 0070 alternative 13 — so the set records one decision in two different sections.
- `0073` — "The alternative — `PackageReference`s on `Microsoft.AspNetCore.Http` and `.Abstractions`… **It is rejected on that trade rather than overlooked.**"
- `0073` — "The alternative was a dependency on the end-of-life … 2.2.x line, which is worse."
- `0074` — "the alternative of two or three larger objects would have been defensible"; and "Deferring the pass until a `Singleton` candidate is found would avoid that… the fixed cost was taken instead."
- `0072` — "detecting this case in advance would cost an artefact construction per pipeline on the fast path…"

0076's *Negative* shows the compliant form: "so the asymmetry is kept deliberately (alternative 11)".
**Recommendation**: move each to `## Alternatives Considered` as a numbered entry and leave a pointer of 0076's shape in the *Negative* bullet. A net reduction in `Consequences` word count.

**7. (68) Risks *Mitigation* cells that are arguments rather than mitigations.**
Mitigation-cell word counts (median ≈ 40): 0076 row 2 = **238**; 0072 row 2 = 126; 0075 = 99, 90, 89; 0070 row 3 = 82; 0073 row 2 = 81; 0074 row 3 = 73. The 238-word cell closes by admitting what it is: "**identical code exercised four times is an argument, not a test**." Three more cells state that the mitigation is not one: 0074 row 3; 0074's FR-25.10 row; 0075's last row. And 0070 row 3 argues a rejected alternative inside the cell.
**Recommendation**: a Risks cell should name the mechanism and the criterion, nothing else. For 0076 row 2, replace the 238 words with the two facts the row turns on and move the coverage argument to *Negative*. Where a row has no mitigation (0075's pump-flow bracket), the cell should say "None. The criterion is owed and is carried in the requirements true-up" and stop.

**8. (64) 0074 records no alternative for "rely on the container's own `ValidateScopes`".**
0074's *Negative* concedes it: "**The container's own `ValidateScopes` remains the complete check**, and FR-25.8 requires the guidance page to say so." The same bullet lists four deliberate bounds on the rule that replaces it, two of which "can report *wrongly*". The predictable question is answered in none of the twelve alternatives. Alternative 6 touches `ValidateScopes` only as a hazard of resolving instances; alternative 8 is written over all six rules, not this one.
**Recommendation**: a new entry in `Alternatives Considered`, not a sentence added to the *Negative* bullet. The material is already in the ADR and needs relocating with its ground: `ValidateScopes` cannot see a `Singleton`-governed *artefact* never registered as a service, it throws where FR-22.3 must warn, and it says nothing about the other five rules.

**9. (62) 0070 alternative 8's rejection ground is circular.**
"**Rejected** by the settled decision on C-8, and for a concrete reason: …". C-8 settles nothing — `requirements.md:374`: "**The ADR should confirm this and settle** whether `IAmAScope` implements `IDisposable` only or both". The alternative is rejected by the decision this ADR is making. The concrete reason that follows is sound and is doing all the work. Contrast alternative 7, "rejected by D4", which is correct.
**Recommendation**: delete the appeal and lead with the reason, which is shorter.

**10. (60) 0070 alternative 1 rejects an option for a cost the set pays four more times.**
Its third count: "it adds a second public core type whose only purpose is to avoid editing six interfaces". The set adds five public core types in total — `IAmAScope`, `IAmAScopeProvider`, `ScopeAffinity`, `AmbientScopeSourceException`, `AmbientScopeSuppression`. Each addition is separately justified, and each ADR's own count is correctly scoped (0072's "three new core types" and 0075's "one" both checked and right). What is undercut is the *ground*.
**Recommendation**: the first two counts are decisive on their own. Drop the third rather than qualify it.

---

#### Verified and CLEAN — do not re-derive

- **`InternalsVisibleTo` is used nowhere in the repository.** Exactly one grep hit across `src/`, `tests/`, `samples/` and the props files, and it is a comment at `SpannerBoxMigrationRunner.cs:131`. 0075 alternative 3a's citation of that line is exact.
- **`src/` holds 824 public classes to 91 internal** (0073 alt 9) — recounted with a multi-line, attribute-tolerant class-declaration scan. Correct.
- **The DI package is 17 public to 1 internal**, and 0070's "seventeen of the eighteen classes" — both correct; the one internal is `ServiceProviderLifetimeScope.cs:42`.
- **Step 7a's ledger arithmetic is sound end to end.** Thirteen entries = five 0070 + eight siblings (0071 ×3, 0072 ×1, 0074 ×2, 0075 ×1, 0076 ×1). The three counts of interfaces are all *correctly scoped repetitions*, not a drift: **nine** across the whole set (`0070:114`), **eight** across 0070 and 0071, **six** for 0070 alone. 0071's "third contribution" numbering matches step 7a's three 0071 items.
- **0075's `PipelineBuilder` counts.** 69 constructions in `tests/`, splitting 48 and 21. Exactly as stated. Three public constructors at `:59`, `:76`, `:92`.
- **0074's "125 files under `tests/` register `IBrighterOptions` themselves."** 125. Correct.
- **`IAmAHandlerFactory.cs:7`** is `public interface IAmAHandlerFactory;` — the bare marker 0071 alternative 6 describes.
- **`IAmAHandlerFactorySync.cs:32-34`** carries the quoted text verbatim, and **`IAmAMessageMapperRegistry.cs:34`** carries "the interface is provided for testing". 0071's characterisation of 0070's argument matches `0070:113`.
- **Requirement attributions that hold**: OOS-12 (0070 alt 9), OOS-13 (0074 alt 5), OOS-4 + D1 (0072 alt 5, 0073 alt 2), C-10 (0076 alt 7), AC-48 (0073 alt 4), FR-9(a) (0075 alt 5, including the paraphrase), FR-27.3 (0075 alt 2), D4/C-11 (0070 alt 7). **0073 alternative 11's "neither half of FR-17" is correct** — FR-17 ¶ at `requirements.md:277` does itself impose the descriptor-visibility obligation.
- **0072's ladder is internally consistent** — ten rows, six OWNED outcomes matching the *Positive* list, row 4 the throw and row 10 the borrow.
- **0072's "three new core types"** = `IAmAScopeProvider`, `ScopeAffinity`, `AmbientScopeSourceException`. Correct.
- **0073's project-file facts**: no `src/` type declares any `Microsoft.*` namespace (alt 5); `AddProducers` (`ServiceCollectionExtensions.cs:247`, `:383`) and `AddControl` (`ControlExtensions.cs:11`) both extend `IBrighterBuilder` (alt 4); `Control.Api.csproj` is `Sdk="Microsoft.NET.Sdk.Web"` (alt 7).
- **No mermaid blocks in any `## Alternatives Considered` or `## Consequences` section.** No HTML entities, unbalanced backticks or broken table pipes in either section across the seven.
- **Remit item 3 — an alternative that is a sibling's adopted decision.** **No clean case** beyond finding 1's per-flow state. Checked and cleared: 0070 alt 1's role-plus-type-test against 0072's `IAmAServiceProviderScope`; 0073 alt 11's `TryAddSingleton` against 0076's `RegisterBrighterOptions`; 0076 alt 9's plain-`AddSingleton` rejection against 0072/0073's mandated plain `AddSingleton`; 0070 alt 13's "one behaviour per factory" against 0071's `ConfigurationException` path.

#### Gaps

- **No `dotnet` probes were run.** Nothing in this corpus turned on runtime behaviour that could not be settled by reading source; the one probe-backed claim (0075 alternative 5's `ExecutionContext` result) was not re-run and should be treated as unverified from this remit.
- **0070 alternative 12's history claim** — "the ledger has gone 4 → 7 → 9 → 10 → 11 → 12 → 13 across five review rounds" — is unverifiable under the blinding rules. Note also that it is a statement about the authoring process, which `documentation.md`'s *Writing tone* section rules out ("Do not reference ephemeral working state"). Unscored because the numbers could not be checked.
- **Not recounted here**: 0070 alternative 11's "seven existing `Warning` messages", alternative 3's four construction-site line numbers, 0070's "12 classes in `src/` and 70 test doubles" — single-ADR citations the 0070 reviewer owns.
- **0074's "ten new types"** was taken from 0074's own *Negative* rather than counted independently; finding 5 holds at nine internals even if the total is off by one.

