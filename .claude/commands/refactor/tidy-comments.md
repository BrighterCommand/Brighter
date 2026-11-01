---
allowed-tools: Read, Edit, Glob, Grep, Bash, AskUserQuestion, TodoWrite
description: Bring comments into line with Brighter's comment rules, and surface refactorings a comment side-stepped
argument-hint: [base-ref | base..head] [path...]
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
| a ref or range, then paths (e.g. `abc123^1..abc123 src/Paramore.Brighter.Foo`) | The ref or range, limited to those paths. This is how a slice is named (see step 3 below) |

Then:

1. List the changed `*.cs` files with `git diff --stat` and show them to the user.
2. **Exclude** generated tests (see `.agent_instructions/generated_tests.md`) — fix the template
   instead, if the template is in scope — and `*.Designer.cs`, migrations, and non-code files.
3. **Size the scope before reading it.** Count the comment lines the scope added, split into `//`
   or `/* */` lines and `///` lines, per project:
   `dotnet run .claude/commands/refactor/comment_census.cs -- <range> [path...] [--liquid]`.
   It prints one row per project (`//` lines excluding bare AAA markers, `///` lines, files, and lines
   carrying a spec, PR, issue or `File.cs:N` reference), skipping licence regions, `Generated/` and
   `*.Designer.cs`; `--liquid` adds the generator's templates.
   If the scope is more than one project, or more than about 150 `//` lines, a single plan is too big
   to review. Propose slices with `AskUserQuestion`:
   - one project per slice, `src/` before `tests/`;
   - start with the project the others depend on (a shared base package before its backends), so
     the first slice calibrates the decisions the rest repeat;
   - name each slice as a command — `/refactor:tidy-comments <ref-or-range> <project-path>`.

   Work only the first slice in this run. Record the remaining slices in `PROMPT.md` (see *Tracking
   Slices* below).
4. **If the scope is a range or ref in the past, the files may have moved on.** Check with
   `git log --oneline <head>..HEAD -- <path>`. You tidy the files as they are now, not as they were at
   `<head>`:
   - A comment the range added that has since been deleted is gone; skip it.
   - A comment the range added that has since been edited is still in scope.
   - A comment added **after** the range, in a file the range **created**, is marked **†** in the
     plan and included: tidying a file once is cheaper than coming back to it. In a file the range
     only modified, a later comment stays out of scope by the rule in step 5.
   - **Files renamed since the range** are still in scope under their current names. Map them with
     `git diff --name-status -M <head> HEAD -- <path>`. A file the range created that has since been
     deleted is gone.
   - **Files added after the range** stay out of scope, even when their names match the slice's
     filter.
5. With a diff scope, tidy only the comments **this diff added or changed**. Untouched comments are
   out of scope; churning them hides the real change in review. Read each file in full anyway — you
   need the surrounding method to decide whether a comment can become an extraction.
6. Check the build is green and the relevant tests pass, **before you edit anything**, and note the
   test count. If the work is still moving or the tests fail, say so and stop: the extractions below
   need a green baseline. For tests that need a container, bring it up first (`/test-infra:run-tests`);
   check the run actually executed tests, since a framework filter the project does not target runs
   nothing and still reports success.

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
| **Rationale to ADR** | Reviewer-facing rationale (why this approach, alternatives rejected) that is **not** already in an ADR | Do not silently delete it. List it in the plan with the ADR and section it will go into; adding it is the default, and the user may drop it instead |
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
`protected` signature, say so: it belongs in `/tdd:test-first` or needs a decision, not a tidy.

## Settled Decisions

These cases came up again and again in earlier tidies and have been decided. Apply them in the plan
without raising them as questions; raise a case only when it does not fit.

### Production code

- **Issue links in XML docs** ("tracked at …/issues/4140") are stripped, even where they point users at
  planned work.
- **ADR section references that do not exist** are corrected when the right section is clear, otherwise
  stripped, keeping the rest of the sentence. Check the section, not just the ADR number.
- **Commit SHAs that give the provenance of a historical literal** (a legacy DDL string, say) are kept;
  `PR #…` beside them is stripped.
- **Data is not comment.** Leave `SourceReference`-style values in catalogs, sentinel strings, exception
  messages and assert messages, even when they carry spec or PR labels.
- **A trailing comment on a `_ = param;` discard** is deleted; the `<param>` docs or class remarks
  already say it. A discard that exists only to carry a comment (the value is used elsewhere) goes with
  it.
- **A `//` comment above a protected override** becomes `/// <inheritdoc />` plus `<remarks>`.
- **Long public remarks** that repeat another type's remarks, or cite `File.cs:line`, are trimmed to the
  point a user needs.
- **A comment justifying a `!`** goes above the one private method that does the `!`. When the same `!`
  and justification appear more than once, extract that method.
- **An invalid `cref`** (a URL, a type in another assembly) becomes plain text or `<c>…</c>`.
- **Rationale living in a catch block** goes to the ADR; the catch keeps at most one trap line, above the
  method signature.

### Rationale and ADRs

- **Rationale not in an ADR** is added to the ADR as its own `docs:` commit, before the comment is
  deleted. Rationale that lives only in a production signature comment still goes to the ADR before a
  test's copy of it is deleted.
- **Verify every claimed gap** against the ADRs before writing. Search every ADR on the subject, including
  later ones (a multi-tenancy ADR may already say what a migrations ADR does not). Many claimed gaps are
  already covered.
- **The ADR can be the thing that is wrong.** When a deleted comment was right and the ADR disagrees with
  the code, fix the ADR in the `docs:` commit. When the comment was wrong, check the code before writing
  anything down.

### Extractions

- Extracted members are `private`; tests pass unedited. Before moving code out of a type, check for tests
  that reflect over it (`GetMethods`, `BindingFlags`).
- **Don't add a comment above an extracted local;** let its name carry it.
- **When an extraction merges two guards**, keep the exception's `ParamName` and message as they were:
  use the name that can actually surface (the first one called).
- **Never re-indent the inside of a verbatim SQL string** when moving it into a helper. Check the diff
  shows no SQL text changed.
- **Magic error numbers** in `catch … when` filters become named `private const int`s; use a provider
  constant (`PostgresErrorCodes.*`) where one exists.
- **A comment over a run of guard clauses** becomes one `private static void ThrowIf…` per guard, keeping
  any trap as that method's signature comment.
- **A literal derived from other constants** (a length budget, say) is computed from them, as a
  `private static readonly` when a `.Length` is involved.

### Tests

- **`///` on a public test class:** keep it as XML, cut to the behaviour pinned and why. Strip spec, PR,
  item, phase and slice references, "pre-fix" or "previously" history, repo file paths and `§… line N`;
  keep the ADR number and section.
- **A `//` block above a test class, or a narrative inside its braces above the fields:** delete the
  history. Any remaining why goes to the ADR; the block becomes a one- or two-line `/// <summary>`. A class
  with no XML doc whose narrative is deleted gets a one- to three-line `/// <summary>`.
- **AAA markers carrying text** (`//Assert — …`) become bare; a reason not carried by the test name or
  class summary moves into the class summary. `//Arrange + Act` and `//Act + Assert` stay.
- **Comments between assertions,** and "discriminator" comments in object initialisers, are deleted.
- **`///` on private nested doubles and `internal` test doubles or helpers:** a one-line `//` above the
  type only when it names a trap (for example "post-Begin hooks throw NotSupportedException, so reaching
  one surfaces as the wrong exception"); otherwise delete it.
- **A trailing comment on `[InlineData]`** keeps a short label and drops `File.cs:N` and review
  references.
- **Empty-catch markers** (`// Best-effort cleanup`, `/* best-effort */`) are kept.
- **Test extractions:** a magic literal becomes a `private const`; a magic duration a
  `private static readonly TimeSpan s_<name>`; a comment-labelled assert block (`// Verify table exists`
  over an inline command) a private helper returning the value; a trailing label on a literal
  (`"PartitionKey" // V4 column`) a `private const string V4Column`; a hard-coded latest version the
  project's `ExpectedMigrationVersions`-style constant; a comment over an Act block repeated across tests
  a shared `private static` helper; a comment justifying a local a better name for the local.

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
   - Check that the pass changed only comments:
     `dotnet run .claude/commands/refactor/comment_only_check.cs -- [git diff arguments]` (with none,
     the working tree against `HEAD`). It exits 0 when every changed line in a `.cs` or `.liquid` file
     is a comment or blank, allowing a changed trailing comment whose code is identical, and exits 2,
     printing each offending line, otherwise. Fix or explain every line it prints before committing.
   - Grep the changed files for references that survived (`Spec 0`, `PR #`, `#NNNN`, `Item X`,
     `File.cs:N`, `Phase N`).
   - Build (`dotnet build` on the affected projects). A comment-only change cannot change behaviour,
     but a broken `/* */` or a malformed `///` can break the build or add warnings.
   - Commit, if the user chose to: `refactor: tidy comments in <area>`
   - **For 30 or more files**, two agents doing this pass on disjoint file sets work well. Neither
     commits, extracts or edits an ADR. Each reports its changes, claimed ADR gaps and extraction
     candidates. Verify the gaps yourself, then run the comment-only check on the combined diff
     before committing.
2. **Extraction pass** — Extract actions only.
   - Extracted members are `private` (or `private static`). Do not change any public or protected
     signature.
   - Run the tests that cover each changed file. They must stay green without being edited; if a test
     has to change, the extraction changed behaviour — revert it and report it as a side-stepped
     refactoring instead.
   - Commit, if the user chose to: `refactor: extract methods to express intent in <area>`

If the user approved adding rationale to an ADR, make that edit as its own `docs:` commit, first, so
the commits read `docs:`, then `refactor: tidy comments in <area>`, then
`refactor: extract methods to express intent in <area>`.

Show the user the diff stat for each pass.

## Phase 5: Schedule the Side-Stepped Refactorings

Report each side-stepped refactoring in the `/refactor:smells` format — location, evidence (the
comment and the code it excuses), the Fowler refactoring, kind (`structural`, `behaviour change`, or
`public API change`) and size. Then offer, with `AskUserQuestion`:

1. **Run with `/refactor:tidy-first`** — for structural ones. Give the exact command. Do not run it yourself.
2. **Run with `/tdd:test-first`** — for behaviour changes.
3. **Add to the spec's `tasks.md`** — when a spec is active. Follow `/refactor:smells` Phase 4 for
   the task format; show the text before editing; do not commit.
4. **File a GitHub issue** — show the text before creating it.
5. **Record only**, or **Skip**.

If this run was one slice of a larger scope, update `PROMPT.md` (see *Tracking Slices*).

Removing the comment over a side-stepped refactoring is the refactoring's job, not this one's: leave
the comment until the refactoring makes it unnecessary.

## Tracking Slices

When a scope is sliced, a fresh session must be able to pick up the next slice. `PROMPT.md` in the
repo root (gitignored, local) is where that state lives; see CLAUDE.md § *Context Management*.

Keep a `## Comment tidy` section in `PROMPT.md` with:

- the branch the tidy commits land on;
- the scope as a whole, and any decisions made on the first slice that later slices should repeat
  and that *Settled Decisions* does not cover;
- a checklist of slices, each as its exact command, ticked with its commit SHAs when it lands;
- the issues or tasks filed in Phase 5, so they are not filed twice.

Update it at the end of each run. Show the user the section before writing it, the first time.

A decision made on one slice that *Settled Decisions* does not already cover goes into `PROMPT.md` so
the next slice repeats it. Once the scope is finished, propose adding the ones that will recur to
*Settled Decisions*.

Each slice gets its own branch and PR from `master`, and touches only its own files, so a slice can
start while the previous slice's PR is still open. Before pushing, if an earlier slice has merged,
merge `origin/master` into the slice branch and re-run its tests.

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
- [`/refactor:tidy-first`](tidy-first.md) — carries out the side-stepped refactorings this command schedules
- [`/tdd:test-first`](../tdd/test-first.md) — for any that need a behaviour change
