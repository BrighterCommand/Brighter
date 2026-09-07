#!/bin/bash
# test_clean_failed_tests_aws_assets.sh
# Integration test for clean_failed_tests_aws_assets.sh
# Creates tagged and untagged AWS resources, then verifies the cleanup script
# only deletes resources tagged with Environment=Test.

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CLEANUP_SCRIPT="$SCRIPT_DIR/clean_failed_tests_aws_assets.sh"

# The fixtures below are created seconds before the sweep runs, so the age guard that protects a
# live CI run from the scheduled sweep would skip every one of them. Turn it off here: what is
# under test is which resources the sweep matches, not when it declines to act on them.
export CLEANUP_MIN_AGE_SECONDS=0

# --- Cleanup trap: ensure test resources are removed regardless of outcome ---
TAGGED_QUEUE_URL=""
UNTAGGED_QUEUE_URL=""
TAGGED_TOPIC_ARN=""
UNTAGGED_TOPIC_ARN=""
TAGGED_SCHEDULE_GROUP=""
TAGGED_SCHEDULE_NAME=""
UNTAGGED_SCHEDULE_GROUP=""
CONVENTION_QUEUE_URL=""
CONVENTION_FIFO_QUEUE_URL=""
CONVENTION_TOPIC_ARN=""
CONVENTION_FIFO_TOPIC_ARN=""
DLQ_ASYNC_QUEUE_URL=""
GUARDED_QUEUE_URL=""
GUARDED_TOPIC_ARN=""

cleanup_test_resources() {
    echo ""
    echo "=== Trap Teardown ==="
    [[ -n "$UNTAGGED_QUEUE_URL" ]] && aws sqs delete-queue --queue-url "$UNTAGGED_QUEUE_URL" 2>/dev/null || true
    [[ -n "$UNTAGGED_TOPIC_ARN" ]] && aws sns delete-topic --topic-arn "$UNTAGGED_TOPIC_ARN" 2>/dev/null || true
    [[ -n "$TAGGED_QUEUE_URL" ]] && aws sqs delete-queue --queue-url "$TAGGED_QUEUE_URL" 2>/dev/null || true
    [[ -n "$TAGGED_TOPIC_ARN" ]] && aws sns delete-topic --topic-arn "$TAGGED_TOPIC_ARN" 2>/dev/null || true
    if [[ -n "$TAGGED_SCHEDULE_NAME" && -n "$TAGGED_SCHEDULE_GROUP" ]]; then
        aws scheduler delete-schedule --name "$TAGGED_SCHEDULE_NAME" --group-name "$TAGGED_SCHEDULE_GROUP" 2>/dev/null || true
    fi
    if [[ -n "$TAGGED_SCHEDULE_GROUP" ]]; then
        aws scheduler delete-schedule-group --name "$TAGGED_SCHEDULE_GROUP" 2>/dev/null || true
    fi
    if [[ -n "$UNTAGGED_SCHEDULE_GROUP" ]]; then
        aws scheduler delete-schedule-group --name "$UNTAGGED_SCHEDULE_GROUP" 2>/dev/null || true
    fi
    [[ -n "$CONVENTION_QUEUE_URL" ]] && aws sqs delete-queue --queue-url "$CONVENTION_QUEUE_URL" 2>/dev/null || true
    [[ -n "$CONVENTION_FIFO_QUEUE_URL" ]] && aws sqs delete-queue --queue-url "$CONVENTION_FIFO_QUEUE_URL" 2>/dev/null || true
    [[ -n "$CONVENTION_TOPIC_ARN" ]] && aws sns delete-topic --topic-arn "$CONVENTION_TOPIC_ARN" 2>/dev/null || true
    [[ -n "$CONVENTION_FIFO_TOPIC_ARN" ]] && aws sns delete-topic --topic-arn "$CONVENTION_FIFO_TOPIC_ARN" 2>/dev/null || true
    [[ -n "$DLQ_ASYNC_QUEUE_URL" ]] && aws sqs delete-queue --queue-url "$DLQ_ASYNC_QUEUE_URL" 2>/dev/null || true
    [[ -n "$GUARDED_QUEUE_URL" ]] && aws sqs delete-queue --queue-url "$GUARDED_QUEUE_URL" 2>/dev/null || true
    [[ -n "$GUARDED_TOPIC_ARN" ]] && aws sns delete-topic --topic-arn "$GUARDED_TOPIC_ARN" 2>/dev/null || true
    echo "  Cleaned up test fixtures"
}
trap cleanup_test_resources EXIT

# --- Test harness ---
PASS=0
FAIL=0

assert_eq() {
    local expected="$1" actual="$2" message="$3"
    if [[ "$expected" == "$actual" ]]; then
        echo "  PASS: $message"
        PASS=$((PASS + 1))
    else
        echo "  FAIL: $message (expected='$expected', actual='$actual')"
        FAIL=$((FAIL + 1))
    fi
}

assert_contains() {
    local haystack="$1" needle="$2" message="$3"
    if echo "$haystack" | grep -qE "$needle"; then
        echo "  PASS: $message"
        PASS=$((PASS + 1))
    else
        echo "  FAIL: $message (output did not contain '$needle')"
        FAIL=$((FAIL + 1))
    fi
}

assert_not_contains() {
    local haystack="$1" needle="$2" message="$3"
    if echo "$haystack" | grep -qE "$needle"; then
        echo "  FAIL: $message (output contained '$needle')"
        FAIL=$((FAIL + 1))
    else
        echo "  PASS: $message"
        PASS=$((PASS + 1))
    fi
}

# Retries the check until it holds or the deadline passes. A delete is eventually
# consistent -- SNS will answer get-topic-attributes for a deleted topic for a short while, and
# SQS documents a queue as visible for up to sixty seconds -- so reading once proves nothing.
assert_eventually_contains() {
    local check="$1" needle="$2" message="$3"
    local deadline=$((SECONDS + 90)) output=""

    while true; do
        output=$(eval "$check" 2>&1 || true)
        if echo "$output" | grep -qE "$needle"; then
            break
        fi
        if (( SECONDS >= deadline )); then
            break
        fi
        sleep 3
    done

    assert_contains "$output" "$needle" "$message"
}

assert_not_empty() {
    local value="$1" message="$2"
    if [[ -n "$value" ]]; then
        echo "  PASS: $message"
        PASS=$((PASS + 1))
    else
        echo "  FAIL: $message (value was empty)"
        FAIL=$((FAIL + 1))
    fi
}

# The GUIDs in the convention names only have to be 32 hex characters. uuidgen covers the CI
# runners, but it is uuid-runtime on Debian and Ubuntu and is missing from leaner images, so fall
# back to urandom rather than assume either it or openssl is present.
hex_id() {
    if command -v uuidgen >/dev/null 2>&1; then
        uuidgen | tr -d '-' | tr '[:upper:]' '[:lower:]'
    else
        od -An -tx1 -N16 /dev/urandom | tr -d ' \n'
    fi
}

# --- Setup: create tagged and untagged resources ---
PREFIX="cleanup-test-$(date +%s)"
ACCOUNT_ID=$(aws sts get-caller-identity --query 'Account' --output text)
echo "=== Setup: creating test resources (prefix: $PREFIX, account: $ACCOUNT_ID) ==="

# Tagged SQS queue
TAGGED_QUEUE="$PREFIX-tagged-queue"
TAGGED_QUEUE_URL=$(aws sqs create-queue \
    --queue-name "$TAGGED_QUEUE" \
    --tags Environment=Test,Source=Brighter \
    --query 'QueueUrl' --output text)
echo "  Created tagged queue: $TAGGED_QUEUE"

# Untagged SQS queue
UNTAGGED_QUEUE="$PREFIX-untagged-queue"
UNTAGGED_QUEUE_URL=$(aws sqs create-queue \
    --queue-name "$UNTAGGED_QUEUE" \
    --query 'QueueUrl' --output text)
echo "  Created untagged queue: $UNTAGGED_QUEUE"

# Tagged SNS topic
TAGGED_TOPIC="$PREFIX-tagged-topic"
TAGGED_TOPIC_ARN=$(aws sns create-topic \
    --name "$TAGGED_TOPIC" \
    --tags Key=Environment,Value=Test Key=Source,Value=Brighter \
    --query 'TopicArn' --output text)
echo "  Created tagged topic: $TAGGED_TOPIC"

# Untagged SNS topic
UNTAGGED_TOPIC="$PREFIX-untagged-topic"
UNTAGGED_TOPIC_ARN=$(aws sns create-topic \
    --name "$UNTAGGED_TOPIC" \
    --query 'TopicArn' --output text)
echo "  Created untagged topic: $UNTAGGED_TOPIC"

# Resources named the way the generated MessageGateway tests name them, and deliberately left
# untagged -- this is what the real leaked resources look like, and what the tag query misses.
# The GUID is generated here rather than reused so that each run exercises the pattern, not a
# literal name.
CONVENTION_QUEUE="sqs-std-$(hex_id)"
CONVENTION_QUEUE_URL=$(aws sqs create-queue \
    --queue-name "$CONVENTION_QUEUE" \
    --query 'QueueUrl' --output text)
echo "  Created convention-named queue: $CONVENTION_QUEUE"

# FIFO variants specifically: these are the bulk of what leaks in practice.
CONVENTION_FIFO_QUEUE="sqs-fifo-$(hex_id).fifo"
CONVENTION_FIFO_QUEUE_URL=$(aws sqs create-queue \
    --queue-name "$CONVENTION_FIFO_QUEUE" \
    --attributes FifoQueue=true \
    --query 'QueueUrl' --output text)
echo "  Created convention-named FIFO queue: $CONVENTION_FIFO_QUEUE"

CONVENTION_TOPIC="sns-std-$(hex_id)"
CONVENTION_TOPIC_ARN=$(aws sns create-topic \
    --name "$CONVENTION_TOPIC" \
    --query 'TopicArn' --output text)
echo "  Created convention-named topic: $CONVENTION_TOPIC"

CONVENTION_FIFO_TOPIC="sns-fifo-$(hex_id).fifo"
CONVENTION_FIFO_TOPIC_ARN=$(aws sns create-topic \
    --name "$CONVENTION_FIFO_TOPIC" \
    --attributes FifoTopic=true \
    --query 'TopicArn' --output text)
echo "  Created convention-named FIFO topic: $CONVENTION_FIFO_TOPIC"

# The hand-written fixtures name resources by prefix rather than by the generated convention.
# Consumer-DLQ-Async is one of those, and it leaks the same way when its constructor throws
# before Dispose can run, so the prefix list has to carry it.
DLQ_ASYNC_QUEUE="Consumer-DLQ-Async-$(hex_id)"
DLQ_ASYNC_QUEUE_URL=$(aws sqs create-queue \
    --queue-name "$DLQ_ASYNC_QUEUE" \
    --query 'QueueUrl' --output text)
echo "  Created prefix-named queue: $DLQ_ASYNC_QUEUE"

# Subscription on the tagged topic (from the tagged queue)
TAGGED_QUEUE_ARN=$(aws sqs get-queue-attributes \
    --queue-url "$TAGGED_QUEUE_URL" \
    --attribute-names QueueArn \
    --query 'Attributes.QueueArn' --output text)
SUBSCRIPTION_ARN=$(aws sns subscribe \
    --topic-arn "$TAGGED_TOPIC_ARN" \
    --protocol sqs \
    --notification-endpoint "$TAGGED_QUEUE_ARN" \
    --query 'SubscriptionArn' --output text)
echo "  Created subscription: $SUBSCRIPTION_ARN"

# Tagged EventBridge Scheduler group with a schedule
TAGGED_SCHEDULE_GROUP="$PREFIX-tagged-group"
aws scheduler create-schedule-group \
    --name "$TAGGED_SCHEDULE_GROUP" \
    --tags Key=Environment,Value=Test Key=Source,Value=Brighter 2>&1
echo "  Created tagged schedule group: $TAGGED_SCHEDULE_GROUP"

TAGGED_SCHEDULE_NAME="$PREFIX-tagged-schedule"
if ! aws scheduler create-schedule \
    --name "$TAGGED_SCHEDULE_NAME" \
    --group-name "$TAGGED_SCHEDULE_GROUP" \
    --schedule-expression "at(2099-01-01T00:00:00)" \
    --schedule-expression-timezone "UTC" \
    --flexible-time-window '{"Mode":"OFF"}' \
    --target "{\"Arn\":\"arn:aws:sqs:us-west-2:${ACCOUNT_ID}:fake-queue\",\"RoleArn\":\"arn:aws:iam::${ACCOUNT_ID}:role/fake-role\",\"Input\":\"test\"}" \
    --action-after-completion DELETE 2>&1; then
    echo "  WARNING: Failed to create schedule (target ARN validation). Schedule group tests may be incomplete."
    TAGGED_SCHEDULE_NAME=""
fi
echo "  Created tagged schedule: $TAGGED_SCHEDULE_NAME"

# Untagged EventBridge Scheduler group (should NOT be deleted)
UNTAGGED_SCHEDULE_GROUP="$PREFIX-untagged-group"
aws scheduler create-schedule-group \
    --name "$UNTAGGED_SCHEDULE_GROUP" 2>&1
echo "  Created untagged schedule group: $UNTAGGED_SCHEDULE_GROUP"

# Allow time for tag propagation — the Resource Groups Tagging API is eventually consistent
sleep 15

# --- Test 1: --dry-run lists tagged resources without deleting ---
echo ""
echo "=== Test 1: --dry-run lists tagged resources without deleting ==="

DRY_RUN_OUTPUT=$("$CLEANUP_SCRIPT" --dry-run 2>&1)
DRY_RUN_EXIT=$?

assert_eq "0" "$DRY_RUN_EXIT" "dry-run exits with 0"
assert_contains "$DRY_RUN_OUTPUT" "DRY RUN" "output indicates dry-run mode"
assert_contains "$DRY_RUN_OUTPUT" "$TAGGED_QUEUE" "output lists tagged queue"
assert_contains "$DRY_RUN_OUTPUT" "$TAGGED_TOPIC" "output lists tagged topic"
assert_contains "$DRY_RUN_OUTPUT" "$TAGGED_SCHEDULE_GROUP" "output lists tagged schedule group"
assert_contains "$DRY_RUN_OUTPUT" "$CONVENTION_QUEUE" "output lists convention-named queue"
assert_contains "$DRY_RUN_OUTPUT" "$CONVENTION_FIFO_QUEUE" "output lists convention-named FIFO queue"
assert_contains "$DRY_RUN_OUTPUT" "$CONVENTION_TOPIC" "output lists convention-named topic"
assert_contains "$DRY_RUN_OUTPUT" "$CONVENTION_FIFO_TOPIC" "output lists convention-named FIFO topic"
assert_contains "$DRY_RUN_OUTPUT" "$DLQ_ASYNC_QUEUE" "output lists prefix-named async DLQ queue"

# Tagged queue must still exist after dry-run
QUEUE_CHECK=$(aws sqs get-queue-url --queue-name "$TAGGED_QUEUE" --query 'QueueUrl' --output text 2>/dev/null || echo "")
assert_not_empty "$QUEUE_CHECK" "tagged queue still exists after dry-run"

# --- Test 2: actual run deletes tagged resources and logs actions ---
echo ""
echo "=== Test 2: actual run deletes tagged resources ==="

RUN_OUTPUT=$("$CLEANUP_SCRIPT" 2>&1)
RUN_EXIT=$?

assert_eq "0" "$RUN_EXIT" "cleanup exits with 0"
assert_contains "$RUN_OUTPUT" "$TAGGED_QUEUE" "output logs tagged queue deletion"
assert_contains "$RUN_OUTPUT" "$TAGGED_TOPIC" "output logs tagged topic deletion"

# --- Test 3: deletion order — subscriptions before topics ---
echo ""
echo "=== Test 3: subscriptions deleted before topics ==="

# The subscription line should appear before the topic line in output
SUB_LINE=$(echo "$RUN_OUTPUT" | grep -n "subscription" | head -1 | cut -d: -f1)
TOPIC_LINE=$(echo "$RUN_OUTPUT" | grep -n "topic" | grep -v "subscription" | head -1 | cut -d: -f1)
if [[ -n "$SUB_LINE" && -n "$TOPIC_LINE" ]]; then
    if [[ "$SUB_LINE" -lt "$TOPIC_LINE" ]]; then
        echo "  PASS: subscriptions deleted before topics (line $SUB_LINE < $TOPIC_LINE)"
        PASS=$((PASS + 1))
    else
        echo "  FAIL: subscriptions should be deleted before topics (sub=$SUB_LINE, topic=$TOPIC_LINE)"
        FAIL=$((FAIL + 1))
    fi
else
    echo "  SKIP: could not determine deletion order from output"
fi

# --- Test 4: tagged resources were actually deleted ---
echo ""
echo "=== Test 4: tagged resources were deleted ==="

# Allow time for eventual consistency — SQS/SNS deletions may take a few seconds to propagate
sleep 10

TAGGED_QUEUE_CHECK=$(aws sqs get-queue-url --queue-name "$TAGGED_QUEUE" 2>&1 || true)
assert_contains "$TAGGED_QUEUE_CHECK" "NonExistentQueue|does not exist" "tagged queue was deleted"

TAGGED_TOPIC_CHECK=$(aws sns get-topic-attributes --topic-arn "$TAGGED_TOPIC_ARN" 2>&1 || true)
assert_contains "$TAGGED_TOPIC_CHECK" "NotFound|not found|Not Found" "tagged topic was deleted"

# --- Test 5: untagged resources were NOT deleted ---
echo ""
echo "=== Test 5: untagged resources were NOT deleted ==="

UNTAGGED_QUEUE_CHECK=$(aws sqs get-queue-url --queue-name "$UNTAGGED_QUEUE" --query 'QueueUrl' --output text 2>/dev/null || echo "")
assert_not_empty "$UNTAGGED_QUEUE_CHECK" "untagged queue was NOT deleted"

UNTAGGED_TOPIC_CHECK=$(aws sns get-topic-attributes --topic-arn "$UNTAGGED_TOPIC_ARN" --query 'Attributes.TopicArn' --output text 2>/dev/null || echo "")
assert_not_empty "$UNTAGGED_TOPIC_CHECK" "untagged topic was NOT deleted"

# --- Test 6: tagged schedule group and schedules were deleted ---
echo ""
echo "=== Test 6: tagged schedule group was deleted ==="

SCHEDULE_GROUP_CHECK=$(aws scheduler get-schedule-group --name "$TAGGED_SCHEDULE_GROUP" 2>&1 || true)
assert_contains "$SCHEDULE_GROUP_CHECK" "ResourceNotFoundException|not found|Not Found" "tagged schedule group was deleted"

# --- Test 7: untagged schedule group was NOT deleted ---
echo ""
echo "=== Test 7: untagged schedule group was NOT deleted ==="

UNTAGGED_GROUP_CHECK=$(aws scheduler get-schedule-group --name "$UNTAGGED_SCHEDULE_GROUP" --query 'Name' --output text 2>/dev/null || echo "")
assert_not_empty "$UNTAGGED_GROUP_CHECK" "untagged schedule group was NOT deleted"

# --- Test 8: untagged resources matching the test naming convention were deleted ---
# This is the case the tag-based tests above cannot cover: the gateway only tags what it creates
# with Source=Brighter, so real leaked resources carry no Environment=Test tag and are found by
# name alone. Test 5 is the counterpart -- untagged resources that do not match the convention
# must survive.
echo ""
echo "=== Test 8: convention-named untagged resources were deleted ==="

assert_eventually_contains \
    "aws sqs get-queue-url --queue-name \"$CONVENTION_QUEUE\"" \
    "NonExistentQueue|does not exist" "convention-named queue was deleted"

assert_eventually_contains \
    "aws sqs get-queue-url --queue-name \"$CONVENTION_FIFO_QUEUE\"" \
    "NonExistentQueue|does not exist" "convention-named FIFO queue was deleted"

assert_eventually_contains \
    "aws sns get-topic-attributes --topic-arn \"$CONVENTION_TOPIC_ARN\"" \
    "NotFound|not found|Not Found" "convention-named topic was deleted"

assert_eventually_contains \
    "aws sns get-topic-attributes --topic-arn \"$CONVENTION_FIFO_TOPIC_ARN\"" \
    "NotFound|not found|Not Found" "convention-named FIFO topic was deleted"

assert_eventually_contains \
    "aws sqs get-queue-url --queue-name \"$DLQ_ASYNC_QUEUE\"" \
    "NonExistentQueue|does not exist" "prefix-named async DLQ queue was deleted"

# --- Test 9: the age guard defers resources a live run may still be using ---
# The sweep fires on every CI completion, and CI declares no concurrency group, so a run other
# than the one that triggered the sweep is routinely still in flight. Queues carry a
# CreatedTimestamp; SNS reports no creation time at all, so a topic's age has to be established
# by stamping it on first sight and reading the stamp back on a later sweep.
echo ""
echo "=== Test 9: young resources are deferred, not deleted ==="

GUARDED_QUEUE="sqs-std-$(hex_id)"
GUARDED_QUEUE_URL=$(aws sqs create-queue \
    --queue-name "$GUARDED_QUEUE" \
    --query 'QueueUrl' --output text)
GUARDED_TOPIC="sns-std-$(hex_id)"
GUARDED_TOPIC_ARN=$(aws sns create-topic \
    --name "$GUARDED_TOPIC" \
    --query 'TopicArn' --output text)
echo "  Created young convention-named queue: $GUARDED_QUEUE"
echo "  Created young convention-named topic: $GUARDED_TOPIC"

# A dry run has to preview what the real run would do, including what it would decline to do --
# a developer who runs the tests locally and then dry-runs to see what leaked must not be shown
# an empty list.
GUARD_DRY_OUTPUT=$(CLEANUP_MIN_AGE_SECONDS=3600 "$CLEANUP_SCRIPT" --dry-run 2>&1)

assert_contains "$GUARD_DRY_OUTPUT" "Would skip \(too young\): $GUARDED_QUEUE" \
    "dry-run previews the young queue as skipped"
assert_contains "$GUARD_DRY_OUTPUT" "Would skip \(too young\): $GUARDED_TOPIC" \
    "dry-run previews the young topic as skipped"
assert_not_contains "$GUARD_DRY_OUTPUT" "Would delete untagged test queue: $GUARDED_QUEUE" \
    "dry-run does not offer to delete the young queue"
assert_not_contains "$GUARD_DRY_OUTPUT" "Would delete untagged test topic: $GUARDED_TOPIC" \
    "dry-run does not offer to delete the young topic"

GUARD_RUN_OUTPUT=$(CLEANUP_MIN_AGE_SECONDS=3600 "$CLEANUP_SCRIPT" 2>&1)
GUARD_RUN_EXIT=$?

assert_eq "0" "$GUARD_RUN_EXIT" "guarded run exits with 0"
assert_contains "$GUARD_RUN_OUTPUT" "too young|created in the last" \
    "the run reports what it deferred rather than passing over it silently"

QUEUE_CHECK=$(aws sqs get-queue-url --queue-name "$GUARDED_QUEUE" --query 'QueueUrl' --output text 2>/dev/null || echo "")
assert_not_empty "$QUEUE_CHECK" "young queue survived the guarded run"

TOPIC_CHECK=$(aws sns get-topic-attributes --topic-arn "$GUARDED_TOPIC_ARN" \
    --query 'Attributes.TopicArn' --output text 2>/dev/null || echo "")
assert_not_empty "$TOPIC_CHECK" "young topic survived the guarded run"

# The stamp is the whole mechanism for topics: without it the next sweep has no more idea how
# old the topic is than this one did, and would defer it forever.
STAMP=$(aws sns list-tags-for-resource --resource-arn "$GUARDED_TOPIC_ARN" \
    --query "Tags[?Key=='BrighterSweepFirstSeen'].Value | [0]" --output text 2>/dev/null || echo "")
assert_contains "$STAMP" "^[0-9]+$" "the guarded run stamped the topic with a first-seen time"

# --- Test 10: an unusable age guard stops the sweep rather than disabling itself ---
# The guard is the only thing standing between the sweep and a live run's resources, and the
# script deliberately omits set -e, so a value bash cannot compare has to be refused up front.
echo ""
echo "=== Test 10: a non-numeric CLEANUP_MIN_AGE_SECONDS refuses to run ==="

BAD_AGE_OUTPUT=$(CLEANUP_MIN_AGE_SECONDS=an-hour "$CLEANUP_SCRIPT" 2>&1)
BAD_AGE_EXIT=$?

assert_eq "1" "$BAD_AGE_EXIT" "cleanup exits non-zero on a non-numeric age guard"
assert_contains "$BAD_AGE_OUTPUT" "CLEANUP_MIN_AGE_SECONDS" "the error names the offending variable"

QUEUE_CHECK=$(aws sqs get-queue-url --queue-name "$GUARDED_QUEUE" --query 'QueueUrl' --output text 2>/dev/null || echo "")
assert_not_empty "$QUEUE_CHECK" "nothing was deleted before the guard was validated"

# --- Test 11: releasing the guard deletes what it deferred ---
# Deferral must not become permanent: a stamped topic is still swept once it is old enough.
echo ""
echo "=== Test 11: deferred resources are deleted once the guard allows it ==="

CLEANUP_MIN_AGE_SECONDS=0 "$CLEANUP_SCRIPT" >/dev/null 2>&1

assert_eventually_contains \
    "aws sqs get-queue-url --queue-name \"$GUARDED_QUEUE\"" \
    "NonExistentQueue|does not exist" "the deferred queue was deleted once old enough"

assert_eventually_contains \
    "aws sns get-topic-attributes --topic-arn \"$GUARDED_TOPIC_ARN\"" \
    "NotFound|not found|Not Found" "the deferred topic was deleted once old enough"

# Teardown is handled by the EXIT trap defined at the top of the script.

# --- Results ---
echo ""
echo "=== Results ==="
echo "  Passed: $PASS"
echo "  Failed: $FAIL"

if [[ $FAIL -gt 0 ]]; then
    exit 1
fi
exit 0
