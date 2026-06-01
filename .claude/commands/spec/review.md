---
allowed-tools: Bash(cat:*), Bash(test:*), Bash(touch:*), Bash(ls:*), Bash(echo:*), Bash(grep:*), Bash(git diff:*), Bash(git log:*), Bash(git status:*), Bash(git rev-parse:*), Bash(git merge-base:*), Bash(git show:*), Read, Write, Glob, Grep, Agent, AskUserQuestion
description: Review current specification phase (requirements, design, tasks, or code)
argument-hint: [requirements|design [adr-number]|tasks|code [base-ref]] [threshold]
---

## Adversarial Specification Review

Current spec directory: specs/

**Workflow**: Issue → Requirements → ADR(s) → Tasks → Tests → Code → **Code Review**

**Philosophy**: The first draft is never good enough. This review is skeptical and adversarial — it assumes problems exist and looks for them. The goal is to force iteration towards quality.

**Phases reviewable here**: `requirements`, `design`, `tasks`, and `code`. The first three review specification documents. `code` reviews the actual implementation against the approved specs — diff vs. base branch, cross-referenced against requirements.md + ADRs + tasks.md.

## Your Task

### Step 1: Parse Arguments and Determine What to Review

Read `specs/.current-spec` to determine the active specification directory.

**Error handling**: If `.current-spec` does not exist, tell the user to run `/spec:new` first and stop. If the spec directory doesn't exist, tell the user and stop. If the document for the requested phase doesn't exist (e.g., no `requirements.md`), tell the user to run the appropriate creation command first (e.g., `/spec:requirements`) and stop. Do NOT launch the sub-agent with missing documents.

Parse $ARGUMENTS:
- Extract **phase**: first word — `requirements`, `design`, `tasks`, or `code`
- Extract **adr-number**: for `design` phase, a zero-padded 4-digit number (e.g., `0053`). **Precedence rule**: a 4-digit zero-padded number is ALWAYS an ADR number, never a threshold.
- Extract **base-ref**: for `code` phase, a token starting with `--base=` (e.g., `--base=origin/master`). Default base is `master`.
- Extract **threshold**: any other numeric value (default: **60**)
- If phase is empty: auto-detect. Check approval markers in order: `.requirements-approved`, `.design-approved`, `.tasks-approved`. The first missing marker is the phase to review. If all three are approved, default to `code` (review the implementation against the approved specs). If no branch is checked out or there is no diff vs. base, tell the user there is nothing to review and suggest `/spec:status`.

**Approved phase warning**: If the user explicitly requests review of an already-approved phase, note this in the sub-agent prompt and include a note in the output: "This phase is already approved. Findings are informational — consider whether any warrant re-opening the phase." (Not applicable to `code` — code is never "approved" by the spec system.)

Examples:
- `/spec:review` → auto-detect phase, threshold 60
- `/spec:review requirements` → review requirements, threshold 60
- `/spec:review requirements 70` → review requirements, threshold 70
- `/spec:review design 0053` → review ADR 0053, threshold 60
- `/spec:review design 0053 80` → review ADR 0053, threshold 80
- `/spec:review design 70` → review all ADRs, threshold 70 (not 4-digit, so it's a threshold)
- `/spec:review tasks 50` → review tasks, threshold 50
- `/spec:review code` → review branch diff vs. master, threshold 60
- `/spec:review code 70` → review branch diff vs. master, threshold 70
- `/spec:review code --base=origin/main 70` → diff vs. origin/main, threshold 70

### Step 2: Gather Documents for the Sub-Agent

Read ALL documents the sub-agent will need. The sub-agent gets a clean context — it only knows what you send it.

**For requirements review:**
- Read `specs/{current-spec}/requirements.md`
- Read `.issue-number` if it exists (for context)

**For design review:**
- Read `specs/{current-spec}/.adr-list` to find ADRs
- Read each ADR from `docs/adr/{filename}` (or just the specified one)
- Read `specs/{current-spec}/requirements.md` (for cross-referencing)

**For tasks review:**
- Read `specs/{current-spec}/tasks.md`
- Read `specs/{current-spec}/requirements.md` (for cross-referencing)
- Read `specs/{current-spec}/.adr-list` and each ADR (for cross-referencing)

**For code review:**
- Resolve `{base-ref}` (default `master`). Run `git rev-parse --verify {base-ref}` — if it fails, tell the user the base ref doesn't exist and stop.
- Run `git merge-base {base-ref} HEAD` to find the divergence point.
- Run `git diff --stat {base-ref}...HEAD` to get the summary of changed files.
- Run `git log --oneline {base-ref}..HEAD` to get the commit list.
- Run `git status --porcelain` to check for uncommitted changes (flag in findings if present).
- If the diff is empty, tell the user there is nothing to review vs. `{base-ref}` and stop.
- Read `specs/{current-spec}/requirements.md`, all ADRs from `.adr-list`, and `specs/{current-spec}/tasks.md`. These are the contracts the code must satisfy.
- Read `.agent_instructions/code_style.md`, `.agent_instructions/testing.md`, and `.agent_instructions/design_principles.md` if they exist — these encode project conventions the sub-agent must check against.
- The sub-agent itself will use `Bash(git diff:* / git show:*)`, `Read`, `Glob`, and `Grep` to pull specific file-level diffs and surrounding context. Do NOT attempt to inline the full diff in the sub-agent prompt — pass the file list, commit list, stats, and base ref, and let the sub-agent drill in where it needs to.

### Step 3: Launch Sub-Agent for Adversarial Review

**Verify scope with the user before launching (MAIN agent).** All user interaction stays in
the main agent — never the sub-agent. If the review scope is ambiguous (which phase to review,
which ADR for a design review, the threshold to apply), confirm it with the user via
`AskUserQuestion` before launching. Unlike the `Plan`-based planning commands, this sub-agent
is `general-purpose` and *could* prompt the user, so the prompt MUST explicitly forbid it (see
the IMPORTANT list below) — it should return findings as text and ask the user nothing.

Launch an Agent (subagent_type: "general-purpose", **model: "opus"**) with the prompt below.
Review is reasoning-heavy work, so it uses opus per the model policy (see
`.claude/commands/spec/README.md` → "Sub-agents & model policy").

**For requirements reviews**: Include the full text of the document in the sub-agent prompt. If the document is very large (you couldn't read it in one Read call), pass the file path instead and instruct the sub-agent to read it.

**For design and tasks reviews**: Tell the sub-agent the file paths to read — it should read the files itself. This handles large documents (ADRs can be very long) and allows the sub-agent to verify codebase references. Include the requirements.md text in the prompt for cross-referencing (or the file path if it's also large).

**Sub-agent tool access**: The sub-agent (general-purpose) inherits tool access. For design and tasks reviews it SHOULD use Read, Glob, and Grep to read documents and verify codebase references. For **code** reviews it SHOULD additionally use `Bash(git diff:* / git show:* / git log:*)` to pull file-level diffs on demand. The sub-agent should NOT write the findings file — it should return the findings as text. The main agent writes the file after validating the output (see Step 6).

**Code review sub-agent prompt must additionally include:**
- The resolved base ref, the commit list, and the `git diff --stat` output
- The full text of requirements.md, tasks.md, and each ADR (so the sub-agent can cross-reference without re-reading)
- The paths to `.agent_instructions/code_style.md`, `testing.md`, `design_principles.md` (sub-agent reads them directly)
- An explicit instruction to drill into at least the top-N changed files (by size) using `git diff {base}...HEAD -- <file>` and to cite specific file:line references in every finding
- A reminder that uncommitted changes reported by `git status --porcelain` should be surfaced as at least a Medium finding

**IMPORTANT**: The sub-agent prompt must include:
1. The review criteria for the relevant phase (from Step 4 below)
2. The full document text(s) or file paths to read
3. The cross-reference documents (when applicable)
4. The threshold value
5. The output format instructions (from Step 5 below)
6. An instruction to RETURN the findings as text output, NOT to write a file
7. An instruction to NOT ask the user any questions — review the supplied inputs and return
   findings; any clarification was handled by the main agent before launch

### Step 4: Phase-Specific Review Criteria

Include the relevant criteria block in the sub-agent prompt.

---

#### Requirements Review Criteria

You are a skeptical reviewer. Assume the requirements have problems — your job is to find them. Check every item below and report concrete, specific findings with evidence.

**Testability and Concreteness:**
- Does every functional requirement have at least one concrete example with specific inputs and expected outputs?
- Can each acceptance criterion be turned directly into a test assertion? If not, it's too vague.
- Are boundary conditions explicitly stated? (What happens at zero? At max? With empty input? With null?)
- Are error scenarios specified? (What happens when things go wrong?)

**Completeness:**
- Is there a clear problem statement with a user story?
- Are functional requirements listed and numbered?
- Are non-functional requirements specified (performance, security, compatibility)?
- Are constraints and assumptions documented?
- Is the out-of-scope section explicit about what is NOT included?
- Are there implicit requirements hiding in the prose that aren't captured as numbered FRs?

**Unambiguity:**
- Is terminology consistent throughout? Are key terms defined?
- Are there vague phrases like "should handle gracefully", "supports multiple", "works as expected", "appropriate", "etc."?
- Are quantities specified where they matter? ("supports multiple" — how many?)
- Could two developers read a requirement and implement it differently? If so, it's ambiguous.

**Boundedness:**
- Is the scope clearly bounded? Could someone add features and claim they're "in scope"?
- Does each FR have clear start/end conditions?
- Are there contradictions between sections?

**Acceptance Criteria Quality:**
- Does every FR map to at least one AC?
- Are ACs written in Given/When/Then or equivalent testable format?
- Are there FRs without corresponding ACs? (List them specifically.)

---

#### Design (ADR) Review Criteria

You are a skeptical reviewer. Assume the design has problems — your job is to find them.

**Grounding in Reality:**
- Do file path references actually exist in the codebase? USE Glob and Grep to verify. Report any references to files, classes, or namespaces that don't exist.
- Are class/type names accurate? Search the codebase to confirm.
- Are interface contracts specified with inputs, outputs, and error conditions?
- Does the design reference real existing patterns in the codebase, or does it invent new patterns without justification?

**Decision Quality:**
- Is the Context section specific about the architectural problem being solved? (Not just restating the requirements.)
- Does the Decision section explain WHY this approach was chosen, not just WHAT?
- Are consequences (both positive AND negative) documented honestly? Designs with only positive consequences are suspicious.
- Are alternatives listed with genuine rationale for rejection? (Not strawman alternatives.)

**Connection to Requirements:**
- Does the ADR reference specific requirements (FR-N) it addresses?
- Are there requirements that this ADR should address but doesn't mention?
- Does the design introduce scope beyond what the requirements ask for?

**Completeness:**
- Is the error handling strategy explicit?
- Are concurrency/threading concerns addressed where relevant?
- Are there "the system will handle this" hand-waves?
- Is the ADR focused on ONE architectural decision, or does it try to cover too much?

**Cross-Reference (when reviewing all ADRs):**
- Do the ADRs collectively cover all the requirements?
- Are there gaps — requirements that no ADR addresses?
- Are there contradictions between ADRs?

---

#### Tasks Review Criteria

You are a skeptical reviewer. Assume the task list has problems — your job is to find them.

**Independent Verifiability:**
- Does each task produce a verifiable result? (A passing test, a visible output, a file that exists.)
- Can each task be verified WITHOUT running the whole system?
- Are there tasks that can only be checked by "looking at the code"? (That's not verifiable.)

**Test-First Framing:**
- Does every behavioral task (not tidy/structural) follow the pattern: write test → get approval → implement → refactor?
- Does each TEST task specify a `/test-first` command?
- Are there implementation tasks without corresponding test tasks?
- Does each test target a single behavior? (Not "test the whole feature".)

**Ordering and Dependencies:**
- Are dependencies between tasks explicit?
- Could a developer do task N without completing task N-1? If not, is that dependency stated?
- Are structural/tidy tasks before behavioral tasks?
- Do tasks build incrementally — no big-bang integration step?

**Granularity:**
- Is each task small enough to complete in one agent session?
- Are there monolithic tasks like "set up project structure" or "implement the validator"?
- Could any task be broken into smaller, independently verifiable pieces?

**Coverage Cross-Reference:**
- Map each FR from requirements.md to tasks. Are there FRs with no corresponding task? LIST THEM.
- Map each ADR decision to tasks. Are there design decisions with no implementation task? LIST THEM.
- Are there tasks that don't trace back to any requirement or design decision? (Scope creep.)

---

#### Code Review Criteria

You are a skeptical, **adversarial** reviewer. Your job is to find problems, not to validate work that has already been done. Assume:

- The author convinced themselves the code is right — they are not a reliable narrator.
- Tests that pass locally may still be wrong (asserting the wrong thing, testing the stub, or passing vacuously).
- Commit messages and PROMPT.md describe intent, not outcome — verify against the actual diff.

Every finding MUST cite concrete evidence: a file path, a line range, a diff hunk, or a spec section. No vibes.

**Requirement & ADR Fidelity (highest priority):**
- For each FR-N in requirements.md, locate where it is implemented in the diff. If you cannot find it, that is a finding (High or Critical depending on FR importance).
- For each AC, locate a test that asserts it. If the AC is satisfied by a unit test where the spec implies integration, downgrade trust and flag it.
- For each ADR decision, locate the corresponding code. Flag any deviation where the code does something different from the ADR without a documented reason.
- Flag scope creep: changed files or new abstractions that don't trace back to any FR, AC, or ADR decision.
- Flag scope holes: tasks in `tasks.md` marked done but with no visible code.

**TDD Compliance (Brighter-specific):**
- For each behavioral change, confirm a test exists. Prefer finding the test commit BEFORE the implementation commit in `git log {base}..HEAD` — that's the TDD signature. Absence isn't proof of violation, but combined with tests committed *after* implementation, it's a finding.
- Test naming: `When_X_should_Y.cs` (Given/When/Then). Class name: `[Behavior]Tests`. Violations are Low–Medium unless pervasive.
- **Critical Brighter rule**: integration tests that touch a database MUST hit a real database, not a mock. Mocked DB tests masking real migration behavior is a High/Critical finding (see feedback memory — prior incident).
- Check for `[Skip]`, `[Fact(Skip=...)]`, commented-out `Assert`, or empty test bodies — any of these in the diff is at least Medium.

**Correctness:**
- Walk the logic of non-trivial new methods. Look for: off-by-one, null derefs on parameters without `[NotNull]`, resource leaks (connections/streams not disposed), async methods that swallow exceptions or skip `ConfigureAwait` where the codebase uses it, race conditions in shared state.
- For each new `if`/`else`, ask: is the else branch tested? Is there a condition where both branches are wrong?
- For new SQL: is it parameterised? Does it use the correct schema/quoting for the target dialect?
- For new configuration/DI changes: is the lifetime correct (singleton vs scoped vs transient)? Are disposables registered correctly?

**Security (apply even in "internal" code):**
- SQL injection / command injection / path traversal at any trust boundary (user input, message headers, file names, connection strings)
- Secrets or connection strings committed to code, logs, or test fixtures
- Unsafe deserialization of attacker-controlled data
- Weak crypto or `Random` used for security-sensitive values
- Missing authorization checks on new endpoints

**Project Conventions (read `.agent_instructions/code_style.md` yourself to verify):**
- Primary constructors for new classes (project default)
- `Async` suffix on async methods; `ValueTask` vs `Task` consistency with surrounding code
- Nullability annotations consistent with the file's existing style
- XML doc comments on public APIs (project requires them)
- No dead code, speculative abstractions, or commented-out blocks
- No mixing of structural and behavioral changes in the same commit (tidy-first rule)

**Cross-backend consistency (Brighter-specific):**
- When a feature touches multiple backends (MSSQL, PostgreSQL, MySQL, SQLite, Spanner, Dynamo), check parity. If MSSQL got a new safety check and PostgreSQL didn't, that's a finding.
- Check case sensitivity for identifiers — PostgreSQL folds to lowercase, Spanner is strict, others vary. Flag any V1Columns-style comparisons with wrong `StringComparer`.

**Hygiene:**
- `git status --porcelain` non-empty → Medium finding ("uncommitted work on branch at time of review").
- New files that look misplaced (e.g., test files in src/, or vice versa).
- Binary files, generated files, or `.DS_Store` committed by accident.
- Build warnings introduced (if the diff touches a project the user has recently built, note whether warnings regressed).

**Grounding — no hallucinated findings:**
- Before filing a finding, verify: did you actually read the file, or are you guessing from the name? If guessing, either read it or don't file the finding.
- If the diff references a class or method, grep for it in the actual codebase (post-change) to confirm it exists as described.
- A finding that says "you should have done X" when X is already present is worse than no finding — it destroys trust in the whole review. Prefer fewer, grounded findings over comprehensive-looking speculation.

---

### Step 5: Output Format for Sub-Agent

Tell the sub-agent to produce output in this exact format:

```markdown
# Review: {phase} — {spec-name}

**Date**: {today's date}
**Threshold**: {threshold}
**Verdict**: {PASS or NEEDS WORK}

{If NEEDS WORK: "N findings at or above threshold {threshold}. Address these before approving."}
{If PASS: "No findings at or above threshold {threshold}. Consider addressing lower-scored items."}

## Findings

### {N}. {Short title} (Score: {0-100})

{Description of the problem — be specific, cite the exact text or section.}

**Evidence**: {Quote the problematic text or reference the specific gap.}

**Recommendation**: {How to fix it.}

---

{Repeat for each finding, ordered by score descending.}

## Summary

| Score Range | Count |
|-------------|-------|
| 90-100 (Critical) | {n} |
| 70-89 (High) | {n} |
| 50-69 (Medium) | {n} |
| 0-49 (Low) | {n} |

**Total findings**: {n}
**Findings at or above threshold ({threshold})**: {n}
```

**Scoring guide for the sub-agent:**
- **90-100 (Critical)**: Blocks approval. A real defect, missing requirement, contradiction, or broken reference. Reserve this for things that would cause incorrect implementation or are factually wrong.
- **70-89 (High)**: Should fix before approval. Significant gap, ambiguity that would cause two developers to implement differently, or missing coverage of important behavior.
- **50-69 (Medium)**: Worth addressing. Vagueness, missing examples, weak rationale, cosmetic inaccuracies (e.g. wrong class name in prose that doesn't affect implementation intent), incomplete but non-blocking.
- **0-49 (Low)**: Suggestion or nit. Style, minor clarity improvement, nice-to-have.

**Scoring calibration — avoid both inflation and deflation:**

Downward calibration (don't over-score):
- A cosmetic naming error in prose (e.g. `RejectMessageOnError` instead of `RejectMessageOnErrorAttribute`) is Medium (50-65), not High — it doesn't change what a developer would build.
- A missing acceptance criterion for an already-clear requirement is Medium (55-65), not High — unless the gap would cause the wrong thing to be tested.

Upward calibration (don't under-score):
- A missing acceptance criterion for a requirement whose correct behavior is NON-OBVIOUS is High (70-80) — different developers would test different things.
- A requirement that two developers would implement differently IS High (70+), even if each interpretation seems reasonable.
- A task list that omits a design decision from the ADR IS High (70+) — that's a coverage gap that could cause the feature to ship incomplete.
- A file path reference in an ADR that doesn't exist in the codebase IS at least Medium-High (65-75) — it misleads anyone reading the ADR.

General:
- Reserve Critical (90+) for contradictions, factually broken references, or requirements/designs that are impossible to implement as written.

**Verdict logic**: If ANY finding scores >= threshold → NEEDS WORK. Otherwise → PASS.

**Accuracy requirements:**
- After writing all findings, COUNT the findings in each score range carefully. The Summary table MUST match the actual findings listed above it.
- The "Findings at or above threshold" count MUST equal the number of findings whose score is >= the threshold value. Count them one by one.
- The verdict header count ("N findings at or above threshold") MUST match the Summary count. Double-check before writing.

### Step 6: Validate, Write Findings, and Present Summary

After the sub-agent returns:

1. **Validate the output** before writing:
   - Check the output contains the expected sections (Findings, Summary table)
   - Verify the Summary counts match the actual findings listed (count them yourself)
   - Verify the verdict is consistent with the threshold and findings
   - If counts are wrong, fix them before writing

2. **Write the findings file** to `specs/{current-spec}/review-{phase}.md` using the validated output. For the `code` phase the file is `specs/{current-spec}/review-code.md`.

3. **Present a summary to the user** in the conversation:
   - Verdict (PASS / NEEDS WORK)
   - Count of findings by severity
   - List just the title and score of each finding at or above threshold
   - Path to the findings file for full details
   - For `requirements`/`design`/`tasks`: remind the user to use `/spec:approve {phase}` when ready, or iterate and re-run `/spec:review {phase}`.
   - For `code`: remind the user that `code` has no approval marker — fix findings, commit, and re-run `/spec:review code` until clean. When clean, the next step is commit/push/PR.

### Step 7: Spec Status

Display overall spec status:
- Spec directory: `specs/{current-spec}`
- Requirements: Approved / In Progress
- Design: {X} ADRs ({Y} approved, {Z} proposed)
- Tasks: Approved / In Progress / Not Started
- Code: Reviewed at {commit sha} / Not reviewed (code is "reviewed" if `review-code.md` exists; include its verdict and the commit sha at the top of HEAD when the review was run)
