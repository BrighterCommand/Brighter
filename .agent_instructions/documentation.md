# Documentation

- Update or add Documentation comments for all exports from assemblies.
  - To be clear exports: means all public and protected methods of public classes/structs/records/enums.
    - We do not add Documentation comments internal or private classes, or internal methods
  - Documentation are indicated by `///`
  - Documentation comments use XML
  - Documentation comments show up in Intellisense for developers. Bear this in mind when writing comments, as they should be helpful to a developer using the API but not so verbose that a developer would not choose to read it when using intellisense. Use `<remarks>` for notes on implementation or more detailed instructions.
  - They should also be helpful to a developer or LLM reading the code.
  - We provide some guidance on specific elements:
  - Use `<summary>` element to provide an overview of the purpose of the class or method. What behavior or state does it encapsulate? What would you use it for. Use `<paramref>` if you refer to parameters in the summary.
  - Use the `<param>` tag to describe parameters to a constructor or method.
    - Use `<see cref="">` to document the type of the parameter
    - Indicate what the parameter is for, what effect setting it has and if it is optional. If it is optional describe any default value and its impact.
    - The developer should be clear what values they need to provide for the parameter to control desired behavior.
  - Use `<returns>` to indicate the `<see cref="">` of the return type, optionality, and what the value represents.
  - Use `<typeparam>` to indicate the intent of a generic type parameter; document any constraints on the type.
  - Use `<exception>` to document any exceptions that the method call can throw.
  - Use `<value>` to document a property. Like a `<summary>` it should indicate purpose. Like a `<param>` or `<return>` it should use `<see cref="">` to indicate type.

```csharp
/// <summary>
/// Gets or sets the current status.
/// </summary>
/// <value>The current status as a <see cref="string"/>.</value>
public string Status { get; set; }
```

- Use `<remarks>` for advice to developers or LLMs working with the code directly. Include information on how the method is implemented where it is not obvious from the code or significant design decisions have been made. Consider what you would want to know if maintaining this method. Use `<see href="">` if you need to link to external documentation.  This can also be used for more detailed information than could be included in the `<summary>`.
  - Prefer to use good variable and method names to express intent, over inline comments.
    - Use the refactoring "Extract Method To Express Intent" to encapsulate code in a named method that explains intent, over using a comment.
    - Do not add comments for what may be easily inferred from the code.
    - In tests you may use //Arrange, //Act, //Assert.
    - If code has a complex algorithm or non-obvious implementation, prefer to use `/// <remarks>`
  - Example:

  ```csharp
  /// <summary>
  /// Sends a message to the specified recipient.
  /// </summary>
  /// <param name="recipient">The recipient's address.</param>
  /// <returns>The message ID.</returns>
  public string SendMessage(string recipient) { ... }
  ```  

- Documentation comments should be changed when APIs change.  
- Document new features and changes in the Docs repository of the BrighterCommand organization.

## Architecture Decision Records

**Recommended Tool**: Use the `/adr <title>` command (see [.claude/commands/adr/adr.md](../../.claude/commands/adr/adr.md)) to create properly formatted ADRs. This automates numbering, template application, and spec linking.

We are using Architecture Decision Records (ADR) to record important design decisions that we make. When you make a significant decicion about design, that would be useful as context to future reviewers, or explorers of the codebase, please record your design decision as an ADR.

Place ADRs in the [ADR directory](../adr)

The template for the ADR is in our [first ADR](../adr/0001-record-architecture-decisions.md).

An ADR should follow the naming convention [Sequence Number]-[Title].md

Scan the ADR directory for existing ADRs to determine the next [Sequence Number] to use.

Use dash-case (aka kebab-case) for the [Title] of the ADR.

### ADR structure

Every ADR follows one skeleton. A reader who learns the shape on one ADR should be able to
navigate the next without re-reading it, so use these headings verbatim, in this order, at this
nesting level.

| Heading | Holds |
| --- | --- |
| `## Context` | 2–4 sentences in plain language: what exists, what is wrong with it, why that matters. Do not open by naming four interfaces — a reader cannot hold type names before they know the problem |
| `### Terms` | **only when the ADR introduces domain vocabulary** — the words the Decision turns on, one bullet each, before the reader meets them in an argument. Prefer a pointer to a sibling's entry over a second statement of it. See *Terms* below |
| `### Scope` | what the ADR covers, as lists rather than narrative. **Parent requirement** — the link. **In scope** — one bullet per FR/NFR it discharges, each naming the mechanism that makes the requirement true, plus a bullet for any scope no tagged requirement carries. **Out of scope** — one bullet per boundary a reader could reasonably mistake, each naming the ADR that does cover it. Where this ADR contributes to a requirement another ADR discharges, say so on the bullet and name the owner |
| `### Where this ADR sits` | **only when the ADR is one of a set** — a table mapping each ADR in the set to the one thing it decides, this one bolded and marked *(this one)*, then the single sentence that unifies them |
| `### {the problem}` | named as a behaviour, not as a structure. Lead with the orienting artefact — a comparison table or a diagram — then the consequences, then the mechanism that produces them |
| `### The forces` | one bullet per constraint that narrows the solution space, so the Decision's shape is legible before it is stated |
| `## Decision` | the decision in **one bold sentence**, then one short paragraph on the shape it takes. No signatures, no file paths |
| `### The mechanism, end to end` | **behaviour**: what happens, in what order. Lead with a sequence diagram, flowchart or decision-ladder table, then read the load-bearing invariants off it |
| `### Where the pieces live` | **structure**: a flowchart with one subgraph per assembly, showing what is new and which way dependencies point |
| `### Key Components` | opens with `#### The roles, and what each is responsible for` — a table of **Role** / **Type** / **Responsibilities** / **Responsibility classifier** / **Collaborators**. *Role* is one phrase saying what the type does; a type needing more than one phrase has too many responsibilities. *Responsibilities* may be several. *Responsibility classifier* is **knowing**, **doing** or **deciding**, and one type may carry more than one. *Collaborators* are the types it works with to meet them. Then each significant type with a contract table (Member / Input / Output / Error conditions), then `#### Where each type is touched` (Assembly / Type / Change), closing with what is deliberately **unchanged** |
| `### Technology Choices` | why this mechanism and not the obvious one, each question its own `####` sub-heading |
| `### Implementation Approach` | the implementor's section, and the only place `file:line` density belongs. Numbered, in commit order, structural changes separated from behavioural ones per Tidy First |
| `## Consequences` | `### Positive`, `### Negative`, `### Risks and Mitigations`. An ADR with only positive consequences reads as unreviewed |
| `## Alternatives Considered` | genuine rejection rationale, not strawmen. State the do-nothing option when the ADR delivers no behaviour |
| `## References` | parent requirement, related ADRs, external references |

`docs/adr/0070-per-pipeline-di-scope-for-mapper-and-transform-factories.md` is the worked example
of the full shape; `0072-ambient-scope-adoption-seam.md` shows the decision-ladder form.
`0001-record-architecture-decisions.md` remains the minimal template.

### Terms

*Sentence construction* below licenses this domain's vocabulary. It does not explain it. Where an
ADR introduces a domain word its Decision turns on, that ADR owes a `### Terms` block in
`## Context`, ahead of `### Scope`.

`0067-per-resolution-di-scope-for-transient-factory-instances.md` carries the definitional form.
`0070-per-pipeline-di-scope-for-mapper-and-transform-factories.md` carries the pointer form, and
the pointer form is the one to reach for first.

**Write an entry as a pointer wherever the body already carries the content.** An entry that
restates a paragraph puts the same sentence in the document twice. *"Step 5 specifies its steps"*
is a complete entry: the reader now knows the word, and knows where its rules live.

**Then sweep for what the block duplicates.** The block has two surfaces, and the second is the one
writers miss:

- **The pointer it replaces.** A new block usually supersedes a sentence elsewhere that deferred to
  a sibling ADR. Delete that sentence. Do not leave it standing beside the block.
- **Every definition the block states** — including the glosses under `## References`, because a
  gloss is prose. Grep the body for each sentence the block now asserts.

Seven blocks were written under this rule and every one of them duplicated something. The two whose
body text came back clean were the two written as pointers from the start, and both still convicted
on `## References`. Treat the sweep as a step in writing a `Terms` block, not as a check on one.

### ADR readability

An ADR has two audiences and they want different things. A **human reviewer** reads for the *why*
of a behaviour — behaviour is what endures, because structure changes under refactoring. An
**implementing agent** reads for the structural detail. Serve the human first, because the agent
will scroll and the human will stop reading.

- **General to specific.** Principle, then approach, then the detail an implementor needs. A
  reader should be able to stop after any section and hold a true, if less complete, picture.
- **Behaviour before structure.** Say what *happens* before you say what *types exist*. Never
  open a Decision with an interface declaration.
- **Lead each section with the artefact that orients** — a table, a diagram, a contract — then
  write prose that reads the consequences off it. Never make the reader assemble a picture from
  paragraphs and then show them the diagram.
- **Concentrate the citations — `file:line` and requirement IDs alike.** Both are load-bearing for
  a reviewer cross-checking coverage and pure noise inside an argument. State the design point in
  prose, then list the FRs and ACs it satisfies as bullets beneath it. A reader following the
  design skims the list; a reviewer checking coverage reads only the list. Never thread three
  requirement IDs through one sentence. At most one `file:line` per forces or Consequences bullet.
  **Prefer a slightly longer document to a terse one:** a paragraph plus a list is more readable
  than one dense sentence, and it serves both readers instead of neither.
  - **The three-ID bar binds argument prose, and exempts a bullet whose subject *is* a
    requirement.** `### Scope` is organised requirement by requirement, so its bullets cite what
    they discharge; the same holds for a `Consequences` bullet and for the `## References`
    inventory. Cutting those lists drops information rather than words. The bar is about an
    argument a reader has to follow, not about a list a reviewer reads as a list.
- **Name what is unchanged**, so a reviewer does not read an omission as an oversight.
- **State the unifying rule once, in one sentence**, and repeat that exact sentence in every
  sibling ADR that applies it. If it will not fit in one sentence, it is not yet one decision.
- **Emphasis is a symptom.** Bold marks the one sentence a section turns on; the Decision's single
  bold sentence is the model. If a paragraph needs bold to make its point findable, the paragraph
  is wrong — split it, or lead with the point. A section with bold in every paragraph has
  emphasis in none of them.
- **A bold lead-in is a heading that was never promoted.** Where a paragraph opens with a bolded
  label — a step number, a question, a sub-topic — make it a `####` sub-heading. Emphasis becomes
  navigation, and no sentence changes. **The test is that the bold-lead count falls by what the
  `####` count rises.** A pass that raises both has added structure on top of the emphasis instead
  of replacing it. Three checks keep it honest:
  - **Promote only bold that leads a *paragraph*.** Bold at the head of a *list item* is a
    different population, and a numbered list is already the structure this rule asks for.
    Promoting one raises `####` and removes no emphasis at all.
  - **Read the start of the block that follows each promotion.** A lead-in trailed by more bold —
    `**4a. A test project…** **Eight** of the criteria…` — leaves that second run at the head of
    the block, where it is a bold lead again. The column does not move, through no fault of the
    heading.
  - **Leave a form that the ADR's siblings share.** `## Alternatives Considered` uses one entry
    form across a whole set. Converting it in one ADR is a set-level change, not a readability
    edit.

**Sentence construction.** Follow these rules from
[Simplified Technical English](https://www.asd-europe.org/standards-specifications/simplified-technical-english/),
so that a non-native reader and a reader in a hurry get the same meaning:

- **One idea per sentence, and no more than about 25 words.** A sentence carrying three
  cross-references is two sentences and a list.
- **Active voice with a named actor.** *"The factory disposes the scope"*, not *"the scope is
  disposed"*.
- **No ambiguous `this`, `it` or `that`.** Repeat the noun. A reader should never have to search
  backwards for a referent.
- **One term per concept, every time.** Do not vary the wording for elegance; vary it only when
  the thing itself differs.
- **No noun stacks.** *"the pipeline scope handle release ordering rule"* is a sentence pretending
  to be a phrase.
- **State the decision, not the argument that reached it.** *"Raising those five was rejected"*
  records a deliberation; the ADR records what is true. Rejected options belong in
  `## Alternatives Considered`, with their reasons.
- **State the rule that holds, not the revision that changed it.** *"FR-27.1 was amended in
  revision 28 to match"* records the requirements document's history; the ADR records what is now
  true. This is a third kind of argument, and neither the rule above nor *Do not reference
  ephemeral working state* reaches it: a requirements history table is durable, so citing it passes
  both as written. The requirements document already keeps that record. An ADR that repeats it
  dates itself, and tells a reader in two years about a revision they will never look up.

This is a named subset of the standard, not the full ASD-STE100: the value is in the writing rules,
and the controlled dictionary would fight this domain's vocabulary (*ambient*, *affinity*,
*discharge*, *borrow*, *bracket*). Licensing those words is not explaining them, which is what the
`### Terms` block above is for.

*Writing tone for design documents* below applies to every ADR and is not optional: an ADR that
records what a participant in the authoring conversation said, rather than what was decided and
why, has failed its only audience.

### Correcting an ADR

Most edits to an ADR are corrections: a reviewer finds a claim that is wrong, over-stated or
mis-cited, and one sentence has to change. Corrections are also how a readable ADR becomes an
unreadable one. Each fix is small and locally justified, and what accumulates is a document of
hedged sentences, bolded caveats and recorded arguments that nobody ever decided to write.
**A correction must leave the document at least as readable as it found it.** The rules above are
not suspended because a finding is being closed.

- **Replace, do not append.** A sentence that over-claims is answered by rewriting that sentence,
  not by adding the qualifier that makes it true. Two sentences where there was one, the second
  taking back part of the first, is how a paragraph reaches four hundred words.
- **Every qualifier a fix adds is a claim**, and it can contradict a section the fix never looked
  at. Prefer the narrow statement to the broad statement plus its exception.
- **Correct the source, not only the statement derived from it.** A wrong summary usually has a
  wrong sentence behind it. Fixing the summary alone leaves the generator in place, and the next
  reader derives the same error again.
- **A fix that needs bold to be found is in the wrong place.** Move the point to the lead of its
  section instead of emphasising it where it sits.
- **The finding's argument is not the fix.** Record what is true. A rejected reading belongs in
  `## Alternatives Considered` with its reason, not as a warning inside the prose.
- **When a finding can only be closed by making the document worse, stop and say so.** Keeping a
  longer, plainer form for one sentence is a legitimate outcome. Record the reason in the commit
  message rather than absorbing it silently.

**Measure a batch of corrections, not only a rewrite.** Run *Measure the readability* below at both
ends of the batch, and put both figures in the commit message. A batch that raises a column owes an
explanation. Then run the whole-document re-read below: a batch of corrections is the input that
check exists for.

### Diagrams in ADRs

Prefer **mermaid** to ASCII art: it is editable, it renders on GitHub, and it survives reflowing.
Choose the form by what is being shown:

| Showing | Use |
| --- | --- |
| a sequence of calls over time, or who owns what and when | `sequenceDiagram` |
| assemblies and packages, and which way dependencies point | `flowchart` with one `subgraph` per assembly |
| a small branch — three or four outcomes | `flowchart` |
| a protocol with more than about four decision points | a **decision-ladder table**, not a flowchart |
| the types a decision introduces, and how they relate — implements, holds, creates | `classDiagram` |

Reach for a diagram sooner than feels necessary. If a paragraph needs three cross-references
before it makes sense, draw it: a class diagram for how types relate, a sequence diagram for who
calls whom and in what order.

A flowchart with nine decision nodes renders as an unreadable column with edges spanning its whole
height. When a protocol branches that much, write it as a numbered table — one row per situation,
in evaluation order, with columns for the outcome and any diagnostic — and leave the ordered
pseudo-code in `Implementation Approach`, pointing back at the table.

Mermaid traps that pass review and then fail to render:

- **`;` is a statement separator in `sequenceDiagram`.** A semicolon anywhere in message or note
  text silently breaks the parse. Use a comma or an em dash.
- **Never put `<` or `>` in a label.** Mermaid renders labels as HTML, so `Lease<T>` swallows the
  type parameter. Write `Lease for T` and let the adjacent prose carry the generic.
- **Never use HTML entities** (`&lt;`, `&gt;`, `&amp;`, `&nbsp;`) — they trip the escaped-markdown
  check below. `<br/>` for a line break is fine; use plain spaces elsewhere.
- **Quote every label** containing parentheses, commas or colons: `node["Create(type, scope)"]`.
- Avoid `rect rgb(...)`: the colours are fixed and read badly in whichever theme they were not
  chosen for.

### Before an ADR is committed

Run these checks every time — they catch defects that survive a careful read.

**Re-read the whole document.** Correcting statements one at a time produces a document that
contradicts itself: a fix lands, and a sentence three sections away that depended on the old
wording is now false, redundant, or an argument for something no longer in the ADR. After any
round of statement-level edits, read the ADR start to finish and fix what the fixes broke. This is
a separate pass from the edits themselves, and it is where near-duplicate paragraphs, stale
summaries and orphaned rationale are found.

**Render every mermaid diagram.** Roughly one diagram in six fails on first draft, and a diagram
that does not render is a broken ADR that looked fine in review. Extract each mermaid block to its
own `.mmd` file under the scratchpad and render it:

```bash
npx -y -p @mermaid-js/mermaid-cli@11 mmdc -i diagram.mmd -o diagram.svg
```

A non-zero exit or a missing `.svg` is a failure — read the parse error, fix the ADR, re-run. Then
render the most complex diagram to PNG (`-o diagram.png -w 1600 -b white`) and actually *look* at
it: a diagram can parse cleanly and still be unreadable, which is the signal to convert it to a
decision-ladder table. If the check cannot run at all (no network for `npx`), say so plainly rather
than reporting the diagrams as verified.

**No escaped markdown**, which breaks C# generics and mermaid labels alike:

```bash
grep -c '&lt;\|&gt;\|&amp;' docs/adr/{file}.md   # must be 0
```

**Measure the readability.** Sentence length, emphasis density and missing structure are the three
rules above that a careful read does not catch. They are also the three this project has breached
worst. All three are cheap to count. Measure before and after any restructuring pass or batch of
corrections, and report both ends in the commit message. A column that moves the wrong way owes an
explanation.

The three instruments are defined here in full, and deliberately so. A definition held somewhere
else does not reproduce. Two of these were written out at this length and rebuilt exactly, first
run, in six separate sessions; the third was recorded as a single line, and a search of its whole
parameter space never recovered the figures it had produced. Same author, same week — the only
variable was how much was written down.

*Length and emphasis.* Strip the frontmatter, every fenced block including its fences, every ATX
heading line and every table line. A **block** is a maximal run of lines bounded by a blank line,
split further at each list-item lead — `^\s*(?:[-*+]|\d+[.)])\s+`, in which an alpha-suffixed
ordinal such as `5a.` is **not** a lead. A **bold run** is one non-greedy `\*\*(.+?)\*\*` match. A
**bullet lead** is the first bold run of a list block that opens with `**`; every other bold run
counts as prose, and the two bold columns are therefore disjoint. Report blocks, blocks over 150
words, blocks over 200 words, bold runs in prose, bold runs at bullet leads, and diagrams.

*Structure.* Over the region from `### Technology Choices` to `## References`, on the same block
model, a heading line, a table line and a fenced block are breaks. A **list block** opens with a
list-item lead; every other block is a **prose block**. Report `####` headings; list blocks as a
share of all blocks; **bold leads**, meaning prose blocks whose text opens with `**` — disjoint
again from the bullet-lead column above; **run**, the longest chain of consecutive prose blocks
with no heading, list, table or fence between them; and **span**, the longest heading-free stretch
of source lines. Span needs two conventions or it will not reproduce: the region includes its
terminating `## References` heading as a boundary, and a span is the lines *strictly between* two
consecutive headings.

*Language.* Take the whole file, frontmatter included. Drop fenced blocks with their fences, but
keep table lines and let `|` separate units, so a long table cell is measured rather than hidden.
Strip `**`. A unit ends at `.`, `:` or `;` before whitespace, at a line break, or at `|`. The line
break is load-bearing: without it, two list items that carry no terminal punctuation merge into one
phantom sentence. Protect `file:line` refs and decimal identifiers such as `FR-27.1` from the
split. Report units of 40+, 50+ and 60+ words, the worst unit, and units carrying two or more
em-dash asides. Match requirement IDs with
`\b(?:NFR|FR|AC|OOS|C|D)-\d+(?:\.\d+)?[a-z]?\b|\bD\d+[a-z]?\b`; the second alternative is
load-bearing, because `D0b` and `D19` carry no hyphen and dropping them loses about a dozen
distinct IDs per document.

**Name the target for each column, and name what floors it.** A column reported without its floor
reads a pass as a failure.

- Sentences over about 25 words, and revision-history phrases, should reach **zero**. Neither has
  a floor.
- `####` should rise, and bold leads should fall by about the same number. That pairing is the
  conversion test above.
- **Run and span are floored by any section whose form is fixed across a set of ADRs.** Where
  `## Alternatives Considered` holds the region's longest unbroken prose chain, that ADR's `run`
  cannot move at all; where it holds the longest heading-free stretch, neither can its `span`. The
  floor is that section's longest **chain**, not its entry count: tables and lists break entries
  apart, and one ADR here had twelve entries and a chain of six. **Measure where a column sits
  before naming it a target** — one pass over the region's consecutive heading pairs settles it.
- **Identifier density has no useful target**, for the reason under *Concentrate the citations*:
  most breaches are citation lists, and cutting them loses information. Report it and leave it.
  Filter it by **source line**, never by sentence — re-partitioning a long citation line multiplies
  its units and fakes a movement that did not happen.

**Re-run an instrument; never hand-correct its output.** A figure that looks wrong is re-measured by
running the definition again. Editing the number instead reintroduces what the definition exists to
prevent. One correction here restated an ID count with the second alternative of that regex silently
dropped. That is the omission the regex's own definition warns about, committed while fixing it.

## Writing tone for design documents

This guidance applies to ADRs, requirements specs, design specs, and any other long-lived document under `docs/` or `specs/`.

**Write for a future reader, not for the current conversation.** The audience is a contributor reading the document six months or two years from now to understand a design decision. They have no visibility into the chat that produced it.

**Refer to requirements and design artifacts, not to the participants in the authoring conversation.** Concretely:

- ❌ "at the user's direction" → ✅ "per requirement C3" or just state the decision
- ❌ "the user's feedback was singular ('an abstract base class')" → ✅ "requirement F5 specifies a single abstract base"
- ❌ "the user explicitly accepted this cost" → ✅ "the cost is accepted per requirement C1 (spec 0028 lands as PR review feedback, not greenfield work)"
- ❌ "if the user wants the interface anyway during review, the re-introduction is mechanical" → ✅ remove — review-loop asides do not survive past the review
- ❌ "Direct rendering from feedback item 5's framing" → ✅ "Aligns with requirement F4 (payload-mode validator role)"
- ❌ "the spec 0027 PROMPT suggested otherwise" → ✅ replace with the actual technical reason; PROMPT.md is ephemeral working state

**Do not quote conversational asides.** Phrases like *"Arguably it would have been better caught earlier"* or *"we could possibly use ..."* belong in chat transcripts and PR review threads, not in design documents that outlive them.

**Do not reference ephemeral working state.** PROMPT.md, current spec phase ("we are in the requirements phase"), conversation transcripts, and unresolved review back-and-forth are all transient. Either fold the substance into the document, or omit it.

**Trace decisions to durable artifacts.** Acceptable references include: requirement IDs (F1, NF2, C3), other ADRs, code locations, principles in `.agent_instructions/`, and external specifications. References to GitHub PRs and issues are acceptable as historical anchors but should not carry the design rationale — the rationale must live in the document itself.

**The rule of thumb:** if removing the sentence would leave the future reader less informed about *the design*, keep it. If it would only leave them less informed about *who said what when*, remove it.

## Licensing

- We add a license comment to every src file
- The license should be at the very top of each source file, before any using statements or code.
- We use the MIT license.
- You should add your name and the year, if it is a new file.
- You should put the license comment in a `#region Licence` block (note: British spelling, no space)
- An LLM should use the name and year of the contributor instructing the LLM
- As an example

```csharp
#region Licence

/* The MIT License (MIT)
Copyright © [Year] [Your Name] [Your Contact Email]

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion
```