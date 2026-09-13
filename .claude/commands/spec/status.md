---
allowed-tools: Bash(cat:*), Bash(grep:*), Bash(test:*), Bash(find:*), Bash(touch:*), Bash(ls:*), Bash(echo:*), Read, Write, Glob
description: Show all specifications and their status
---

## Gather Status Information

Current spec directory: specs/

**Workflow**: Issue → Requirements → ADR(s) → Tasks → Tests → Code

## Your Task

### Step 1: Gather Information

1. Read `specs/.current-spec` to determine the active specification
2. Use Bash to find all spec directories: `ls -d specs/*/`
3. For each spec directory, check:
   - Approval markers: `.requirements-approved`, `.design-approved`, `.tasks-approved`
   - Phase files: `requirements.md`, `tasks.md`
   - ADR list: `.adr-list`
   - GitHub issue: `.issue-number`
   - **Review gear**: `.current-gear` (untracked working state — see
     [`gear.md`](gear.md)). Report the resolved gear for any spec that has one. A spec with no
     `.current-gear` is in the default `review-before` gear; say so only for the **active** spec,
     to keep the report short.
4. For each ADR in `.adr-list`, read from `docs/adr/` and check Status field

### Step 2: Present Status Report

Display a comprehensive status report with the following structure:

```
Specification Status Report
===========================

Active Spec: specs/{current-spec}/ (marked with *)

Specifications:
---------------

* specs/0001-feature-name/
  Issue: #123 (if linked)

  Requirements: ✓ Approved

  Design (ADRs):
    - docs/adr/0042-feature-aspect-one.md [Accepted]
    - docs/adr/0043-feature-aspect-two.md [Proposed]
    - docs/adr/0044-feature-aspect-three.md [Accepted]
  Status: ⏳ In Progress (1 ADR pending approval)

  Tasks: ✓ Approved
    Progress: 15/30 tasks complete (50%)

  Review gear: ➖ review-after — scoped to "Phase 5 — Provider rejection tests"
    Since 2026-09-13: Phase 4's six tests were all approved unchanged; the shape is settled
    (untracked; shift with /spec:gear)

  Next Action: Approve remaining ADR or begin implementation

  specs/0002-another-feature/
  Issue: #124

  Requirements: ✓ Approved

  Design (ADRs): ⏹ Not Started

  Tasks: ⏹ Not Started

  Next Action: Create ADRs using /spec:design [focus-area]
```

### Step 3: Legend

Include a legend:
```
Status Indicators:
  ✓ Approved
  ⏳ In Progress
  ⏹ Not Started
  * Active specification

ADR Status:
  [Proposed] - Not yet approved
  [Accepted] - Approved and ready

Review gear:
  ✅ review-before - approval gate armed (the default)
  ➖ review-after  - approval gate not armed; reviewed as a batch
```

**Call out an ungated spec.** The gear file is untracked, so it will not show up in a diff or a
`git status` — this report is one of the few places it surfaces. If any spec resolves to
`review-after`, say so plainly and give the downshift command (`/spec:gear review-before`). If a
gear file exists but cannot be parsed, report the spec as `review-before` (the fail-safe default)
and flag the malformed file.

### Step 4: Summary

Provide a summary:
- Total specifications: {count}
- Active spec: {name}
- Review gear for the active spec (and any other spec not in the default gear)
- Next recommended action for active spec

Use Read, Bash, and Glob tools to gather information.