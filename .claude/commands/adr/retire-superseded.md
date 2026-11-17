---
allowed-tools: Read, Glob, Bash(grep:*), AskUserQuestion, Skill
description: Retire the older ADRs that the selected ADRs supersede (a sub-flow command of the design approval)
argument-hint: [questions|actions] "{spec}" {step} {artefacts} [--select {word}]... [--answer {key}={value}]...
---

# Retire Superseded ADRs

You are the `/adr:retire-superseded` command, `{command}` below. `/spec:approve` runs you for a `design` step, first with `questions` and then with `actions`. You can also be run by hand. You learn which earlier ADRs each selected ADR supersedes or retires, and then supersede or deprecate them through `write_adr_metadata`. You know nothing of slips.

## Arguments

```
[questions|actions] "{spec}" {step} {artefacts} [--select {word}]... [--answer {key}={value}]...
```

- With `questions` or `actions`, run only that section.
- With no mode word, run `## Questions`, then `## Actions`. `## Actions` uses the answers that `## Questions` printed in the same run, plus any `--answer` arguments given.
- `{spec}` is the spec directory. `{artefacts}` names the file in it that lists the ADRs, normally `.adr-list`. `{step}` is not used.
- An answer key is `/adr:retire-superseded.{stem}`, where `{stem}` is the selected ADR's filename stem. Its value is `none`, or a `,`-list of `supersede:{stem}` and `deprecate:{stem}` that names the retirement targets.
- If `{spec}` or `{artefacts}` is missing, print `sub-flow-failed: /adr:retire-superseded {pass}: usage` and stop.

## ADR selection rule

The ADRs you act on are chosen by this rule. Its canonical text lives in [README.md](README.md).

<!-- routing-text:adr-selection-rule:begin -->
1. Read `{spec}/.adr-list`. Each non-blank line, trimmed, names one ADR. A leading `docs/adr/` is removed, because both forms occur in existing specs. The path is `docs/adr/{name}`.
2. With no `--select`, select every ADR. With one, select the line whose stem equals the word. Failing that, select the lines whose name starts with `{word}-`.
3. Fail on an empty list, more than one `--select`, no match, more than one match, or a selected file that does not exist.
<!-- routing-text:adr-selection-rule:end -->

`--select` words are the selector words of the rule. Read the list from `{spec}/{artefacts}`.

## Message templates

The text of the supersession question. Its canonical text lives in [README.md](README.md).

<!-- routing-text:templates:begin -->
| Template | Text |
| --- | --- |
| Supersession question | ``Does `{adr}` supersede or retire an earlier ADR?`` Options `No`, then `Supersedes {stem}` for each of the first three ADRs its text names that exist, are not `{adr}` and are not already `Superseded` by a different ADR. The tool's free-text answer is read as one or more of `supersede {stem}` or `deprecate {stem}`, separated by commas |
<!-- routing-text:templates:end -->

## Questions

Write nothing. Apply the ADR selection rule, then, for each selected ADR `{adr}` in list order, decide its answer:

1. A selection fault (the rule's third step): print `sub-flow-failed: /adr:retire-superseded questions: {what the rule found}` and stop.
2. Read `{adr}`'s `## Status` and `## References` for the ADRs named after "Supersedes", in the order the text names them. Keep a name only when its file exists in `docs/adr/`, it is not `{adr}`, and it is not already `Superseded` by a different ADR.
3. **No earlier ADR named after "Supersedes"**: the answer is `none`. Ask nothing.
4. **Exactly one existing ADR named after "Supersedes", with no section mark (`§`)**: the answer is `supersede:{stem}`. Ask nothing, unless that target is already `Superseded` by a different ADR, which makes this case a question.
5. **Every other ADR** gets the supersession question, with the `Supersession question` template above through `AskUserQuestion`. Offer `No`, then `Supersedes {stem}` for each of the first three kept names. The user may type free text instead: one or more of `supersede {stem}` or `deprecate {stem}`, separated by commas. Store it as the `,`-list of `supersede:{stem}` and `deprecate:{stem}`.
   - An answer that names a missing file, `{adr}` itself, or a target already `Superseded` by a different ADR repeats the question with the reason.
   - A question the user dismisses fails with the reason `question not answered`.
6. Each retirement target must pass the six checks of `write_adr_metadata` Step 3, read-only. Fail with the reason naming the ADR, the check and the fix if one is missing a key, has an `id` that differs from its stem, has a `status` outside the vocabulary, has an invalid `created`, has a tag outside the taxonomy, or has a frontmatter `status` word that differs from its body `## Status` word. A mismatch where one side is already `Superseded` or `Deprecated` is not a fault, because the Actions section sets both.

Print one line per selected ADR, each alone on a line: `answer: /adr:retire-superseded.{stem}={value}`. Then print `sub-flow-done: /adr:retire-superseded questions`.

## Actions

Asks nothing. For each selected ADR `{adr}` with its answer from `--answer` or from this run, and for each target in the answer, in order:

1. `supersede:{stem}`: skip the target when it is already `Superseded` by `{adr}`. Otherwise run `write_adr_metadata docs/adr/{stem}.md supersede --by {adr stem}` through the Skill tool.
2. `deprecate:{stem}`: skip the target when it is already `Deprecated`. Otherwise run `write_adr_metadata docs/adr/{stem}.md deprecate` through the Skill tool.
3. An answer of `none` writes nothing.

If `write_adr_metadata` does not pass its checks for a target, or raises a question, answer nothing and print `sub-flow-failed: /adr:retire-superseded actions: write_adr_metadata did not pass its checks for docs/adr/{stem}.md`, then stop.

Print `sub-flow-done: /adr:retire-superseded actions` as the last line.

Running the command again with the same answers changes nothing further.
