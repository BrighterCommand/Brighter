---
allowed-tools: Bash(dotnet run .claude/commands/spec/resolve_route.cs -- specs/:*), Bash(cat:*), Bash(grep:*), Bash(test:*), Bash(find:*), Bash(touch:*), Bash(ls:*),  Bash(echo:*), Read, Write, Glob
description: Switch to a different specification
argument-hint: <spec-id>
---

## Available Specifications

!`ls -d specs/*/ 2>/dev/null | sort`

## Your Task

Switch the active specification to: $ARGUMENTS

**If no argument is given**, list all the available specs above and stop. Create or modify no file.

### Step 1: Pick the spec

Candidates are the directory names in the listing above, with the trailing slash removed. Take
`$ARGUMENTS` as a whole: trim leading and trailing whitespace, remove any wrapping quotes, and keep
internal whitespace as it is, because a spec directory name can contain spaces.

Match it against the candidates by this ordered rule set, stopping at the first rule that yields
exactly one match:

1. Exact directory-name match.
2. Exact four-digit id match.
3. Case-insensitive substring match on the directory name.

- If a rule yields more than one match, stop and create or modify no file. Print the Ambiguous spec id
  template.
- If no rule yields any match, stop and create or modify no file. Print the No spec matches template.
- Otherwise exactly one rule yields exactly one match, and that directory is the target `{dir}`.

### Step 2: Run the resolver before writing anything

Run `dotnet run .claude/commands/spec/resolve_route.cs -- specs/ --spec "{dir}"`. Never read the
approval marker files yourself, and never fall back to them: the resolver is the only source of the
spec's flow, steps and next step.

It is a tooling fault when the exit status is not `0`, or no line beginning `route-report: ` parses.
Print the message below, with `{state}` as `exited {code}`, `no route-report line` or
`route-report line does not parse`, and print nothing else. Leave `specs/.current-spec` exactly as it
was, and create or modify no file. `{command}` is `/spec:switch`.

`{command} could not run the route resolver (.claude/commands/spec/resolve_route.cs): {state}. This is a tooling fault, not a fault in the spec — re-run after restoring the script.`

### Step 3: Switch

Otherwise write `{dir}` to `specs/.current-spec`.

### Step 4: Show the spec's status

Show the newly active spec as `/spec:status` shows a spec: its flow (name, `source`, and whether
`definition_present`), `valid`, each step's `name`, `kind` and `state`, then any `violations`,
`tampered`, `route_change`, `stray_markers` and `out_of_order`. Report `.issue-number` and the
`tasks.md` progress (count of `- [x]` against all `- [ ]` and `- [x]` task lines) when they exist.
When the report's `stop` field is not `null`, print its `message` after the status and carry on.

Close with the next-step line, filled from the report's `next`. It is the Next-step line template.

The templates this command prints:

<!-- routing-text:templates:begin -->
| Template | Printed by | Text |
| --- | --- | --- |
| Next-step line | the closing instructions, `/spec:status`, `/spec:switch`, `/spec:review`'s closing reminder, the route-changed template (0091), `/spec:new`'s post-check (0091), and the three templates below that end in it | ``Next: {next.commands joined with " then "}.``, or ``No next step: the slip is invalid (see violations).`` when `next.commands` is empty. Then ``Unattended: {next.unattended_driver}.`` when it is not `null`, and one ``Optional: {detour.command}, then return to {detour.return_to}.`` for each entry of `next.detours`, in order, all on the same line. With empty `next.commands` there is no driver and no detour |
| Ambiguous spec id | `/spec:switch` | ``Ambiguous spec id '{arg}' — matches: {list of matching directory names}. Re-run with the full directory name.`` Identical to `show-me.md:45-46` |
| No spec matches | `/spec:switch` | ``No spec matches '{arg}'. Run /spec:status to list specs.`` Identical to `show-me.md:48` |

<!-- routing-text:templates:end -->
