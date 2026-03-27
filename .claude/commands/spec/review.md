---
allowed-tools: Bash(cat:*), Bash(test:*), Bash(touch:*), Bash(ls:*), Bash(echo:*), Bash(grep:*), Read, Write, Glob, Grep, Agent
description: Review current specification phase
argument-hint: [requirements|design [adr-number]|tasks] [threshold]
---

## Adversarial Specification Review

Current spec directory: specs/

**Workflow**: Issue → Requirements → ADR(s) → Tasks → Tests → Code

**Philosophy**: The first draft is never good enough. This review is skeptical and adversarial — it assumes problems exist and looks for them. The goal is to force iteration towards quality.

## Your Task

### Step 1: Parse Arguments and Determine What to Review

Read `specs/.current-spec` to determine the active specification directory.

**Error handling**: If `.current-spec` does not exist, tell the user to run `/spec:new` first and stop. If the spec directory doesn't exist, tell the user and stop. If the document for the requested phase doesn't exist (e.g., no `requirements.md`), tell the user to run the appropriate creation command first (e.g., `/spec:requirements`) and stop. Do NOT launch the sub-agent with missing documents.

Parse $ARGUMENTS:
- Extract **phase**: first word — `requirements`, `design`, or `tasks`
- Extract **adr-number**: for `design` phase, a zero-padded 4-digit number (e.g., `0053`). **Precedence rule**: a 4-digit zero-padded number is ALWAYS an ADR number, never a threshold.
- Extract **threshold**: any other numeric value (default: **60**)
- If phase is empty: auto-detect (first unapproved phase by checking `.requirements-approved`, `.design-approved`, `.tasks-approved` markers). If ALL phases are approved, tell the user all phases are approved and suggest `/spec:status` or `/spec:implement` instead.

**Approved phase warning**: If the user explicitly requests review of an already-approved phase, note this in the sub-agent prompt and include a note in the output: "This phase is already approved. Findings are informational — consider whether any warrant re-opening the phase."

Examples:
- `/spec:review` → auto-detect phase, threshold 60
- `/spec:review requirements` → review requirements, threshold 60
- `/spec:review requirements 70` → review requirements, threshold 70
- `/spec:review design 0053` → review ADR 0053, threshold 60
- `/spec:review design 0053 80` → review ADR 0053, threshold 80
- `/spec:review design 70` → review all ADRs, threshold 70 (not 4-digit, so it's a threshold)
- `/spec:review tasks 50` → review tasks, threshold 50

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

### Step 3: Launch Sub-Agent for Adversarial Review

Launch an Agent (subagent_type: "general-purpose") with the prompt below.

**For requirements reviews**: Include the full text of the document in the sub-agent prompt. If the document is very large (you couldn't read it in one Read call), pass the file path instead and instruct the sub-agent to read it.

**For design and tasks reviews**: Tell the sub-agent the file paths to read — it should read the files itself. This handles large documents (ADRs can be very long) and allows the sub-agent to verify codebase references. Include the requirements.md text in the prompt for cross-referencing (or the file path if it's also large).

**Sub-agent tool access**: The sub-agent (general-purpose) inherits tool access. For design and tasks reviews it SHOULD use Read, Glob, and Grep to read documents and verify codebase references. The sub-agent should NOT write the findings file — it should return the findings as text. The main agent writes the file after validating the output (see Step 6).

**IMPORTANT**: The sub-agent prompt must include:
1. The review criteria for the relevant phase (from Step 4 below)
2. The full document text(s) or file paths to read
3. The cross-reference documents (when applicable)
4. The threshold value
5. The output format instructions (from Step 5 below)
6. An instruction to RETURN the findings as text output, NOT to write a file

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

2. **Write the findings file** to `specs/{current-spec}/review-{phase}.md` using the validated output.

3. **Present a summary to the user** in the conversation:
   - Verdict (PASS / NEEDS WORK)
   - Count of findings by severity
   - List just the title and score of each finding at or above threshold
   - Path to the findings file for full details
   - Remind: Use `/spec:approve {phase}` when ready, or iterate on the document and re-run `/spec:review {phase}` after changes.

### Step 7: Spec Status

Display overall spec status:
- Spec directory: `specs/{current-spec}`
- Requirements: Approved / In Progress
- Design: {X} ADRs ({Y} approved, {Z} proposed)
- Tasks: Approved / In Progress / Not Started
