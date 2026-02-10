#!/bin/bash
# clean_failed_tests_aws_assets.sh
# Cleans up orphaned AWS test resources identified by the Environment=Test tag.
#
# Usage:
#   ./clean_failed_tests_aws_assets.sh            # delete tagged resources
#   ./clean_failed_tests_aws_assets.sh --dry-run   # list without deleting

set -uo pipefail

DRY_RUN=false
if [[ "${1:-}" == "--dry-run" ]]; then
    DRY_RUN=true
    echo "[DRY RUN] No resources will be deleted"
fi

# --- Discover tagged resources via Resource Groups Tagging API ---
echo "Querying resources tagged Environment=Test ..."

RESOURCE_ARNS=$(aws resourcegroupstaggingapi get-resources \
    --tag-filters Key=Environment,Values=Test \
    --query 'ResourceTagMappingList[*].ResourceARN' \
    --output text 2>/dev/null || echo "")

if [[ -z "$RESOURCE_ARNS" ]]; then
    echo "No resources found with Environment=Test tag."
    exit 0
fi

# --- Categorise ARNs by resource type ---
SUBSCRIPTIONS=()
TOPICS=()
QUEUES=()

for arn in $RESOURCE_ARNS; do
    case "$arn" in
        *:sns:*:*:*:*)
            # SNS subscription ARNs have a 6th colon-separated segment after the topic name
            SUBSCRIPTIONS+=("$arn")
            ;;
        *:sns:*)
            TOPICS+=("$arn")
            ;;
        *:sqs:*)
            QUEUES+=("$arn")
            ;;
        *)
            echo "  Skipping unknown resource type: $arn"
            ;;
    esac
done

echo "Found: ${#SUBSCRIPTIONS[@]} subscription(s), ${#TOPICS[@]} topic(s), ${#QUEUES[@]} queue(s)"

# --- Delete in order: subscriptions, then topics, then queues ---

# 1. Subscriptions
for arn in "${SUBSCRIPTIONS[@]+"${SUBSCRIPTIONS[@]}"}"; do
    [[ -z "$arn" ]] && continue
    if $DRY_RUN; then
        echo "  [DRY RUN] Would delete subscription: $arn"
    else
        echo "  Deleting subscription: $arn"
        aws sns unsubscribe --subscription-arn "$arn" 2>/dev/null || echo "    WARNING: failed to delete subscription $arn"
    fi
done

# 2. Topics
for arn in "${TOPICS[@]+"${TOPICS[@]}"}"; do
    [[ -z "$arn" ]] && continue

    # Delete any subscriptions on this topic that weren't tagged individually
    if ! $DRY_RUN; then
        TOPIC_SUBS=$(aws sns list-subscriptions-by-topic --topic-arn "$arn" \
            --query 'Subscriptions[*].SubscriptionArn' --output text 2>/dev/null || echo "")
        for sub_arn in $TOPIC_SUBS; do
            [[ "$sub_arn" == "PendingConfirmation" ]] && continue
            echo "  Deleting subscription on topic: $sub_arn"
            aws sns unsubscribe --subscription-arn "$sub_arn" 2>/dev/null || echo "    WARNING: failed to delete subscription $sub_arn"
        done
    fi

    if $DRY_RUN; then
        echo "  [DRY RUN] Would delete topic: $arn"
    else
        echo "  Deleting topic: $arn"
        aws sns delete-topic --topic-arn "$arn" 2>/dev/null || echo "    WARNING: failed to delete topic $arn"
    fi
done

# 3. Queues — need queue URL from ARN
for arn in "${QUEUES[@]+"${QUEUES[@]}"}"; do
    [[ -z "$arn" ]] && continue
    # Extract queue name from ARN (last segment)
    QUEUE_NAME="${arn##*:}"

    if $DRY_RUN; then
        echo "  [DRY RUN] Would delete queue: $QUEUE_NAME ($arn)"
    else
        QUEUE_URL=$(aws sqs get-queue-url --queue-name "$QUEUE_NAME" --query 'QueueUrl' --output text 2>/dev/null || echo "")
        if [[ -n "$QUEUE_URL" ]]; then
            echo "  Deleting queue: $QUEUE_NAME ($QUEUE_URL)"
            aws sqs delete-queue --queue-url "$QUEUE_URL" 2>/dev/null || echo "    WARNING: failed to delete queue $QUEUE_NAME"
        else
            echo "  Queue already gone: $QUEUE_NAME"
        fi
    fi
done

echo "Cleanup complete."
exit 0
