# Spec 0040 — Routing slips for the spec workflow

**Created:** 2026-10-27

## Feature

Let the spec workflow support more than one route to an approved `tasks.md`. A spec carries a
`.flow` file: a **routing slip** copied from a named flow definition, listing the steps up to
`tasks.md`, the artefact each produces and the approval marker each needs. Today's route becomes the
default `feature` flow, and a spec with no `.flow` behaves exactly as it does now. The `/spec:*`
commands follow the slip instead of a built-in route, so adding a flow means adding a slip
definition, not editing every command. The acceptance test is a `light` flow (design → tasks, no
requirements), stated as a slip and verified on a fixture but not shipped.

Prerequisite for spec 0039 (`/modules:*`, [#4659](https://github.com/BrighterCommand/Brighter/issues/4659)),
which will declare a `modules` slip. Tracked in
[GitHub issue #4661](https://github.com/BrighterCommand/Brighter/issues/4661).

## Status

- [ ] Requirements (`/spec:requirements`)
- [ ] Design / ADR (`/spec:design`)
- [ ] Adversarial review rounds
- [ ] Task breakdown (`/spec:tasks`)
- [ ] Implementation (`/spec:implement`)

## Next steps

Run `/spec:requirements 4661` to draft `requirements.md` for this spec.
