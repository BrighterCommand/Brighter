# Pipeline Cache Type Key

**Created:** 2026-06-18
**Tracking issue:** [BrighterCommand/Brighter#4192](https://github.com/BrighterCommand/Brighter/issues/4192)
**Branch:** `fix/pipeline-attribute-cache-key-collision`

## Summary

`PipelineBuilder<TRequest>`, `TransformPipelineBuilder`, and `TransformPipelineBuilderAsync` memoise reflection-derived pipeline metadata in process-global static dictionaries keyed by the **simple class name** (`GetType().Name`). Two types sharing a simple name in different namespaces collide on one cache slot — the first built wins and every later one silently reuses its metadata. Fix: key the mementos by `Type`.

## Status

- [x] Requirements (`/spec:requirements`)
- [x] Design (`/spec:design`)
- [x] Tasks (`/spec:tasks`)
- [x] Implementation (`/spec:implement`, interactive TDD via `/test-first`)
- [ ] PR (open: #4194)

## Notes

- Interactive TDD chosen over Ralph: small, subtle change where per-test human review earns its keep.
- ADR 0064 (`docs/adr/0064-pipeline-cache-type-key.md`) records the decision to key the mementos by runtime `Type`.
- Maintainer greenlit #4192; PR raised as #4194.
