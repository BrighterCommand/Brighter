# DontAckAction

**Created:** 2026-02-16
**Status:** Design

## Progress Checklist

- [x] Requirements (`requirements.md`) - Use `/spec:requirements`
- [ ] Design (`design.md`) - Use `/spec:design`
  - [ ] [ADR 0038 - Don't Ack Action](../../docs/adr/0038-dont-ack-action.md)
- [ ] Tasks (`tasks.md`) - Use `/spec:tasks`
- [ ] Implementation - Use `/spec:implement`

## Description

Add a DontAckAction exception that signals the message pump to not acknowledge a message, allowing it to be re-presented on the next loop iteration. Includes FeatureSwitchAttribute integration and a DontAckOnErrorAttribute for catching handler errors.
