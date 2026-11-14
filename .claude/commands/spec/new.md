---
allowed-tools: Bash(dotnet run .claude/commands/spec/resolve_route.cs -- specs/:*), Bash(cat:*), Bash(test:*), Bash(touch:*), Bash(ls:*),  Bash(echo:*), Read, Write, Glob
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

Only when `valid` is `true` carry on with the steps below.

1. Determine the next ID number (format: 001, 002, etc.)
2. Create directory: `specs/[ID]-$ARGUMENTS/`
3. Update `specs/.current-spec` with the new spec directory name ([ID]-$ARGUMENTS)
4. Create a README.md in the new directory with:
   - Feature name
   - Creation date
   - Initial status checklist
5. Inform the user about next steps

Use the Bash tool to create directories and files as needed.

The templates this command prints:

<!-- routing-text:templates:begin -->
| Template | Printed by | Text |
| --- | --- | --- |
| Flow not usable | `/spec:new` | ``Cannot start a spec: flow `feature` is not usable. {violations[0].message} Fix `.claude/flows/feature.flow` or `.claude/flows/approvals.md` first.`` |

<!-- routing-text:templates:end -->
