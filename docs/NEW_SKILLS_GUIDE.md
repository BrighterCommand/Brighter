# New Claude Code Skills - Team Guide

**Date**: 2026-01-17
**Status**: Ready to Use

## What's New?

Three new Claude Code skills have been added to automate and enforce Brighter's engineering practices:

1. **`/test-first`** - TDD with mandatory approval gate
2. **`/adr`** - Architecture Decision Record creation
3. **`/tidy-first`** - Separate structural from behavioral changes

## Why This Matters

Previously, our engineering practices were **documented but not enforced**. It was easy to:
- Skip the mandatory approval step in TDD
- Create inconsistent or incorrectly numbered ADRs
- Mix refactoring with functionality changes in the same commit

**Now**: These practices are **automated and enforced** through skills. The correct approach is now the easy path.

## What Are Skills?

Skills are slash commands you type in Claude Code that automate multi-step workflows:

```bash
/test-first <behavior>      # Automatic TDD with approval gate
/adr <title>                # Auto-numbered, formatted ADR
/tidy-first <change>        # Automatic structural/behavioral separation
```

Instead of manually following documented procedures, you invoke a skill and Claude guides you through.

## Quick Start

### Try `/test-first` First

The easiest way to see skills in action is to try test-first development:

```bash
# In Claude Code, type:
/test-first when a message timeout occurs it should retry with backoff
```

**What happens:**
1. Claude writes a test following all Brighter conventions
2. Claude shows you the test and asks for approval
3. You review and approve (or request changes)
4. Claude implements the minimum code to pass
5. Claude suggests refactoring improvements
6. You get a clean commit

**Key benefit**: You can't skip the approval step - it's enforced.

### Try `/adr` for Your Next Design Decision

```bash
# In Claude Code, type:
/adr error handling strategy for kafka consumer
```

**What happens:**
1. Claude finds the next ADR number (e.g., 0037)
2. Claude checks if you're working on a spec
3. Claude prompts you for key information
4. Claude creates properly formatted ADR file
5. Claude updates spec tracking if applicable

**Key benefit**: Perfect formatting, correct numbering, proper linking every time.

### Try `/tidy-first` When Refactoring

```bash
# In Claude Code, type:
/tidy-first optimize message processing in KafkaConsumer
```

**What happens:**
1. Claude analyzes and categorizes: structural vs behavioral changes
2. Claude shows you the categorization and asks for approval
3. Claude makes structural changes (refactoring only)
4. Claude runs tests to validate behavior unchanged
5. Claude commits: `refactor: simplify message processing structure`
6. Claude makes behavioral changes (new functionality)
7. Claude runs tests with new behavior
8. Claude commits: `feat: add caching and exponential backoff`

**Key benefit**: Two clear commits - easier code review, cleaner git history.

## Migration Guide

### From Manual TDD → `/test-first`

**Before** (manual):
```
❌ Write test
❌ (Might forget approval step)
❌ Implement code
❌ (Might skip refactoring)
```

**Now** (skill):
```
✅ /test-first <behavior>
✅ Claude writes test
✅ Approval enforced (can't skip)
✅ Claude implements
✅ Claude suggests refactoring
```

### From Manual ADR Creation → `/adr`

**Before** (manual):
```
❌ Count existing ADRs manually
❌ Remember file naming convention
❌ Copy template
❌ Remember to link to spec
❌ Update .adr-list manually
```

**Now** (skill):
```
✅ /adr <title>
✅ Auto-numbered
✅ Perfect format
✅ Auto-linked to spec
✅ .adr-list updated automatically
```

### From Mixed Commits → `/tidy-first`

**Before** (manual):
```
❌ Make refactoring changes
❌ Make feature changes
❌ Commit everything together
❌ Reviewer has to untangle
```

**Now** (skill):
```
✅ /tidy-first <change>
✅ Claude separates structural from behavioral
✅ Two commits: refactor + feat
✅ Easy review, clear history
```

## When to Use Each Skill

### Decision Matrix

| Situation | Use This Skill |
|-----------|---------------|
| Adding new feature (clean code) | `/test-first` |
| Adding feature (messy code needs cleanup) | `/tidy-first` |
| Fixing bug with test-first | `/test-first` |
| Documenting architecture decision | `/adr` |
| Refactoring without adding features | `/tidy-first` |
| Full feature from requirements to implementation | `/spec:*` commands |

### Real Examples

**Example 1: New Feature**
```bash
# Clean codebase, just add feature
/test-first when message fails validation it should log error details
```

**Example 2: Feature Requiring Cleanup**
```bash
# Code is messy, needs refactoring + new feature
/tidy-first add detailed validation error logging
# Results in: refactor commit + feat commit
```

**Example 3: Architectural Decision**
```bash
# Need to document a design choice
/adr message validation strategy for kafka consumer
```

**Example 4: Bug Fix**
```bash
# Fix with test-first approach
/test-first when offset acknowledgment fails it should retry
```

## What Changes for You?

### For Solo Development

**Before**: Manually follow .agent_instructions/ procedures
**Now**: Type skill command, Claude guides you

**Before**: Easy to skip approval or mix commits
**Now**: Skills enforce best practices automatically

### For Code Reviews

**Before**: Mixed commits (structure + behavior together)
**Now**: Separate commits (easier to review)

**Before**: Inconsistent ADR formatting
**Now**: All ADRs perfectly formatted

**Before**: Tests might be added after implementation
**Now**: TDD enforced with approval gate

### For Team Consistency

**Before**: Each developer follows practices differently
**Now**: Skills enforce consistency across team

**Before**: New developers need to learn all procedures
**Now**: Skills guide new developers through correct process

## Benefits Summary

### Immediate Benefits

- ⚡ **Faster**: One command replaces multi-step process
- 🛡️ **Safer**: Approval gates prevent mistakes
- 📝 **Better docs**: ADRs always formatted correctly
- 🧹 **Cleaner commits**: Structural/behavioral separated
- ✅ **Better tests**: TDD enforced with approval

### Long-term Benefits

- 📈 **Quality**: Practices actually followed, not just documented
- 👥 **Onboarding**: New developers follow best practices automatically
- 🔍 **Reviews**: Easier to review with clear, focused commits
- 📚 **History**: Clear git history with single-purpose commits
- 🧠 **Learning**: Skills teach best practices through use

## Common Questions

### Q: Do I have to use skills?

**A**: Skills are strongly recommended for the workflows they cover:
- TDD → Use `/test-first`
- ADRs → Use `/adr`
- Refactoring + features → Use `/tidy-first`

They enforce mandatory practices (like TDD approval) that are easy to skip manually.

### Q: Can I still work manually?

**A**: Yes, but you'll need to manually ensure compliance:
- TDD: Get approval before implementing (required per testing.md)
- ADRs: Follow numbering and format conventions
- Commits: Separate structural from behavioral changes

Skills make compliance automatic.

### Q: What if I disagree with what the skill does?

**A**: Skills ask for approval at key points:
- `/test-first`: Approve test before implementation
- `/tidy-first`: Approve categorization before changes

You can request adjustments at any approval point.

### Q: Can I modify skill output?

**A**: Yes! Skills generate starting point, you can edit before committing.

### Q: What if I'm already partway through a task?

**A**: Skills work with existing code:
- `/test-first`: Can add tests to existing features
- `/tidy-first`: Specifically designed for existing code
- `/adr`: Works standalone or with specs

### Q: Do skills replace the spec workflow?

**A**: No, they complement it:
- `/spec:design` uses `/adr` internally
- `/spec:implement` uses `/test-first` approach
- Skills can be used standalone or as part of spec workflow

## Getting Help

### Documentation

- **Quick Reference**: [.agent_instructions/skills_overview.md](../.agent_instructions/skills_overview.md)
- **Detailed Guide**: [.claude/commands/README.md](../.claude/commands/README.md)
- **Individual Skills**:
  - [/test-first docs](../.claude/commands/tdd/README.md)
  - [/adr docs](../.claude/commands/adr/README.md)
  - [/tidy-first docs](../.claude/commands/refactor/README.md)

### Support

- **Questions**: Ask in team chat or during standup
- **Issues**: Report at https://github.com/anthropics/claude-code/issues
- **Feedback**: Suggestions for improving skills are welcome

## Try It Today

### 5-Minute Challenge

Pick your next task and try the appropriate skill:

1. **Adding a feature?** → Try `/test-first`
2. **Documenting a decision?** → Try `/adr`
3. **Refactoring + feature?** → Try `/tidy-first`

### What to Expect

- **First time**: May feel slower as you learn the workflow
- **Second time**: Noticeably faster than manual process
- **Third time**: Can't imagine going back to manual

### Share Your Experience

After trying a skill:
- Share what worked well
- Share what was confusing
- Suggest improvements

## Summary

Three new skills enforce Brighter's engineering practices:

| Skill | Enforces | Key Benefit |
|-------|----------|-------------|
| `/test-first` | TDD with approval | Can't skip approval step |
| `/adr` | Documented decisions | Perfect formatting every time |
| `/tidy-first` | Structural/behavioral separation | Two clear commits |

**Bottom line**: Skills make the **correct approach the easy path**.

**Next step**: Try `/test-first` on your next feature.

---

**Questions?** See [skills_overview.md](agent_instructions/skills_overview.md) or ask the team.
