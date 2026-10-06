---
allowed-tools: Bash(git diff:*), Bash(git log:*), Bash(git status:*), Bash(git rev-parse:*), Bash(git merge-base:*), Bash(git show:*), Bash(cat:*), Bash(ls:*), Bash(test:*), Read, Glob, Grep, Edit, AskUserQuestion, TodoWrite
description: Review a completed unit of work for Fowler's code smells and schedule the refactorings
argument-hint: [base-ref | path...]
---

# Code Smells - Review and Schedule Refactorings

You are reviewing a **completed** unit of work for the code smells catalogued in Martin Fowler's
*Refactoring* (2nd ed., ch. 3), and helping the user schedule the refactorings that remove them.

This command is **opt-in** and **reports and schedules only**. You do not change production or test
code here. The refactorings themselves are carried out later, through `/tidy-first` or as
`STRUCTURAL` tasks in a spec's `tasks.md`.

## Scope

$ARGUMENTS

## When To Run

After a unit of work is complete — a task, a section of a spec's `tasks.md`, or a branch — and its
tests are green. In the per-task sequence it comes last:

1. GREEN
2. `/tidy-first`, if needed
3. `/tdd:coverage` (planned)
4. `/refactor:smells` — optional

If the working tree has failing tests or a half-finished change, say so and stop: smells in code that
is still moving are noise.

## Phase 1: Establish the Change Under Review

Smells are looked for in **the code this unit of work added or changed**, not across the whole
codebase.

Resolve the scope from the arguments:

| Arguments | Scope |
|-----------|-------|
| none | The current branch against its merge-base with `master`, plus any uncommitted changes: `git diff $(git merge-base HEAD master)` |
| a git ref (e.g. `HEAD~3`, a commit SHA, a branch) | `git diff <ref>` — the changes since that ref |
| one or more paths | The changes to those paths on the current branch; if they have no changes, the files themselves |

Then:

1. List the changed files with `git diff --stat` for the resolved scope and show the list to the user.
2. **Exclude** from review:
   - generated files (see `.agent_instructions/generated_tests.md`) — a smell there is fixed in the
     template, so report it against the template instead if the template is in scope
   - `*.Designer.cs`, migrations, `Directory.Packages.props`, project files, and non-code files
3. Read each remaining changed file **in full**, not just the hunks. Many smells (Large Class,
   Divergent Change, Feature Envy) only show against the surrounding class.
4. Read [design_principles.md](../../../.agent_instructions/design_principles.md) and
   [code_style.md](../../../.agent_instructions/code_style.md). Brighter's conventions decide some
   cases (see *Brighter Calibration* below).

If the scope is empty, say so and stop.

## Phase 2: Look for Smells

Work through the catalogue. For each smell you report you **must** have concrete evidence: the file,
the line range, and the code that shows it. "This class feels large" is not a finding.

| Smell | What to look for | Usual refactorings |
|-------|------------------|--------------------|
| **Mysterious Name** | A name that does not say what the thing does or is for | Change Function Declaration, Rename Variable, Rename Field |
| **Duplicated Code** | The same knowledge expressed in more than one place, including in the new code and the code it sits beside | Extract Function, Slide Statements, Pull Up Method |
| **Long Function** | A method doing several things; more than one level of indentation; comments marking sections | Extract Function, Replace Temp with Query, Introduce Parameter Object, Decompose Conditional |
| **Long Parameter List** | Many parameters, flag arguments, parameters that travel together | Introduce Parameter Object, Preserve Whole Object, Remove Flag Argument, Combine Functions into Class |
| **Global Data** | Static mutable state, ambient singletons reachable from anywhere | Encapsulate Variable |
| **Mutable Data** | State changed in place where a value or a returned copy would do; settable properties nothing needs to set | Encapsulate Variable, Split Variable, Separate Query from Modifier, Remove Setting Method, Change Reference to Value |
| **Divergent Change** | One class changed for several unrelated reasons by this unit of work | Split Phase, Move Function, Extract Class |
| **Shotgun Surgery** | One conceptual change that had to touch many classes (visible across the diff) | Move Function, Move Field, Combine Functions into Class, Inline Class |
| **Feature Envy** | A method more interested in another object's data than its own | Move Function, Extract Function |
| **Data Clumps** | The same group of fields or parameters appearing together repeatedly | Extract Class, Introduce Parameter Object, Preserve Whole Object |
| **Primitive Obsession** | `string`, `int`, `bool`, `TimeSpan` etc. standing in for a domain concept | Replace Primitive with Object, Replace Type Code with Subclasses |
| **Repeated Switches** | The same `switch` / `if`-chain on a type or kind in more than one place | Replace Conditional with Polymorphism |
| **Loops** | A loop that a LINQ pipeline would express more clearly | Replace Loop with Pipeline |
| **Lazy Element** | A class, method or interface that no longer earns its place | Inline Function, Inline Class, Collapse Hierarchy |
| **Speculative Generality** | Parameters, hooks, abstract classes or branches nothing uses yet | Collapse Hierarchy, Inline Function, Inline Class, Change Function Declaration, Remove Dead Code |
| **Temporary Field** | A field only set or meaningful in some circumstances | Extract Class, Move Function, Introduce Special Case |
| **Message Chains** | `a.B().C().D()` navigation the caller should not depend on | Hide Delegate, Extract Function, Move Function |
| **Middle Man** | A class whose methods mostly just delegate | Remove Middle Man, Inline Function, Replace Superclass with Delegate |
| **Insider Trading** | Classes that know too much about each other's internals | Move Function, Move Field, Hide Delegate, Replace Subclass with Delegate |
| **Large Class** | Too many fields, too much code, or a role that cannot be said in one phrase | Extract Class, Extract Superclass, Replace Type Code with Subclasses |
| **Alternative Classes with Different Interfaces** | Interchangeable classes whose method signatures differ | Change Function Declaration, Move Function, Extract Superclass |
| **Data Class** | A class with fields and nothing else, whose behaviour lives in its callers | Encapsulate Record, Remove Setting Method, Move Function, Extract Function |
| **Refused Bequest** | A subclass that ignores or overrides away most of what it inherits | Push Down Method, Push Down Field, Replace Subclass with Delegate, Replace Superclass with Delegate |
| **Comments** | A comment explaining *what* code does because the code does not say it | Extract Function, Change Function Declaration, Introduce Assertion |

## Brighter Calibration

Fowler's catalogue is a set of prompts for judgement, not rules. Brighter's own principles settle some
cases. **Do not report** these as smells:

- **An `IAmA*` interface with one implementation on a public type** — the design principles provide
  interfaces for user override and for TDD. That is not Speculative Generality.
- **Message, command and event types (`IRequest`, `Message`, DTOs, configuration records)** — these
  carry data across a boundary by design. Report Data Class only where behaviour that belongs to the
  type lives in its callers.
- **A `public` type that is public so tests can reach it** — that is Brighter's visibility rule, not
  Insider Trading. (An `InternalsVisibleTo` added to reach across packages *is* a finding: the design
  principles forbid it.)
- **Primitives used for serialisation or interoperability**, which the design principles allow.
- **XML documentation comments** on public members — required by
  [documentation.md](../../../.agent_instructions/documentation.md). The Comments smell is about
  comments inside method bodies that narrate the code.
- **Sync/async pairs** (`Send`/`SendAsync`, `IAmA*`/`IAmA*Async`) with parallel bodies — the duplication
  is deliberate across the sync and async pipelines. Report it only where shared logic could be
  extracted without crossing that line.

**Do report**, as Brighter-specific evidence of the smells above:

- A class whose role cannot be stated in one phrase, or whose collaborators cannot be named
  (Large Class, Divergent Change — the *single role principle*).
- Primitive obsession as the design principles define it.
- More than one level of indentation in a method (Long Function — `code_style.md`).
- Visibility widened only so a test can reach a member (Insider Trading; see `testing.md`).

## Phase 3: Report

Present the findings, grouped by file, ranked within the report by how much each one costs the next
person to change this code:

```
## Code smells in <scope>

### 1. Feature Envy — `src/Paramore.Brighter/Foo.cs:42-61`
**Evidence**: `Foo.Calculate` reads five properties of `Bar` and none of its own.
**Refactoring**: Move Function — move `Calculate` to `Bar`.
**Kind**: structural | public API change
**Size**: small | medium | large
```

For each finding:

- **Kind** — `structural` if the refactoring leaves every public signature unchanged, so `/tidy-first`
  can carry it with all tests green. `public API change` if it renames, removes, moves or changes the
  signature of a `public` or `protected` member: that is a breaking change for Brighter's users, so it
  needs a decision (and possibly an ADR via `/adr`), not just a tidy.
- **Size** — a rough guide for scheduling: *small* is one method or a rename; *medium* is one class;
  *large* crosses several classes or packages.

If you are not confident a finding is a smell — it could be justified by context you cannot see — put
it in a separate **Worth a look** list at the end, with the question that would settle it. Do not pad
the main list.

If you find nothing, say so plainly. A clean result is a valid result.

## Phase 4: Schedule

Ask the user, with `AskUserQuestion`, which findings to schedule and how. Offer:

1. **Run now with `/tidy-first`** — for small and medium structural findings. Give the exact command
   for each, e.g. `/tidy-first move Calculate from Foo to Bar to remove Feature Envy`. Do not run it
   yourself; the user starts it so each refactoring gets its own `refactor:` commit.
2. **Add to the spec's `tasks.md`** — when a spec is active (`specs/.current-spec` exists and its
   `tasks.md` exists). Append a new phase after the last phase:

   ```markdown
   ## Phase N: Refactoring

   *Scheduled by `/refactor:smells` against <scope> on <date>.*

   - [ ] **M. STRUCTURAL: <refactoring> to remove <smell> in <type>**
     - Smell: <smell> — `<file>:<lines>`
     - Refactoring: <Fowler refactoring>
     - Run with: `/tidy-first <description>`
   ```

   Number tasks on from the last task in the file. Follow the file's existing heading and label
   conventions (`## Phase N: <name>`, the `STRUCTURAL` label). A `public API change` finding is not a
   `STRUCTURAL` task: add it as a `DOC` task to decide the change (and write an ADR if one is needed)
   before any code moves. Show the user the exact text before editing `tasks.md`, then edit it. Do not
   commit — the user commits the tick as `docs:`.
3. **Record only** — leave the report in the conversation and change nothing.
4. **Skip** individual findings the user disagrees with. Do not argue them again.

If no spec is active, offer only options 1, 3 and 4.

## Rules

- **Never edit production or test code** in this command. Its only possible write is the `tasks.md`
  edit in Phase 4, and only after the user approves the text.
- **Only review the change under review.** A smell in untouched code is out of scope, unless this
  unit of work made it worse — then say how.
- **Evidence or nothing.** Every finding names a file, a line range and the code that shows it.
- **Name the refactoring from Fowler's catalogue**, so the follow-up work has a clear, known shape.
- **No behaviour changes disguised as refactorings.** If removing a smell needs a behaviour change,
  say so; it belongs in `/test-first`, not `/tidy-first`.

## Related

- [`/tidy-first`](tidy-first.md) — carries out the structural refactorings this command schedules
- [`/test-first`](../tdd/test-first.md) — for any finding that turns out to need a behaviour change
- [`/adr`](../adr/adr.md) — for a `public API change` finding that needs a recorded decision
- [Design Principles](../../../.agent_instructions/design_principles.md) and
  [Code Style](../../../.agent_instructions/code_style.md) — Brighter's calibration of the catalogue
