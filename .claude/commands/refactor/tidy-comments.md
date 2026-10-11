---
allowed-tools: Read, Edit, Glob, Grep, Bash, AskUserQuestion, TodoWrite
description: Bring comments into line with Brighter's comment rules, and surface refactorings a comment side-stepped
argument-hint: [base-ref | base..head | path...]
---

# Tidy Comments - Replace Comments With Intent

You are bringing the comments in a unit of work into line with Brighter's rules, and finding the
places where a comment was written **instead of** a refactoring.

This is a structural change. Nothing you do here may change behaviour.

## Scope

$ARGUMENTS

## The Rules

The source of truth is [documentation.md § Comments in Code](../../../.agent_instructions/documentation.md#comments-in-code).
Read it before you start. In short:

| Where | What is allowed |
|-------|-----------------|
| Public and protected members of public types | `///` XML documentation. `<summary>`, `<param>`, `<returns>` etc. say how to use the API; `<remarks>` holds notes to the user. |
| Internal or private types and members | No XML documentation. At most a `//` or `/* */` comment immediately before the signature, when the method is complex or guards against a common trap. |
| Inside a method body | Nothing, as a rule — except `//Arrange` `//Act` `//Assert` in tests, the reason on an analyser suppression, and the rare one-line warning about a trap that cannot be extracted. |

And for every comment: no narration of what the code does, no history of the change, no notes to a
reviewer, no issue, PR or spec references. Commented-out code is deleted.

## Phase 1: Establish the Change Under Review

Resolve the scope from the arguments:

| Arguments | Scope |
|-----------|-------|
| none | The current branch against its merge-base with `master`, plus uncommitted changes: `git diff $(git merge-base HEAD master)` |
| a git ref (e.g. `HEAD~3`) | `git diff <ref>` |
| a range `<base>..<head>` | `git diff <base>..<head>` — work already committed or merged |
| one or more paths | The changes to those paths on the current branch; if they have no changes, **every** comment in those files |

Then:

1. List the changed `*.cs` files with `git diff --stat` and show them to the user.
2. **Exclude** generated tests (see `.agent_instructions/generated_tests.md`) — fix the template
   instead, if the template is in scope — and `*.Designer.cs`, migrations, and non-code files.
3. With a diff scope, tidy only the comments **this diff added or changed**. Untouched comments are
   out of scope; churning them hides the real change in review. Read each file in full anyway — you
   need the surrounding method to decide whether a comment can become an extraction.
4. Check the build is green and the relevant tests pass. If the work is still moving or the tests
   fail, say so and stop: the extractions below need a green baseline.

If the scope is empty, say so and stop.

## Phase 2: Classify Every Comment

Find each comment in scope (`//`, `/* */`, `///`) and give it **one** action. Read the code each
comment sits beside before deciding: the action depends on what the code would need to say for
itself.

| Action | When | What you do |
|--------|------|-------------|
| **Keep** | It conforms to the rules | Nothing |
| **Delete** | It narrates or restates the code; describes how the code used to be; is a note to a reviewer whose substance is already in the ADR or commit; or is commented-out code | Remove it |
| **Strip reference** | Its content is fine but it cites an issue, PR or spec (`#4207`, `Spec 0027`) | Remove the reference, keep the rest. An ADR reference that explains the design is fine. |
| **Trim** | It is right in kind but a paragraph where a line would do | Cut it to what a maintainer of the code as it now stands needs |
| **Move to signature** | A genuine "what is this for" or "beware this trap" comment inside the body of a method that already does one thing | Move it above the signature — `//` for a non-public member, into `<remarks>` for a public or protected one |
| **Convert XML** | `///` on an internal or private type or member | Replace with a `//` comment above the signature if it says something the name does not; otherwise delete |
| **Extract** | The comment labels a block, or explains an expression, a literal or a variable | Let the code say it. **Extract Method** for a block (the comment becomes the method name); **Extract Variable** or **Decompose Conditional** for a condition; a **named constant** for a magic literal; **Rename** for a variable or parameter. Keep a comment above the new method's signature only if the name cannot carry it |
| **Rationale to ADR** | Reviewer-facing rationale (why this approach, alternatives rejected) that is **not** already in an ADR | Do not silently delete it. List it for the user: add it to the relevant ADR, or drop it |
| **Side-stepped refactoring** | The comment papers over a design problem that a local extraction does not fix — see below | Leave the code alone. Report it and schedule it in Phase 5 |

### Spotting a side-stepped refactoring

These are the most valuable findings. A comment often marks the spot where someone noticed a problem
and wrote it down instead of fixing it:

| The comment says… | The refactoring it avoided |
|-------------------|----------------------------|
| "must be called before / after X", "do not reorder" | Temporal coupling. Make the order impossible to get wrong: combine the steps, or return a type that only exposes the next valid step |
| "can be null when…", "only set if…" | The type does not tell the truth. Nullable annotation, Introduce Special Case, or Extract Class for a Temporary Field |
| "x is in seconds", "this string is a topic name" | Primitive Obsession. Replace Primitive with Object |
| "the caller must ensure…", "assumes…" | Introduce Assertion or a guard clause — **a behaviour change** if it adds a throw |
| "same as in Foo", "keep in sync with…" | Duplicated Code or Shotgun Surgery. Extract Function, Move Function |
| "step 1 / step 2 / step 3" across a long method | Long Function. Split Phase, Extract Function |
| "hack", "workaround", "TODO", "for now" | Unfinished work. Ask whether it is still true; if it is, it is a task, not a comment |
| "this is public so tests can reach it" | Insider Trading — Brighter's visibility rule (`testing.md`, `design_principles.md`) |

Name the refactoring from Fowler's catalogue, as `/refactor:smells` does. If the refactoring would
change behaviour (a new guard that throws, a changed type a caller depends on) or change a `public` or
`protected` signature, say so: it belongs in `/test-first` or needs a decision, not a tidy.

## Phase 3: Present the Plan

Show the plan grouped by file, then a count by action:

```
## Comment tidy for <scope>

### src/Paramore.Brighter/Foo.cs
- L42-46 **Delete** — narrates the history of the fix (substance is in ADR 0057)
- L71    **Strip reference** — "(Spec 0027, #2541)"
- L88-90 **Extract** — "validate the header" labels L91-104 → `private bool HasValidHeader(Message)`
- L130   **Side-stepped refactoring** — "must call Connect() first": temporal coupling

Delete 6 · Strip 3 · Trim 2 · Move 1 · Convert 2 · Extract 3 · Keep 4
Rationale to ADR: 1 · Side-stepped refactorings: 2
```

For every **Keep** of a comment inside a method body, say which exception it falls under. For every
**Rationale to ADR**, quote the text and name the ADR you would add it to.

Then ask the user, with `AskUserQuestion`, whether to apply the plan as shown, apply it with
changes they name, or stop. Also ask whether to commit as you go or leave the commits to them.

## Phase 4: Apply

Two passes, so a reviewer can read each one quickly:

1. **Comment-only pass** — Delete, Strip reference, Trim, Move to signature, Convert XML.
   - Build (`dotnet build` on the affected projects). A comment-only change cannot change behaviour,
     but a broken `/* */` or a malformed `///` can break the build or add warnings.
   - Commit, if the user chose to: `refactor: tidy comments in <area>`
2. **Extraction pass** — Extract actions only.
   - Extracted members are `private` (or `private static`). Do not change any public or protected
     signature.
   - Run the tests that cover each changed file. They must stay green without being edited; if a test
     has to change, the extraction changed behaviour — revert it and report it as a side-stepped
     refactoring instead.
   - Commit, if the user chose to: `refactor: extract methods to express intent in <area>`

If the user approved adding rationale to an ADR, make that edit as its own `docs:` commit.

Show the user the diff stat for each pass.

## Phase 5: Schedule the Side-Stepped Refactorings

Report each side-stepped refactoring in the `/refactor:smells` format — location, evidence (the
comment and the code it excuses), the Fowler refactoring, kind (`structural`, `behaviour change`, or
`public API change`) and size. Then offer, with `AskUserQuestion`:

1. **Run with `/tidy-first`** — for structural ones. Give the exact command. Do not run it yourself.
2. **Run with `/test-first`** — for behaviour changes.
3. **Add to the spec's `tasks.md`** — when a spec is active. Follow `/refactor:smells` Phase 4 for
   the task format; show the text before editing; do not commit.
4. **Record only**, or **Skip**.

Removing the comment over a side-stepped refactoring is the refactoring's job, not this one's: leave
the comment until the refactoring makes it unnecessary.

## Rules

- **No behaviour changes.** If a tidy needs one, it is not a tidy.
- **No public or protected signature changes.**
- **Only the change under review** with a diff scope. Whole files only when the user names paths.
- **Do not lose information silently.** Rationale not in an ADR goes to the user, not the bin.
- **Do not add comments** beyond moving or condensing ones that were already there; and do not
  write XML documentation for undocumented public members. If public members lack it, mention it in
  the report as a documentation gap for the user to schedule.
- **Generated tests are fixed in their template**, never in the generated file.
- Leave the `#region Licence` block and `// <auto-generated>` markers alone.

## Related

- [documentation.md § Comments in Code](../../../.agent_instructions/documentation.md#comments-in-code) — the rules
- [`/refactor:smells`](smells.md) — the broader smell review; its **Comments** smell is this command's subject
- [`/tidy-first`](tidy-first.md) — carries out the side-stepped refactorings this command schedules
- [`/test-first`](../tdd/test-first.md) — for any that need a behaviour change
