# Spec 0031: Test Naming Conventions

**Created:** 2026-06-09

## Status

- [x] Requirements
- [x] ~~Design (ADR)~~ — **N/A**: this is a correction to match an existing, already-documented convention (`.agent_instructions/testing.md`), not a new design decision. No trade-offs to record. The few "how" tactics live in `tasks.md`.
- [x] Tasks (approved 2026-06-09)
- [ ] Implementation
- [ ] Verified
- [ ] Reviewed

## Description

Rename the BoxProvisioning tests to follow Brighter's authoritative test-naming convention
(`.agent_instructions/testing.md`): test classes `[Behavior]Tests`, test methods and files
`When_[condition]_should_[expected_behavior]`. Pure rename/refactor — no test behavior changes.
See `requirements.md`. Linked issue: #4157.
