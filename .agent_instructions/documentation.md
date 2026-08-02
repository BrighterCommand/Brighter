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
| `### Where this ADR sits` | **only when the ADR is one of a set** — a table mapping each ADR in the set to the one thing it decides, this one bolded and marked *(this one)*, then the single sentence that unifies them |
| `### {the problem}` | named as a behaviour, not as a structure. Lead with the orienting artefact — a comparison table or a diagram — then the consequences, then the mechanism that produces them |
| `### The forces` | one bullet per constraint that narrows the solution space, so the Decision's shape is legible before it is stated |
| `## Decision` | the decision in **one bold sentence**, then one short paragraph on the shape it takes. No signatures, no file paths |
| `### The mechanism, end to end` | **behaviour**: what happens, in what order. Lead with a sequence diagram, flowchart or decision-ladder table, then read the load-bearing invariants off it |
| `### Where the pieces live` | **structure**: a flowchart with one subgraph per assembly, showing what is new and which way dependencies point |
| `### Key Components` | opens with `#### The roles, and what each is responsible for` — a table of Role / Type / Stereotype (**knowing**, **doing**, **deciding**) / Responsibility. Then each significant type with a contract table (Member / Input / Output / Error conditions), then `#### Where each type is touched` (Assembly / Type / Change), closing with what is deliberately **unchanged** |
| `### Technology Choices` | why this mechanism and not the obvious one, each as a bolded question |
| `### Implementation Approach` | the implementor's section, and the only place `file:line` density belongs. Numbered, in commit order, structural changes separated from behavioural ones per Tidy First |
| `## Consequences` | `### Positive`, `### Negative`, `### Risks and Mitigations`. An ADR with only positive consequences reads as unreviewed |
| `## Alternatives Considered` | genuine rejection rationale, not strawmen. State the do-nothing option when the ADR delivers no behaviour |
| `## References` | parent requirement, related ADRs, external references |

`docs/adr/0070-per-pipeline-di-scope-for-mapper-and-transform-factories.md` is the worked example
of the full shape; `0072-ambient-scope-adoption-seam.md` shows the decision-ladder form.
`0001-record-architecture-decisions.md` remains the minimal template.

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
- **Concentrate the citations.** `file:line` references are load-bearing for the implementor and
  pure noise inside an argument. At most one per forces or Consequences bullet.
- **Name what is unchanged**, so a reviewer does not read an omission as an oversight.
- **State the unifying rule once, in one sentence**, and repeat that exact sentence in every
  sibling ADR that applies it. If it will not fit in one sentence, it is not yet one decision.

*Writing tone for design documents* below applies to every ADR and is not optional: an ADR that
records what a participant in the authoring conversation said, rather than what was decided and
why, has failed its only audience.

### Diagrams in ADRs

Prefer **mermaid** to ASCII art: it is editable, it renders on GitHub, and it survives reflowing.
Choose the form by what is being shown:

| Showing | Use |
| --- | --- |
| a sequence of calls over time, or who owns what and when | `sequenceDiagram` |
| assemblies and packages, and which way dependencies point | `flowchart` with one `subgraph` per assembly |
| a small branch — three or four outcomes | `flowchart` |
| a protocol with more than about four decision points | a **decision-ladder table**, not a flowchart |

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

Run both checks every time — they catch defects that survive a careful read.

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