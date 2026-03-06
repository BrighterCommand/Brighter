# Spec 0023: Pipeline Validation at Startup

**GitHub Issue**: [#2176 - Enable pipeline validation upon startup](https://github.com/BrighterCommand/Brighter/issues/2176)
**Created**: 2026-02-24
**Branch**: `pipeline_validation`

## Problem

Setting up Brighter pipelines correctly is error-prone, especially for newcomers. Common mistakes include mixing sync and async handlers, subscriptions, policies, etc. Currently these issues are only discovered at runtime when a message is sent, leading to slow feedback cycles.

## Goal

Validate pipelines at startup so configuration errors are caught immediately, reducing the "build, run, see problem, fix, try again" cycle.

## Status Checklist

- [ ] Requirements (`requirements.md`) — use `/spec:requirements`
- [ ] Design (`design.md` / ADR) — use `/spec:design`
- [ ] Tasks (`tasks.md`) — use `/spec:tasks`
- [ ] Implementation — use `/spec:implement`
- [ ] Review & merge
