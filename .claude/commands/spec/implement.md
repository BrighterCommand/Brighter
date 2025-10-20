---
allowed-tools: Bash(cat:*), Bash(test:*), Bash(touch:*), Bash(ls:*),  Bash(echo:*), Read, Write, Glob
description: Start implementation from approved tasks
argument-hint: [phase-number]
---

## Context

Current spec directory: specs/

## Your Task

First, gather context by:
1. Reading specs/.current_spec to determine the active specification directory
2. Checking for .tasks-approved file in that directory
3. Reading tasks.md to see current task list

Then:

1. Verify all phases are approved
2. If phase number provided ($ARGUMENTS), focus on that phase
3. Display current incomplete tasks
4. Create an implementation session log
5. Guide user to:
   - Work on tasks sequentially
   - Update task checkboxes as completed
   - Commit changes regularly
6. Remind about using Write tool to update tasks.md

Start implementing based on the task list!