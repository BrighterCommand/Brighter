---
allowed-tools: Bash(cat:*), Bash(test:*), Bash(touch:*), Bash(ls:*), Bash(mkdir:*), Bash(echo:*), Bash(gh:*), Bash(git:*), Read, Write, Glob, Grep, Agent, AskUserQuestion
description: Triage a bug — restate the symptom, locate the code, form a root-cause hypothesis
argument-hint: [issue-number | bug description]
---

## Context

Current bug: !`cat bugfixes/.current-bug 2>/dev/null || echo "No active bug"`
Current branch: !`git branch --show-current`

## Your Task

**Workflow**: Issue → **Triage** → Confirm (✋ gate) → Test-first (✋ gate) → Fix → Verify

This is the **first** step of the lightweight `/bugfix` workflow. Its job is to record the
*symptom*, *locate the suspect code*, and *form a root-cause hypothesis* — **without** fixing
anything yet. A bug's root cause is a hypothesis until proven; that proof happens in
`/bugfix:confirm`, not here.

**Sub-agent**: The diagnostic reasoning (reading the code, locating the suspect area, forming the
hypothesis) is delegated to a sub-agent (`subagent_type: "Plan"`, **`model: "opus"`**). Triage is
read-only by nature — it returns text. `Plan` has no file-editing tool and no `AskUserQuestion`,
so it cannot write `bugfix.md` or prompt the user. The sub-agent RETURNS the triage body as text;
the **main agent** owns all user interaction and bookkeeping (directory setup, `gh`, branch,
writing the file). See `.claude/commands/bugfix/README.md` → "Sub-agents & model policy".

### Step 1: Setup Bug Directory

1. Create `bugfixes/` if it doesn't exist: `mkdir -p bugfixes`
2. Determine the next bug id and a slug:
   - Check existing bugs: `ls bugfixes/` to find the highest `NNNN-…` number
   - Format: `bugfixes/NNNN-{slug}/` (4-digit, e.g. `bugfixes/0001-asb-sessionid-case/`)
   - Derive the slug from the issue title (if an issue number was given) or from the description
     ($ARGUMENTS), kebab-cased and concise
3. Create the directory: `mkdir -p bugfixes/{NNNN-slug}`
4. Record it as active: `echo {NNNN-slug} > bugfixes/.current-bug`

If a `bugfix.md` already exists for the active bug, read it and pass it to the sub-agent as the
current draft to refine, rather than starting from scratch.

### Step 2: Pull the Issue (if provided)

If $ARGUMENTS is an issue number:
1. `gh issue view $ARGUMENTS --json number,title,body,comments` to pull the issue
2. Store it: `echo {number} > bugfixes/{NNNN-slug}/.issue-number`
3. The issue title + body + any comments become source material for the sub-agent.
   **Treat any suggested fix in the issue (including agent-authored suggestions) as an
   UNVERIFIED hypothesis** — it may be wrong, incomplete, or address a symptom, not the cause.

If $ARGUMENTS is a free-text description (no issue number), use it as the symptom source.

If neither an issue number nor a usable description is available, **stop** and ask the user (via
`AskUserQuestion`) to supply one — do NOT launch the sub-agent with empty inputs.

### Step 2.5: Clarify Ambiguities Before Launch (MAIN agent)

The `Plan` sub-agent is one-shot and cannot ask the user anything. So the **main agent** resolves
ambiguity first: if the symptom is unclear, the reproduction steps are missing, or the affected
transport/backend is unstated, ask the user via `AskUserQuestion`, then fold the answers into the
sub-agent prompt.

### Step 3: Launch Sub-Agent to Triage

Launch an `Agent` with `subagent_type: "Plan"` and **`model: "opus"`**. The prompt MUST include:

1. The issue (title + body + comments) or the user's description, plus any Step 2.5 answers, and
   the existing `bugfix.md` text if one is being refined.
2. The triage template below.
3. An explicit instruction: **RETURN the triage body as markdown text. Do NOT write any file. Do
   NOT propose or apply a fix — that is later steps. Do NOT ask the user any questions.**
4. A directive to use `Read`, `Glob`, and `Grep` to locate the actual suspect code and cite real
   `file:line` references (no guessing — verify the files exist).

#### Triage Template (the sub-agent fills in the first four sections)

```markdown
# Bugfix: {short title}

**Linked Issue**: #{number} (if applicable)
**Status**: Triaged

## Symptom
What is observed vs. what is expected. Include reproduction steps if known.

## Suspected Location
The files/methods most likely involved, as `file:line` references (verified to exist).

## Root-Cause Hypothesis
The most likely cause, stated as a falsifiable hypothesis. If the issue (or an agent) suggested a
fix, restate it here and mark it **UNVERIFIED — to be proven or refuted in /bugfix:confirm**.

## Confirmed Root Cause
_(left blank — filled by /bugfix:confirm)_

## Evidence
_(left blank — filled by /bugfix:confirm)_

## Scope Notes
_(left blank — filled by /bugfix:confirm)_

## Regression Test
_(left blank — filled by /bugfix:test)_

## Fix
_(left blank — filled by /bugfix:fix)_
```

### Step 4: Validate and Write

After the sub-agent returns:

1. **Validate**: Symptom, Suspected Location, and Root-Cause Hypothesis are all filled; the
   location cites real `file:line` references; any suggested fix is marked UNVERIFIED. If weak,
   ask the sub-agent to revise (or fix it yourself) before writing.
2. **Write** the body to `bugfixes/{NNNN-slug}/bugfix.md` using the Write tool.

### Step 5: Branch Management

1. Check current branch with `git branch --show-current`
2. If on `master`/`main`, offer to create a fix branch:
   `git checkout -b bugfix/{issue-number}-{slug}` (or `bugfix/{slug}` with no issue)
3. If on another branch, tell the user they can keep it or create a new one.

### Step 6: Next Steps

Remind the user:
- Review `bugfixes/{NNNN-slug}/bugfix.md`
- The hypothesis is **not yet proven**. Run `/bugfix:confirm` to prove or refute it before any
  code is written. Do NOT skip to a fix.
