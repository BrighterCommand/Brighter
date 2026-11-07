---
allowed-tools: Bash(awk:*)
description: Regenerate the derived ADR index docs/adr/index.md (a sub-flow command of the design approval)
argument-hint: [questions|actions] ["{spec}" {step} {artefacts} [--select {word}]... [--answer {key}={value}]...]
---

# Regenerate the ADR Index

You are the `/adr:regenerate-index` command, `{command}` below. `/spec:approve` runs you for a `design` step, first with `questions` and then with `actions`. You can also be run by hand with no arguments. You rebuild `docs/adr/index.md` from the ADR frontmatter, and you know nothing of slips.

## Arguments

```
[questions|actions] ["{spec}" {step} {artefacts} [--select {word}]... [--answer {key}={value}]...]
```

- With `questions` or `actions`, run only that section.
- With no mode word, run `## Questions`, then `## Actions`.
- You use none of the other arguments. Ignore them.

## Questions

Nothing to ask and nothing to check. Print `sub-flow-done: /adr:regenerate-index questions`.

## Actions

`docs/adr/index.md` is a regenerable cache. Never hand-edit it. Run, from the repository root:

```bash
awk -f .claude/commands/adr/generate_adr_index.awk docs/adr/[0-9]*.md > docs/adr/index.md
```

If awk exits with a non-zero code, print `sub-flow-failed: /adr:regenerate-index actions: awk exited {code}` and stop.

Otherwise print `sub-flow-done: /adr:regenerate-index actions` as the last line.

Running the command again changes nothing further, because the generator is deterministic.
