---
allowed-tools: Read, Glob, Bash(grep:*)
description: Report which ADRs were approved, superseded and deprecated (a sub-flow command of the design approval)
argument-hint: [questions|actions] "{spec}" {step} {artefacts} [--select {word}]... [--answer {key}={value}]...
---

# Report an ADR Approval

You are the `/adr:report-approval` command, `{command}` below. `/spec:approve` runs you for a `design` step, first with `questions` and then with `actions`. You can also be run by hand. You print the outcome of the approval and write nothing, and you know nothing of slips.

## Arguments

```
[questions|actions] "{spec}" {step} {artefacts} [--select {word}]... [--answer {key}={value}]...
```

- With `questions` or `actions`, run only that section.
- With no mode word, run `## Questions`, then `## Actions`.
- `{spec}` is the spec directory. `{artefacts}` names the file in it that lists the ADRs, normally `.adr-list`. `{step}` is not used.
- `--answer` arguments with the key `/adr:retire-superseded.{stem}` carry the retirement answers. Their value is `none`, or a `,`-list of `supersede:{stem}` and `deprecate:{stem}`.
- If `{spec}` or `{artefacts}` is missing, print `sub-flow-failed: /adr:report-approval {pass}: usage` and stop.

## ADR selection rule

The ADRs you report on are chosen by this rule. Its canonical text lives in [README.md](README.md).

<!-- routing-text:adr-selection-rule:begin -->
1. Read `{spec}/.adr-list`. Each non-blank line, trimmed, names one ADR. A leading `docs/adr/` is removed, because both forms occur in existing specs. The path is `docs/adr/{name}`.
2. With no `--select`, select every ADR. With one, select the line whose stem equals the word. Failing that, select the lines whose name starts with `{word}-`.
3. Fail on an empty list, more than one `--select`, no match, more than one match, or a selected file that does not exist.
<!-- routing-text:adr-selection-rule:end -->

`--select` words are the selector words of the rule. Read the list from `{spec}/{artefacts}`.

## Message templates

The text of the report. Its canonical text lives in [README.md](README.md).

<!-- routing-text:templates:begin -->
| Template | Text |
| --- | --- |
| Report | ``ADRs approved: {selected stems joined with ", "}.`` then ``ADRs superseded: {old} by {new}, ….`` or ``ADRs superseded: none.``, then ``ADRs deprecated: {old}, ….`` or ``ADRs deprecated: none.``, then ``Commit the approved ADRs and the regenerated docs/adr/index.md to git.`` |
<!-- routing-text:templates:end -->

## Questions

Write nothing and ask nothing. Apply the ADR selection rule. A selection fault fails the pass with the reason the rule found: print `sub-flow-failed: /adr:report-approval questions: {what the rule found}` and stop.

Otherwise print `sub-flow-done: /adr:report-approval questions`.

## Actions

Write nothing. Apply the ADR selection rule. A selection fault fails the pass: print `sub-flow-failed: /adr:report-approval actions: {what the rule found}` and stop.

Print the `Report` template, one line at a time, each alone on a line:

1. `ADRs approved:` followed by the stem of every selected ADR, joined with `, `, and a full stop.
2. `ADRs superseded:` followed by `{old} by {new}` for each `supersede:{old}` target in the `--answer` values, where `{new}` is the stem in the answer's key, joined with `, `, and a full stop. When there are none, print `ADRs superseded: none.`
3. `ADRs deprecated:` followed by `{old}` for each `deprecate:{old}` target, joined with `, `, and a full stop. When there are none, print `ADRs deprecated: none.`
4. `Commit the approved ADRs and the regenerated docs/adr/index.md to git.`

Then print `sub-flow-done: /adr:report-approval actions` as the last line.

Running the command again with the same arguments prints the same report.
