# TimedOutboxArchiver Sync Fallback

**Spec ID:** 0026
**Created:** 2026-04-15
**Issue:** [#3670](https://github.com/BrighterCommand/Brighter/issues/3670)

## Summary

Fix `TimedOutboxArchiver` so it falls back to the sync `Archive` method when only a sync outbox implementation is registered, instead of unconditionally calling `ArchiveAsync` and failing.

## Status

- [ ] Requirements (`requirements.md`)
- [ ] Design (`design.md`)
- [ ] Tasks (`tasks.md`)
- [ ] Implementation
- [ ] Review
