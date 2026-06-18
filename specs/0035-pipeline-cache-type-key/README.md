# Pipeline Cache Type Key

**Created:** 2026-06-18
**Tracking issue:** [BrighterCommand/Brighter#4192](https://github.com/BrighterCommand/Brighter/issues/4192)
**Branch:** `fix/pipeline-attribute-cache-key-collision`

## Summary

`PipelineBuilder<TRequest>`, `TransformPipelineBuilder`, and `TransformPipelineBuilderAsync` memoise reflection-derived pipeline metadata in process-global static dictionaries keyed by the **simple class name** (`GetType().Name`). Two types sharing a simple name in different namespaces collide on one cache slot — the first built wins and every later one silently reuses its metadata. Fix: key the mementos by `Type`.

## Status

- [x] Requirements (`/spec:requirements`)
- [ ] Design (`/spec:design`)
- [ ] Tasks (`/spec:tasks`)
- [ ] Implementation (`/spec:implement`, interactive TDD via `/test-first`)
- [ ] PR (gated on maintainer greenlight on #4192)

## Notes

- Interactive TDD chosen over Ralph: small, subtle change where per-test human review earns its keep.
- No ADR — internal cache-key bugfix (precedent: #4061, #4100/#4101 carried code + test, no ADR).
- PR held until a maintainer signals openness on #4192; spec + tests authored in parallel meanwhile.
