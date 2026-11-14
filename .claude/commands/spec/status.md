---
allowed-tools: Bash(dotnet run .claude/commands/spec/resolve_route.cs -- specs/:*), Bash(cat:*), Bash(grep:*), Bash(test:*), Bash(ls:*), Read, Glob
description: Show all specifications and their status
---

## Gather Status Information

Current spec directory: specs/

**Workflow**: Issue → Requirements → ADR(s) → Tasks → Tests → Code

## Your Task

### Step 1: Gather Information

Run `dotnet run .claude/commands/spec/resolve_route.cs -- specs/ --all` once. It prints one
`route-current: ` line and then one `route-report: ` line per spec directory. Never read the approval
marker files yourself, and never fall back to them: the resolver
is the only source of a spec's flow, steps and next step. Never stop on a spec's `stop` field.

Apply the `--all` rule:

- It is a tooling fault when the exit status is not `0`, or no line beginning `route-current: `
  parses. Print the message below, with `{state}` as `exited {code}`, `no route-current line` or
  `route-current line does not parse`, and print nothing else. Create or modify no file.
  `{command}` is `/spec:status`.

  `{command} could not run the route resolver (.claude/commands/spec/resolve_route.cs): {state}. This is a tooling fault, not a fault in the spec — re-run after restoring the script.`

- Otherwise parse each `route-report: ` line on its own, and match it to its spec directory by
  `spec`. `specs: 0` with no `route-report: ` line is a valid run.
- List a spec directory with no parsed line as `route unavailable`. When fewer lines parsed than
  `specs` on the `route-current: ` line says, add a note that the output may have been cut short.

Then gather the rest:

1. `current_spec` on the `route-current: ` line is the active specification. Report its `state`
   (`ok`, or `missing`, `empty` or `stale`) when it is not `ok`.
2. Print each entry of `shared_violations` once, in full, under the list of specs.
3. For each spec, from its line: `flow` (name, `source`, and whether `definition_present`), `valid`,
   `steps` (each `name`, `kind` and `state`), `violations`, `tampered`, `route_change`,
   `stray_markers`, `out_of_order` and `next`.
4. Spec files the resolver does not report: `.issue-number` (GitHub issue) and `tasks.md`
   (progress: count of `- [x]` against all `- [ ]` and `- [x]` task lines).
5. For a step whose `kind` is `design`, read each ADR named in the spec's `.adr-list` from `docs/adr/`
   and take its Status field.
6. **Review gear**: `.current-gear` (untracked working state — see [`gear.md`](gear.md)). Report the
   resolved gear for any spec that has one. A spec with no `.current-gear` is in the default
   `review-before` gear; say so only for the **active** spec, to keep the report short.

### Step 2: Present Status Report

Display a comprehensive status report with the following structure:

```
Specification Status Report
===========================

Active Spec: specs/{current-spec}/ (marked with *)

Shared faults (once, in full):
  {shared_violations entries, or omitted when none}

Specifications:
---------------

* specs/0001-feature-name/
  Issue: #123 (if linked)
  Flow: feature (default — no .flow)   # {flow.name} and its source; add "definition missing" when absent

  Steps:
    requirements: ✓ approved
    design: ⏳ ready
      - docs/adr/0042-feature-aspect-one.md [Accepted]
      - docs/adr/0043-feature-aspect-two.md [Proposed]
    tasks: ⏹ pending
    Progress: 15/30 tasks complete (50%)

  Route change: 2026-10-01 feature → docs-only — "first 60 characters of the reason…"
  Stray markers: {stray_markers joined with ", "}
  Out-of-order approvals: {out_of_order joined with ", "}
  Tampered: {tampered.case}
  Invalid: {rule} {code} at step {step}   # one line per violation, named by rule, code and step

  Review gear: ➖ review-after — scoped to "Phase 5 — Provider rejection tests"
    Since 2026-09-13: Phase 4's six tests were all approved unchanged; the shape is settled
    (untracked; shift with /spec:gear)

  {next-step line, from this spec's `next`}

  specs/0002-another-feature/
  route unavailable
```

Show each of the optional lines (route change, stray markers, out-of-order approvals, tampered,
invalid) only for a spec that carries the field. A step `state` of `pending` is ⏹, `ready` is ⏳,
`approved` is ✓, and `approved-no-artefacts` is ✓ with the note "marker present, artefacts
missing". Show a route change's reason as the line gives it, with its `…` when cut; the full reason
is one `/spec:switch` away.

The next-step line is the Next-step line template, filled from the spec's `next`:

<!-- routing-text:templates:begin -->
| Template | Printed by | Text |
| --- | --- | --- |
| Next-step line | the closing instructions, `/spec:status`, `/spec:switch`, `/spec:review`'s closing reminder, the route-changed template (0091), `/spec:new`'s post-check (0091), and the three templates below that end in it | ``Next: {next.commands joined with " then "}.``, or ``No next step: the slip is invalid (see violations).`` when `next.commands` is empty. Then ``Unattended: {next.unattended_driver}.`` when it is not `null`, and one ``Optional: {detour.command}, then return to {detour.return_to}.`` for each entry of `next.detours`, in order, all on the same line. With empty `next.commands` there is no driver and no detour |

<!-- routing-text:templates:end -->

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