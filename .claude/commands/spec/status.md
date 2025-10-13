---
allowed-tools: Bash(cat:*), Bash(grep:*), Bash(test:*), Bash(find:*), Bash(touch:*), Bash(ls:*),  Bash(echo:*), Read, Write, Glob
description: Show all specifications and their status
---

## Gather Status Information

Current spec directory: specs/

## Your Task

First, gather information by:
1. Reading specs/.current_spec to determine the active specification
2. Using Glob to find all spec directories under specs/
3. Using Bash ls command to check each spec directory for phase files and approval markers
4. Using Grep to check task progress in any tasks.md files

Then,

Present a clear status report showing:
1. All specifications with their IDs and names
2. Current active spec (highlighted)
3. Phase completion status for each spec
4. Task progress percentage if applicable
5. Recommended next action for active spec