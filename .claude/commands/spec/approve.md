---
allowed-tools: Bash(dotnet run .claude/commands/spec/resolve_route.cs -- specs/:*), Bash(cat:*), Bash(test:*), Bash(git rev-parse:*), Bash(tail:*), Bash(ls:*), Bash(echo:*), Bash(grep:*), Bash(awk:*), Read, Write, Edit, Glob, AskUserQuestion, Skill
description: Approve a specification phase
argument-hint: <step> [selector]
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

Parse $ARGUMENTS: the step is the first word, and any further words are the selector words for the step's
approval.

- If there is no step, or `steps` has no entry named by it, stop and create or modify no file. Print the Step not on the slip template, with `{arg}` as the word typed (the missing-argument form when none was typed).
- If the step is not in `approve_steps`, stop and create or modify no file. Print the Another marker-writer template, with `{marker_writer}` as `steps[step].marker_writer`.
- If `steps[step].artefacts_present` is false, stop and create or modify no file. Print the Artefacts missing template, with `{producer}` as `steps[step].producer`.

### Step 2: Run the approval sub-flow

`{approver}` is `/spec:approve`. The artefacts are present, so run the sub-flow.

<!-- routing-text:sub-flow-instructions:begin -->
1. Read `steps[{step}].approval_subflow`. If it is `null`, print the no-usable-entry message and stop.
2. If it is empty, go to step 7.
3. Build the common arguments: `"{spec}" {step} {artefacts}`, then `--select {word}` for each of the approver's selector words, in order. `{spec}` is the report's `spec`, and `{artefacts}` is `steps[{step}].artefacts` joined with `,`. `/spec:approve` takes its selector words from the words after the step name. A gate or a flow's own approver passes none, unless its own command defines some.
4. Questions pass. For each command in table order, run it through the Skill tool (its name without the leading `/`), with arguments `questions`, the common arguments, and `--answer {key}={value}` for every answer kept from the commands before it. Keep every `answer:` line it prints, in order.
5. Actions pass. For each command in table order, run it with `actions`, the common arguments, and `--answer {key}={value}` for every kept answer.
6. After each run in steps 4 and 5, the command has completed only when the last protocol line it printed is `sub-flow-done: {command} {pass}`. Otherwise print the sub-flow-failed message for that pass and stop. Write no marker.
7. Append the provenance line to `steps[{step}].marker`, and print the step-approved message.
<!-- routing-text:sub-flow-instructions:end -->

### Step 3: Close

<!-- routing-text:closing-instructions:begin -->
After your last write, run `dotnet run .claude/commands/spec/resolve_route.cs -- specs/ --current` again, and apply the same test for a tooling fault as above. If it passes, print the next-step line, built from `next` in this second report. If it fails, print the tooling-fault message in place of the next-step line. Undo no write.
<!-- routing-text:closing-instructions:end -->

The provenance line is appended with these commands.

<!-- routing-text:provenance-commands:begin -->
1. `git rev-parse --verify --quiet HEAD`. It prints the full sha. With no commits it exits `1` silently, and outside a git work tree it exits `128`. In both cases the sha is `unavailable`.
2. `test -s {m}`. When it succeeds, `tail -c 1 {m}`. When the byte it prints is not a newline (the output looks empty when the file ends in one), the marker lacks a final newline, so run `echo >> {m}`.
3. `echo "approved: step={step} artefacts={approved} sha={sha}" >> {m}`.

Worked examples, at HEAD `0691e67beb1ce7242c734bcd0a9a5960610d432d`:

```
approved: step=tasks artefacts=tasks.md sha=0691e67beb1ce7242c734bcd0a9a5960610d432d
approved: step=design artefacts=docs/adr/0089-slips-resolver-and-routing.md,docs/adr/0090-approval-sub-flows.md sha=0691e67beb1ce7242c734bcd0a9a5960610d432d
approved: step=requirements artefacts=requirements.md sha=unavailable
```
<!-- routing-text:provenance-commands:end -->

The templates this command prints:

<!-- routing-text:templates:begin -->
| Template | Printed by | Text |
| --- | --- | --- |
| Next-step line | the closing instructions, `/spec:status`, `/spec:switch`, `/spec:review`'s closing reminder, the route-changed template (0091), `/spec:new`'s post-check (0091), and the three templates below that end in it | ``Next: {next.commands joined with " then "}.``, or ``No next step: the slip is invalid (see violations).`` when `next.commands` is empty. Then ``Unattended: {next.unattended_driver}.`` when it is not `null`, and one ``Optional: {detour.command}, then return to {detour.return_to}.`` for each entry of `next.detours`, in order, all on the same line. With empty `next.commands` there is no driver and no detour |
| Step not on the slip | `/spec:approve` | ``Step `{arg}` is not on flow `{flow}`'s route. Approvable steps: {approve_steps joined with ", ", or "none"}.`` For a missing argument the text is ``/spec:approve needs a step. Approvable steps on flow `{flow}`: {approve_steps joined with ", ", or "none"}.`` |
| Another marker-writer | `/spec:approve` | ``Step `{step}` on flow `{flow}` is approved by `{marker_writer}`, not /spec:approve. Run `{marker_writer}`.`` |
| Artefacts missing | `/spec:approve` | ``Step `{step}` cannot be approved: its artefacts ({steps[step].artefacts joined with ", "}) are not all present. Run `{producer}` first.`` |
| No usable entry | every approver | ``Step `{step}` cannot be approved: kind `{steps[step].kind}` has no usable entry in the approval table. Nothing was written.`` |
| Sub-flow failed, questions | every approver | ``{approver} stopped: `{command}` failed in its questions pass: {reason}. {steps[step].marker} was not written, and this sub-flow wrote nothing.`` |
| Sub-flow failed, actions | every approver | ``{approver} stopped: `{command}` failed in its actions pass: {reason}. {steps[step].marker} was not written. Writes made by earlier sub-flow commands stay. Re-run {approver} to run the sub-flow again from `{approval_subflow[0]}`.`` |
| Step approved | every approver | ``Approved step `{step}` on flow `{flow}`: {steps[step].marker} gained a provenance line.`` |

<!-- routing-text:templates:end -->

## Amending an approved tasks.md

`.tasks-approved` freezes the **content** of `tasks.md` against unreviewed change. Checkbox state (`[ ]` → `[x]`/`[!]`) is progress bookkeeping and both drivers write it. Rewording, adding, removing or reordering tasks after approval needs the owner's approval of that specific amendment, expected when implementation, a refactoring or a review shows the list is wrong. Draft the amendment, get approval, and say in the amending commit that the owner approved it; `.tasks-approved` stays. A large rework still warrants a fresh `/spec:review tasks`. An unattended `/spec:ralph-implement` run never amends the list: it marks a wrong task `[!]`.

## Examples

```bash
/spec:approve requirements
/spec:approve design 0043
/spec:approve tasks
```
