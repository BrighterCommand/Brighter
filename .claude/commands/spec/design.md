---
allowed-tools: Bash(cat:*), Bash(test:*), Bash(touch:*), Bash(ls:*), Bash(echo:*), Read, Write, Glob
description: Create technical design specification
---

## Context

Current spec directory: specs/

## Your Task

First, read specs/.current_spec to determine the active specification directory.

1. Verify requirements are approved (look for .requirements-approved file in the spec directory)
2. If not approved, inform user to complete requirements first
3. If approved, create/update design.md with:
   - Architecture overview (with diagrams)
   - Technology stack decisions
   - Data model and schema
   - API design
   - Security considerations
   - Performance considerations
   - Deployment architecture
   - Technical risks and mitigations
4. Use ASCII art or mermaid diagrams where helpful

Use the Write tool to create the design document.