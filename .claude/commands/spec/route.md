---
allowed-tools: Bash(dotnet run .claude/commands/spec/resolve_route.cs -- specs/:*), Bash(tr:*), Bash(shasum:*), Bash(sha256sum:*), Bash(date:*), Bash(cat:*), Bash(test:*), Bash(rm:*), Read, AskUserQuestion
description: Change or repair the route of the current spec
argument-hint: <flow> [reason] [--flows-dir <dir>]
---

## Your Task

Move the current spec onto the flow named in $ARGUMENTS, or repair its slip, by copying the flow's
definition into `.flow`. Work through the rows below in order. The first row that matches ends the run.
Rows 1 to 6 are checks and write nothing. Never read the approval marker files or the flow
definition yourself: the resolver is the only judge of the route and of whether a definition is usable.

Everything this command prints comes from the templates at the end of this file, filled from the
resolver's reports and the arguments.

### Arguments

- The first word is the target flow, `{target}`.
- `--flows-dir {dir}` may appear anywhere after the target. Take it out before the reason is read. It
  defaults to `.claude/flows`. Pass it as `--flows-dir {dir}` to every resolver run, and use
  `{flows-dir}/{target}.flow` as `{S}` in the installation steps.
- The rest of the text, trimmed and with any wrapping quotes removed, is the reason. Replace line breaks
  in it with spaces. It reaches the shell only inside a quoted here-document.

### The ladder

1. When there is no target, the target does not match `[a-z][a-z0-9-]*`, or the `--flows-dir` value holds
   `$`, a backtick, `"` or `\`, print the Route usage template and stop.
2. Run `dotnet run .claude/commands/spec/resolve_route.cs -- specs/ --current`, with the `--flows-dir`
   option when one was given. It is a tooling fault when the exit status is not `0`, or no line
   beginning `route-report: ` parses as JSON. Print the message below, with `{state}` as `exited {code}`,
   `no route-report line` or `route-report line does not parse`, and print nothing else. Create or modify
   no file. `{command}` is `/spec:route`. Apply the same test to the run in row 4 and to the post-check.

   `{command} could not run the route resolver (.claude/commands/spec/resolve_route.cs): {state}. This is a tooling fault, not a fault in the spec — re-run after restoring the script.`

   Otherwise hold the JSON on the last such line as the current report.
3. When `current_spec.state` is not `ok`, print `stop.message` and stop. When the report's `spec` holds
   `$`, a backtick, `"` or `\`, print the Route spec name template and stop. A `stop` whose reason is
   `tampered` or `invalid` does not stop this command, because those are the states it repairs.
4. Run `dotnet run .claude/commands/spec/resolve_route.cs -- specs/ --definition {target}`, with the
   `--flows-dir` option when one was given, and apply the tooling-fault test of row 2. When its report's
   `valid` is `false`, print the Route target not usable template and stop.
5. When, in the current report, `flow.name` equals the target, `valid` is `true`, `tampered.value` is
   `false`, and `route_change` is `null` or its `to` equals the target, print the Already on target
   template and stop. When the last log entry names another flow, `route_change.to` differs and the run
   goes on, so that a change abandoned part-way can be recorded.
6. When no reason was given, ask for one with `AskUserQuestion`, offering free text and suggestions such as
   "the route drops a step this work does not need" and "re-copy after a fixed definition". When the
   question is dismissed or answered empty, print the No reason template and stop.
7. Otherwise run the write order below. A failed step prints the Installation failed template, with
   `{command}` as `/spec:route`, `{step}` as the failed step and `{files}` as what was written so far
   (`nothing` at W0), and stops.
8. After W5, run `dotnet run .claude/commands/spec/resolve_route.cs -- specs/ --current` again, with the
   `--flows-dir` option when one was given, applying the tooling-fault test of row 2. On a fault, print
   the tooling-fault message in place of rows 9 and 10, and undo no write.
9. When the post-check report shows `flow.name` equal to the target, `valid` `true` and `tampered.value`
   `false`, print the Route changed template, with `{from}` as the flow the log entry names, then the
   next-step line built from `next` in this second report.
10. Otherwise print the Post-check template, with `{command}` as `/spec:route`.

Every check stops before the first write. The history is written before the slip changes, and the record
is written last. The resolver judges the result: never decide for yourself that the route changed.

### The slip and the log

`{D}` is the current spec's directory, `specs/{dir}`. `{flow}` in the installation steps is the target.
`{from}` is `tampered.recorded_flow` when `tampered.case` is `b`, and `flow.name` otherwise. When that is
`null`, it is `route_change.to`, then `unknown`.

The installation steps:

<!-- routing-text:installation-steps:begin -->
1. `tr -d '\r' < "{S}" > "{D}/.flow"`
2. `shasum -a 256 < "{D}/.flow"`. If the command is not found, use `sha256sum < "{D}/.flow"`. The slip hash is the first field of the output, and it must match `^[0-9a-f]{64}$`.
3. `date +%Y-%m-%d`
4. Write the record with a quoted here-document, so that nothing is expanded:
   `cat > "{D}/.flow.record" <<'FLOW_RECORD'` followed by the three lines, then `FLOW_RECORD`.

If any step fails, stop and print a failure template. `/spec:new` and the starters carry the steps in this order. `/spec:route` carries the same four steps verbatim, then runs them in the order its write order sets.
<!-- routing-text:installation-steps:end -->

The record holds exactly three lines, each ending in a line feed: `flow: {target}`, `sha256: {the slip
hash}` and `date: {the date from step 3}`.

The log entry is a line `## {date}: {from} -> {target}`, a blank line, `Reason: {reason}` and a blank
line. A new `route-changes.md` starts with `# Route changes` and a blank line.

Run the steps in this order:

<!-- routing-text:write-order:begin -->
| # | Step | State if the run stops after this step |
| --- | --- | --- |
| W0 | installation step 3 (`date`, read-only). Its value dates both the log heading and the record | unchanged |
| W1 | `rm -f "{D}/.flow.record"` | case (c) if `.flow` exists; otherwise legacy-looking |
| W2 | append the log entry with `test -f` and `cat >> "{D}/route-changes.md" <<'ROUTE_CHANGE'`, writing the `# Route changes` header first if the file is new | as W1, with the entry logged |
| W3 | installation step 1 (the shell creates `.flow` before `tr` runs) | case (c) |
| W4 | installation step 2 (read-only) | case (c) |
| W5 | installation step 4 | complete |
<!-- routing-text:write-order:end -->

Delete nothing else. Markers and artefacts stay where they are.

The templates this command prints:

<!-- routing-text:templates:begin -->
| Template | Printed by | Text |
| --- | --- | --- |
| Next-step line | the closing instructions, `/spec:status`, `/spec:switch`, `/spec:review`'s closing reminder, the route-changed template (0091), `/spec:new`'s post-check (0091), and the three templates below that end in it | ``Next: {next.commands joined with " then "}.``, or ``No next step: the slip is invalid (see violations).`` when `next.commands` is empty. Then ``Unattended: {next.unattended_driver}.`` when it is not `null`, and one ``Optional: {detour.command}, then return to {detour.return_to}.`` for each entry of `next.detours`, in order, all on the same line. With empty `next.commands` there is no driver and no detour |
| Route usage | `/spec:route` | ``/spec:route needs a target flow name and, if given, a --flows-dir without $, `, " or \. Usage: /spec:route {flow} [reason]. Nothing was changed.`` |
| Route spec name | `/spec:route` | ``Cannot change the route of `{spec}`: its directory name holds $, `, " or \. Nothing was changed.`` |
| Route target not usable | `/spec:route` | ``Cannot change the route to `{target}`: {violations[0].message} Nothing was changed.`` |
| Already on target | `/spec:route` | ``Spec `{spec}` is already on flow `{target}`, and its slip is valid and not tampered. Nothing was changed.`` |
| No reason | `/spec:route` | ``/spec:route needs a reason for the route change. Nothing was changed.`` |
| Route changed | `/spec:route` | ``Spec `{spec}` moved from flow `{from}` to `{target}`. Reason recorded in route-changes.md: {reason}`` then, when `stray_markers` is non-empty, ``Kept as stray markers: {stray_markers joined with ", "}.`` then the next-step line |
| Installation failed | `/spec:route` | ``{command} stopped: {step} failed. Written so far: {files}. Re-run {command}.`` |
| Post-check | `/spec:route`, `/spec:new` | ``{command} wrote the slip, but the resolver reports flow `{flow.name, or "none"}`, valid {valid}, tampered {tampered.case, or "no"}. Re-run /spec:route {target} <reason> to repair.`` |

<!-- routing-text:templates:end -->
