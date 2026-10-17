# Spec 0038: Outbox stale message telemetry

**Created:** 2026-10-11
**GitHub Issue:** [#4489](https://github.com/BrighterCommand/Brighter/issues/4489)

## Feature

Telemetry that reports outbox messages that have stayed outstanding for longer than a configured age, so an
operator can see a message that was never sent, including in an app that runs without the outbox sweeper. This is
the first of the two parts proposed on issue #4489; the channel that notifies failed clears is not part of this
spec.

## Status

- [x] Requirements (`/spec:requirements`), approved 2026-10-11, revised on 2026-10-13 after the maintainer's review on PR #4598 and approved again
- [x] Design / ADR (`/spec:design`): ADR `0085-outbox-stale-message-telemetry`, written and reviewed on 2026-10-11, then renumbered from ADR 0084 and reviewed again on 2026-10-12 after ADR `0084-outbox-metrics` merged; revised on 2026-10-13 after the maintainer's review on PR #4598, together with the new ADR `0086-outbox-per-topic-stale-count`; both stay Proposed until the maintainer agrees on the pull request
- [x] Adversarial review rounds (requirements: 4 passes, then 4 more after the maintainer's review on PR #4598; design: 4 passes before the renumbering, 4 after it, and 3 more over both ADRs after the maintainer's review on PR #4598)
- [ ] Task breakdown (`/spec:tasks`)
- [ ] Implementation