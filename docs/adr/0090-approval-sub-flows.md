---
id: 0090-approval-sub-flows
title: "Approval Sub-flows, Provenance Lines and the Gate Run Contract"
status: Proposed
author:
  - "Ian Cooper"
created: 2026-10-31
summary: "Every approver (/spec:approve, a gate, or a flow's own marker-writer) runs an approval sub-flow by invoking each table-named command through the Skill tool, first in a questions pass, where each command receives the answers of the commands before it, and then in an actions pass that carries all the collected answers, and writes the marker only when every command prints sub-flow-done, appending one approved: step=... artefacts=... sha=... provenance line. The design sub-flow is four idempotent commands (/adr:accept, /adr:retire-superseded, /adr:regenerate-index, /adr:report-approval) extracted from approve.md, and gates follow a five-row run-contract ladder that never overwrites an artefact without asking."
tags:
  - "meta"
  - "workflow"
---

# 90. Approval Sub-flows, Provenance Lines and the Gate Run Contract

Date: 2026-10-31

## Status

Proposed

Implements part of [issue #4661](https://github.com/BrighterCommand/Brighter/issues/4661). Supersedes no prior ADR. This decision is about the project's own agent tooling, not its runtime. [ADR 0089](0089-slips-resolver-and-routing.md) is its accepted sibling.

## Context

Approving a step can involve work beyond writing a marker, and that work depends on the kind of artefact. Today only `design` has any, and `/spec:approve` holds it: it flips each ADR to `Accepted`, retires the ADRs that the approved ones supersede, regenerates `docs/adr/index.md` and reports the result. A gate, or a flow that brings its own kind and its own approving command, would have to copy that work by hand, and the copies would drift. Nothing says how such a command must behave when it stops part-way through, and a marker records only that a step was approved, not what was approved or at which commit.

### How an approval runs

Every approver follows the same stages for every step, whatever its kind. An approver is `/spec:approve`, a gate or a flow's own marker-writing command.

1. **Route check** (0089). The approver asks the resolver about the step: it must be on the slip, this command must be its marker writer, and its artefacts must be present, such as `requirements.md` or `tasks.md`. A failed check stops the approver before any write. For a gate, this stage is step 1 of *The gate run contract*.
2. **Choose the sub-flow.** The approver reads the step's `approval_subflow`, which the approval table keys by artefact kind. `null` means the step has no usable entry (0089 R12 (a) to (c): no entry, two entries, or a command that does not exist), so the slip is invalid and the approver stops. An **empty** entry means there is no approval work, and the approver goes to stage 5. Otherwise it goes to stage 3.
3. **Questions pass.** Each sub-flow command asks its questions and checks its inputs, and writes nothing. A failure stops the approver, with nothing written.
4. **Actions pass.** Each sub-flow command makes its writes. A failure stops the approver with no marker.
5. **Marker.** The approver appends a provenance line to the step's marker, and prints the step-approved message. `/spec:approve` then runs 0089's closing instructions. A gate runs them once, at the last step of its ladder.

| Kind | Table entry | What runs between the route check and the marker |
| --- | --- | --- |
| `requirements` | empty | Nothing. The approval is the route check and the marker |
| `tasks` | empty | Nothing, as for `requirements` |
| `design` | four `/adr:*` commands | Stages 3 and 4: accept the ADRs, retire the superseded ones, regenerate the index, report |
| `change` (a refactoring flow's kind; illustrative, not shipped) | `/refactor:smells, /refactor:tidy-first` | Stages 3 and 4: ask which smells to act on, confirm the analysis, then make the structural refactorings |
| Any other kind | whatever its flow's entry names, possibly empty | The same stages, with that flow's commands |

Stages 1 and 5 are the same for every kind. The rest of this ADR covers the sub-flow (stages 2 to 4), the provenance line, and the gate ladder, which applies stages 1 to 5 to each step it approves, except that it runs the closing instructions only once. `design` is the only non-empty sub-flow shipped today, and it is one use of a general mechanism. A flow can give any kind its own sub-flow by naming commands in the approval table. This ADR uses two examples throughout: the shipped `design` sub-flow, which is about ADRs, and the proposed `change` sub-flow of a refactoring flow, which is not about ADRs and is described under *A second sub-flow*. Where a rule belongs to the mechanism, the text says so and the `change` sub-flow follows it as `design` does. Where a rule belongs to the `design` commands alone, the text says that instead.

### Terms

These words keep the meaning that [requirements § Definitions](../../specs/0040-routing-slips/requirements.md#definitions) gives them: **step**, **artefact**, **artefact kind**, **gate**, **approval table**, **approval sub-flow**, **approval marker**, **provenance line** and **marker-writing command**. **Route report**, **route instructions** and **closing instructions** are defined in [ADR 0089 § Terms](0089-slips-resolver-and-routing.md#terms). This ADR introduces these words:

- **Approver** — a marker-writing command, while it runs an approval sub-flow: `/spec:approve`, a gate, or a flow's own command.
- **Sub-flow command** — one command named in an approval-table entry. *The sub-flow command shape* gives its form.
- **Questions section** — the `## Questions` section of a sub-flow command. It asks the user and checks its inputs, and never writes.
- **Actions section** — the `## Actions` section of a sub-flow command. It makes the command's writes and asks nothing.
- **Answers** — the values that Questions sections produce. They reach every later Questions section in the same pass, and every Actions section, as arguments.
- **Protocol line** — one line, in a fixed form, that a sub-flow command prints for its approver to read (*The protocol lines*).
- **Selector word** — a word that an approver passes to its sub-flow as `--select`. Each sub-flow defines what its selector words mean.
- **Sub-flow instructions** — the canonical procedure every approver follows to run a sub-flow and write the marker (*The sub-flow instructions*).

### Scope

**Parent requirement**: [specs/0040-routing-slips/requirements.md](../../specs/0040-routing-slips/requirements.md)

**In scope**:

- FR-12, approve side — `/spec:approve` runs the sub-flow instructions after 0089's checks. It writes the marker only when every sub-flow command completes, and it holds no kind-specific action. A `null` sub-flow stops it, naming the kind. The words after the step name reach the sub-flow as `--select` arguments.
- FR-13 — the provenance line: its format, the append-only write, and the `unavailable` sha.
- FR-27, run side — the gate ladder, its questions, the three interrupted states and the never-overwrite rule. The structural rules (R9, R10) and the `gates` report fields are 0089's.
- FR-29, run side — the two-pass protocol, the argument convention, the protocol lines, failure and re-run, and per-command idempotence. It also names the four `/adr:*` commands that carry the `design` sub-flow. 0089 step 2 writes the `design` row of `.claude/flows/approvals.md` with those names.
- FR-19, contributing — four checklist items, which 0091 owns: (a) the step checks a flow's own marker-writing command runs before its sub-flow (*The sub-flow instructions*, lead-in), (b) the provenance line, (c) running the sub-flow before the marker, and (d) the gate run contract. Items (b) to (d) point at this ADR's README text.
- FR-26, two parts — the README notes on running a sub-flow (and the gate run contract) and on the provenance line.
- NFR-2 — a provenance line is one plain-text line that `grep` and `awk` read.
- NFR-3, contributing — sub-flow commands write their kind's files (for `design`, ADR files and the index), which are not routing state. Only the approver writes the marker.
- AC-24, AC-25, AC-26, AC-27, AC-76 and AC-77 are verified by this ADR's mechanism (Implementation Approach step 7), and AC-79 by step 6.
- Scope no tagged requirement carries: the `allowed-tools` of the four new commands and the two entries `approve.md` gains, and the doc touch-ups in `.claude/commands/adr/` and `.agent_instructions/adr_frontmatter.md`.

**Out of scope**:

- The approval table's format, location and rule R12, the `approval_subflow`, `approve_steps`, `artefacts`, `artefacts_present`, `marker_writer` and `gate` report fields, the route instructions and closing instructions, and the step-not-on-the-slip, another-marker-writer and artefacts-missing templates. All are 0089's.
- That a marker counts as approved whatever it holds (FR-5). The resolver decides that (0089). It never reads a provenance line.
- Tampering, route change, slip creation and the starter-command checklist itself. All are 0091's.
- Fixture directories, a fixture gate, a fixture sub-flow command and every test script. All are 0092's. *What 0092 needs from this ADR* lists the inputs.
- Any change to what the `design` sub-flow does to a valid ADR and the index (requirements, Out of Scope). Its location moves. It differs from today's `approve.md:34-58` in these places, which narrows requirements Out of Scope, FR-29's description of the move as a structural refactoring, AC-24 and AC-77: an ADR that fails `write_adr_metadata`'s Step 3 checks, which today is repaired or asked about in the middle of the approval, now stops it before any write; an ADR selector that matches other than one ADR fails, and so does more than one selector word, where today the words after the first selector word are ignored; the supersession question comes before the first write; an ADR already `Superseded` by another selected ADR is not accepted again on a re-run; the question offers at most three ADRs; a target already `Superseded` by a different ADR is refused; and `deprecate` is reached by a free-text answer. For an ADR whose frontmatter passes the checks, and whose supersession targets are not already retired, the files that result are today's. The rule for when to ask is today's, made explicit.

### Where this ADR sits

| ADR | Decides |
| --- | --- |
| 0089 | The slip and approval-table formats, the route resolver and its report, and how commands consult it |
| **0090** *(this one)* | How a command runs a sub-flow (approval is the first trigger), the provenance line, the gate run contract, the `/adr:*` commands that carry the `design` sub-flow, and the README's notes on running a sub-flow and on the provenance line |
| 0091 | How a slip is installed and frozen at creation (`/spec:new`'s copy and record, numbering, the starter contract), how tampering is detected, how a route is changed, and the README's notes on the route-change command and the starter checklist |
| 0092 | The resolver's and show-me's fixture tests, the fixture flow-definition directories, and the README's notes on running the tests and on a `/bugfix:*` migration |

*"A spec's route is data it carries; commands ask the resolver, not themselves, where it is going."*

### Approval work is written into one command

`design` is the only kind with approval work today (*How an approval runs*). The table below shows how `design` is handled now and after this ADR; the mechanism it leads to does not depend on ADRs.

| Approval question | Answered today by | After this ADR |
| --- | --- | --- |
| Which ADRs become `Accepted`? | Prose in `/spec:approve` | `/adr:accept` |
| Which older ADRs are retired? | Prose in `/spec:approve`, asking mid-write | `/adr:retire-superseded`, asking before any write |
| Who regenerates the index? | A section of `approve.md` | `/adr:regenerate-index` |
| Who reports the outcome? | Prose in `/spec:approve` | `/adr:report-approval` |
| What does the marker record? | Nothing; `touch` | One provenance line per approval |
| What if approval stops part-way? | Undefined | No marker; re-run from the first command |

Every row has the same shape: approval behaviour lives in one command's prose. That arrangement has three consequences:

- A gate or a flow's own approver cannot produce the same outcome without copying the prose.
- The supersession question can come after the first ADR write, so a cancelled question leaves a half-approved design.
- A marker cannot say which ADRs, or which commit, an approval covered.

The mechanism behind all three is the same. Approval is a procedure, but it is written as part of one command instead of as a contract that any approver can follow.

### The forces

- **A command is an instruction for an agent, not code.** The run procedure must be concrete enough for an agent to follow step by step, and for a person to check from a transcript.
- **Questions before writes.** A sub-flow asks everything before its first write, and a re-run asks again (FR-29).
- **No progress record.** A failed sub-flow re-runs from its first command, so every command must be idempotent for the same answers (FR-29).
- **The approver stays generic.** `/spec:approve` holds no kind-specific action, and the table cell holds bare command names (FR-12, 0089).
- **Same outcome as today.** A `feature` spec's design approval must end with the same ADRs, statuses and index as `approve.md` produces now, within the limits that Scope lists (AC-24, AC-77).
- **Markers stay valid as they are.** Existing markers hold nothing, free text or paragraphs, and the resolver tests only that the file exists (FR-5).
- **A flow adds files, not edits.** A flow-specific kind adds a table row and its own commands. A flow that reuses an existing command edits that command, and the edit changes it when run directly too. It edits no `/spec:*` file (FR-25, FR-28).
- **No new runtime or parser.** A provenance line must be readable with the shell tools the commands already use (NFR-2).

## Decision

**Every approver runs the sub-flow that the approval table names for a step's kind in two passes — all Questions sections, then all Actions sections in table order — and writes the marker, with one appended provenance line, only when every command has completed; a gate does this for each step it approves, inside a run contract that never overwrites an artefact without asking.**

The approval table names the commands, and any kind may name any number, including none. The approver runs each one through the Skill tool, first with the mode word `questions` and then with `actions`. It passes the same common arguments each time and adds the answers collected so far: in the questions pass, those of the earlier commands, and in the actions pass, all of them. Each command reports completion or failure in a fixed protocol line. A failure stops the approver with no marker written, and a re-run starts again from the first command. The mechanism knows nothing of any kind. `requirements` and `tasks` run an empty sub-flow. `design` runs four narrow `/adr:*` commands extracted from today's approve command. *A second sub-flow* shows a kind with different commands.

### The mechanism, end to end

```mermaid
sequenceDiagram
    participant U as User
    participant AP as Approver
    participant AC as accept
    participant RS as retire-superseded
    participant RI as regenerate-index
    participant RP as report-approval
    participant MK as Marker
    AP->>AP: route instructions, step checks, read approval_subflow
    AP->>AC: questions pass
    AC-->>AP: sub-flow-done
    AP->>RS: questions pass
    RS->>U: supersession question when the ADR is ambiguous
    U-->>RS: answer
    RS-->>AP: answer lines, sub-flow-done
    AP->>RI: questions pass with earlier answers
    RI-->>AP: sub-flow-done
    AP->>RP: questions pass with earlier answers
    RP-->>AP: sub-flow-done
    AP->>AC: actions pass with answers
    AC-->>AP: approved-artefacts, sub-flow-done
    AP->>RS: actions pass with answers
    RS-->>AP: sub-flow-done
    AP->>RI: actions pass with answers
    RI-->>AP: sub-flow-done
    AP->>RP: actions pass with answers
    RP-->>U: report and commit reminder
    RP-->>AP: sub-flow-done
    AP->>MK: append provenance line
    AP->>U: step approved, then next-step line from the closing instructions
```

The diagram shows the `design` sub-flow. Another kind changes the participants between the approver and the marker, and nothing else. These invariants follow from it:

- **No write precedes the last question.** Every Questions section runs, and every input fault surfaces, before the first Actions section.
- **The marker is the last write of the sub-flow.** A missing `sub-flow-done` line from any command means no marker.
- **The approver decides nothing about the kind.** It reads command names from the report and protocol lines from the commands.
- **A re-run is a full re-run.** The approver keeps no record of which commands finished.

The same two passes serve both examples. Only the commands, their answers and their selector words differ:

| | `design` (shipped) | `change` (illustrative) |
| --- | --- | --- |
| Questions pass | `accept` checks each ADR's frontmatter. `retire-superseded` asks the supersession question, when the ADR is ambiguous. `regenerate-index` and `report-approval` check their inputs | `smells` asks which smells to act on and prints one answer per finding of each chosen smell. `tidy-first` receives those answers, confirms its analysis and checks that each named file exists |
| Actions pass | Set each ADR `Accepted`, retire the superseded ADRs, regenerate the index, print the report | `smells` writes nothing. `tidy-first` makes each chosen structural refactoring and skips one already made |
| Selector words | Name one ADR | None |
| Artefacts recorded in the provenance line | The selected ADR files, from `approved-artefacts:` | The step's artefacts, say `scope.md`, because neither command prints `approved-artefacts:` |
| Marker | Written last, by the approver | Written last, by the approver |

#### The sub-flow instructions

The canonical text lives in `.claude/commands/spec/README.md`. Every approver carries an identical copy: `approve.md`, each gate and each flow's own marker-writing command. An approver runs them after 0089's route instructions and its step checks. For `/spec:approve` the step checks are those of 0089's approve row. For a gate they are step 1 of *The gate run contract*; the artefacts of N+1 are present because the ladder has just produced or kept them. This lead-in is not part of the canonical copy. For a flow's own marker-writing command they are the equivalent checks that 0091's checklist adds to FR-19's. Gates and flow approvers carry copies of 0089's route instructions and closing instructions as part of FR-19, outside the counts of eight and five commands, which count the commands this spec ships.

1. Read `steps[{step}].approval_subflow`. If it is `null`, print the no-usable-entry message and stop.
2. If it is empty, go to step 7.
3. Build the common arguments (*Arguments and answers*): `"{spec}" {step} {artefacts}`, then `--select {word}` for each of the approver's selector words, in order. `{spec}` is the report's `spec`, and `{artefacts}` is `steps[{step}].artefacts` joined with `,`. `/spec:approve` takes its selector words from the words after the step name. A gate or a flow's own approver passes none, unless its own command defines some.
4. Questions pass. For each command in table order, run it through the Skill tool (its name without the leading `/`), with arguments `questions`, the common arguments, and `--answer {key}={value}` for every answer kept from the commands before it. Keep every `answer:` line it prints, in order.
5. Actions pass. For each command in table order, run it with `actions`, the common arguments, and `--answer {key}={value}` for every kept answer.
6. After each run in steps 4 and 5, the command has completed only when the last protocol line it printed is `sub-flow-done: {command} {pass}`. Otherwise print the sub-flow-failed message for that pass and stop. Write no marker.
7. Append the provenance line to `steps[{step}].marker` (*The provenance line*), and print the step-approved message.

Then `/spec:approve` runs 0089's closing instructions, and a gate runs them at the last step of its ladder. With an empty sub-flow, any selector words are ignored, as `approve.md` ignores words after the step name today.

#### Failure and re-runs

| Command | Re-run with the same answers | Why it changes nothing further |
| --- | --- | --- |
| `/adr:accept` | No change | It skips an ADR whose frontmatter and body Status are both `Accepted`, and an ADR already `Superseded` by another selected ADR |
| `/adr:retire-superseded` | No change | It skips a target already `Superseded` by the same ADR, or, for `deprecate`, already `Deprecated` |
| `/adr:regenerate-index` | The same `index.md` | The awk generator is deterministic |
| `/adr:report-approval` | The same report | It writes nothing and reads the same inputs |
| `/refactor:smells` (`change`, not shipped) | No change | It writes nothing |
| `/refactor:tidy-first` (`change`, not shipped) | No change | It skips a refactoring already made |

Idempotence for the same answers is a rule of the mechanism (FR-29), so every sub-flow command meets it, whatever its kind. A failure in the questions pass leaves nothing written. A failure in the actions pass leaves the writes of earlier commands in place, and the message says so. Idempotence holds only for the same answers. A re-run that gives a different supersession answer leaves the earlier retirement in place. That limitation is accepted (requirements, Constraints). The user sees it in `git diff` and reverts it with git.

#### The provenance line

One line is appended per approval:

```
approved: step={step} artefacts={approved} sha={full HEAD sha, or unavailable}
```

- `{approved}`, the value of `artefacts=`, comes from the last `approved-artefacts:` line, or from `steps[{step}].artefacts` joined with `,` when no command printed one. For `design` the line lists every selected ADR file, including one that was already `Accepted` and skipped, so a re-run records the same artefacts as the first run.
- `artefacts=` holds paths as the step's artefacts or the `approved-artefacts:` line give them: spec-relative names such as `tasks.md`, or repository paths such as `docs/adr/0089-slips-resolver-and-routing.md`.
- Existing content is kept. If the marker is non-empty and lacks a final newline, a newline is added first, so the line starts a line (the case in *Negative* excepted).
- Re-approving appends a further line. The resolver never reads the content (0089).

The commands that write the line, with worked examples, are under *The provenance commands*.

#### The gate run contract

This spec ships no gate. The contract binds every flow that owns one (FR-19). A gate approves step N and produces step N+1. It finds its step as the one whose `gate.command` is itself, and reads `steps[N].approved`, `steps[N+1].artefacts_present`, `steps[N+1].approved` and `gate.also_approves_produced`.

| # | Starting state | Interrupted state | What the gate does |
| --- | --- | --- | --- |
| 1 | N unapproved, N+1 absent and unapproved | A, when N's sub-flow failed before | First question, approve N, produce N+1, then second question |
| 2 | N unapproved, N+1 present and unapproved | A, when N's sub-flow failed before | First question, approve N, existing-artefact question |
| 3 | N approved, N+1 absent and unapproved | B: stopped after N's marker | Produce N+1, then second question |
| 4 | N approved, N+1 present and unapproved | C: stopped after N+1's artefact | Existing-artefact question |
| 5 | N+1 approved, its artefacts present or absent | — | First question if N is unapproved. Never touches N+1 |

Rows 1 to 4 need N+1 unapproved and row 5 needs it approved, so exactly one row matches. The gate applies the row in this order:

1. When N is unapproved and `steps[N].artefacts_present` is false, print 0089's artefacts-missing template and stop. Nothing is written.
2. Ask the first question in rows 1, 2 and 5 when N is unapproved. On `Cancel`, print the declined message and stop. Nothing is written.
3. In rows 1 and 2, and in row 5 when N is unapproved, approve N with the sub-flow instructions. A failure stops the gate with no marker for N, and a re-run lands on the same row.
4. In rows 1 to 4, run the resolver again and read `steps[N+1].precondition`. When `holds` is false, print its `message` and go to step 8, leaving N approved (FR-27).
5. In rows 1 and 3, produce N+1. In rows 2 and 4, ask the existing-artefact question. `Regenerate` produces N+1 again. `Cancel` leaves the state of row 4 and goes to step 8.
6. When `also_approves_produced` is true, ask the second question after a fresh production or `Regenerate`. Choosing `Approve as is` at the existing-artefact question counts as approval.
7. On approval, run the sub-flow instructions for N+1. `Not now` leaves the state of row 4 and goes to step 8.
8. Run the closing instructions.

A gate produces N+1 only in rows 1 and 3, or after `Regenerate`, so it never overwrites an artefact without asking. A gate that is not N+1's marker-writer offers `Keep as is`, and goes to step 8 after it. Every path ends at step 8, except the stops in steps 1 to 3, which write no marker, so the user sees the next step.

### Where the pieces live

```mermaid
flowchart LR
    subgraph SPEC[".claude/commands/spec/"]
        AP["approve.md<br/>changed - sub-flow instructions and provenance"]
        RM["README.md<br/>changed - running a sub-flow, provenance, gate contract"]
        RR["resolve_route.cs<br/>0089"]
    end
    subgraph ADRC[".claude/commands/adr/"]
        ACC["accept.md - new"]
        RET["retire-superseded.md - new"]
        REG["regenerate-index.md - new"]
        REP["report-approval.md - new"]
        WAM["write_adr_metadata.md<br/>unchanged"]
        AWK["generate_adr_index.awk<br/>unchanged"]
    end
    subgraph FLOWS[".claude/flows/"]
        AT["approvals.md<br/>0089 - design row names the four"]
    end
    subgraph DOCS["docs/adr/"]
        ADRS["NNNN-slug.md"]
        IDX["index.md"]
    end
    subgraph SPECS["specs/NNNN-name/"]
        MK["markers<br/>gain provenance lines"]
        AL[".adr-list"]
    end
    AP -->|route report| RR
    RR -->|reads| AT
    AP -->|runs in order| ACC
    AP -->|runs in order| RET
    AP -->|runs in order| REG
    AP -->|runs in order| REP
    AP -->|appends| MK
    ACC -->|reads| AL
    RET -->|reads| AL
    REP -->|reads| AL
    ACC --> WAM
    RET --> WAM
    WAM -->|writes| ADRS
    REG --> AWK
    AWK -->|writes| IDX
```

Dependencies point one way. The approver depends on the resolver's report and on the commands it names. The `/adr:*` commands depend on `write_adr_metadata` and the awk generator, and know nothing of slips. A gate or flow approver sits where `approve.md` sits, and carries the same sub-flow instructions. The diagram shows the `design` sub-flow, the only one this spec ships.

### Key Components

#### The roles, and what each is responsible for

These types are agent instructions and named sections, not compiled types. Each role is still one phrase, and each can be checked from what it prints.

| Role | Type | Responsibilities | Responsibility classifier | Collaborators |
| --- | --- | --- | --- | --- |
| Runs a sub-flow | sub-flow instructions (section of each approver) | Runs both passes in table order, judges completion, writes the marker last | doing, deciding | route report, sub-flow commands, provenance line |
| Asks before acting | Questions section | Checks inputs read-only, asks the user, prints answers | knowing, doing | the user |
| Acts on answers | Actions section | Makes the command's writes, asks nothing | doing | the kind's own writes (for `design`, `write_adr_metadata`, awk) |
| Accepts ADRs | `/adr:accept` | Flips each selected ADR to `Accepted` | doing | `write_adr_metadata`, `.adr-list` |
| Retires older ADRs | `/adr:retire-superseded` | Learns what each ADR supersedes, then supersedes or deprecates | deciding, doing | `write_adr_metadata`, the user |
| Rebuilds the index | `/adr:regenerate-index` | Regenerates `docs/adr/index.md` | doing | `generate_adr_index.awk` |
| Reports the approval | `/adr:report-approval` | Prints the outcome and the commit reminder | knowing | answers, `.adr-list` |
| Records provenance | provenance step (sub-flow instructions step 7) | Appends one line, keeping existing content | doing | `git rev-parse`, `test`, `tail`, `echo` |
| Approves and produces | a flow's gate | Follows the gate ladder | deciding | sub-flow instructions, route report |

#### The sub-flow command shape

Every sub-flow command, in any flow, is one Markdown command file with these parts:

| Member | Input | Output | Error conditions |
| --- | --- | --- | --- |
| `## Arguments` | the argument form in *Arguments and answers* | dispatch on the mode word | a missing common argument that the command uses: `sub-flow-failed`, reason `usage` |
| `## Questions` | common arguments and the answers of earlier commands | `answer:` lines, then `sub-flow-done` | an input fault, or a dismissed question: `sub-flow-failed` |
| `## Actions` | common arguments and every answer | its writes, optional `approved-artefacts:`, then `sub-flow-done` | any failed write: `sub-flow-failed` |

A command with no questions has a Questions section that checks its inputs, or says it has nothing to ask, and prints `sub-flow-done`. A command that uses none of the common arguments, such as `/adr:regenerate-index`, needs none and can be run by hand with no arguments.

An existing command joins a sub-flow by gaining these three parts, by moving any question out of its writes, and by making its Actions idempotent for the same answers (FR-29). The mechanism does not change it otherwise.

#### Arguments and answers

Every sub-flow command takes one argument form:

```
[questions|actions] "{spec}" {step} {artefacts} [--select {word}]... [--answer {key}={value}]...
```

A command requires only the common arguments it uses (*The sub-flow command shape*).

- With no mode word the command was run directly. It runs its Questions section, then its Actions section, and the Actions section uses the answers that its own Questions section printed in the same run, plus any `--answer` arguments given.
- `{command}` is the command's name as the approval table gives it, with the leading `/`. The Skill call drops the `/`.
- `{spec}` is always double-quoted, because a spec directory name may contain spaces.
- `--select` carries a selector word. Each sub-flow defines what its selector words mean, and a command that defines none ignores them. The `design` commands read them as an ADR selector. The `change` commands define none.
- An answer key is `{command}.{name}`, for example `/adr:retire-superseded.0090-approval-sub-flows` or `/refactor:smells.{type}.{smell-slug}.{n}`, where `{name}` is whatever the command chooses to tell its answers apart by. The prefix keeps keys from two commands apart. A Questions section receives the answers printed by the commands before it in the pass, and an Actions section receives every answer.
- A value that contains a space is quoted as `--answer "{key}={value}"`. A value may not contain `"` or a newline.
- `/spec:approve design 0043` therefore reaches each `design` command as `questions "specs/0040-routing-slips" design .adr-list --select 0043`.

#### The protocol lines

A sub-flow command prints these lines, each alone on a line. Their canonical text lives in `.claude/commands/spec/README.md`.

| Line | Printed in | Meaning |
| --- | --- | --- |
| `answer: {key}={value}` | the questions pass | One answer, whether the user gave it or the command derived it |
| `approved-artefacts: {paths joined with ","}` | the actions pass, optional | Every file this approval covers, including any the command found already approved and skipped. The approver uses the last such line |
| `sub-flow-done: {command} {pass}` | the last line of each pass | The pass completed |
| `sub-flow-failed: {command} {pass}: {reason}` | instead of `sub-flow-done` | The pass failed. A question the user dismissed has the reason `question not answered` |

#### The provenance commands

The approver writes the line with these commands, each run on its own from the repository root. `{m}` is `steps[{step}].marker` joined to `{spec}`, in double quotes. The text names no literal marker, so AC-9 holds:

1. `git rev-parse --verify --quiet HEAD`. It prints the full sha. With no commits it exits `1` silently, and outside a git work tree it exits `128`. In both cases the sha is `unavailable`.
2. `test -s {m}`. When it succeeds, `tail -c 1 {m}`. When the byte it prints is not a newline (the output looks empty when the file ends in one), the marker lacks a final newline, so run `echo >> {m}`.
3. `echo "approved: step={step} artefacts={approved} sha={sha}" >> {m}`.

Running them separately lets the harness judge each by its own prefix, and no compound command or substitution is needed. 0092 compares this text between the README and every approver. Worked examples, at HEAD `0691e67beb1ce7242c734bcd0a9a5960610d432d`:

```
approved: step=tasks artefacts=tasks.md sha=0691e67beb1ce7242c734bcd0a9a5960610d432d
approved: step=design artefacts=docs/adr/0089-slips-resolver-and-routing.md,docs/adr/0090-approval-sub-flows.md sha=0691e67beb1ce7242c734bcd0a9a5960610d432d
approved: step=requirements artefacts=requirements.md sha=unavailable
```

A `change` step would record `artefacts=scope.md`, because neither of its commands prints `approved-artefacts:`. A free-text marker keeps its text, and the line follows it. `grep '^approved: ' {marker}` lists every approval. `awk '/^approved: /{print $2, $NF}' {marker}` prints each step and sha. `test -f` still decides approval.

#### The `design` sub-flow commands

The `design` row of `.claude/flows/approvals.md` reads `/adr:accept, /adr:retire-superseded, /adr:regenerate-index, /adr:report-approval`. Three commands apply the **ADR selection rule**, whose canonical text lives in `.claude/commands/adr/README.md`. Each of the three carries an identical copy, and 0092 compares them:

1. Read `{spec}/.adr-list`. Each non-blank line, trimmed, names one ADR. A leading `docs/adr/` is removed, because both forms occur in existing specs. The path is `docs/adr/{name}`.
2. With no `--select`, select every ADR. With one, select the line whose stem equals the word. Failing that, select the lines whose name starts with `{word}-`.
3. Fail on an empty list, more than one `--select`, no match, more than one match, or a selected file that does not exist.

| Member | Input | Output | Error conditions (the `{reason}`) |
| --- | --- | --- | --- |
| `/adr:accept` Questions | common arguments | `sub-flow-done` | a selection fault; a selected ADR with no frontmatter (`run write_adr_metadata docs/adr/{name} init first`); a selected ADR that fails any of the six checks in `write_adr_metadata` Step 3, with the fix named. A mismatch between the frontmatter and body status, where one side is already `Accepted`, is not a fault, because the Actions section sets both |
| `/adr:accept` Actions | common arguments | `write_adr_metadata docs/adr/{name} status Accepted` per ADR not yet `Accepted` in both frontmatter and body and not already `Superseded` by another selected ADR; `approved-artefacts:` listing every selected ADR | `write_adr_metadata did not pass its checks for docs/adr/{name}`, including a question it raises, which the Actions section does not answer; the Actions section prints `sub-flow-failed` whoever could answer |
| `/adr:retire-superseded` Questions | common arguments | one `answer:` per selected ADR. The key is `/adr:retire-superseded.{stem}` and the value is `none` or a `,`-list of `supersede:{stem}` and `deprecate:{stem}` | a selection fault; `question not answered`; a retirement target that fails the six checks, with the fix named, except a frontmatter and body status that differ where one side is already `Superseded` or `Deprecated`, which the Actions section sets |
| `/adr:retire-superseded` Actions | answers | `supersede --by {approved stem}` or `deprecate` on each target, through `write_adr_metadata` | `write_adr_metadata did not pass its checks for docs/adr/{stem}.md`, including a question it raises, which the Actions section does not answer |
| `/adr:regenerate-index` both passes | none used | `awk -f .claude/commands/adr/generate_adr_index.awk docs/adr/[0-9]*.md > docs/adr/index.md` in Actions | `awk exited {code}` |
| `/adr:report-approval` Questions | common arguments | `sub-flow-done` | a selection fault |
| `/adr:report-approval` Actions | common arguments, the retire answers | the four report lines | a selection fault |

The six checks in the Questions rows are read-only. They stop an input fault before the first write, where today `write_adr_metadata` would repair it or ask in the middle of the approval. An ADR whose frontmatter passes them is approved as today, within the limits that Scope lists.

`/adr:retire-superseded` asks only when needed, as `approve.md` does today. An ADR whose `## Status` and `## References` name no earlier ADR after "Supersedes" gets `none` without a question. An ADR that names exactly one existing ADR after "Supersedes", with no section mark (`§`), gets `supersede:{stem}` without a question, unless that target is already `Superseded` by a different ADR. Every other ADR gets the supersession question, which offers the first three ADRs named after "Supersedes" that exist, are not the ADR itself and are not already `Superseded` by a different ADR, in the order the text names them. Others can still be named in the free-text answer, which is validated. The free-text answer is one or more of `supersede {stem}` or `deprecate {stem}`, separated by commas, and the command stores it as the `,`-list above. An answer that names a missing file, the ADR itself, or a target already `Superseded` by a different ADR repeats the question with the reason.

#### A second sub-flow

A refactoring flow can give a kind of its own, `change`, the sub-flow `/refactor:smells, /refactor:tidy-first`. The flow adds one row to the approval table and edits no `/spec:*` file. This spec does not ship it. It shows a sub-flow that is not about ADRs, with different commands, different answers and no `--select` word. Neither command fits as it stands, and the changes each needs belong to the flow:

| Command | Questions | Actions | What the flow must change in the command |
| --- | --- | --- | --- |
| `/refactor:smells` | Reads the scope from the step's artefact, asks which smells to act on, prints one `answer:` per finding of each chosen smell | Writes nothing | Add the three sub-flow parts. Drop its rule that the user, not the command, starts `/refactor:tidy-first`. Drop or move its write to `tasks.md` |
| `/refactor:tidy-first` | Confirms its analysis with the user, and checks that each file an answer names exists | Makes each chosen structural refactoring, skipping one already made | Add the three sub-flow parts. Move its questions into Questions. Limit it to structural changes. Make its commits idempotent (FR-29) |

A step's artefacts are file names in the spec directory (0089), so a `change` step cannot name source code. Its artefact is a spec file, say `scope.md`, that names the base ref or the paths, and `/refactor:smells` reads the scope from it. An answer key is `/refactor:smells.{type}.{smell-slug}.{n}`, where `{n}` numbers the findings of that smell in that type, and its value is the path of the file that holds the finding, so two types that share a smell name do not collide and `/refactor:tidy-first` can check the file. `/refactor:tidy-first`'s Questions section receives those answers, because a Questions section receives the answers of the commands before it. The instructions, the protocol lines, the provenance line and the failure rules are as for `design`. Choosing this flow is the user's opt-in to `/refactor:smells` and `/refactor:tidy-first` running at approval.

#### Message templates

The approver's templates and the gate's templates have canonical text in `.claude/commands/spec/README.md`. The `/adr:*` templates have canonical text in `.claude/commands/adr/README.md`. Each command that prints one carries an identical copy, and 0092 compares the copies. `{approver}` is the approver's own invocation as typed, `{pass}` is `questions` or `actions`, and `{reason}` comes from the `sub-flow-failed` line, or is `no sub-flow-done line`. `{command}` is the sub-flow command named in the `sub-flow-failed` line, or the command that printed no `sub-flow-done` line. It is not the command printing the template, as it is in 0089's templates.

| Template | Printed by | Text |
| --- | --- | --- |
| No usable entry | every approver | ``Step `{step}` cannot be approved: kind `{steps[step].kind}` has no usable entry in the approval table. Nothing was written.`` |
| Sub-flow failed, questions | every approver | ``{approver} stopped: `{command}` failed in its questions pass: {reason}. {steps[step].marker} was not written, and this sub-flow wrote nothing.`` |
| Sub-flow failed, actions | every approver | ``{approver} stopped: `{command}` failed in its actions pass: {reason}. {steps[step].marker} was not written. Writes made by earlier sub-flow commands stay. Re-run {approver} to run the sub-flow again from `{approval_subflow[0]}`.`` |
| Step approved | every approver | ``Approved step `{step}` on flow `{flow}`: {steps[step].marker} gained a provenance line.`` |
| Gate, first question | a gate | ``Approve step `{N}` ({steps[N].artefacts joined with ", "}) and go on to produce step `{N+1}`?`` Options `Approve`, `Cancel` |
| Gate, declined | a gate | ``{approver} stopped: step `{N}` was not approved. Nothing was written.`` |
| Gate, existing artefact | a gate | ``Step `{N+1}`'s artefacts ({steps[N+1].artefacts joined with ", "}) already exist. What should {approver} do?`` Options `Approve as is` (or `Keep as is`), `Regenerate`, `Cancel` |
| Gate, second question | a gate | ``Approve step `{N+1}` ({steps[N+1].artefacts joined with ", "})?`` Options `Approve`, `Not now` |
| Supersession question | `/adr:retire-superseded` | ``Does `{adr}` supersede or retire an earlier ADR?`` Options `No`, then `Supersedes {stem}` for each of the first three ADRs its text names that exist, are not `{adr}` and are not already `Superseded` by a different ADR. The tool's free-text answer is read as one or more of `supersede {stem}` or `deprecate {stem}`, separated by commas |
| Report | `/adr:report-approval` | ``ADRs approved: {selected stems joined with ", "}.`` then ``ADRs superseded: {old} by {new}, ….`` or ``ADRs superseded: none.``, then ``ADRs deprecated: {old}, ….`` or ``ADRs deprecated: none.``, then ``Commit the approved ADRs and the regenerated docs/adr/index.md to git.`` |

A gate also prints 0089's artefacts-missing template, with `{step}` as `{N}` and `{producer}` as `steps[N].producer`. In a gate, `{N}` is `steps[i].gate.approves` and `{N+1}` is `steps[i].name`, for the step `i` that the gate produces.

#### Where each type is touched

| Assembly | Type | Change |
| --- | --- | --- |
| `.claude/commands/adr/` | `accept.md`, `retire-superseded.md`, `regenerate-index.md`, `report-approval.md` | **new** — none collides with an existing command name |
| `.claude/commands/spec/` | `approve.md` | The per-ADR status work, the supersession work, the index regeneration and the report move out (lines `:34-58`, `:60-61`, `:102-112` and `:130` of today's file). The marker `touch` (`:59`) becomes the provenance step. The route-dependent lines (`:32-33`) become generic (0089 step 9.6). The sub-flow instructions and templates are added. `allowed-tools` (`approve.md:2`) gains `Bash(git rev-parse:*)` and `Bash(tail:*)`; `Bash(test:*)` and `Bash(echo:*)` are already granted |
| `.claude/commands/spec/` | `README.md` | Sections on running a sub-flow, the protocol lines, the gate run contract and the provenance line. Templates. The approve entry (`README.md:233` and `:244-260`). The marker comments in File Structure (`README.md:817-819`) |
| `.claude/commands/adr/` | `README.md` | The four commands, the ADR selection rule and their templates. `README.md:51-52`, `:64-66` and `:182-194` name the `design` sub-flow |
| `.agent_instructions/` | `adr_frontmatter.md` | `:78-79` and `:159` name the `design` sub-flow, not `/spec:approve` alone |
| `.claude/commands/adr/` | `write_adr_metadata.md` | `:116` names `/adr:accept` and `/adr:retire-superseded` as callers, in place of `/spec:approve` |

These are deliberately **unchanged**:

- What `write_adr_metadata` does, and `generate_adr_index.awk` including its header (`generate_adr_index.awk:41`), so `index.md` regenerates byte-identically. The header says `/spec:approve` regenerates the index, which stays loosely true because `/spec:approve` still triggers it.
- `adr.md`'s own index regeneration when it creates an ADR.
- `.claude/commands/README.md` and `.agent_instructions/skills_overview.md`. Neither lists the ADR helper skills today, so neither lists the four.
- `.claude/settings.json`, and every existing marker under `specs/`.

### Technology Choices

#### Why four narrow commands

Each command does one thing that a re-run can repeat safely. A failure message that names `/adr:regenerate-index` tells the user exactly which write did not happen. One combined command would hide that, and a flow could not reuse index regeneration alone.

#### Why two passes and not questions asked inline

A question asked inside a command's work comes after earlier commands have written. Cancelling it then leaves a half-done approval. Asking everything first makes "nothing was written" true for every cancel and every input fault.

#### Why the Skill tool with a mode word

The Skill tool runs a command under its own name, with its own instructions loaded, so the transcript shows each command run. The mode word keeps each file whole and runnable by hand: a command that uses no common argument, such as `/adr:regenerate-index`, rebuilds the index with no arguments. Reading files and following their sections inline would leave no record of which command ran.

#### Why answers travel as arguments

Arguments keep the sub-flow free of state. A file of answers would be a progress record by another name, which FR-29 rules out, and it would need clean-up after a failure.

#### Why a `key=value` line

Fixed fields separated by single spaces read with `grep` and `awk`, and no field holds a space. A JSON line would read worse in an IDE and gains nothing, because no program parses the line.

### Implementation Approach

Numbered in commit order. A step that changes what a command does is behavioural, even when a valid approval ends with the same files. Line numbers of `approve.md` are those of today's file, before 0089 step 9.6.

1. **Behavioural, prerequisite for 0089 step 1.** Add the four `/adr:*` commands, each with Arguments, Questions and Actions sections and its template copies. Their text comes from `approve.md:38-46` (accept), `:48-54` (retire), `:56-57` and `:102-112` (index) and `:60-61` (report). Their `allowed-tools` are: accept `Read, Glob, Bash(grep:*), Skill`; retire `Read, Glob, Bash(grep:*), AskUserQuestion, Skill`; regenerate `Bash(awk:*)`; report `Read, Glob, Bash(grep:*)`. 0089 step 2 then names them in `approvals.md`.
2. **Documentation.** Add the canonical sub-flow instructions, protocol lines, templates, gate run contract and provenance-line notes to `.claude/commands/spec/README.md`. Add the ADR selection rule and `/adr:*` templates to `.claude/commands/adr/README.md`.
3. **Behavioural, after 0089 step 9.6.** In `approve.md`, replace the per-ADR status work and the supersession work (`:34-58`) and the report (`:60-61`) with the sub-flow instructions, and remove the index regeneration (`:102-112`) and the `:130` note. This adds the two-pass protocol, the questions-before-writes rule, the stricter ADR selection and the frontmatter checks, so the approval can now stop where it did not before. Keep `touch` for the marker in this commit, so a valid approval ends with the files it ends with today.
4. **Behavioural.** Replace the `touch` at `approve.md:59` with the provenance commands, and add the two `allowed-tools` entries at `approve.md:2`. Change `argument-hint` at `approve.md:4` to `<step> [selector]`.
5. **Documentation.** Update `adr_frontmatter.md:78-79` and `:159`, `adr/README.md:51-52`, `:64-66` and `:182-194`, `write_adr_metadata.md:116`, and `spec/README.md:233`, `:244-260` and `:817-819`.
6. **Verification of AC-79.** `grep -n -e write_adr_metadata -e supersede -e deprecate -e generate_adr_index -e index.md -e Accepted -e '\.adr-list' -e 'ADRs approved' .claude/commands/spec/approve.md` matches nothing.
7. **Verification of AC-24, AC-25, AC-26, AC-27, AC-76 and AC-77, by hand on a throwaway branch.** Use a `feature` spec with two `Proposed` ADRs, one recording "Supersedes" an `Accepted` ADR. Items 1 to 5 are one scenario: approve every ADR in the list, then commit the result. Item 6 and item 7 each start from the starting commit again, so that neither `Proposed` ADR is yet `Accepted` and the older ADR is not yet `Superseded`. Record the outcome of today's `approve.md` before step 3, then repeat after step 4, and compare items 1 to 6. Items 7 to 11 run after step 4 only. Items 8 to 10 start from any state, and item 9 needs a `tasks.md`. For item 7, also note today's behaviour on the same input before step 3 as a baseline; it is not compared:
   1. Each approved ADR has frontmatter `status: Accepted` and body Status `Accepted`.
   2. The superseded ADR is `Superseded`, with one `Superseded by` link.
   3. A fresh run of the awk command produces output identical to `docs/adr/index.md` (`awk -f .claude/commands/adr/generate_adr_index.awk docs/adr/[0-9]*.md | diff - docs/adr/index.md` prints nothing).
   4. `.design-approved` exists. After step 4 it also ends with one provenance line listing both ADR files.
   5. The report names both ADRs and the superseded one, then the commit reminder.
   6. `/spec:approve design {number}`, where `{number}` is the ADR that records no "Supersedes", changes only that ADR's status and the index, and writes the marker.
   7. With the superseded ADR made read-only (`chmod a-w`), approval stops naming `/adr:retire-superseded`, and no marker is written. After `chmod u+w` a re-run completes, with no duplicated link and one provenance line that lists both ADR files, as the first run would have.
   8. With `GIT_DIR` set to a directory that does not exist, `git rev-parse --verify --quiet HEAD` exits `128`, and the line ends `sha=unavailable` (AC-27).
   9. `/spec:approve tasks`, with HEAD at sha `S`, adds one line `approved: step=tasks artefacts=tasks.md sha=S` to `.tasks-approved` (AC-25).
   10. An approval of a step whose marker holds free text with no final newline keeps that text and adds the line on a new line (AC-26).
   11. With the superseding and the superseded ADR both selected, run the approval, then run it again. The older ADR stays `Superseded` and carries one `Superseded by` link (AC-76).

#### What 0092 needs from this ADR

- The gate ladder's five rows as test cases. The resolver states for rows 1–4 already appear as AC-56 (row 1), AC-89 (row 2), AC-57 (row 3) and AC-73 (row 4).
- The sub-flow command shape and the protocol lines, if 0092 ships a fixture sub-flow command or a fixture gate. Whether to ship them, and where, is 0092's to decide.
- The canonical texts and template copies to compare: the sub-flow instructions, the provenance commands and the approver templates in `approve.md`, and the `/adr:*` templates and the ADR selection rule in their files.
- The provenance-line pattern `^approved: step=[a-z][a-z0-9-]* artefacts=[^ ]+ sha=([0-9a-f]{40}|[0-9a-f]{64}|unavailable)$`.

## Consequences

### Positive

- Every approver of a step produces the same outcome, because the table names the commands and the README holds the procedure (FR-29).
- `/spec:approve` holds no kind-specific action, so a new kind needs a table row and commands, not an edit to `approve.md` (FR-12).
- A cancelled question, a bad ADR number or a faulty ADR frontmatter block writes nothing, where today's flow can stop after the first status flip.
- Each marker shows which step, which files and which commit an approval covered (FR-13).
- A gate has one ladder for every starting state, so an interrupted run resumes without overwriting anything (FR-27).
- `/adr:regenerate-index` can be run by hand.

### Negative

- Four more files under `.claude/commands/adr/`, and the ADR family now holds part of the spec workflow.
- An agent must follow a two-pass protocol and read protocol lines. A careless agent that skips the questions pass breaks the "nothing written" promise.
- A `design` approval makes eight Skill runs more than today, on top of the `write_adr_metadata` runs it already makes, which costs time and context.
- An ADR whose frontmatter fails a `write_adr_metadata` check now stops the approval with a named fix, where today the skill repairs it or asks mid-approval.
- A command that already exists, such as `/refactor:smells`, must gain Arguments, Questions and Actions sections, and idempotent Actions, to join a sub-flow.
- A marker whose last byte is a space or a tab shows no visible final byte in `tail -c 1`, so the newline is not added and the provenance line joins the text. The edge case is accepted.
- Each provenance write is three small commands, and the harness may prompt for the `echo >>` write the first time.
- Answers travel as arguments, so a value cannot hold a double quote or a newline.
- There is no progress record. A failure in the actions pass leaves earlier writes in place, and the user reverts them by hand.
- Idempotence holds only for the same answers.
- Sub-flow instructions and templates are copied into every approver. Only 0092's comparison keeps the copies identical.
- The provenance line adds the commit sha the approver was on, which does not prove the artefact was committed (requirements, Constraints).

### Risks and Mitigations

- **Risk: a sub-flow command needs a tool outside the approver's grants.** Mitigation: the harness prompts, which is not a failure. A denied prompt makes the command fail, and the approver stops with no marker.
- **Risk: `write_adr_metadata` asks a question during the actions pass.** Mitigation: `/adr:accept` and `/adr:retire-superseded` run the six `write_adr_metadata` checks read-only in their questions passes, so a fault stops the approval before any write. A half-written status is exempt, because the Actions set it. Any question that still arises in the actions pass is a failure with a named fix.
- **Risk: re-approving an old spec re-accepts an ADR superseded since.** Mitigation: the behaviour is today's and stays out of scope. The report names every ADR accepted, and `git diff` shows the change before commit.
- **Risk: a marker without a final newline joins the line to its last line of text.** Mitigation: provenance command 2 adds a newline first, except for a marker ending in a space or a tab (*Negative*).

## Alternatives Considered

### Alternative 1: Do nothing, keep the ADR work in `approve.md`

FR-12 and AC-79 forbid kind-specific actions in `/spec:approve`. A gate or flow approver could not reach the work without copying it.

### Alternative 2: One `/adr:approve-design` command

It would be shorter, but a failure could not name the write that failed. No other flow could reuse index regeneration alone. Its questions would still need a separate pass to come before its own first write.

### Alternative 3: Commands that write progress records

A per-command record would let a re-run skip finished commands. FR-29 rules it out, and it would add state that must be cleaned up, ignored by git and kept consistent with the files.

### Alternative 4: A table cell that carries arguments

0089 fixed bare command names. Arguments in the cell would put a second grammar in the table and tie the table to each command's signature.

### Alternative 5: Questions asked inline by each command

This is today's arrangement. It lets a cancel or an input fault arrive after the first write.

### Alternative 6: A runner in code that calls the commands

A C# runner cannot run agent instructions, ask the user, or call `write_adr_metadata`. It would replace one agent procedure with two halves.

### Alternative 7: A JSON or YAML provenance record

No program reads the record, and the requirements ask for readability with shell tools (NFR-2). One `key=value` line is easier to read in an IDE and in a diff.

## References

- Parent requirement: [specs/0040-routing-slips/requirements.md](../../specs/0040-routing-slips/requirements.md)
- Issue: [#4661](https://github.com/BrighterCommand/Brighter/issues/4661)
- [ADR 0089 — Routing Slips, the Route Resolver and the Approval Table](0089-slips-resolver-and-routing.md): the approval table, rule R12, the report fields, the route and closing instructions, and the templates this ADR reuses
- Siblings 0091 and 0092, as *Where this ADR sits* words them
- [ADR 0065 — Add frontmatter to ADRs](0065-add-frontmatter-to-adrs.md): `write_adr_metadata`, status vocabulary and the derived index
- [ADR 0071 — Shiftable Review Gear for the TDD Approval Gate](0071-tdd-review-gear.md): per-spec state with a reason, and asking before a write
- [ADR 0078 — Machine-Readable Forms in the `/spec` Family](0078-spec-family-machine-readable-forms.md): fixed forms that a command writes and another reads
- `.agent_instructions/adr_frontmatter.md`, `.agent_instructions/documentation.md`, `.agent_instructions/design_principles.md`
