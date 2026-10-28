# Requirements

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

**Linked Issue**: #4661

## Problem Statement

As a **maintainer of Brighter's spec workflow** I would like a spec directory to declare the route it follows to an approved `tasks.md`, and the `/spec:*` commands to follow that declared route, so that I can add a new route (such as the `/modules:*` flow of spec 0039, issue #4659) by writing one route definition, without editing every `/spec:*` command and without the commands sending a spec on a different route through the wrong steps.

As a **developer or agent working a spec** I would like every command's precondition check and every "next step" it prints to match the route my spec is on, so that I am never told to run a command that does not belong to my spec's route.

Today each command has the `requirements → design → tasks` route built into it:

| Command | Built-in route behaviour today | Where |
| --- | --- | --- |
| `/spec:requirements` | No precondition; creates a spec directory when `specs/.current-spec` does not exist; closing next steps are `/spec:review requirements`, `/spec:approve requirements`, then "Create ADR in `docs/adr/`" | `requirements.md:30-33`, `requirements.md:203-208` |
| `/spec:design` | Exits unless `.requirements-approved` exists; stops if `requirements.md` is missing; the ADR template links a parent `requirements.md` and FR ids | `design.md:31-37`, `design.md:97`, `design.md:108`, `design.md:164-166` |
| `/spec:design` | Closing next step is `/spec:approve design` "to proceed to tasks phase" | `design.md:424` |
| `/spec:tasks` | Exits unless `.design-approved` exists; requires `requirements.md`; maps every FR-N to a task | `tasks.md:24-31`, `tasks.md:172-173` |
| `/spec:approve` | Accepts only `requirements`, `design [adr]`, `tasks`; prints fixed next steps (`/spec:design`, then `/spec:tasks`, then the drivers) | `approve.md:22-28`, `approve.md:62-64`, `approve.md:83-87`, `approve.md:97-100` |
| `/spec:implement` | Requires `.requirements-approved`, `.design-approved`, `.tasks-approved` and every ADR `Accepted` | `implement.md:67`, `implement.md:113-118` |
| `/spec:ralph-implement` | Requires all three markers; passes `requirements.md` to each per-task sub-agent | `ralph-implement.md:152-155`, `ralph-implement.md:222` |
| `/spec:review` | Auto-detects phase from the three markers in fixed order; design, tasks and code reviews read `requirements.md` and grade FR coverage | `review.md:30`, `review.md:57`, `review.md:68`, `review.md:78`, `review.md:183`, `review.md:294`, `review.md:316` |
| `/spec:status` | Checks the three fixed markers and phase files; example next action "Create ADRs using /spec:design" | `status.md:19-21`, `status.md:71` |
| `/spec:switch` | Displays "next recommended action" (derived the same way as status) | `switch.md:18` |
| `/adr:adr` | Reads the current spec's `requirements.md`; suggests `/spec:tasks` when design is complete | `adr.md:50`, `adr.md:345` |
| `/spec:show-me` | Ledger records `requirements.present`; absence forces F5 = `Medium` and writes "No requirements.md found for this spec — scope reconciliation is not possible." | `show_me_facts.cs:272-301`, `show-me.md:447`, `show-me.md:559-560`, `show-me.md:689-691` |

A spec directory does not say which route it is on, so no command can tell one kind of spec from another. Spec 0039's first adversarial review found the consequence: once a modules spec's gate writes `.requirements-approved`, `/spec:status` says "Create ADRs using /spec:design" and `/spec:approve design` says the next step is `/spec:tasks`. Giving each command a lookup table keyed on flow name would mean editing every command again for every new flow.

## Proposed Solution

Each spec carries a **routing slip** (`.flow`): an ordered list of the steps up to an approved `tasks.md`, copied from a named **flow definition** when the spec starts. Today's route becomes the `feature` flow, defined as a slip like any other. A spec with no `.flow` is a `feature` spec and behaves exactly as it does today, so no existing spec under `specs/` needs migrating.

A single deterministic **route resolver** answers the routing questions for a spec directory: which flow, which steps are approved, what the next step is, whether a given command's precondition holds, and whether the slip has been tampered with. The `/spec:*` commands ask the resolver instead of each deriving the answer in its own prose. That is what lets a new flow be added with only a new definition. From `tasks.md` onward every flow is the same: `/spec:implement`, `/spec:ralph-implement`, `/spec:gear`, `/spec:show-me`.

A slip is frozen when the spec starts. The only way to change it is an explicit **route-change command** that records a reason, as `/spec:gear` does for the review gate. A slip that has been edited by hand is detected and refused. Approving a step also writes a one-line provenance record into the marker.

There are two acceptance tests, both stated as slips, verified end to end on fixtures, and not shipped:

- **`light`** (`design → tasks → /spec:implement`, no requirements step) shows that a flow can drop a step.
- **`bugfix`** (`diagnosis → tasks → /spec:implement`) shows that a flow can have a step whose artefact is not one the `/spec:*` commands know, and a gate that approves one step and produces the next step's `tasks.md`. It is the existing `/bugfix:*` workflow restated as a slip. The workflow itself is not migrated.

## Requirements

### Definitions

Each term is defined once here and used with this meaning throughout.

- **Flow**: a named route to an approved `tasks.md`, for example `feature`.
- **Flow definition**: the single canonical, repository-level statement of one flow's slip, for example `.claude/flows/feature.flow`. The location and file format are for the ADR to decide.
- **Slip** (routing slip): the content of a flow definition, or of a spec's copy of one. It is the flow's name, an ordered list of steps, and a hand-off.
- **Spec slip**: the copy of a slip held in a spec directory as `.flow`.
- **Step**: one entry on a slip. It has a **step name** (unique on the slip), an **artefact kind**, a **producing command** that creates its artefact(s), its **artefact(s)**, its **approval marker**, and its **marker-writing command**.
- **Artefact**: a file a step produces and an approval covers, for example `requirements.md`, `tasks.md`, or the ADRs listed in `.adr-list`. A step's artefact(s) are **present** when the files the slip names for it exist in the spec directory. For an ADR step this means `.adr-list` exists and is non-empty.
- **Artefact kind**: a name the slip declares for each step, saying what sort of artefact the step produces. The set of kinds is **open**: a slip may declare any kind name.
  - Three kinds are **known kinds**, because `/spec:*` commands read or review them: `requirements` (the step's artefacts include `requirements.md`), `design` (they include `.adr-list` and the ADRs it lists) and `tasks` (they include `tasks.md`).
  - Any other kind is a **flow-specific kind**, for example `diagnosis` for `bugfix.md` or `evaluation` for spec 0039's `evaluation.md`. No `/spec:*` command reads, requires or reviews a flow-specific kind's artefacts; only the flow's own commands do.
  - Commands that read another step's output look it up by artefact kind, not by step name.
- **Gate**: a command that is the marker-writing command of one step and the producing command of the next step on the same slip. In one run it approves the first step and then produces the next step's artefact(s). It may also be the next step's marker-writing command, in which case that run approves both. `/bugfix:confirm` in the `bugfix` fixture (FR-28) and spec 0039's `/modules:evaluate` are gates.
- **Approval table**: a table kept as data in the flow-definition directory, so that each flow-definition directory (the shipped one, or a fixture one) has exactly one. It holds at most one **entry** per artefact kind; two entries for one kind make the table invalid, and every slip resolved against it is then invalid. The entry says what approving a step of that kind does. Its format is for the ADR to decide.
- **Approval sub-flow**: the content of an approval-table entry. It is an ordered list of commands, possibly empty, that run when a step of that kind is approved. A sub-flow has no artefacts and no approval markers of its own, and every command in it is idempotent. Completing it is what returns control to the main flow: only then is the step's approval marker written.
- **Approval marker**: a file in the spec directory whose existence means the step is approved, for example `.design-approved`. A step is **approved** when its marker exists, whatever the file contains.
- **Provenance line**: one line written into an approval marker when a step is approved. It records the step name, the artefact(s) approved, and the HEAD commit sha.
- **Hand-off**: the command a slip names to run once every step is approved. On every valid slip it is `/spec:implement`.
- **Starter command**: a command that creates a new spec directory on a given flow and copies that flow's slip into it. `/spec:new` is the starter for `feature`.
- **Creation record**: whatever a starter command or the route-change command leaves behind to show which slip content it installed. The mechanism is for the ADR to decide.
- **Route change**: replacing a spec's slip with a different flow's slip, done only through the route-change command.
- **Tampered slip**: a spec slip that does not match its most recent creation record, as specified in FR-16.
- **Legacy spec**: a spec directory with no `.flow` and no creation record. This covers every spec under `specs/` that existed before this change.
- **Intent marker**: a file name that some flow definition in the flow-definition directory names as a step's approval marker, for example `.requirements-approved`, `.design-approved` or `.tasks-approved`. These belong to the intent phase, which ends at an approved `tasks.md`.
- **Stray marker**: an intent marker present in a spec directory that is not the approval marker of any step on that spec's slip. For example, `.requirements-approved` in a `light` spec.
- **Ignored marker**: a file in a spec directory whose name starts with `.` and ends with `-approved` and that is neither an approval marker on that spec's own slip nor an intent marker, for example `.code-approved` in `specs/0028-box-provisioning-rdd-role-interfaces/`. Such markers belong to the construction phase that follows `tasks.md`, not to the route. The resolver neither reports them nor lets them affect any routing decision.
- **Route-following command**: one of `/spec:requirements`, `/spec:design`, `/spec:tasks`, `/spec:approve`, `/spec:review`, `/spec:implement`, `/spec:ralph-implement` and `/adr:adr`. These are the commands that stop on an invalid or tampered slip (FR-7). `/spec:status` and `/spec:switch` report the route instead; the route-change command repairs it; `/spec:new` and starter commands create it. `/spec:gear`, `/spec:show-me` and `/spec:write_release_notes` are not route-following: they do not stop on an invalid or tampered slip and are otherwise as stated in FR-23 and the Constraints.
- **Out-of-order approval**: a step is approved while an earlier step on the same slip is not.
- **Route resolver**: the deterministic, read-only procedure of FR-6. Whether it is one script or several is for the ADR to decide.
- **Behaves as today**: for a given spec directory state, a command reaches the same decision as the current command text cited in the Problem Statement table. That means the same precondition passes or fails, the same files are required or read, and the same next command(s) are named. Byte-identical prose is not required. The one deliberate departure is FR-20: a missing, empty or stale `specs/.current-spec` now stops every command that acts on the current spec, including `/spec:requirements`.

### Functional Requirements

**FR-1 — Flows are defined once, in one repository location.**
- Every flow has exactly one flow definition, and all flow definitions live in one directory in the repository. The ADR confirms the location; `.claude/flows/{name}.flow` is an example.
- A flow's name is the name its definition gives it. A definition whose stated name differs from its filename stem is invalid (FR-3).
- This spec ships exactly one flow definition, `feature`. `light` and `bugfix` are not shipped (FR-25, FR-28).
- Example: `.claude/flows/feature.flow` exists, and no `light` or `bugfix` definition exists in that directory.

**FR-2 — A slip expresses the route to an approved `tasks.md`.**
A slip states, in a form that a person and the commands can both read:
- the flow name;
- the ordered list of steps, and for each step its step name, artefact kind, producing command, artefact(s), approval marker and marker-writing command;
- the hand-off command.
Example, the `light` slip: flow `light`; step `design` of kind `design`, produced by `/spec:design`, artefacts the ADRs in `docs/adr/` listed in `.adr-list`, marker `.design-approved`, written by `/spec:approve design`; step `tasks` of kind `tasks`, produced by `/spec:tasks`, artefact `tasks.md`, marker `.tasks-approved`, written by `/spec:approve tasks`; hand-off `/spec:implement`.

**FR-3 — A slip is valid only if it meets every well-formedness rule.**
- It has at least one step.
- Step names are unique within the slip.
- Approval markers are unique within the slip.
- Every producing command and every marker-writing command resolves to a command definition present in the repository's `.claude/commands/` tree. For example, `/spec:design` resolves to `.claude/commands/spec/design.md`.
- No producing command is named by more than one step. This keeps the question "which step is this command producing?" unambiguous. A command may be both one step's marker-writing command and the next step's producing command; that makes it a gate (FR-27).
- Every step declares an artefact kind. A step of a known kind names that kind's artefact (for `requirements`, `requirements.md`; for `design`, `.adr-list`; for `tasks`, `tasks.md`). At most one step on a slip has each known kind, so "the slip's `requirements` step" is unambiguous.
- A flow-specific kind name is any non-empty name other than the three known kinds. Example: `diagnosis`.
- A command that is one step's marker-writing command and another step's producing command is a gate only if the producing step comes immediately after (FR-27). Any other pairing is invalid.
- A step's producing command and marker-writing command may be the same command only when that command is a gate approving the step before it (FR-27). A step that produces and approves itself with no gate is invalid, because nothing would then stand between producing an artefact and approving it. A gate is allowed only because it asks the user before each approval it makes.
- The slip ends at the hand-off: no step follows it.
- Every step's artefact kind has an entry in the approval table (FR-29). A slip with a step whose kind has no entry is invalid. Example: a slip with a step of kind `spike` and no `spike` entry is reported invalid, naming the step and the missing entry.
- The last step's artefact kind is `tasks`, its artefact is `tasks.md`, and its marker is `.tasks-approved`. Everything after `tasks.md` is the same for every flow and already depends on that marker (`approve.md:91-93`, `ralph-implement.md:445`, `tasks.md:121-124`).
- The hand-off is `/spec:implement`.
- The flow name matches the definition's filename stem (applies to flow definitions only).
When a slip breaks a rule, the resolver reports the slip as invalid and names the rule and the offending step (FR-6). Every route-following command stops with that report (FR-7). Example: a slip whose `design` step names `/spec:designx` is reported invalid because step `design` names a command that does not exist, `/spec:designx`.

**FR-4 — The `feature` flow is today's route.**
The `feature` slip has three steps in this order:

| Step | Producing command | Artefact(s) | Marker | Marker-writing command |
| --- | --- | --- | --- | --- |
| `requirements` | `/spec:requirements` | `requirements.md` | `.requirements-approved` | `/spec:approve requirements` |
| `design` | `/spec:design` | ADRs in `docs/adr/` listed in `.adr-list` | `.design-approved` | `/spec:approve design` |
| `tasks` | `/spec:tasks` | `tasks.md` | `.tasks-approved` | `/spec:approve tasks` |

Its hand-off is `/spec:implement`. Every `/spec:*` command behaves as today for a `feature` spec, whether the spec is legacy or has a `.flow`.

**FR-5 — A spec with no `.flow` is a `feature` spec, with no migration.**
- A legacy spec resolves to flow `feature`, following the `feature` flow definition current at resolution time, and the resolver reports where the route came from (default, not `.flow`).
- Markers that are empty, hold free text, or hold provenance lines all count as approved. Examples: the free-text `specs/0031-test_naming_conventions/.design-approved`, the multi-paragraph `specs/0036-scoped-lifetime-per-pipeline/.requirements-approved`, and the empty markers in most other specs.
- No file in any existing spec directory is created, modified or deleted by this change.
- A legacy spec is never reported as tampered (FR-16).

**FR-6 — Route resolution is deterministic and answers every routing question.**
Given a spec directory, the resolver reports:
- the flow name and where it came from: `.flow`, or the default for a legacy spec;
- whether the slip is valid, and if not, which FR-3 rule failed;
- whether the slip is tampered (FR-16);
- for each step, in slip order, its producing and marker-writing commands, whether its artefact(s) are present and whether it is approved;
- any stray markers and any out-of-order approvals (FR-21); ignored markers are not reported;
- the next step, computed by FR-11;
- for a named step, whether the precondition to produce it holds (FR-8), and for the hand-off whether FR-9's precondition holds; if not, which step(s) are unapproved and the command that would approve each. Queries are by step, not by command, so a gate's two roles are never confused;
- for a known kind, which step on the slip has that kind and which command produces it, so that a command can tell whether it is that step's producer (FR-8);
- each step's artefact kind, and whether the slip has a step of each known kind (`requirements`, `design`, `tasks`), so that commands can apply FR-10;
- which steps, if any, are produced or approved by a gate (FR-27);
- for each step, its approval sub-flow from the approval table, so that every approver runs the same commands (FR-29);
- the most recent route change, if there has been one (FR-17).
The resolver also answers, given a flow name and no spec directory, whether that flow's definition exists and is valid, so that `/spec:new`, the route-change command and starter commands can check a definition before creating anything.
The same inputs (NFR-1) always give the same report. The resolver only reads, and is never consulted for a write. A resolver test (FR-24) can tell it to use a flow-definition directory other than the shipped one, together with that directory's approval table. The fixture flow-definition directory holds `light`, `bugfix`, a copy of `feature` (so that `feature`'s markers are intent markers there), and its own approval table.

**FR-7 — Commands take routing decisions from the resolver.**
- Every route-following command, `/spec:status`, `/spec:switch`, `/spec:new` and the route-change command gets every precondition outcome, next step, step-membership check and artefact-kind check from the resolver. None derives them from a list of markers or step names written into its own text.
- When the resolver reports an invalid or tampered slip, every route-following command stops before doing any work and shows the resolver's reason. The exceptions are `/spec:status` and `/spec:switch`, which report it (FR-15), and the route-change command (FR-17).
- Check: after this change, no file under `.claude/commands/spec/` other than `README.md`, and not `.claude/commands/adr/adr.md`, contains the literal text `.requirements-approved` or `.design-approved`. Those markers appear only in flow definitions, in documentation, and in fixtures.

**FR-8 — A step's precondition is that the previous step on the slip is approved.**
- A step's producing command may run only if the step immediately before it on the spec's slip is approved. The first step has no precondition.
- Example (feature): `/spec:tasks` needs `design` approved, as `tasks.md:24-25` does today. `/spec:design` needs `requirements` approved, as `design.md:31-37` does today.
- Example (light): `/spec:design` has no precondition, because `design` is the first step.
- If the producing command's precondition fails, it stops. Its message names the command, the unapproved step and that step's marker-writing command. Example: "`/spec:tasks` needs step `design` approved — run `/spec:approve design`." The exact wording is for the ADR.
- Each `/spec:*` producing command asks the resolver which step on the slip has its known kind and who produces it (FR-6). If the slip has no step of that kind, the command stops, says that it is not on flow `{flow}`'s route, and names the resolver's next step (FR-11). Example: `/spec:requirements` on a `light` spec with nothing present names `/spec:design`.
- If the slip has a step of that kind but a different command produces it, the command stops and names that producer. Example: `/spec:tasks` on a `bugfix` spec names `/bugfix:confirm`.
- Marker-writing commands keep today's preconditions and do not gain this one. `/spec:approve` verifies only that the step's artefact is present (`approve.md:24`, `approve.md:32`, `approve.md:83`).

**FR-9 — Implementation starts once every step on the slip is approved.**
- `/spec:implement` and `/spec:ralph-implement` start only when every step on the spec's slip is approved. Stray markers neither help nor hinder.
- `/spec:implement` also keeps its check that every ADR is `Accepted` (`implement.md:115`), but only when the slip has a `design`-kind step. `/spec:ralph-implement` gains no check it does not have today (`ralph-implement.md:152-155`).
- Example (feature): the same as today, all three markers. Example (light): `.design-approved` and `.tasks-approved`, with no `.requirements-approved` needed.
- On failure the command stops and names every unapproved step in slip order.

**FR-10 — A command reads another step's artefact only when the slip has that step.**
Every command that reads, requires, passes on or grades another step's artefact does so by known artefact kind. If the spec's slip has a step of that kind, it behaves as today. If not, it does not require the artefact and does not report its absence as a defect. No `/spec:*` command reads, requires or grades the artefacts of a flow-specific kind; a flow that wants `/spec:implement`'s sub-agents to see such an artefact (as `bugfix` would want for `bugfix.md`) cites it from `tasks.md`. This covers:
- `/spec:design` inputs (`design.md:97`, `design.md:108`) and the ADR's Parent Requirement and FR-id scope lines (`design.md:164-166`). With no `requirements`-kind step, the ADR cites the linked issue (`.issue-number`) or the spec's `README.md` as its parent instead.
- `/spec:tasks` inputs and the FR-coverage cross-reference (`tasks.md:26-31`, `tasks.md:46`, `tasks.md:172-173`). With no `requirements`-kind step, coverage is checked against the ADR decisions only.
- `/spec:review design`, `tasks` and `code` cross-references and FR criteria (`review.md:57`, `review.md:68`, `review.md:78`, `review.md:182-185`, `review.md:294`, `review.md:316`). These criteria are left out when the kind is absent, and the review states that the spec's route has no requirements step.
- The `/spec:ralph-implement` sub-agent context (`ralph-implement.md:222`) and `/spec:implement`'s ADR reads (`implement.md:69-70`). ADR reads apply only when the slip has a `design`-kind step.
- `/adr:adr` (`adr.md:50`).
- `/spec:show-me` (FR-23).
- A slip with neither a `requirements`-kind nor a `design`-kind step is valid, and these commands then need only `tasks.md`.

**FR-11 — Next-step guidance is read from the slip.**
The resolver computes the next step as follows:
- Find the first step on the slip that is not approved.
- If that step's artefact(s) are not present, the next step is its producing command.
- If they are present, the next step is `/spec:review {kind}` for the known kinds (`requirements`, `design`, `tasks`), which are the reviewable ones, followed by its marker-writing command. For a flow-specific kind it is the marker-writing command alone. Example (bugfix, `bugfix.md` present, nothing approved): the next step is `/bugfix:confirm`.
- A gate needs no special rule here. Example (bugfix, `.confirm-approved` present, no `tasks.md`, as after an interrupted gate run): the first unapproved step is `tasks`, its artefact is absent, so the next step is its producing command, `/bugfix:confirm`.
- If every step is approved, the next step is the hand-off.
Every command that prints a next step prints the resolver's next step: `/spec:status`, `/spec:switch`, `/spec:approve`, every producing command's closing guidance (`requirements.md:203-208`, `design.md:413-424`, `tasks.md:201-206`), `/spec:review`'s closing reminder (`review.md:462`) and `/adr:adr` (`adr.md:345`). Advice that does not depend on the route stays as it is. Examples are "run `/spec:design` again for another focus area" (`approve.md:62-63`) and the gear and driver explanation (`approve.md:66-79`).
Example (light, design approved, no `tasks.md`): `/spec:approve design` names `/spec:tasks`. Example (feature, requirements approved, no `.adr-list`): `/spec:status` names `/spec:design`, as today.

**FR-12 — `/spec:approve` approves steps on the slip.**
- `/spec:approve {step}` accepts any step on the spec's slip whose marker-writing command is `/spec:approve {step}`.
- It stops if the step is not on the slip. Example: `/spec:approve requirements` on `light`. The message names the flow and lists the steps it can approve.
- It stops if the step's marker-writing command is a different command, and names that command.
- For a step it accepts, it checks that the step's artefact(s) are present, runs the step's approval sub-flow (FR-29), and only when the sub-flow completes writes the marker the slip names, with a provenance line (FR-13). It holds no kind-specific approval actions of its own; route-independent closing advice (FR-11) is unaffected. For `design`, the sub-flow does what `approve.md:34-58` does today: the ADR status flips, supersession and index regeneration, honouring the optional single ADR number.
- If the approval table has no entry for the step's kind, it stops before writing anything and names the kind. FR-3 normally catches this earlier.
- With no argument, or an argument that is not a step, it lists the slip's approvable steps in place of the fixed list at `approve.md:97-100`.

**FR-13 — Approving a step writes a provenance line into its marker.**
- Each approval adds one line to the marker. The line records the step name, the artefact(s) approved (for `design`, each ADR file approved), and the full HEAD commit sha.
- Any existing content of the marker is kept. Re-approving adds a further line.
- If HEAD cannot be resolved (no commits, or not a git work tree), the line records the sha as unavailable rather than stopping.
- Approval is still established by the marker existing. `test -f` checks and the resolver both treat a marker as approved regardless of its content.
- Example: `/spec:approve tasks` at HEAD `5560022749…` adds a line naming step `tasks`, artefact `tasks.md` and sha `5560022749…`. `test -f .tasks-approved` succeeds.
- The format of the line is for the ADR to decide. It must be readable with the tools the commands already use (NFR-2).

**FR-14 — `/spec:review` selects phases from the slip.**
- With no phase given, the phase is the artefact kind of the first unapproved step, in place of the fixed marker order at `review.md:30`. If every step is approved, it is `code`, as today.
- If the first unapproved step's artefact kind is flow-specific, so has no review criteria, the command says so and names the step and its marker-writing command. Example: on the `bugfix` fixture with `bugfix.md` present, `/spec:review` names step `diagnosis` and `/bugfix:confirm`. It does not review a different phase.
- An explicit phase (`requirements`, `design`, `tasks`) is accepted only if the slip has a step of that kind. Otherwise it stops and says the phase is not on flow `{flow}`'s route. `code` is always accepted.
- The closing "Spec Status" block (`review.md:466-472`) lists the slip's steps instead of fixed Requirements, Design and Tasks lines.

**FR-15 — `/spec:status` and `/spec:switch` report the route.**
For each spec, status reports:
- the flow name and its source, for example `feature (default — no .flow)`;
- each slip step in order, with whether it is approved and whether its artefact is present;
- the resolver's next step for the active spec;
- the most recent route change and its reason, if any;
- any stray marker, out-of-order approval, invalid slip, tampered slip, or flow name with no definition (FR-22), each named.
For a `feature` spec, the step lines carry the same information as the Requirements, Design (ADRs) and Tasks lines today (`status.md:45-60`). Gear reporting is unchanged. `/spec:switch` shows the same per-spec report and next step for the spec it switches to. Status still reports a spec whose slip is invalid or tampered; it does not stop.

**FR-16 — A spec's slip is frozen at creation, and a hand-edited slip is refused.**
A spec slip is **tampered** when any of these holds:
- (a) its content differs from the content in the most recent creation record. Line-ending differences of the kind git's `* text=auto` normalisation produces (`.gitattributes:2`) do not count; every other difference does.
- (b) a creation record exists but `.flow` does not;
- (c) `.flow` exists but no creation record does. An example is a slip copied in by hand.
A legacy spec, which has neither, is not tampered. When the resolver reports a slip as tampered, route-following commands stop (FR-7). The message names the spec, says its slip does not match what was recorded when the route was set, and gives both recoveries: restore `.flow` from version control, or run the route-change command with a reason. Changing a flow definition after a spec started never makes that spec tampered.

**FR-17 — Changing a spec's route needs the explicit route-change command and a reason.**
- One command changes a spec's route. Its name is for the ADR; `/spec:gear` is the analogue.
- It takes the target flow and a reason. It does not run without a reason. If none is given, it asks, as `/spec:gear` does (`gear.md:124-128`).
- It stops if the target flow has no definition, if that definition is invalid (FR-3), or if the spec is already on the target flow with a slip that is not tampered.
- On success it copies the target flow's slip into `.flow` and updates the creation record. It also records the date, the previous flow, the new flow and the reason in a tracked file in the spec directory. Earlier route changes are kept.
- After a route change the spec resolves exactly like any other spec on the new flow with the same markers. Markers for steps not on the new slip become stray markers (FR-21). The command deletes no marker and no artefact.
- It works on a legacy spec, which gains a `.flow` and a creation record, and on a tampered spec.
- Like the resolver (FR-6), it can be pointed at a flow-definition directory other than the shipped one, so that tests can change a spec's route to a fixture flow such as `light`.
- Example: changing `specs/0041-foo/` from `feature` to a flow without `requirements` while `.requirements-approved` exists leaves that file in place. Status then reports it as a stray marker.

**FR-18 — `/spec:new` always starts a `feature` spec. There is no generic flow switch.**
- `/spec:new {name}` creates the spec directory, `specs/.current-spec` and `README.md` as today (`new.md:15-22`). It also copies the `feature` slip into `.flow` and writes a creation record.
- `/spec:requirements` no longer creates a spec directory. With no usable `specs/.current-spec` it stops as FR-20 states, instead of creating one as `requirements.md:30-33` does today. `/spec:new` is the only starter for `feature`.
- No command takes a `--flow` option, or anything like one, to pick a flow at creation. Other flows start through their own starter command (FR-19).
- If the `feature` definition is missing or invalid, `/spec:new` stops before creating anything and names the fault.

**FR-19 — A well-formed starter command meets a fixed contract.**
A starter command for flow `{name}` (for example spec 0039's `/modules:suggest`) is well formed when it:
- stops before creating anything if `{name}`'s definition is missing or invalid (FR-3);
- creates the spec directory with the same numbering and `specs/.current-spec` update as `/spec:new`;
- copies `{name}`'s slip into `.flow` and writes a creation record, so that the resolver reports flow `{name}`, valid and not tampered;
- uses marker-writing commands that write the slip's markers with a provenance line (FR-13) for any step it or its sibling commands approve;
- produces artefacts whose kinds and filenames match the slip. For example, a step that writes `requirements.md` declares it, so that `/spec:tasks` applies FR-10;
- ensures every gate on its slip meets FR-27's run contract;
- ensures that its flow's own producing and marker-writing commands take routing decisions from the resolver and stop on an invalid or tampered slip, as route-following commands do (FR-7);
- adds an approval-table entry for every flow-specific kind its slip uses (FR-29), even when the entry is empty, and ensures that any command of its own that approves a step runs that step's approval sub-flow before writing the marker;
- ships resolver fixture tests (FR-24) that cover its slip at each step's approved and unapproved state.
A starter command adds no edits to `/spec:*` command files. `.claude/commands/spec/README.md` carries this contract as a checklist, one yes/no item per clause, each naming how it is checked (a fixture test or a statement in the command's text) (FR-26).

**FR-20 — A missing or stale `specs/.current-spec` is reported, not guessed.**
The resolver tells apart a `specs/.current-spec` that is missing, one that is empty, and one that is stale (names a directory that does not exist under `specs/`). Every route-following command, including `/spec:requirements`, that finds one of these states stops, names the state, and names the recovery: `/spec:switch` to choose an existing spec, or `/spec:new` to start one. It never falls back to some other spec and never creates one. `/spec:status` still lists every spec and reports the state of `.current-spec`. `/spec:gear`, `/spec:show-me` and `/spec:write_release_notes` keep today's handling of these states (`gear.md:105`, `show-me.md:51-56`, `write_release_notes.md:51`). This is a deliberate change from today for `/spec:requirements` (see *Behaves as today*); a stale `.current-spec` left by a switched branch is common, and creating a spec in that state hides the problem.

**FR-21 — Stray markers and out-of-order approvals are reported, and their effect is fixed.**
- A stray marker never satisfies a precondition and never blocks one. Status reports it by name (FR-15). An ignored marker is not reported at all. Example: `specs/0028-box-provisioning-rdd-role-interfaces/.code-approved` is ignored, so status for 0028 is as today.
- With an out-of-order approval, each step's approved state is reported as it is. The next step is the first unapproved step (FR-11). Each producing command's precondition looks only at the step immediately before it (FR-8). Implementation stays blocked until every step is approved (FR-9). Status names the earlier unapproved step and the later approved one.
- Example (feature): `.design-approved` exists and `.requirements-approved` does not. The next step is `/spec:review requirements` then `/spec:approve requirements` (if `requirements.md` is present). `/spec:tasks`'s precondition holds, as it does today. `/spec:implement` is refused.

**FR-22 — The spec's copy of the slip is authoritative.**
A spec with a `.flow` follows its copy, even if the flow definition has since changed or been deleted. If the copy's flow name has no definition in the flow-definition directory, the resolver reports this for information and goes on following the copy. If the copy is invalid under FR-3, for example because it names a command that has been removed since, the spec is invalid and FR-7's stop applies.

**FR-23 — `/spec:show-me` reports a route without a requirements step explicitly.**
- `show_me_facts.cs` records in the ledger whether the spec's slip has a `requirements`-kind step, as it resolves the route. The ADR decides whether it calls the resolver or shares its logic.
- When the slip has no `requirements`-kind step:
  - `## Did it ship what it said?` writes a line saying that the spec's route (naming the flow) has no requirements step, so there is nothing to reconcile, in place of row 8's "No requirements.md found…" line (`show-me.md:559-560`);
  - `## Inputs used` row 1 reads `not available: not on this spec's route` (`show-me.md:689-691`);
  - F5's level is unchanged from row 8 (`Medium`, `show-me.md:447`), with a measured-value cell naming the route rather than a missing file.
- `declared_total` is never reported as `0` for such a spec. A real `0` keeps row 9's meaning: `requirements.md` exists and declares no ids.
- For a spec whose slip has a `requirements`-kind step, including every legacy spec, the ledger and the output are unchanged.
- The existing `show_me_facts_tests.cs` rows keep passing, with no assertions edited.

**FR-24 — Route resolution has fixture tests that run without a test framework.**
- A sibling test script, in the style of `show_me_facts_tests.cs` (`README.md:737-747`), runs the resolver against tracked fixture spec directories. It needs no test framework, project, or files under `src/` or `tests/`, and runs as a .NET file-based app from the repository root. It prints a pass count or one `FAIL` line per failed assertion, and exits non-zero on any failure.
- Fixtures cover at least:
  - a legacy `feature` spec at each stage (nothing, requirements approved, design approved, all approved), including empty and free-text markers;
  - a `feature` spec with a `.flow` and creation record;
  - the `light` cases of FR-25;
  - the `bugfix` cases of FR-28, and a slip with a flow-specific kind other than `diagnosis`;
  - each error path: unknown flow name in `.flow` (FR-22), a step naming a nonexistent command, zero steps, duplicate step names, a last step that is not `tasks`, a stray marker, an ignored marker (`.code-approved`), a step that produces and approves itself with no gate, a step placed after the hand-off, a kind with no approval-table entry, a table with two entries for one kind, an out-of-order approval, each gate state of FR-27, each tamper case (a), (b) and (c), a CRLF-only difference (not tampered), and a spec directory whose name contains a space (as `specs/0021-Expose Unacceptable Message Window/` does).
- The script also checks that the resolver, run against every existing directory under `specs/` that has no `.flow`, reports flow `feature`, valid, not tampered, no stray markers, and the next step FR-11 computes on the `feature` slip. For the existing specs whose state is unusual, the expected next steps are:
  - `specs/0036-universal-transport-conformance-tests/` (all three markers, no `tasks.md`): the hand-off, `/spec:implement`, because every step is approved;
  - `specs/0031-test_naming_conventions/` (all three markers, no `.adr-list`): the hand-off, `/spec:implement`;
  - `specs/0028-box-provisioning-rdd-role-interfaces/` (all three markers and an ignored `.code-approved`): the hand-off, `/spec:implement`, with no stray marker reported.
- Fixture names and layout are for the ADR to decide.

**FR-25 — The `light` flow proves a new flow needs no command edits.**
`light` is the slip in FR-2's example. It lives only in a test-fixture flow-definition directory, and its fixture tests assert the following:
- status: flow `light`; steps `design`, `tasks`; next step `/spec:design` when nothing exists;
- design: `/spec:design`'s precondition holds with no `.requirements-approved`; `/spec:requirements` and `/spec:approve requirements` are reported not on the route;
- approve: after `design` is approved, the next step is `/spec:tasks`;
- tasks: `/spec:tasks`'s precondition holds, and the slip reports no `requirements`-kind step;
- review: auto-detect with `.adr-list` present and unapproved selects `design`, and the slip reports no `requirements`-kind step for the design cross-reference;
- implement/ralph: preconditions hold with `.design-approved` and `.tasks-approved` only, and fail naming `tasks` when `.tasks-approved` is absent;
- show-me: `show_me_facts.cs` on a finished `light` fixture records the no-requirements-step state of FR-23.
Adding `light` takes the fixture definition and fixtures only. No file under `.claude/commands/` changes.

**FR-26 — The workflow documentation describes flows.**
`.claude/commands/spec/README.md` describes:
- flows, flow definitions and the `.flow` file;
- the `feature` flow as the default;
- the legacy rule (FR-5);
- the route-change command;
- the starter-command contract (FR-19), as a checklist;
- intent, stray and ignored markers;
- provenance lines;
- known and flow-specific artefact kinds, and gates (FR-27);
- the approval table and approval sub-flows, including how a flow adds an entry (FR-29);
- how to run the resolver tests.
The File Structure block (`README.md:812-822`) shows `.flow` and the creation record. The Workflow diagram is labelled as the `feature` flow. The requirement lists at `README.md:403-405` and `README.md:609-612` describe slip-based preconditions.

**FR-27 — A gate approves one step and produces the next, and the slip says so.**
- A slip expresses a gate by naming the same command as one step's marker-writing command and the next step's producing command. It may also name that command as the next step's marker-writing command. No other declaration is needed, and the resolver reports the gate (FR-6).
- A gate may not span more than two consecutive steps, and the producing step must come immediately after the step the gate approves. A slip that names one command as marker-writer of step N and producer of any step other than N+1 is invalid (FR-3).
- The rest of this FR is the **run contract** a gate command must meet. This spec ships no gate: the contract binds the flow that owns one (FR-19), such as spec 0039's `/modules:evaluate` or a future migrated `/bugfix:confirm`. The resolver supports it by reporting each state below (FR-6).
- In one run a gate first does the approval: it runs step N's approval sub-flow (FR-29), then writes step N's marker with a provenance line (FR-13). Only then does it check the precondition to produce step N+1 (FR-8), which now holds, and produce step N+1's artefact(s). If it is also step N+1's marker-writing command, it then shows the user step N+1's artefact(s) and **asks** a second time whether to approve them. On approval it runs step N+1's approval sub-flow and writes that marker with its own provenance line. If the user declines the second question, the gate stops with step N approved and step N+1's artefact present but unapproved, which is the second interrupted state below.
- If the user declines the first question (approving step N), the gate writes nothing: no marker for step N, no artefact for step N+1.
- If a run stops after step N's marker and before step N+1's artefact, the next step is the gate again (FR-11). Re-running it skips the approval it already made and produces step N+1.
- If a run stops after step N+1's artefact is written but before its marker (only possible when the gate is also N+1's marker-writer), the next step is `/spec:review {kind}` for a known kind, then the gate (FR-11). Re-running the gate in that state **asks** the user whether to approve the existing artefact, regenerate it, or cancel. It never regenerates or approves without asking.
- Example (bugfix, FR-28): `/bugfix:confirm` approves `diagnosis` (`.confirm-approved`), writes `tasks.md`, and approves `tasks` (`.tasks-approved`). Example (spec 0039): `/modules:evaluate` approves its evaluation step and produces `requirements.md`.
- `/spec:approve {step}` on a step whose marker-writing command is a gate stops and names the gate (FR-12).

**FR-28 — The `bugfix` flow tests the model against a route with a flow-specific kind and a gate.**
`bugfix` is the existing `/bugfix:*` workflow restated as a slip. It lives only in the test-fixture flow-definition directory, beside `light`. Its slip is:

| Step | Kind | Producing command | Artefact(s) | Marker | Marker-writing command |
| --- | --- | --- | --- | --- | --- |
| `diagnosis` | `diagnosis` | `/bugfix:triage` | `bugfix.md` | `.confirm-approved` | `/bugfix:confirm` |
| `tasks` | `tasks` | `/bugfix:confirm` | `tasks.md` | `.tasks-approved` | `/bugfix:confirm` |

Its hand-off is `/spec:implement`.
Its fixture tests assert:
- the slip is valid: `/bugfix:triage` and `/bugfix:confirm` resolve to existing command definitions, and `diagnosis` is a flow-specific kind;
- status, with nothing present: flow `bugfix`; steps `diagnosis`, `tasks`; next step `/bugfix:triage`;
- with `bugfix.md` present and nothing approved: next step `/bugfix:confirm`, with no `/spec:review` (FR-11); `/spec:review` with no phase names `diagnosis` and `/bugfix:confirm` (FR-14);
- the gate is reported: `/bugfix:confirm` is marker-writer of `diagnosis` and producer and marker-writer of `tasks` (FR-27);
- with `.confirm-approved` and no `tasks.md`: next step `/bugfix:confirm` (FR-27's interrupted run);
- with `.confirm-approved` and `tasks.md` but no `.tasks-approved`: next step `/spec:review tasks` then `/bugfix:confirm` (FR-27's second interrupted run, where the gate asks);
- `/spec:requirements` and `/spec:design` are reported not on the route; `/spec:tasks` is reported as having a different producer, and `/spec:approve tasks` a different marker-writing command, both naming `/bugfix:confirm`;
- the slip reports no `requirements` or `design` step, so `/spec:tasks`-style FR coverage, ADR `Accepted` checks and ADR reads do not apply (FR-9, FR-10);
- implement/ralph: preconditions hold with `.confirm-approved` and `.tasks-approved`, and fail naming `diagnosis` when `.confirm-approved` is absent;
- show-me: `show_me_facts.cs` on a finished `bugfix` fixture records the no-requirements-step state of FR-23.
Adding `bugfix` takes the fixture definition and fixtures only. No file under `.claude/commands/` changes.
The fixture's approval table holds an empty entry for `diagnosis`. `/bugfix:confirm`'s own checks, proving the diagnosis, happen before it approves, so they are not part of the sub-flow.
The fixture does not migrate the `/bugfix:*` workflow. These differences from today's workflow are recorded in `.claude/commands/spec/README.md` (FR-26) as what a later migration would have to decide, not as defects of the slip model:
- **Location.** Today a bug's state lives in `bugfixes/{NNNN-slug}/` with `bugfixes/.current-bug`; the fixture places it in a spec directory under the resolver's one root.
- **Gating.** Today `/bugfix:test` is always gated and never reads the gear (`CLAUDE.md`). Through `/spec:implement`, a bug fix's TEST tasks would follow the gear, so they could run in `review-after`.
- **Commands.** `/bugfix:test` and `/bugfix:fix` would be replaced by `/spec:implement` running the TEST and FIX tasks; `/bugfix:verify` by the VERIFY task.
- **Gate output.** Today `/bugfix:confirm` writes no `tasks.md`; the fixture's slip assumes a confirm that does. A migrated confirm would write one TEST task per confirmed defect (including any extra defects in the diagnosis's scope notes), a FIX task, and a VERIFY task that runs the suite and records the root cause and `Fixes #N` for the commit and PR. Verify becomes a task because the slip ends at the hand-off (FR-3).

**FR-29 — Approving a step runs its kind's approval sub-flow, taken from one table.**
- The shipped approval table has one entry for each of the three known kinds:

  | Kind | Approval sub-flow |
  | --- | --- |
  | `requirements` | empty |
  | `design` | accept each ADR being approved (all of `.adr-list`, or the one named, as in `/spec:approve design 0043`); retire any ADR it supersedes; regenerate `docs/adr/index.md` |
  | `tasks` | empty |

  `tasks` is empty because "the content of an approved `tasks.md` is frozen" is a rule the commands that read `tasks.md` enforce (`approve.md:91-93`), not an action taken at approval.
- The `design` sub-flow is the behaviour of `approve.md:34-58` (ADR status flips, supersession, index regeneration) moved into commands of its own, owned by the ADR command family. The presence check (`approve.md:32`), the marker write (`approve.md:59`) and the report to the user (`approve.md:60-61`) stay with the approver. The commands are, for example extending `.claude/commands/adr/write_adr_metadata.md` and adding commands for supersession and index regeneration. The move is a structural refactoring: a `feature` spec's design approval has the same outcome as today (AC-24).
- Every approver runs the sub-flow: `/spec:approve`, a gate (FR-27), and any flow's own marker-writing command. No approver performs kind-specific approval behaviour that is not in the table.
- Each sub-flow command receives the spec directory, the step, and the artefact(s) being approved. Which arguments each command takes is for the ADR.
- Every sub-flow command is idempotent: running it again on the same input changes nothing further. Example: accepting an ADR that is already `Accepted` leaves it unchanged, and regenerating the index rebuilds the same file.
- The step's marker is written only after the whole sub-flow completes. If a sub-flow command fails, the approver stops, names the failed command, and writes no marker. Re-running the approval re-runs the whole sub-flow from its first command; there is no per-command progress record.
- The table is data, not command text, so a flow that adds a flow-specific kind adds an entry and edits no `/spec:*` command file (FR-25, FR-28). Its entry may be empty, but it must exist (FR-3).
- Example: the fixture flow-definition directory's approval table holds an empty `diagnosis` entry for `bugfix`; the shipped table is not edited. A fixture slip with a step of kind `spike` and no `spike` entry is invalid.

### Non-functional Requirements

- **NFR-1 — Determinism.** The resolver's report depends only on the spec directory, `specs/.current-spec`, the flow-definition directory (including its approval table) and the command-definition directory. It needs no network access and no `gh`. Running it twice on unchanged inputs gives byte-identical output.

- **NFR-2 — Readable without a new parser.** Flow definitions, the approval table, spec slips, creation records, route-change records and provenance lines are plain text. A person can review them in an IDE or on GitHub. They can be read with tools the commands already use: the shell tools in their `allowed-tools`, or a .NET file-based app like `show_me_facts.cs`. No new language runtime, package or parsing library is added.

- **NFR-3 — Read-only resolution.** The resolver creates, modifies and deletes no file. Only starter commands, the route-change command and marker-writing commands write routing state.

- **NFR-4 — Responsiveness.** Once the resolver has been built, resolving every spec directory under `specs/` for one `/spec:status` run finishes in under 10 seconds on a developer machine.

- **NFR-5 — Tracked routing state.** `.flow`, the creation record and the route-change record are tracked by git, unlike `.current-gear` (`.gitignore:357-361`). A route is a project decision that travels with the spec, not working state.

- **NFR-6 — Backward compatibility.** Every existing spec behaves as today (FR-4, FR-5). The existing `show_me_facts_tests.cs` passes unchanged (FR-23).

### Constraints and Assumptions

- Slash commands are markdown instructions an agent executes, so a command's routing can be tested only through the resolver it consults. FR-7's text check and FR-24's fixture tests together stand in for running each command.
- Tamper detection catches accidental or casual hand edits. It is not a security boundary. Anyone who can edit both `.flow` and its creation record can get past it, as anyone can edit `.current-gear`. Deleting both `.flow` and the creation record makes a spec look like a legacy `feature` spec, and this is accepted.
- A legacy spec follows the `feature` definition current at resolution time. So any later change to the `feature` definition must keep FR-24's all-existing-specs check passing.
- The sha in a provenance line identifies the commit the approver was on. It does not prove that the approved artefact had been committed.
- Marker-writing commands keep today's preconditions (FR-8). That is how out-of-order approvals can arise, and FR-21 fixes their effect.
- Today `/spec:ralph-implement` does not check ADR status (`ralph-implement.md:152-155`), although `README.md:612` says it does. This spec keeps the command's behaviour (FR-9) and corrects the README (FR-26).
- `/spec:write_release_notes` already treats `requirements.md` as optional (`write_release_notes.md:82-86`, `write_release_notes.md:154-164`) and prints no route next step. It needs no routing change. On a route with neither a `requirements`-kind nor a `design`-kind step (such as `bugfix`) it stops with today's message (`write_release_notes.md:84-87`), and this is an accepted limitation. `/spec:gear` does not depend on any approval marker (`gear.md:105`, `gear.md:123`) and is unchanged.
- The ADR decides the file format, the flow-definition location, the creation-record mechanism, the route-change command's name and arguments, the resolver's packaging, the approval table's location and format, the names and arguments of the commands the `design` sub-flow is extracted into, and exact message wording. Two kinds of string are fixed here and are not the ADR's to choose: `/spec:show-me`'s output lines in FR-23, which are part of show-me's output contract; and the names of the three `.current-spec` states in FR-20 (missing, empty, stale), though not the sentences around them.
- The flow-definition directory and the `feature` definition are present in every checkout. A missing `feature` definition is a tooling fault that commands report (FR-18), not a case to work around.

### Out of Scope

- The `/modules:*` commands and the `modules` slip (spec 0039, #4659). This spec provides only the contract they must meet (FR-19).
- Shipping `light`, `bugfix`, or any flow other than `feature`, as a supported flow.
- Migrating the `/bugfix:*` workflow onto a slip, changing any `/bugfix:*` command, moving `bugfixes/` or `.current-bug`, or changing `/bugfix:test`'s always-gated guarantee. FR-28 records what such a migration would have to decide.
- Any change to the review gear (`.current-gear`), its meaning, or `/spec:gear`.
- Any change to what happens after `tasks.md`, beyond `/spec:implement` and `/spec:ralph-implement` reading their preconditions from the slip (FR-9) and their reads of other artefacts (FR-10).
- Migrating existing specs: adding `.flow`, creation records or provenance lines to them.
- Changing ADR conventions, the ADR template beyond FR-10's Parent Requirement fallback, or the outcome of `/spec:approve design`'s ADR status handling. Its location moves into the `design` approval sub-flow (FR-29); what it does to ADRs and the index does not change.
- Changing `/spec:show-me`'s risk model (ADR 0073), including F5's forced level.
- A generic `--flow` option on any command.
- Permission allow-list entries in `.claude/settings.json` for the resolver or its test script. A permission prompt on first use is accepted.
- Adding the new resolver or its tests to CI. Like `show_me_facts_tests.cs`, they are run by hand.

## Acceptance Criteria

**AC-1 (FR-1, FR-25, FR-28)** Given the merged change, when the flow-definition directory is listed, then it holds a `feature` definition and no `light` or `bugfix` definition.

**AC-2 (FR-2, FR-4)** Given the `feature` definition, when it is read in an IDE, then it shows the flow name, the three steps of FR-4's table in order with every column's value, and the hand-off `/spec:implement`.

**AC-3 (FR-3)** Given fixture slips each breaking one rule (zero steps, duplicate step name, duplicate marker, nonexistent command `/spec:designx`, producing command used twice, last step not `tasks`, hand-off not `/spec:implement`, name differing from filename stem), when the resolver runs on each, then each is reported invalid, naming that rule and the offending step.

**AC-4 (FR-4, FR-5, FR-24, NFR-6)** Given every directory under `specs/` with no `.flow`, when the resolver test script runs, then each resolves to flow `feature` from the default source, valid and not tampered, with no stray marker, and with the next step FR-11 computes on the `feature` slip, including the three unusual specs FR-24 lists.

**AC-5 (FR-5)** Given a legacy fixture with an empty `.requirements-approved` and a free-text `.design-approved`, when it is resolved, then both steps are reported approved.

**AC-6 (FR-5)** Given the change is merged, when `git diff` is taken against the merge base for paths under existing spec directories (all but `specs/0040-routing-slips/`), then it is empty.

**AC-7 (FR-6, NFR-1)** Given any fixture, when the resolver runs twice, then the two outputs are byte-identical, and the fixture directory's contents are unchanged (NFR-3).

**AC-8 (FR-6)** Given the `light` fixture with `.design-approved` only, when the precondition for `/spec:implement` is queried, then it is reported failed, naming step `tasks` and `/spec:approve tasks`.

**AC-9 (FR-7)** Given the merged change, when `grep -l -e '.requirements-approved' -e '.design-approved'` runs over `.claude/commands/spec/*.md` (excluding `README.md`) and `.claude/commands/adr/adr.md`, then no file matches.

**AC-10 (FR-7, FR-3)** Given a spec whose `.flow` names `/spec:designx`, when `/spec:design` is run, then it stops before launching any sub-agent and shows the resolver's invalid-slip reason.

**AC-11 (FR-8)** Given a `feature` spec with `requirements.md` and no `.requirements-approved`, when `/spec:design` is run, then it stops and names step `requirements` and `/spec:approve requirements`, as `design.md:35-37` does today.

**AC-12 (FR-8, FR-25)** Given the `light` fixture with nothing approved, when `/spec:design`'s precondition is queried, then it holds. When the slip is asked which step has kind `requirements`, then none is reported, so `/spec:requirements` stops saying it is not on flow `light`'s route and names the resolver's next step, `/spec:design`.

**AC-13 (FR-8, FR-21)** Given a `feature` spec with `tasks.md` and no `.design-approved`, when `/spec:approve tasks` is run, then it writes `.tasks-approved` (no new precondition), and status then reports an out-of-order approval.

**AC-14 (FR-9)** Given a `feature` spec with all three markers and one ADR `Proposed`, when `/spec:implement` is run, then it refuses, as `implement.md:115-118` does today.

**AC-15 (FR-9, FR-25)** Given the `light` fixture with `.design-approved` and `.tasks-approved` and no `.requirements-approved`, when the `/spec:implement` and `/spec:ralph-implement` preconditions are queried, then both hold.

**AC-16 (FR-10, FR-25)** Given a `light` spec with approved ADRs and no `requirements.md`, when `/spec:tasks` is run, then it does not stop for a missing `requirements.md`, and its coverage cross-reference covers ADR decisions with no FR section.

**AC-17 (FR-10)** Given a `light` spec with no `requirements.md`, when `/spec:design` drafts an ADR, then the ADR's Parent Requirement cites the linked issue or spec `README.md`, and no FR ids are claimed.

**AC-18 (FR-10, FR-14, FR-25)** Given a `light` spec with an unapproved `.adr-list`, when `/spec:review` is run with no phase, then it reviews `design`, does not require `requirements.md`, leaves out the *Connection to Requirements* criteria, and states the route has no requirements step.

**AC-19 (FR-10)** Given a `feature` spec, when `/spec:tasks`, `/spec:review design` or `/spec:ralph-implement` runs, then it reads `requirements.md` exactly as today.

**AC-20 (FR-11)** Given a `feature` spec with `.requirements-approved` and no `.adr-list`, when `/spec:status` runs, then the next action names `/spec:design`.

**AC-21 (FR-11, FR-25)** Given a `light` spec, when `/spec:approve design` completes, then the next step it prints is `/spec:tasks`. Given every step approved, status prints the hand-off `/spec:implement`.

**AC-22 (FR-11)** Given a `feature` spec with `requirements.md` and no `.requirements-approved`, when the next step is resolved, then it is `/spec:review requirements` followed by `/spec:approve requirements`.

**AC-23 (FR-12)** Given a `light` spec, when `/spec:approve requirements` is run, then it stops, names flow `light`, lists `design` and `tasks`, and writes no file.

**AC-24 (FR-12, FR-29)** Given a `feature` spec, when `/spec:approve design 0043` is run, then ADR 0043 is flipped to `Accepted` with `write_adr_metadata`, the index is regenerated, and `.design-approved` is written, as today.

**AC-25 (FR-13)** Given HEAD at sha `S`, when `/spec:approve tasks` runs, then `.tasks-approved` gains one line naming `tasks`, `tasks.md` and `S`, and `test -f` on it succeeds.

**AC-26 (FR-13)** Given a marker with existing free text, when the step is approved again, then the original text is preserved and one provenance line is added.

**AC-27 (FR-13)** Given a directory where `git rev-parse HEAD` fails, when a step is approved, then the marker is written and its provenance line records the sha as unavailable.

**AC-28 (FR-14)** Given a `light` spec, when `/spec:review requirements` is run, then it stops, saying the phase is not on flow `light`'s route. `/spec:review code` is accepted.

**AC-29 (FR-15)** Given a legacy spec, when `/spec:status` runs, then the spec is shown as `feature` with the default source, and its steps match today's Requirements, Design and Tasks information.

**AC-30 (FR-15, FR-21)** Given a `light` spec with a stray `.requirements-approved`, when `/spec:status` runs, then it names the stray marker. The `design` precondition and next step are the same as without it.

**AC-31 (FR-16)** Given a spec with `.flow` and a creation record, when one character of a step's command in `.flow` is changed, then the resolver reports tampered case (a), and `/spec:tasks` stops, naming both recoveries.

**AC-32 (FR-16)** Given the same spec with `.flow` converted only from LF to CRLF, when it is resolved, then it is not tampered.

**AC-33 (FR-16)** Given a creation record with `.flow` deleted (case b), or a `.flow` with no creation record (case c), when each is resolved, then each is reported tampered. A legacy spec is not.

**AC-34 (FR-16, FR-22)** Given a spec started on `feature`, when the `feature` definition is later edited, then the spec is not tampered and follows its own `.flow`.

**AC-35 (FR-17)** Given a `feature` spec, when the route-change command is run without a reason, then no file changes until a reason is given.

**AC-36 (FR-17)** Given a `feature` spec with `.requirements-approved`, when the route-change command, pointed at the fixture flow-definition directory, changes the route to `light` with reason `R`, then `.flow` holds the new slip, the slip is not tampered, a tracked record holds the date, both flow names and `R`, `.requirements-approved` still exists and is reported stray, and status shows the change and `R`.

**AC-37 (FR-17)** Given an unknown target flow, or a spec already on the target flow and not tampered, when the route-change command is run, then it stops and changes no file.

**AC-38 (FR-18)** Given `/spec:new foo`, when it completes, then `specs/NNNN-foo/` holds `README.md`, a `.flow` equal to the `feature` slip, and a creation record, and the resolver reports `feature` from `.flow`, valid, not tampered.

**AC-39 (FR-18, FR-20)** Given no `specs/.current-spec`, when `/spec:requirements 123` is run, then it stops, naming state missing and recoveries `/spec:switch` and `/spec:new`, and creates no spec directory.

**AC-40 (FR-18)** Given the `/spec:*` command files, when searched for `--flow`, then no command accepts it.

**AC-41 (FR-19, FR-26, FR-27)** Given the updated `README.md`, when its starter-command checklist is read, then it has one yes/no item for each FR-19 clause, including the FR-27 run contract, and each item names whether it is checked by a resolver fixture test or by a statement in the relevant flow command's text.

**AC-42 (FR-20)** Given `specs/.current-spec` missing, empty, or naming `0099-gone`, when `/spec:design` runs, then it stops, naming the state (missing, empty, or stale together with the directory `0099-gone`) and the recoveries `/spec:switch` and `/spec:new`. `/spec:status` still lists all specs. The same holds for `/spec:requirements`.

**AC-43 (FR-21)** Given a `feature` fixture with `.design-approved`, `requirements.md` and no `.requirements-approved`, when resolved, then the next step is `/spec:review requirements` then `/spec:approve requirements`, `/spec:tasks`'s precondition holds, `/spec:implement`'s fails, and an out-of-order approval is reported.

**AC-44 (FR-22)** Given a spec whose `.flow` names flow `retired`, which has no definition, when resolved, then the copy is followed and a missing-definition notice is reported.

**AC-45 (FR-23, FR-25)** Given a finished `light` fixture with no `requirements.md`, when `show_me_facts.cs` runs, then the ledger records that the route has no requirements step and `declared_total` is not `0`. `/spec:show-me`'s reconciliation line names the route rather than "No requirements.md found", and *Inputs used* row 1 reads `not available: not on this spec's route`.

**AC-46 (FR-23, NFR-6)** Given the merged change, when `dotnet run .claude/commands/spec/show_me_facts_tests.cs` runs, then every pre-existing row passes and none of its assertions were edited.

**AC-47 (FR-24)** Given the resolver test script, when run from the repository root with `dotnet run`, then it exercises every fixture class FR-24 lists, prints a pass count, and exits 0. A deliberately broken expectation produces a `FAIL` line and a non-zero exit.

**AC-48 (FR-25, FR-7)** Given the `light` fixture definition and fixtures added, when the diff is taken over `.claude/commands/`, then it is empty for that addition, and every AC that cites FR-25 or names the `light` fixture passes.

**AC-49 (FR-26)** Given the updated `README.md`, when read, then it covers each topic FR-26 lists, and the File Structure block shows `.flow`.

**AC-50 (NFR-2)** Given the merged change, when the dependencies are checked, then no new package, runtime or parsing library has been added, and each routing file opens as plain text.

**AC-52 (NFR-4)** Given a built resolver, when `/spec:status` resolves all specs under `specs/`, then resolution finishes in under 10 seconds.

**AC-53 (NFR-5)** Given a spec started by `/spec:new`, when `git status` runs, then `.flow`, the creation record and any route-change record show as untracked-but-not-ignored (stageable) files.

**AC-54 (FR-3)** Given fixture slips that each break one kind rule (a step with no kind; two steps of kind `requirements`; a `tasks`-kind step whose artefact is not `tasks.md`; one command named as marker-writer of step 1 and producer of step 3), when the resolver runs on each, then each is reported invalid, naming the rule and the step.

**AC-55 (FR-3, FR-10, FR-11, FR-29)** Given a fixture slip with a step of flow-specific kind `evaluation` followed by a `tasks` step, a fixture approval table with an empty `evaluation` entry, and the `evaluation` artefact present and unapproved, when resolved, then the slip is valid, the step's kind is reported as `evaluation`, the next step is its marker-writing command with no `/spec:review`, and no known-kind step is reported for `requirements` or `design`.

**AC-56 (FR-27, FR-28)** Given the `bugfix` fixture, when resolved, then `/bugfix:confirm` is reported as a gate that approves `diagnosis` and produces and approves `tasks`.

**AC-57 (FR-27, FR-28)** Given the `bugfix` fixture with `bugfix.md` present and no marker, when the precondition to produce step `tasks` is queried before and after `.confirm-approved` is added, then it fails naming `diagnosis` before, and holds after.

**AC-58 (FR-27, FR-11)** Given the `bugfix` fixture with `.confirm-approved` present and no `tasks.md`, when resolved, then the next step is `/bugfix:confirm`.

**AC-59 (FR-27, FR-12)** Given the `bugfix` fixture with `tasks.md` present, when `/spec:approve tasks`'s membership is queried, then it is reported as having a different marker-writing command, naming `/bugfix:confirm`.

**AC-60 (FR-28, FR-11, FR-14)** Given the `bugfix` fixture with `bugfix.md` present and nothing approved, when resolved, then the next step is `/bugfix:confirm` with no `/spec:review`, and `/spec:review` with no phase names step `diagnosis` and `/bugfix:confirm` rather than reviewing a phase.

**AC-61 (FR-28, FR-9, FR-10)** Given the `bugfix` fixture with `.confirm-approved` and `.tasks-approved`, when the `/spec:implement` and `/spec:ralph-implement` preconditions are queried, then both hold and no `requirements` or `design` step is reported. With `.confirm-approved` removed, both fail naming `diagnosis`.

**AC-62 (FR-28, FR-8)** Given the `bugfix` fixture, when the slip is asked which step has kind `requirements`, `design` and `tasks`, then none is reported for `requirements` or `design` (so `/spec:requirements` and `/spec:design` are not on the route), and for `tasks` step `tasks` is reported with producer `/bugfix:confirm` (so `/spec:tasks` stops naming `/bugfix:confirm`).

**AC-63 (FR-28, FR-23)** Given a finished `bugfix` fixture, when `show_me_facts.cs` runs, then the ledger records that the route has no requirements step.

**AC-64 (FR-28, FR-7)** Given the `bugfix` fixture definition and fixtures added, when the diff is taken over `.claude/commands/`, then it is empty for that addition, and no file under `.claude/commands/bugfix/` or `bugfixes/` has changed.

**AC-66 (FR-9)** Given a `feature` fixture with only `.requirements-approved`, when the `/spec:implement` precondition is queried, then it fails, naming `design` then `tasks` in that order.

**AC-67 (FR-12)** Given a `light` spec, when `/spec:approve` is run with no argument or with `foo`, then it lists `design` and `tasks` as the approvable steps and writes no file.

**AC-68 (FR-14)** Given a `light` spec, when `/spec:review` completes, then its closing Spec Status block lists steps `design` and `tasks`, with no Requirements line.

**AC-69 (FR-17)** Given a target flow whose definition is invalid under FR-3, when the route-change command is run, then it stops and changes no file. Given a legacy spec, or a tampered spec, when it is run with a valid target and a reason, then afterwards the spec has a `.flow` and creation record and is reported valid and not tampered.

**AC-70 (FR-18)** Given the `feature` definition removed from, or made invalid in, the flow-definition directory, when `/spec:new foo` is run, then it stops, names the fault, and creates no directory.

**AC-71 (FR-22, FR-7)** Given a spec whose `.flow` was valid at creation but names a command definition that has since been deleted, when resolved, then the slip is reported invalid naming that command, and `/spec:tasks` stops showing that reason.

**AC-72 (FR-21)** Given `specs/0028-box-provisioning-rdd-role-interfaces/`, when resolved, then no stray marker is reported and `.code-approved` affects no precondition or next step.

**AC-73 (FR-3)** Given a fixture slip whose first step names one command as both its producing and its marker-writing command, with no gate, when resolved, then it is reported invalid.

**AC-74 (FR-27, FR-28, FR-11)** Given the `bugfix` fixture with `.confirm-approved` and `tasks.md` but no `.tasks-approved`, when resolved, then the next step is `/spec:review tasks` followed by `/bugfix:confirm`, and the resolver reports that step `tasks`'s artefact is present and its gate has already approved `diagnosis`.

**AC-75 (FR-3, FR-29)** Given a fixture slip with a step of kind `spike` and a fixture approval table with no `spike` entry, when resolved, then the slip is reported invalid, naming the step and the missing entry. Given the same table with an empty `spike` entry, the slip is valid.

**AC-76 (FR-29, FR-6)** Given the shipped approval table, when the resolver reports a `feature` spec's steps, then `requirements` and `tasks` have empty sub-flows and `design` has steps that accept the ADRs, then retire superseded ones, then regenerate the index, in that order.

**AC-77 (FR-29, FR-12)** Given a `feature` spec whose `design` sub-flow fails at its second command, when `/spec:approve design` runs, then it stops naming that command and `.design-approved` is not written. When the failure is removed and it is run again, then the sub-flow runs from its first command, the ADRs end `Accepted` with frontmatter and body status agreeing and no duplicated entries, and `.design-approved` is written with one provenance line.

**AC-78 (FR-29)** Given a `feature` spec with two `Proposed` ADRs in `.adr-list`, one superseding an `Accepted` ADR, when `/spec:approve design` runs after the extraction, then both ADRs are `Accepted`, the superseded ADR is `Superseded` with its back-link, and `docs/adr/index.md` matches a fresh regeneration — the same outcome as `approve.md:34-58` today.

**AC-79 (FR-29, FR-28)** Given the `bugfix` fixture, when resolved, then step `diagnosis` reports an empty approval sub-flow and the slip is valid.

**AC-80 (FR-29, FR-7)** Given the merged change, when `.claude/commands/spec/approve.md` is read, then it contains no ADR status, supersession or index-regeneration instructions; those appear only in the `design` sub-flow's commands.

**AC-65 (FR-26, FR-28)** Given the updated `README.md`, when read, then it records FR-28's four migration differences (location, gating, commands, gate output) as decisions for a later migration.

## Additional Context

- Spec 0039's `/modules:*` flow reaches `tasks.md` through `suggest → evaluate → ✋ gate (writes requirements.md + .requirements-approved) → design → tasks`. Its first adversarial review found that `/spec:status` ("Create ADRs using /spec:design", `status.md:71`) and `/spec:approve design` ("the next step is /spec:tasks", `approve.md:64`) route a modules spec through the wrong commands. Keying FR-10 on artefact kind rather than step name lets a modules `gate` step that writes `requirements.md` feed `/spec:tasks`'s FR coverage without a step called `requirements`.
- Current state the requirements had to respect:
  - several existing markers hold free text rather than being empty (`specs/0031-test_naming_conventions/.design-approved`, `specs/0027-replay-matching-outbox-events-when-inbox-has-already-seen/.requirements-approved`, `specs/0036-scoped-lifetime-per-pipeline/.requirements-approved`);
  - `specs/0036-universal-transport-conformance-tests/` has `.tasks-approved` but no `tasks.md`;
  - `specs/0031-test_naming_conventions/` has no `.adr-list`;
  - one spec directory name contains spaces;
  - `specs/0028-box-provisioning-rdd-role-interfaces/` holds a `.code-approved` written in the construction phase, which the resolver ignores (FR-21).
  "Behaves as today" means these keep producing today's decisions, including the same failure where a command needs a file that is absent.
- **Why `bugfix` as a second experiment.** `light` only drops a step, which the original three-kind model already handled. `bugfix` stresses the model where `light` does not: a step whose artefact (`bugfix.md`) is none of the known kinds, and a gate (`/bugfix:confirm`) that approves the diagnosis and produces `tasks.md`. Spec 0039 needs both: its `evaluation.md` is a flow-specific kind, and its gate produces `requirements.md`. Settling them here, against a workflow that already exists, means 0039 does not have to reopen the model. Whether to actually migrate `/bugfix:*` onto a slip is a separate decision, chiefly because it would put a bug fix's TEST tasks under the gear (FR-28).
- The issue's open questions are settled here as follows: the format is left to the ADR (NFR-2); a spec may change its slip only through the route-change command (FR-17); markers gain a provenance line (FR-13).
