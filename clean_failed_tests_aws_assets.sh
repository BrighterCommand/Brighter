#!/bin/bash
# clean_failed_tests_aws_assets.sh
# Cleans up orphaned AWS test resources identified by the Environment=Test tag.
#
# Usage:
#   ./clean_failed_tests_aws_assets.sh            # delete tagged resources
#   ./clean_failed_tests_aws_assets.sh --dry-run   # list without deleting

# Intentionally omitting -e: individual deletion failures are soft errors handled inline.
set -uo pipefail

DRY_RUN=false
if [[ "${1:-}" == "--dry-run" ]]; then
    DRY_RUN=true
    echo "[DRY RUN] No resources will be deleted"
fi

# --- Helper: delete all schedules in a given group ---
# AWS CLI v2 auto-paginates by default, so all schedules are returned across pages.
delete_schedules_in_group() {
    local group_name="$1"
    local schedules
    schedules=$(aws scheduler list-schedules --group-name "$group_name" \
        --query 'Schedules[*].Name' --output text 2>&1 || echo "")
    for sched_name in $schedules; do
        [[ -z "$sched_name" || "$sched_name" == "None" ]] && continue
        if $DRY_RUN; then
            echo "    [DRY RUN] Would delete schedule: $sched_name (group: $group_name)"
        else
            echo "    Deleting schedule: $sched_name (group: $group_name)"
            aws scheduler delete-schedule --name "$sched_name" --group-name "$group_name" 2>&1 \
                || echo "      WARNING: failed to delete schedule $sched_name"
        fi
    done
}

# --- Discover tagged resources via Resource Groups Tagging API ---
# Note: AWS CLI v2 auto-paginates by default. The --query/--output flags are applied
# after all pages are aggregated, so this handles >100 resources without manual pagination.
echo "Querying resources tagged Environment=Test ..."

RESOURCE_ARNS=$(aws resourcegroupstaggingapi get-resources \
    --tag-filters Key=Environment,Values=Test \
    --resource-type-filters sqs:queue sns:topic scheduler:schedule-group \
    --query 'ResourceTagMappingList[*].ResourceARN' \
    --output text 2>&1)
TAG_API_EXIT=$?
if [[ $TAG_API_EXIT -ne 0 ]]; then
    echo "ERROR: Failed to query Resource Groups Tagging API (exit code $TAG_API_EXIT)."
    echo "  Ensure the caller has the resourcegroupstaggingapi:GetResources IAM permission."
    echo "  Response: $RESOURCE_ARNS"
    exit 1
fi

if [[ -z "$RESOURCE_ARNS" || "$RESOURCE_ARNS" == "None" ]]; then
    echo "No resources found with Environment=Test tag."
fi

# --- Categorise ARNs by resource type ---
# Note: SNS subscriptions cannot be tagged and will not appear in the Tagging API response.
# Subscriptions are cleaned up implicitly in the topic-deletion loop below via list-subscriptions-by-topic.
# The subscription bucket is kept for completeness in case AWS adds subscription tagging in future.
SUBSCRIPTIONS=()
TOPICS=()
QUEUES=()
SCHEDULE_GROUPS=()

if [[ -n "$RESOURCE_ARNS" && "$RESOURCE_ARNS" != "None" ]]; then
    for arn in $RESOURCE_ARNS; do
        # Count colons to distinguish SNS topics (5 colons) from subscriptions (6 colons)
        COLON_COUNT=$(echo "$arn" | tr -cd ':' | wc -c | tr -d ' ')
        case "$arn" in
            *:sns:*)
                if [[ "$COLON_COUNT" -ge 6 ]]; then
                    SUBSCRIPTIONS+=("$arn")
                else
                    TOPICS+=("$arn")
                fi
                ;;
            *:sqs:*)
                QUEUES+=("$arn")
                ;;
            *:scheduler:*/schedule-group/*)
                SCHEDULE_GROUPS+=("$arn")
                ;;
            *)
                echo "  Skipping unknown resource type: $arn"
                ;;
        esac
    done
fi

echo "Found: ${#SUBSCRIPTIONS[@]} subscription(s), ${#TOPICS[@]} topic(s), ${#QUEUES[@]} queue(s), ${#SCHEDULE_GROUPS[@]} schedule group(s)"

# --- Delete in order: subscriptions, then topics, then queues, then schedule groups ---

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

# 4. EventBridge Scheduler — delete schedules within groups, then the groups themselves
if [[ ${#SCHEDULE_GROUPS[@]} -gt 0 ]]; then
    for arn in "${SCHEDULE_GROUPS[@]}"; do
        # Extract group name from ARN (last segment after schedule-group/)
        GROUP_NAME="${arn##*/}"

        # Skip the 'default' group — it cannot be deleted, but we clean its schedules
        if [[ "$GROUP_NAME" == "default" ]]; then
            echo "  Cleaning schedules in default group (group itself cannot be deleted)"
            delete_schedules_in_group "$GROUP_NAME"
            continue
        fi

        echo "  Processing schedule group: $GROUP_NAME"
        delete_schedules_in_group "$GROUP_NAME"

        if $DRY_RUN; then
            echo "  [DRY RUN] Would delete schedule group: $GROUP_NAME"
        else
            echo "  Deleting schedule group: $GROUP_NAME"
            aws scheduler delete-schedule-group --name "$GROUP_NAME" 2>&1 \
                || echo "    WARNING: failed to delete schedule group $GROUP_NAME"
        fi
    done
fi

# --- Also clean up Brighter-tagged schedule groups (Source=Brighter) not caught above ---
# The AwsSchedulerFactory tags groups with Source=Brighter. We require both Source=Brighter
# AND Environment=Test to avoid accidentally deleting non-test resources in shared accounts.
echo "Checking for Brighter-tagged schedule groups ..."
BRIGHTER_GROUPS=$(aws resourcegroupstaggingapi get-resources \
    --tag-filters Key=Source,Values=Brighter Key=Environment,Values=Test \
    --resource-type-filters scheduler:schedule-group \
    --query 'ResourceTagMappingList[*].ResourceARN' \
    --output text 2>&1 || echo "")

if [[ -n "$BRIGHTER_GROUPS" && "$BRIGHTER_GROUPS" != "None" ]]; then
    for arn in $BRIGHTER_GROUPS; do
        GROUP_NAME="${arn##*/}"
        [[ "$GROUP_NAME" == "default" ]] && continue

        # Skip if already processed above
        if [[ ${#SCHEDULE_GROUPS[@]} -gt 0 ]] && printf '%s\n' "${SCHEDULE_GROUPS[@]}" | grep -qF "$arn"; then
            continue
        fi

        echo "  Processing Brighter schedule group: $GROUP_NAME"
        delete_schedules_in_group "$GROUP_NAME"

        if $DRY_RUN; then
            echo "  [DRY RUN] Would delete Brighter schedule group: $GROUP_NAME"
        else
            echo "  Deleting Brighter schedule group: $GROUP_NAME"
            aws scheduler delete-schedule-group --name "$GROUP_NAME" 2>&1 \
                || echo "    WARNING: failed to delete schedule group $GROUP_NAME"
        fi
    done
else
    echo "  No additional Brighter schedule groups found."
fi

echo "Cleanup complete."
exit 0
