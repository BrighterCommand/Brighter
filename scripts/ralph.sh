#!/usr/bin/env bash
set -euo pipefail

# Ralph Loop - Unattended TDD implementation via Claude Code
#
# Runs Claude Code in a loop, completing atomic TDD tasks one at a time,
# then restarting with fresh context to prevent context rot.
#
# Usage:
#   ./scripts/ralph.sh [tasks-per-run] [max-iterations] [cooldown-seconds]
#
# Environment variables:
#   RALPH_MODEL   - Claude model to use (default: sonnet)
#   RALPH_BUDGET  - Max USD per invocation (default: 5)

TASKS_PER_RUN="${1:-1}"
MAX_ITERATIONS="${2:-50}"
COOLDOWN_SECONDS="${3:-5}"
MODEL="${RALPH_MODEL:-sonnet}"
BUDGET="${RALPH_BUDGET:-5}"

# Find repo root (where .git is)
REPO_ROOT="$(git rev-parse --show-toplevel 2>/dev/null)" || {
    echo "Error: Not in a git repository"
    exit 1
}

STOP_FILE="$REPO_ROOT/RALPH_STOP"
LOG_FILE="$REPO_ROOT/ralph.log"

# ──────────────────────────────────────────────
# Help
# ──────────────────────────────────────────────

if [[ "${1:-}" == "--help" || "${1:-}" == "-h" ]]; then
    cat <<'HELP'
Ralph Loop - Unattended TDD implementation via Claude Code

USAGE:
    ./scripts/ralph.sh [tasks-per-run] [max-iterations] [cooldown-seconds]

ARGUMENTS:
    tasks-per-run     Number of tasks per Claude invocation (default: 1)
    max-iterations    Maximum loop iterations before stopping (default: 50)
    cooldown-seconds  Seconds to wait between iterations (default: 5)

ENVIRONMENT:
    RALPH_MODEL       Claude model to use (default: sonnet)
    RALPH_BUDGET      Max USD per invocation (default: 5)

STOP MECHANISMS:
    1. Count-based:  Stops after max-iterations
    2. File-based:   touch RALPH_STOP in repo root to halt after current task
    3. All-done:     Stops when all ralph-tasks are checked off

EXAMPLES:
    # Default: 1 task per run, max 50 iterations
    ./scripts/ralph.sh

    # 2 tasks per run, max 10 iterations, 10s cooldown
    ./scripts/ralph.sh 2 10 10

    # Use opus model with higher budget
    RALPH_MODEL=opus RALPH_BUDGET=10 ./scripts/ralph.sh

    # Stop a running loop
    touch RALPH_STOP

OUTPUT:
    Progress is printed to stdout and logged to ralph.log
    Does NOT push to remote - you decide when to push.
HELP
    exit 0
fi

# ──────────────────────────────────────────────
# Pre-flight checks
# ──────────────────────────────────────────────

if ! command -v claude &>/dev/null; then
    echo "Error: 'claude' command not found. Install Claude Code first."
    exit 1
fi

# Clean up any leftover stop file from a previous run
if [[ -f "$STOP_FILE" ]]; then
    echo "Warning: RALPH_STOP file exists from a previous run. Removing it."
    rm "$STOP_FILE"
fi

# Verify ralph-tasks.md exists
CURRENT_SPEC=""
if [[ -f "$REPO_ROOT/specs/.current-spec" ]]; then
    CURRENT_SPEC="$(cat "$REPO_ROOT/specs/.current-spec")"
fi

if [[ -z "$CURRENT_SPEC" ]]; then
    echo "Error: No current spec set. Run /spec:new first."
    exit 1
fi

RALPH_TASKS="$REPO_ROOT/specs/$CURRENT_SPEC/ralph-tasks.md"
if [[ ! -f "$RALPH_TASKS" ]]; then
    echo "Error: ralph-tasks.md not found at $RALPH_TASKS"
    echo "Run /spec:ralph-tasks first to generate it."
    exit 1
fi

# ──────────────────────────────────────────────
# Helper functions
# ──────────────────────────────────────────────

count_remaining() {
    grep -c '^\- \[ \]' "$RALPH_TASKS" 2>/dev/null || echo 0
}

count_complete() {
    grep -c '^\- \[x\]' "$RALPH_TASKS" 2>/dev/null || echo 0
}

count_failed() {
    grep -c '^\- \[!\]' "$RALPH_TASKS" 2>/dev/null || echo 0
}

count_total() {
    grep -c '^\- \[.\]' "$RALPH_TASKS" 2>/dev/null || echo 0
}

timestamp() {
    date '+%Y-%m-%d %H:%M:%S'
}

log() {
    local msg="[$(timestamp)] $1"
    echo "$msg"
    echo "$msg" >> "$LOG_FILE"
}

# ──────────────────────────────────────────────
# Main loop
# ──────────────────────────────────────────────

echo "============================================"
echo "  Ralph Loop - Unattended TDD"
echo "============================================"
echo "  Model:           $MODEL"
echo "  Budget/run:      \$$BUDGET"
echo "  Tasks/run:       $TASKS_PER_RUN"
echo "  Max iterations:  $MAX_ITERATIONS"
echo "  Cooldown:        ${COOLDOWN_SECONDS}s"
echo "  Spec:            $CURRENT_SPEC"
echo "  Tasks remaining: $(count_remaining)"
echo "  Log file:        $LOG_FILE"
echo "  Stop with:       touch RALPH_STOP"
echo "============================================"
echo ""

log "Ralph loop starting"

ITERATION=0
TASKS_COMPLETED_TOTAL=0

while true; do
    ITERATION=$((ITERATION + 1))

    # ── Check stop conditions ──

    if [[ -f "$STOP_FILE" ]]; then
        log "RALPH_STOP file detected. Halting."
        break
    fi

    if [[ "$ITERATION" -gt "$MAX_ITERATIONS" ]]; then
        log "Max iterations ($MAX_ITERATIONS) reached. Halting."
        break
    fi

    REMAINING=$(count_remaining)
    if [[ "$REMAINING" -eq 0 ]]; then
        log "All tasks complete. Halting."
        break
    fi

    # ── Run Claude ──

    log "─── Iteration $ITERATION/$MAX_ITERATIONS (remaining: $REMAINING) ───"

    # Primary invocation using slash command in print mode
    CLAUDE_OUTPUT=$(claude -p "/spec:ralph-implement $TASKS_PER_RUN" \
        --model "$MODEL" \
        --max-turns 50 \
        --verbose 2>&1) || true

    # Fallback: if slash command expansion doesn't work in print mode,
    # uncomment the following and comment out the above:
    #
    # CLAUDE_OUTPUT=$(claude -p "Read the file .claude/commands/spec/ralph-implement.md and execute those instructions with count=$TASKS_PER_RUN. The \$ARGUMENTS variable is $TASKS_PER_RUN." \
    #     --model "$MODEL" \
    #     --max-turns 50 \
    #     --verbose 2>&1) || true

    # Log output
    echo "$CLAUDE_OUTPUT" >> "$LOG_FILE"

    # Extract summary if present
    if echo "$CLAUDE_OUTPUT" | grep -q "=== RALPH SUMMARY ==="; then
        SUMMARY=$(echo "$CLAUDE_OUTPUT" | sed -n '/=== RALPH SUMMARY ===/,/=== END RALPH ===/p')
        log "Summary from iteration $ITERATION:"
        echo "$SUMMARY" | while IFS= read -r line; do log "  $line"; done
    fi

    # Check for ALL_DONE status
    if echo "$CLAUDE_OUTPUT" | grep -q "Status: ALL_DONE"; then
        log "All tasks reported complete by Claude."
        break
    fi

    # Update running count
    COMPLETE_NOW=$(count_complete)
    TASKS_COMPLETED_TOTAL=$COMPLETE_NOW

    # ── Cooldown ──

    if [[ "$COOLDOWN_SECONDS" -gt 0 ]]; then
        log "Cooling down for ${COOLDOWN_SECONDS}s..."
        sleep "$COOLDOWN_SECONDS"
    fi
done

# ──────────────────────────────────────────────
# Final summary
# ──────────────────────────────────────────────

FINAL_COMPLETE=$(count_complete)
FINAL_FAILED=$(count_failed)
FINAL_REMAINING=$(count_remaining)
FINAL_TOTAL=$(count_total)

echo ""
echo "============================================"
echo "  Ralph Loop Complete"
echo "============================================"
echo "  Iterations run:   $ITERATION"
echo "  Tasks completed:  $FINAL_COMPLETE/$FINAL_TOTAL"
echo "  Tasks failed:     $FINAL_FAILED"
echo "  Tasks remaining:  $FINAL_REMAINING"
echo "  Log file:         $LOG_FILE"
echo "============================================"
echo ""
echo "Review changes with: git log --oneline"
echo "Push when ready:     git push"

log "Ralph loop finished. Completed: $FINAL_COMPLETE/$FINAL_TOTAL, Failed: $FINAL_FAILED, Remaining: $FINAL_REMAINING"

# Clean up stop file if it was the reason we stopped
if [[ -f "$STOP_FILE" ]]; then
    rm "$STOP_FILE"
    log "Cleaned up RALPH_STOP file."
fi
