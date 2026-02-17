# Claude Code Skills Overview

This document provides a quick reference for the Claude Code skills available for Brighter development.

## What Are Skills?

Skills are slash commands that automate multi-step workflows and enforce Brighter's engineering practices. Instead of manually following documented procedures, you invoke a skill that guides you through the process.

## Available Skills

### Core Development Skills

| Skill | Purpose | Usage |
|-------|---------|-------|
| `/test-first` | TDD with mandatory approval | `/test-first <behavior description>` |
| `/tidy-first` | Separate refactoring from features | `/tidy-first <change description>` |
| `/adr` | Create Architecture Decision Record | `/adr <title>` |

### Specification Workflow Skills

| Skill | Purpose | Usage |
|-------|---------|-------|
| `/spec:requirements` | Capture requirements | `/spec:requirements [issue-number]` |
| `/spec:design` | Create design ADRs | `/spec:design <focus-area>` |
| `/spec:tasks` | Break down implementation | `/spec:tasks` |
| `/spec:implement` | TDD implementation | `/spec:implement [task-number]` |
| `/spec:status` | Show spec status | `/spec:status` |
| `/spec:approve` | Approve phases | `/spec:approve <phase> [adr-number]` |
| `/spec:review` | Review phases | `/spec:review [phase] [adr-number]` |
| `/spec:switch` | Switch to different spec | `/spec:switch <spec-name>` |
| `/spec:ralph-tasks` | Generate unattended TDD tasks | `/spec:ralph-tasks` |
| `/spec:ralph-implement` | Unattended TDD implementation | `/spec:ralph-implement [count]` |

## Quick Reference Card

```
┌────────────────────────────────────────────────────────────────┐
│                    CLAUDE CODE SKILLS                          │
│                    Quick Reference                              │
└────────────────────────────────────────────────────────────────┘

🧪 TEST-DRIVEN DEVELOPMENT
   /test-first <behavior>
   • Write test → Approve → Implement → Refactor
   • Enforces mandatory approval before implementation
   • Example: /test-first when message fails it should retry

🏗️  REFACTORING
   /tidy-first <change>
   • Separate structural changes from behavioral changes
   • Creates two commits: refactor + feat/fix/perf
   • Example: /tidy-first optimize message processing

📋 ARCHITECTURE DECISIONS
   /adr <title>
   • Auto-numbered, properly formatted ADRs
   • Links to current spec if applicable
   • Example: /adr kafka error handling strategy

📐 SPECIFICATION WORKFLOW
   /spec:requirements [issue]    → Capture requirements
   /spec:design <focus>          → Create design ADR
   /spec:tasks                   → Break down work
   /spec:implement [task]        → TDD implementation
   /spec:status                  → Show all specs
   /spec:approve <phase>         → Approve phase

🔄 RALPH LOOP (UNATTENDED)
   /spec:ralph-tasks              → Generate ralph tasks
   /spec:ralph-implement [count]  → Unattended TDD
   scripts/ralph.sh [n] [max]     → Run the loop
```

## Decision Tree: Which Skill Should I Use?

```
                    Need to document decision?
                              │
                    ┌─────────┴─────────┐
                   YES                 NO
                    │                   │
                 /adr                   ↓
                                Adding behavior?
                                       │
                            ┌──────────┴──────────┐
                          YES                    NO
                            │                     │
                            ↓                     ↓
                  Code needs cleanup?      Just refactoring?
                            │                     │
                    ┌───────┴───────┐            │
                  YES              NO             │
                    │               │             │
              /tidy-first     /test-first   /tidy-first
```

## Enforcement of Brighter Practices

Each skill enforces specific practices from `.agent_instructions/`:

### `/test-first` → testing.md

**Enforces**:
- Red-Green-Refactor TDD cycle
- **MANDATORY approval before implementation** (lines 21-25)
- BDD-style test naming (`When_X_should_Y`)
- One test per file
- Developer tests (not unit tests)
- InMemory* implementations instead of mocks

**Reference**: [testing.md](testing.md) lines 11-26

### `/adr` → documentation.md

**Enforces**:
- Proper ADR numbering sequence
- Standard ADR template structure
- Dash-case file naming
- Linking to parent requirements
- Tracking in spec's `.adr-list`

**Reference**: [documentation.md](documentation.md) lines 49-62

### `/tidy-first` → code_style.md

**Enforces**:
- Separation of structural from behavioral changes
- **Never mix in same commit** (line 78)
- Structural changes FIRST (line 79)
- Test validation after structural changes (line 80)
- Two distinct commits for clarity

**Reference**: [code_style.md](code_style.md) lines 74-83

## Common Workflows

### Workflow 1: Add New Feature (Clean Code)

```bash
# 1. Write test first
/test-first when message timeout occurs it should retry with backoff

# 2. Repeat for each behavior
/test-first when max retries reached it should send to dead letter queue

# 3. Document decision if significant
/adr retry strategy for message processing
```

### Workflow 2: Add Feature (Code Needs Cleanup)

```bash
# 1. Clean up and add feature in one workflow
/tidy-first add retry logic with exponential backoff

# Output: Two commits
#   - refactor: simplify error handling structure
#   - feat: add exponential backoff retry logic
```

### Workflow 3: Specification-Driven Development

```bash
# 1. Create spec from GitHub issue
/spec:requirements 123

# 2. Review and approve requirements
/spec:approve requirements

# 3. Create design ADRs (uses /adr internally)
/spec:design message-serialization
/spec:design error-handling
/spec:approve design

# 4. Break down into tasks
/spec:tasks
/spec:approve tasks

# 5. Implement (uses /test-first approach)
/spec:implement

# 6. Check progress
/spec:status
```

### Workflow 4: Pure Refactoring

```bash
# Refactor without adding features
/tidy-first simplify nested conditionals in KafkaConsumer

# Output: Single commit
#   - refactor: simplify nested conditionals in KafkaConsumer
```

### Workflow 5: Ralph Loop (Unattended)

```bash
# 1. Complete spec workflow up to approved tasks (Workflows 3 steps 1-4)

# 2. Generate ralph-tasks from approved tasks
/spec:ralph-tasks

# 3. Review ralph-tasks.md in your IDE

# 4. Run the unattended loop
./scripts/ralph.sh              # 1 task/run, 50 max iterations
./scripts/ralph.sh 2 20 10      # 2 tasks/run, 20 max, 10s cooldown

# 5. Stop if needed
touch RALPH_STOP

# 6. Review results
git log --oneline
```

## Benefits Summary

### For You (Developer)

| Benefit | How Skills Help |
|---------|----------------|
| **Faster** | One command replaces multi-step manual process |
| **Correct** | Skills encode best practices, hard to do wrong |
| **Consistent** | Everyone follows same workflow |
| **Less to remember** | Invoke skill, it guides you through |
| **Built-in quality** | Approval gates prevent mistakes |

### For Reviewers

| Benefit | How Skills Help |
|---------|----------------|
| **Easier review** | Structural/behavioral changes separated |
| **Better context** | ADRs explain WHY, not just WHAT |
| **Confidence** | Know tests were approved before implementation |
| **Cleaner history** | Each commit has single purpose |

### For Project

| Benefit | How Skills Help |
|---------|----------------|
| **Quality** | TDD and approval gates enforced |
| **Documentation** | ADRs created consistently |
| **Maintainability** | Better structure, clearer commits |
| **Onboarding** | New developers follow best practices automatically |

## Comparison: Manual vs Skill-Guided

### TDD Without `/test-first`

```
❌ Manual Process:
1. Remember to write test first
2. Remember BDD naming convention
3. Remember one test per file
4. Remember to get approval (often forgotten!)
5. Implement code
6. Remember to refactor
7. Hope you didn't skip any steps

Risk: Easy to skip approval or go straight to implementation
```

### TDD With `/test-first`

```
✅ Skill-Guided:
1. Type: /test-first <behavior>
2. Claude writes test following all conventions
3. Mandatory approval gate (can't proceed without it)
4. Claude implements minimum code
5. Claude suggests refactoring
6. All steps enforced

Benefit: Impossible to skip approval, all conventions followed
```

### Creating ADR Without `/adr`

```
❌ Manual Process:
1. Check what the next ADR number should be
2. Remember the file naming convention
3. Find the ADR template
4. Copy template structure
5. Check if linked to spec
6. Update spec's .adr-list
7. Remember all required sections

Risk: Numbering conflicts, inconsistent format, missing sections
```

### Creating ADR With `/adr`

```
✅ Skill-Guided:
1. Type: /adr <title>
2. Claude auto-numbers correctly
3. Claude checks for spec linking
4. Claude prompts for all sections
5. Claude updates .adr-list
6. Perfect formatting guaranteed

Benefit: Consistent ADRs, no numbering errors, proper tracking
```

### Refactoring Without `/tidy-first`

```
❌ Manual Process:
1. Make structural changes
2. Make behavioral changes
3. Commit everything together
4. Reviewer has to untangle structure from behavior
5. If bug appears, unclear which change caused it

Risk: Mixed commits hard to review, unclear git history
```

### Refactoring With `/tidy-first`

```
✅ Skill-Guided:
1. Type: /tidy-first <change>
2. Claude categorizes: structural vs behavioral
3. Get your approval of categorization
4. Structural commit (tests validate no behavior change)
5. Behavioral commit (new functionality)
6. Two clear, reviewable commits

Benefit: Easy review, clear history, safer refactoring
```

## Tips for Success

### Getting Started

1. **Start simple**: Try `/test-first` for your next small feature
2. **Read the output**: Skills explain what they're doing and why
3. **Trust the process**: The approval gates are there for good reasons
4. **Combine skills**: Use `/adr` then `/test-first` for feature development

### Best Practices

1. **Use proactively**: Don't wait until stuck - start with skill
2. **Review before approving**: Approval gates are your quality check
3. **Iterate**: If skill's categorization is wrong, adjust and continue
4. **Learn from skills**: Watch what they do to learn best practices

### Common Questions

**Q: Can I modify skill output?**
A: Yes! Skills generate starting point, you can edit before committing.

**Q: What if I disagree with skill's categorization?**
A: Skills ask for approval - tell Claude to adjust categorization.

**Q: Can I skip the approval steps?**
A: No, and that's the point - approvals prevent mistakes.

**Q: Do skills work with existing code?**
A: Yes! `/tidy-first` is specifically for working with existing code.

## Next Steps

1. **Read detailed docs**: Each skill has README in `.claude/commands/[category]/`
2. **Try a skill**: Pick one and use it for your next task
3. **Give feedback**: Report issues or suggestions
4. **Share learnings**: Help team members discover skills

## Detailed Documentation

- **Skills Overview**: [.claude/commands/README.md](../../.claude/commands/README.md)
- **Test-First**: [.claude/commands/tdd/README.md](../../.claude/commands/tdd/README.md)
- **ADR**: [.claude/commands/adr/README.md](../../.claude/commands/adr/README.md)
- **Tidy First**: [.claude/commands/refactor/README.md](../../.claude/commands/refactor/README.md)
- **Spec Workflow**: [.claude/commands/spec/README.md](../../.claude/commands/spec/README.md)

---

**Remember**: Skills make the **correct approach the easy path**. Instead of remembering procedures, just invoke the skill and it guides you through.
