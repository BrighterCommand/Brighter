# The `## Context` scope restructure — findings, options, and the plan

Working document for step 3 of `PROMPT.md`'s resume block (§19.7). **No ADR has been edited.** This
file exists so the owner can add their feedback before the shape is settled; §19.7's skeleton is a
proposal, not an approved form.

Sections 1–5 are Claude's findings, written before any owner input. Section 6 is the owner's.
Section 7 is the plan, to be written together once section 6 exists.

---

## 1. What §19.7 asks for, and where its premise has aged

§19.7 states the problem as: `## Context`'s scope narrative is run-on prose that is hard to read;
`0072:31` alone is **410 words in one paragraph and eleven sentences**; `:33` adds 175 more. It
proposes a house-style amendment applied set-wide:

```
## Context
  <narrative opening, 2-4 sentences, plain language>
  ### Scope
    Summary        - one short paragraph
    In scope       - bullet list of the FR/NFR this ADR addresses
    Out of scope   - bullet list of the FR/NFR it deliberately does NOT,
                     where the boundary needs to be explicit
```

**Re-derived against the ADRs as they now stand** (`f5613099e`), the premise has moved in two ways —
which is the round-6 lesson that *a finding's premise about the state of the set ages*:

- `0072:31` is now **498 words**, not 410. Round 6's twenty-six commits grew it. Sentence count is
  unchanged at eleven.
- §19.7 names 0072 as *the* worst case. **`0071:30` is a second one it never names** — 490 words in
  **twelve** sentences, the highest sentence count in the set.

Both are recorded as §19.7 branch-3 sightings (see section 5).

## 2. The measurements

Word counts, `docs/adr/007{0..6}*.md` at `f5613099e`:

| ADR | `**Scope**:` paragraph | `It does not decide` paragraph | `## Context` before the first `###` | whole `## Context` |
| --- | --- | --- | --- | --- |
| 0070 | 145 w / 5 sent. | 91 w | 841 w | 2501 w |
| **0071** | **490 w / 12 sent.** | 102 w | 671 w | 2493 w |
| **0072** | **498 w / 11 sent.** | 175 w / 7 sent. | 798 w | 1794 w |
| 0073 | 224 w / 5 sent. | 129 w | 571 w | 1642 w |
| 0074 | 183 w / 5 sent. | 97 w | 583 w | 1973 w |
| 0075 | 117 w / 5 sent. | 89 w | 411 w | 1411 w |
| 0076 | 155 w / 2 sent. | 73 w | 606 w | 1505 w |

**The set is not uniform** — 117 to 498 words. A word cap is therefore the wrong instrument; the
structure is what should be fixed, and the two long ones will shrink most because they have the most
routing to do.

### How far the house-style change actually reaches

`**Scope**:` is **not** a convention of this set. **46 ADRs use it**, and `**Parent Requirement**:`
appears in 46 as well. Outside the seven:

| | n | median | maximum |
| --- | --- | --- | --- |
| `**Scope**:` paragraph, other ADRs | 39 | **32 w** | **100 w** (`0061-box-provisioning-value-types`) |
| `**Scope**:` paragraph, this set | 7 | 155 w | 498 w |

So the run-on problem belongs to a seven-ADR set whose every `Scope` must route requirements across
six siblings. It is not a general ADR defect, and that bears directly on question 2 in section 4.

### What the house-style file currently says

`.agent_instructions/documentation.md:73` gives `## Context` as *"2–4 sentences in plain language:
what exists, what is wrong with it, why that matters"*, and `:74` gives `### Where this ADR sits` as
*"only when the ADR is one of a set"*.

⚠ **The skeleton table has no row at all for the `**Parent Requirement**` / `**Scope**` /
`It does not decide` block.** That block is an undocumented convention in 46 files. Whatever is
decided here, the house-style change **adds a row**, it does not edit one — and the wording of that
row is what decides whether the other 39 ADRs are retrospectively non-conforming.

## 3. What the prose is actually carrying

Four distinct things are threaded through each `Scope` paragraph, and one of them has no natural home
in a bullet list:

1. **The decision** — one bold sentence. Already in good shape everywhere.
2. **Requirements discharged here**, each with the mechanism that makes it true.
3. **Requirements served but discharged elsewhere** — the *serves-vs-discharges* distinction.
4. **Requirements deliberately not decided**, each pointing at the owning sibling.

Items 2–4 are all the same shape: *requirement → relation → where*.

The relation vocabulary in use across the seven, counted from the `Scope` paragraphs:

| Relation | Occurrences | Example |
| --- | --- | --- |
| **discharges** | 15 | `0070:30` — *"It discharges FR-1 … FR-6, **FR-20**"* |
| **serves** (incl. *is served here*) | 12 | `0070:30` — *"**FR-7 is served here, not discharged here**"* |
| **is ADR 00xx's** (defers) | 12 | `0072:33` — *"Three are ADR 0073's"* |
| **supplies the mechanism / substance for** | 2 | `0075:34` — *"supplies the mechanism for FR-19's consumer-side inertness … without discharging FR-19"* |
| **does not discharge** (explicit) | 1 | `0075:32` |
| **closes Defect n** / **constraint C-nn** | 2 | `0070:30` |

**This is the finding that shapes the options.** The set has a five-value relation vocabulary, used
consistently, and stated only in prose. `serves` and `supplies the mechanism for` are the two the
reviewers keep getting wrong: round 6's finding 0070 #7, `set #5`, `0076 #2` and decision 13 were all
mis-stated relations or mis-scoped ownership claims. A bullet list records **that** a requirement is
in scope. It does not record **how**.

## 4. The two questions

### Question 1 — the shape

**Option A — §19.7's skeleton as written.** `### Scope` = summary paragraph, `In scope` bullets,
`Out of scope` bullets.

- *For:* it is the owner's own proposal; least surprise; smallest vocabulary to learn.
- *Against:* two bullet lists model a **binary** in/out distinction, and section 3 shows the set uses
  **five** relations. `serves` and `supplies the mechanism for` are neither in nor out — they would
  have to be flattened into one list or the other, or carried in trailing prose, which is what the
  restructure is trying to end.

**Option B — `### Scope` = narrative + summary + a requirement-routing table.** Same heading and
opening as A; In/Out become one table, `Requirement | Relation | Mechanism or owning ADR`, with prose
kept beneath only where a qualification is load-bearing.

```
### Scope

<one short summary paragraph: the one thing this ADR decides>

| Requirement | Relation | Mechanism / owning ADR |
| --- | --- | --- |
| FR-1 … FR-6 | discharges | the transform pipeline's single scope |
| FR-20 | discharges | clean break on `MapperLifetime.Scoped`, release-noted in step 7a |
| FR-7 | serves | ADR 0071 — this ADR leaves handler scoping alone |
| FR-19 | supplies the mechanism for | ADR 0072 discharges it; step 4a's third bracket |
| FR-22 | defers | ADR 0074 |

<prose only where the long form is load-bearing>
```

- *For:* the relation becomes a column with a fixed vocabulary rather than a phrasing choice, so
  drift between two ADRs' accounts of the same requirement becomes visible in a diff. Makes the
  set-level reviewer's FR→ADR ownership table — the single most valuable artefact of rounds 4–6 —
  mechanically checkable each round instead of re-derived from prose.
- *Against:* heavier to maintain; a table invites the reader to skim past the mechanism note; and it
  is a deviation from the owner's proposal, which needs a reason to be worth it.

**Option C — bullets under the existing bold leads, no new heading.** Keep `**Scope**:` and
`It does not decide` where they sit; break each into a lead sentence plus bullets.

- *For:* smallest diff; no heading-order change; no house-style row needed; nothing to unpick if it
  reads badly.
- *Against:* shortens lines without giving the section a scannable shape, and leaves the five-relation
  vocabulary in prose. Fixes the symptom §19.7 names and not the structure beneath it.

**Claude's recommendation: B**, with the caveat stated plainly — A is the owner's own proposal and the
lower-risk call, and the only reason to deviate is section 3's finding that the relation vocabulary is
five-valued and load-bearing.

### Question 2 — how far the house-style change reaches

Given 39 other ADRs at a median of 32 words:

- **Conditional rule, applied to the seven** *(Claude's recommendation)* — document the new shape as
  the form to use **when an ADR is one of a set, or when its scope routing runs past a few
  sentences**; apply it to 0070–0076 now; leave the other 39 alone and conforming.
- **General skeleton row for all future ADRs** — every new ADR gets `### Scope`, including ones whose
  scope is a 32-word sentence that needs no heading.
- **No house-style change** — restructure the seven, document nothing. Keeps `documentation.md`
  accurate about the other 39, but `/spec:review`'s *Structure and Readability* criteria grade against
  that file, so round 7's reviewers would read the new shape as a deviation rather than the standard.

## 5. The branch-2 / branch-3 running list

§19.7 requires this list be kept and reported with the batch **even if empty**. Two entries so far,
both branch-3-adjacent — they are defects in §19.7's own premise rather than in an ADR, so neither
needs a correction commit; they are recorded because the premise is what the batch is sized against:

| # | Branch | Sighting |
| --- | --- | --- |
| 1 | 3 (premise) | §19.7 says `0072:31` is 410 words. It is **498**. Round 6's commits grew it; sentence count unchanged. |
| 2 | 3 (premise) | §19.7 names 0072 as the worst case. `0071:30` — **490 w / 12 sentences** — is a second, unnamed in §19.7 and in every round-6 finding. |

Nothing yet in branch 2 (*cannot be simplified without distorting*) or in the true branch 3 (*a fact
that looks wrong once isolated*), because no ADR prose has been touched.

## 6. Owner's feedback

<!-- Ian: add yours here. -->

## 7. The plan

To be agreed once section 6 exists. The constraints already fixed, whatever the shape:

- **The house-style change lands in `.agent_instructions/documentation.md` first** (§3b — it is the
  single source for the skeleton), **then** all seven ADRs. The skeleton is not restated in a command
  file.
- **Two commits minimum in the same session, and they do not mix.** The carried-forward
  **0075-description correction** — all seven `## References` sibling lists and all seven *Where this
  ADR sits* tables still describe 0075 as suppression *"for a `Publish` subscriber"* only, which the
  pump bracket made incomplete — is **substantive**, so it lands in its **own** commit, never inside
  the readability diff. A set-wide readability diff is the worst possible place to hide a change of
  meaning.
- **Anything the compression exposes as wrong stops the pass** (§19.7 branch 3) and becomes its own
  call and its own commit, before or after the restructure — never inside it.
- **§19.4's byte-identical blocks stay byte-identical** across all seven, and are re-verified after
  the pass.
- Tick `PROMPT.md` (§19.7, the resume block) before `/clear`, per §19.8's one-batch-per-session rule.
