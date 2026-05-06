# 0027 - Improve Brighter Performance with Span\<T\>

**Created:** 2026-05-01

**Reference:** https://learn.microsoft.com/en-us/dotnet/standard/memory-and-spans/

## Overview

Improve Brighter performance by adopting `Span<T>`, `ReadOnlySpan<T>`, `Memory<T>`, and related types to reduce heap allocations, avoid unnecessary copies, and leverage stack-based or pooled buffers in hot paths.

## Status

- [ ] Requirements (`requirements.md`)
- [ ] Design (`design.md` / ADR)
- [ ] Tasks (`tasks.md`)
- [ ] Implementation
- [ ] Review

## Next Steps

1. Run `/spec:requirements` to define what needs to change and why
2. Run `/spec:design` to create a technical design / ADR
3. Run `/spec:tasks` to break the work into implementable tasks
4. Run `/spec:implement` to begin TDD implementation
