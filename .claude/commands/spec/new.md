---
allowed-tools: Bash(dotnet run .claude/commands/spec/resolve_route.cs -- specs/:*), Bash(cat:*), Bash(test:*), Bash(touch:*), Bash(ls:*),  Bash(echo:*), Bash(mkdir:*), Bash(tr:*), Bash(shasum:*), Bash(sha256sum:*), Bash(date:*), Bash(git check-ignore:*), Read, Write, Glob
description: Create a new feature specification
argument-hint: <feature-name>
---

## Current Spec Status

!`ls -la specs/ 2>/dev/null | grep "^d" | wc -l | xargs -I {} echo "Total specs: {}"`

## Your Task

Create a new specification directory for the feature: $ARGUMENTS

### Step 0: Check the `feature` definition

Run `dotnet run .claude/commands/spec/resolve_route.cs -- specs/ --definition feature` before anything
else. Never read the flow or approval files yourself: the resolver is the only judge of whether the
definition is usable.

It is a tooling fault when the exit status is not `0`, or no line beginning `route-report: ` parses.
Print the message below, with `{state}` as `exited {code}`, `no route-report line` or
`route-report line does not parse`, and print nothing else. Create or modify no file. `{command}` is
`/spec:new`.

`{command} could not run the route resolver (.claude/commands/spec/resolve_route.cs): {state}. This is a tooling fault, not a fault in the spec — re-run after restoring the script.`

Otherwise, when the report's `valid` is `false`, print the Flow not usable template with
`{violations[0].message}` and stop. Create or modify no file and do not touch `specs/.current-spec`.

Only when `valid` is `true` carry on with the steps below. Each step that prints a template stops the run after it.

1. **Name.** Take $ARGUMENTS whole: trimmed, wrapping quotes removed, inner spaces kept. When it is empty, or contains `/`, `\`, `$`, a backtick or `"`, print the Spec name template and stop. `{name}` is the result.
2. **Numbering.** List the immediate subdirectories of `specs/` whose names match `^[0-9]{4,}-` and take the highest number. The new number `{NNNN}` is that plus one, zero-padded to at least four digits, or `0001` when there are none. Only the whole name `{NNNN}-{name}` must be unique, because numbers repeat. `{dir}` is `{NNNN}-{name}`.
3. **Ignore check.** Run `git check-ignore -q --no-index "specs/{dir}/.flow"`. Exit `0` means git would ignore the spec's files: print the Spec ignored template and stop. Exit `1` means it would not. Any other exit prints the Start failed template.
4. Run `mkdir "specs/{dir}"`, with no `-p`. When it fails because the directory exists, including one that arrived after step 2, print the Spec exists template and stop. Any other failure prints the Start failed template. Every later step is skipped.
5. Run the installation steps below with `{flow}` as `feature`, `{flows-dir}` as `.claude/flows`, `{S}` as `.claude/flows/feature.flow` and `{D}` as `specs/{dir}`.
6. Write `specs/{dir}/README.md` with the feature name, the creation date and an initial status checklist. Then write `specs/.current-spec` with `{dir}`, last of all, so that the current spec changes only once the new spec is complete.
7. **Post-check.** Run `dotnet run .claude/commands/spec/resolve_route.cs -- specs/ --spec "{dir}"` and apply the same tooling-fault test as Step 0. On a fault, print the tooling-fault message in place of the rest of this step, and undo no write. When the report shows flow `feature` from `slip`, `valid` `true` and `tampered.value` `false`, print the Next-step line built from `next`. Otherwise print the Post-check template, with `{command}` as `/spec:new`, `{target}` as `feature` and `{flow.name}`, `{valid}` and `{tampered.case}` read from this report.

A failure at step 5 or 6 prints the New spec failed template, with `{command}` as `/spec:new`, `{step}` as the failed step and `{files}` as what the directory holds. A failure at step 3 or 4 prints the Start failed template with the same `{command}` and `{step}`.

The installation steps:

<!-- routing-text:installation-steps:begin -->
1. `tr -d '\r' < "{S}" > "{D}/.flow"`
2. `shasum -a 256 < "{D}/.flow"`. If the command is not found, use `sha256sum < "{D}/.flow"`. The slip hash is the first field of the output, and it must match `^[0-9a-f]{64}$`.
3. `date +%Y-%m-%d`
4. Write the record with a quoted here-document, so that nothing is expanded:
   `cat > "{D}/.flow.record" <<'FLOW_RECORD'` followed by the three lines, then `FLOW_RECORD`.

If any step fails, stop and print a failure template. `/spec:new` and the starters carry the steps in this order. `/spec:route` carries the same four steps verbatim, then runs them in the order its write order sets.
<!-- routing-text:installation-steps:end -->

The templates this command prints:

<!-- routing-text:templates:begin -->
| Template | Printed by | Text |
| --- | --- | --- |
| Flow not usable | `/spec:new` | ``Cannot start a spec: flow `feature` is not usable. {violations[0].message} Fix `.claude/flows/feature.flow` or `.claude/flows/approvals.md` first.`` |
| Next-step line | the closing instructions, `/spec:status`, `/spec:switch`, `/spec:review`'s closing reminder, the route-changed template (0091), `/spec:new`'s post-check (0091), and the three templates below that end in it | ``Next: {next.commands joined with " then "}.``, or ``No next step: the slip is invalid (see violations).`` when `next.commands` is empty. Then ``Unattended: {next.unattended_driver}.`` when it is not `null`, and one ``Optional: {detour.command}, then return to {detour.return_to}.`` for each entry of `next.detours`, in order, all on the same line. With empty `next.commands` there is no driver and no detour |
| Post-check | `/spec:route`, `/spec:new` | ``{command} wrote the slip, but the resolver reports flow `{flow.name, or "none"}`, valid {valid}, tampered {tampered.case, or "no"}. Re-run /spec:route {target} <reason> to repair.`` |
| New spec failed | `/spec:new`, starters | ``{command} stopped: {step} failed. `specs/{dir}/` was created and holds: {files}. Delete it and run {command} {name} again.`` |
| Start failed | `/spec:new`, starters | ``{command} stopped: {step} failed. Nothing was created.`` |
| Spec name | `/spec:new`, starters | ``{command} needs a spec name that is not empty and has none of / \ $ ` ". Nothing was created.`` |
| Spec exists | `/spec:new`, starters | ``Cannot start a spec: `specs/{dir}/` already exists. Nothing was created. Re-run {command} to take the next number.`` |
| Spec ignored | `/spec:new`, starters | ``Cannot start a spec named `{name}`: git would ignore `specs/{dir}/.flow`. Nothing was created. Choose a name that does not match a .gitignore rule.`` |

<!-- routing-text:templates:end -->
