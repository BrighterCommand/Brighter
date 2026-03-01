# Spec 0022: Defer Message Action Backstop Handler

**Created**: 2026-02-23
**Status**: Requirements

## Overview

Create `DeferMessageOnErrorAttribute` / `DeferMessageOnErrorAsyncAttribute` and their corresponding handlers, completing the set of declarative backstop attributes alongside `RejectMessageOnError` and `DontAckOnError`. The attribute catches unhandled exceptions and throws `DeferMessageAction` with a configurable delay, causing the message pump to requeue the message for later retry.

## Phases

- [x] Requirements (`requirements.md`) — `/spec:requirements`
- [ ] Design (`design.md` / ADR) — `/spec:design`
- [ ] Tasks (`tasks.md`) — `/spec:tasks`
- [ ] Implementation — `/spec:implement`
