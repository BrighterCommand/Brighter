# Spec 0038: Outbox stale message telemetry

**Created:** 2026-10-11
**GitHub Issue:** [#4489](https://github.com/BrighterCommand/Brighter/issues/4489)
**Docs PR:** [BrighterCommand/Docs#207](https://github.com/BrighterCommand/Docs/pull/207)

## Feature

Telemetry that reports outbox messages that have stayed outstanding for longer than a configured age, so an
operator can see a message that was never sent, including in an app that runs without the outbox sweeper. This is
the first of the two parts proposed on issue #4489; the channel that notifies failed clears is not part of this
spec.

## Status

- [x] Requirements (`/spec:requirements`), approved 2026-10-11, revised on 2026-10-13 after the maintainer's review on PR #4598 and approved again; revised on 2026-10-21 for PR #4613 and approved again
- [x] Design / ADR (`/spec:design`): ADR `0085-outbox-stale-message-telemetry`, written and reviewed on 2026-10-11, then renumbered from ADR 0084 and reviewed again on 2026-10-12 after ADR `0084-outbox-metrics` merged; revised on 2026-10-13 after the maintainer's review on PR #4598, together with the new ADR `0086-outbox-per-topic-stale-count`; both Accepted on 2026-10-21, when the maintainer asked on PR #4598 to mark them Accepted (`/spec:approve design`)
- [x] Adversarial review rounds (requirements: 4 passes, then 4 more after the maintainer's review on PR #4598; design: 4 passes before the renumbering, 4 after it, 3 more after the maintainer's review on PR #4598, 2 more after the overall count tests moved to the test generator, and 1 more after the Firestore facts moved to the Firestore emulator; tasks: 11 passes)
- [x] Task breakdown (`/spec:tasks`), approved 2026-10-22
- [ ] Implementation