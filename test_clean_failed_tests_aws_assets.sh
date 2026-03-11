#!/bin/bash
# test_clean_failed_tests_aws_assets.sh
# Integration test for clean_failed_tests_aws_assets.sh
# Creates tagged and untagged AWS resources, then verifies the cleanup script
# only deletes resources tagged with Environment=Test.

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CLEANUP_SCRIPT="$SCRIPT_DIR/clean_failed_tests_aws_assets.sh"

# --- Cleanup trap: ensure test resources are removed regardless of outcome ---
TAGGED_QUEUE_URL=""
UNTAGGED_QUEUE_URL=""
TAGGED_TOPIC_ARN=""
UNTAGGED_TOPIC_ARN=""

cleanup_test_resources() {
    echo ""
    echo "=== Trap Teardown ==="
    [[ -n "$UNTAGGED_QUEUE_URL" ]] && aws sqs delete-queue --queue-url "$UNTAGGED_QUEUE_URL" 2>/dev/null || true
    [[ -n "$UNTAGGED_TOPIC_ARN" ]] && aws sns delete-topic --topic-arn "$UNTAGGED_TOPIC_ARN" 2>/dev/null || true
    [[ -n "$TAGGED_QUEUE_URL" ]] && aws sqs delete-queue --queue-url "$TAGGED_QUEUE_URL" 2>/dev/null || true
    [[ -n "$TAGGED_TOPIC_ARN" ]] && aws sns delete-topic --topic-arn "$TAGGED_TOPIC_ARN" 2>/dev/null || true
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

# --- Setup: create tagged and untagged resources ---
PREFIX="cleanup-test-$(date +%s)"
echo "=== Setup: creating test resources (prefix: $PREFIX) ==="

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

# Allow time for tag propagation — the Resource Groups Tagging API is eventually consistent
sleep 5

# --- Test 1: --dry-run lists tagged resources without deleting ---
echo ""
echo "=== Test 1: --dry-run lists tagged resources without deleting ==="

DRY_RUN_OUTPUT=$("$CLEANUP_SCRIPT" --dry-run 2>&1)
DRY_RUN_EXIT=$?

assert_eq "0" "$DRY_RUN_EXIT" "dry-run exits with 0"
assert_contains "$DRY_RUN_OUTPUT" "DRY RUN" "output indicates dry-run mode"
assert_contains "$DRY_RUN_OUTPUT" "$TAGGED_QUEUE" "output lists tagged queue"
assert_contains "$DRY_RUN_OUTPUT" "$TAGGED_TOPIC" "output lists tagged topic"

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
sleep 5

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
