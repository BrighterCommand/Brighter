---
allowed-tools: Read, Glob, Bash(grep:*), Skill
description: Accept the selected ADRs of a spec (a sub-flow command of the design approval)
argument-hint: [questions|actions] "{spec}" {step} {artefacts} [--select {word}]... [--answer {key}={value}]...
---

# Accept ADRs

You are the `/adr:accept` command, `{command}` below. `/spec:approve` runs you for a `design` step, first with `questions` and then with `actions`. You can also be run by hand. You make each selected ADR `Accepted`, in its frontmatter and in its body `## Status` together, and you know nothing of slips.

## Arguments

```
[questions|actions] "{spec}" {step} {artefacts} [--select {word}]... [--answer {key}={value}]...
```

- With `questions` or `actions`, run only that section.
- With no mode word, run `## Questions`, then `## Actions`.
- `{spec}` is the spec directory. `{artefacts}` names the file in it that lists the ADRs, normally `.adr-list`. `{step}` is not used. `--answer` arguments are not used.
- If `{spec}` or `{artefacts}` is missing, print `sub-flow-failed: /adr:accept {pass}: usage` and stop.

## ADR selection rule

The ADRs you act on are chosen by this rule. Its canonical text lives in [README.md](README.md).

<!-- routing-text:adr-selection-rule:begin -->
1. Read `{spec}/.adr-list`. Each non-blank line, trimmed, names one ADR. A leading `docs/adr/` is removed, because both forms occur in existing specs. The path is `docs/adr/{name}`.
2. With no `--select`, select every ADR. With one, select the line whose stem equals the word. Failing that, select the lines whose name starts with `{word}-`.
3. Fail on an empty list, more than one `--select`, no match, more than one match, or a selected file that does not exist.
<!-- routing-text:adr-selection-rule:end -->

`--select` words are the selector words of the rule. Read the list from `{spec}/{artefacts}`.

## Questions

Read-only. Write nothing and ask nothing. Apply the ADR selection rule, then check each selected ADR:

1. A selection fault (the rule's third step): print `sub-flow-failed: /adr:accept questions: {what the rule found}` and stop.
2. A selected ADR with no frontmatter: fail with the reason `run write_adr_metadata docs/adr/{name} init first`.
3. Every selected ADR must pass the six checks of `write_adr_metadata` Step 3, so fail with the reason naming the ADR, the check and the fix, if any of these hold:
   - one of the seven keys `id, title, status, author, created, summary, tags` is missing;
   - `id` differs from the filename stem;
   - `status` is not one of `Proposed, Accepted, Deprecated, Superseded`;
   - `created` is not a valid `YYYY-MM-DD` date;
   - a tag is outside the controlled taxonomy, or there are fewer than one or more than four tags;
   - the frontmatter `status` word differs from the body `## Status` word, **unless** one side is already `Accepted`. The Actions section sets both, so that mismatch is not a fault.

Print `sub-flow-done: /adr:accept questions`.

## Actions

For each selected ADR, in list order:

1. Skip it when its frontmatter `status` and its body `## Status` are both `Accepted`.
2. Skip it when it is already `Superseded` by another selected ADR.
3. Otherwise run `write_adr_metadata docs/adr/{name} status Accepted` through the Skill tool, and confirm that it passes its checks.

If `write_adr_metadata` does not pass its checks for an ADR, or raises a question, answer nothing and print `sub-flow-failed: /adr:accept actions: write_adr_metadata did not pass its checks for docs/adr/{name}`, then stop.

When every ADR is done, print `approved-artefacts: ` followed by the path `docs/adr/{name}` of **every** selected ADR, joined with `,`. List an ADR you skipped too. Then print `sub-flow-done: /adr:accept actions` as the last line.

Running the command again with the same arguments changes nothing further.
