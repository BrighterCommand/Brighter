# Spec 0037 — `/spec:show-me`

**Created:** 2026-09-18

## Feature

A `/spec:show-me` command: summarise what a spec actually changed, in a form a
reviewer (or the owner, or a teammate) can read without opening the ADRs,
`tasks.md`, or every commit message. Bundles a risk-based merge assessment
(blast radius, breaking-change count, review-findings history, regression
results) as part of the same command, rather than leaving "is this safe to
merge" an implicit per-session judgement call.

Originated as a follow-up noted while wrapping up spec 0036
(`specs/0036-scoped-lifetime-per-pipeline/PROMPT.md` § "FOLLOW-UP (owner,
2026-09-18)"). Not spec-0036-specific — shared-skill work, like
[GitHub issue #4357](https://github.com/BrighterCommand/Brighter/issues/4357)
(the sibling "switching gears" follow-up).

## Status

- [ ] Requirements (`/spec:requirements`)
- [ ] Design / ADR (`/spec:design`)
- [ ] Adversarial review rounds
- [ ] Task breakdown (`/spec:tasks`)
- [ ] Implementation (`/spec:implement`)

## Next steps

Run `/spec:requirements` to draft `requirements.md` for this spec.
