---
allowed-tools: Bash(dotnet run .claude/commands/spec/resolve_route.cs -- specs/:*), Bash(cat:*), Bash(test:*), Bash(touch:*), Bash(ls:*), Bash(echo:*), Bash(grep:*), Bash(awk:*), Read, Write, Edit, Glob, AskUserQuestion, Skill
description: Approve a specification phase
argument-hint: requirements|design [adr-number]|tasks
---

## Context

Current spec directory: specs/

**Workflow**: Issue → Requirements → ADR(s) → Tasks → Tests → Code

## Your Task

### Step 1: Resolve the route

Resolve the route first, before reading any artefact. `{command}` is `/spec:approve`.

<!-- routing-text:route-instructions:begin -->
Run `dotnet run .claude/commands/spec/resolve_route.cs -- specs/ --current`. If the exit status is not `0`, or standard output has no line beginning `route-report: `, or the JSON after that prefix on the last such line does not parse, stop. Print `{command} could not run the route resolver (.claude/commands/spec/resolve_route.cs): {state}. This is a tooling fault, not a fault in the spec — re-run after restoring the script.` Create or modify no file. Otherwise hold that JSON object as the route report. If `stop` is not `null`, print `stop.message` and stop, creating or modifying no file.
<!-- routing-text:route-instructions:end -->

The active specification directory is the report's `spec`. Never read the approval marker files
yourself: the route report is the only source of the spec's flow, steps and approval state.

Parse $ARGUMENTS: the step is the first word, and any further words are the selector for the step's
approval (for a `design` step, an ADR number such as `0043`).

- If there is no step, or `steps` has no entry named by it, stop and create or modify no file. Print the Step not on the slip template, with `{arg}` as the word typed (the missing-argument form when none was typed).
- If the step is not in `approve_steps`, stop and create or modify no file. Print the Another marker-writer template, with `{marker_writer}` as `steps[step].marker_writer`.
- If `steps[step].artefacts_present` is false, stop and create or modify no file. Print the Artefacts missing template, with `{producer}` as `steps[step].producer`.

### Step 2: Approve the step

1. The artefacts are present: `artefacts_present` is true for the step.
2. Read each file in `steps[step].artefacts` from the spec directory. Items 3 to 6 and 8 to 10 apply
   only when the step's kind is `design`; for any other kind, go straight to item 7.
3. Determine which ADRs to approve:
   - **If ADR number specified** (e.g., "design 0043"): Approve only that ADR
   - **If no ADR number**: Approve ALL ADRs in the `.adr-list`

4. For each ADR to approve, flip its status to `Accepted` with the `write_adr_metadata` skill so
   the **frontmatter `status`** and the **body `## Status`** move together (do not hand-edit only the
   body — that would leave the frontmatter stale, and the derived index reads the frontmatter):

   ```
   write_adr_metadata docs/adr/{adr-filename} status Accepted
   ```

   Confirm the skill reports its checks passing (frontmatter `status` matches body `## Status`).

5. **Handle supersession.** If an approved ADR replaces an earlier decision, retire the older ADR in
   the same pass. Prefer the relationship the ADR already records: read the approved ADR's `## Status`
   / `## References` for a "Supersedes {old-id}" note, and if it is ambiguous ask the user with
   `AskUserQuestion` which prior ADR (if any) is superseded rather than guessing.
   - Replaced by a specific ADR → mark the **older** one `Superseded` and record the back-link:
     `write_adr_metadata docs/adr/{old-id}.md supersede --by {new-id}`
   - Retired with no named replacement → `write_adr_metadata docs/adr/{old-id}.md deprecate`

6. **Regenerate the derived index.** After the status changes above, refresh `docs/adr/index.md` so
   it reflects the new statuses — see "Regenerate the ADR index" at the end of this file.

7. Create the approval marker: `touch specs/{spec}/{steps[step].marker}`
8. Show user which ADRs were approved (and any superseded/deprecated as a result).
9. Remind the user to commit the approved ADRs (and the regenerated `docs/adr/index.md`) to git.
10. **Point at the task breakdown.** If more ADRs are still needed, tell the user to run
   `/spec:design [another-focus-area]` first and stop here. Otherwise the design is complete:
   the closing instructions below print the next step.

   There is **one** task list (`tasks.md`) and **one** route to it. What used to be a fork in
   the artifacts is now a **gear** you select at implementation time, and can change as you go
   ([ADR 0071](../../../docs/adr/0071-tdd-review-gear.md)) — so there is nothing to choose here.
   Mention both drivers so the user knows what is coming, but do not ask them to commit to one:

   - `/spec:implement` — one task at a time, on **sonnet**. Red → **user approval** → Green →
     Refactor. The approval gate is armed by default (`review-before`).
   - `/spec:ralph-implement` — a self-driving unattended loop over the **same** `tasks.md`, on
     **opus** with auto mode, delegating each task to a **sonnet** sub-agent. Always
     `review-after`.

   Shift gear with `/spec:gear` at any point, in either direction, without regenerating or
   abandoning anything. Do **not** run these commands yourself — just direct the user to
   `/spec:tasks`.

For a step of kind `tasks`, the marker freezes the **content** of `tasks.md`. Checkbox state (`[ ]` →
`[x]`/`[!]`) is progress bookkeeping and both drivers write it; rewording, adding, removing or
reordering tasks after this point needs a fresh review.

### Step 3: Close

<!-- routing-text:closing-instructions:begin -->
After your last write, run `dotnet run .claude/commands/spec/resolve_route.cs -- specs/ --current` again, and apply the same test for a tooling fault as above. If it passes, print the next-step line, built from `next` in this second report. If it fails, print the tooling-fault message in place of the next-step line. Undo no write.
<!-- routing-text:closing-instructions:end -->

The templates this command prints:

<!-- routing-text:templates:begin -->
| Template | Printed by | Text |
| --- | --- | --- |
| Next-step line | the closing instructions, `/spec:status`, `/spec:switch`, `/spec:review`'s closing reminder, the route-changed template (0091), `/spec:new`'s post-check (0091), and the three templates below that end in it | ``Next: {next.commands joined with " then "}.``, or ``No next step: the slip is invalid (see violations).`` when `next.commands` is empty. Then ``Unattended: {next.unattended_driver}.`` when it is not `null`, and one ``Optional: {detour.command}, then return to {detour.return_to}.`` for each entry of `next.detours`, in order, all on the same line. With empty `next.commands` there is no driver and no detour |
| Step not on the slip | `/spec:approve` | ``Step `{arg}` is not on flow `{flow}`'s route. Approvable steps: {approve_steps joined with ", ", or "none"}.`` For a missing argument the text is ``/spec:approve needs a step. Approvable steps on flow `{flow}`: {approve_steps joined with ", ", or "none"}.`` |
| Another marker-writer | `/spec:approve` | ``Step `{step}` on flow `{flow}` is approved by `{marker_writer}`, not /spec:approve. Run `{marker_writer}`.`` |
| Artefacts missing | `/spec:approve` | ``Step `{step}` cannot be approved: its artefacts ({steps[step].artefacts joined with ", "}) are not all present. Run `{producer}` first.`` |

<!-- routing-text:templates:end -->

## Regenerate the ADR index

After any ADR status change (approval, supersession, deprecation), refresh the derived
`docs/adr/index.md` so it matches the frontmatter. This is the single canonical command (documented
in [`.agent_instructions/adr_frontmatter.md`](../../../.agent_instructions/adr_frontmatter.md)):

```bash
awk -f .claude/commands/adr/generate_adr_index.awk docs/adr/[0-9]*.md > docs/adr/index.md
```

`docs/adr/index.md` is a regenerable cache — never hand-edit it; always regenerate from frontmatter.

## Examples

```bash
# Approve requirements
/spec:approve requirements

# Approve all ADRs for current spec
/spec:approve design

# Approve specific ADR
/spec:approve design 0043

# Approve tasks
/spec:approve tasks
```

Use Edit tool to update ADR status, touch command for approval markers.