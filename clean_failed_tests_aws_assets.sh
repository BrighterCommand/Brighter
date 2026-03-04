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
    --output text 2>&1)
TAG_API_EXIT=$?
if [[ $TAG_API_EXIT -ne 0 ]]; then
    echo "ERROR: Failed to query Resource Groups Tagging API (exit code $TAG_API_EXIT)."
    echo "  Ensure the caller has the resourcegroupstaggingapi:GetResources IAM permission."
    echo "  Response: $RESOURCE_ARNS"
    exit 1
fi

if [[ -z "$RESOURCE_ARNS" ]]; then
    echo "No resources found with Environment=Test tag."
    exit 0
fi

# --- Categorise ARNs by resource type ---
SUBSCRIPTIONS=()
TOPICS=()
QUEUES=()

for arn in $RESOURCE_ARNS; do
    # Count colon-separated segments to distinguish SNS topics (6) from subscriptions (7)
    COLON_COUNT=$(echo "$arn" | tr -cd ':' | wc -c | tr -d ' ')
    case "$arn" in
        *:sns:*)
            if [[ "$COLON_COUNT" -ge 7 ]]; then
                SUBSCRIPTIONS+=("$arn")
            else
                TOPICS+=("$arn")
            fi
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
if [[ ${#SUBSCRIPTIONS[@]} -gt 0 ]]; then
    for arn in "${SUBSCRIPTIONS[@]}"; do
        if $DRY_RUN; then
            echo "  [DRY RUN] Would delete subscription: $arn"
        else
            echo "  Deleting subscription: $arn"
            aws sns unsubscribe --subscription-arn "$arn" 2>&1 || echo "    WARNING: failed to delete subscription $arn"
        fi
    done
fi

# 2. Topics
if [[ ${#TOPICS[@]} -gt 0 ]]; then
    for arn in "${TOPICS[@]}"; do
        # Delete any subscriptions on this topic that weren't tagged individually
        if ! $DRY_RUN; then
            TOPIC_SUBS=$(aws sns list-subscriptions-by-topic --topic-arn "$arn" \
                --query 'Subscriptions[*].SubscriptionArn' --output text 2>&1 || echo "")
            for sub_arn in $TOPIC_SUBS; do
                [[ "$sub_arn" == "PendingConfirmation" ]] && continue
                echo "  Deleting subscription on topic: $sub_arn"
                aws sns unsubscribe --subscription-arn "$sub_arn" 2>&1 || echo "    WARNING: failed to delete subscription $sub_arn"
            done
        fi

        if $DRY_RUN; then
            echo "  [DRY RUN] Would delete topic: $arn"
        else
            echo "  Deleting topic: $arn"
            aws sns delete-topic --topic-arn "$arn" 2>&1 || echo "    WARNING: failed to delete topic $arn"
        fi
    done
fi

# 3. Queues — need queue URL from ARN
if [[ ${#QUEUES[@]} -gt 0 ]]; then
    for arn in "${QUEUES[@]}"; do
        # Extract queue name from ARN (last segment)
        QUEUE_NAME="${arn##*:}"

        if $DRY_RUN; then
            echo "  [DRY RUN] Would delete queue: $QUEUE_NAME ($arn)"
        else
            QUEUE_URL=$(aws sqs get-queue-url --queue-name "$QUEUE_NAME" --query 'QueueUrl' --output text 2>&1 || echo "")
            if [[ -n "$QUEUE_URL" && "$QUEUE_URL" != *"NonExistentQueue"* ]]; then
                echo "  Deleting queue: $QUEUE_NAME ($QUEUE_URL)"
                aws sqs delete-queue --queue-url "$QUEUE_URL" 2>&1 || echo "    WARNING: failed to delete queue $QUEUE_NAME"
            else
                echo "  Queue already gone: $QUEUE_NAME"
            fi
        fi
    done
fi

echo "Cleanup complete."
exit 0
