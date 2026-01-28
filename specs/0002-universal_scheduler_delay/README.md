# Universal Scheduler Delay

**Created:** 2026-01-28

## Status

- [x] Requirements (`requirements.md`) - **Ready for review**
- [ ] Design (`design.md`) - Use `/spec:design` to create
- [ ] Tasks (`tasks.md`) - Use `/spec:tasks` to create
- [ ] Implementation - Use `/spec:implement` to start

## Overview

Add universal scheduler support for delayed message delivery across all Brighter transports. Currently, scheduler 
integration is inconsistent - some producers use schedulers, some use native transport delays, and the in-memory 
transport uses direct timers. This feature standardizes delay handling so all producers use the configurable 
scheduler system, with `InMemoryScheduler` as the default and all consumers without native support for delay, use 
the producer to delay messages.

## Files

| File | Description | Status |
|------|-------------|--------|
| `requirements.md` | Feature requirements and acceptance criteria | Ready for review |
| `design.md` | Technical design and architecture decisions | Not started |
| `tasks.md` | Implementation task breakdown | Not started |
