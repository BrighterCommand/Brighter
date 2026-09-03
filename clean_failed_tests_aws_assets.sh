#!/bin/bash
# clean_failed_tests_aws_assets.sh
# Cleans up orphaned AWS test resources, found two ways: by the Environment=Test tag, and by the
# naming conventions the AWS test suites use. The name sweep is not a nicety -- the gateway only
# tags what it creates with Source=Brighter, so most leaked resources carry no Environment=Test
# tag at all and the tag query alone finds nothing.
#
# Operates on the ambient AWS region (AWS_REGION / AWS_DEFAULT_REGION / the configured default).
# Resources leaked in any other region are invisible to it, so a developer running the tests
# locally against a different default region needs to run it for that region too.
#
# Usage:
#   ./clean_failed_tests_aws_assets.sh            # delete orphaned resources
#   ./clean_failed_tests_aws_assets.sh --dry-run   # list without deleting
#
# Environment:
#   CLEANUP_PARALLELISM     concurrent deletions in the name sweep (default 16)
#   CLEANUP_MIN_AGE_SECONDS  queues younger than this are left alone (default 3600; 0 disables)

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

# Helper: convert an ISO-8601 timestamp to a Unix epoch second (cross-platform).
iso_to_epoch() {
    local ts="$1"
    local result
    # GNU date (Linux / GitHub Actions)
    result=$(date -d "$ts" +%s 2>/dev/null) && { echo "$result"; return; }
    # Python fallback (macOS / BSD)
    result=$(python3 -c "import sys,datetime; ts=sys.argv[1].replace('Z','+00:00'); print(int(datetime.datetime.fromisoformat(ts).timestamp()))" "$ts" 2>/dev/null) && { echo "$result"; return; }
    echo ""
}

# --- Age guard: resources younger than this are left alone ---
# Applied in both the tag sweep and the name sweep so that an in-flight CI job's
# resources are never deleted.  SNS has no creation-time API so topics are excluded.
MIN_AGE_SECONDS="${CLEANUP_MIN_AGE_SECONDS:-3600}"
NOW=$(date +%s)

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
                if [[ "$MIN_AGE_SECONDS" -gt 0 ]]; then
                    CREATED=$(aws sqs get-queue-attributes --queue-url "$QUEUE_URL" \
                        --attribute-names CreatedTimestamp \
                        --query "Attributes.CreatedTimestamp" --output text 2>/dev/null || echo "")
                    if [[ -n "$CREATED" && "$CREATED" != "None" && $(( NOW - CREATED )) -lt "$MIN_AGE_SECONDS" ]]; then
                        echo "  Skipping tagged queue (too young, $(( NOW - CREATED ))s old): $QUEUE_NAME"
                        continue
                    fi
                fi
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

        if [[ "$MIN_AGE_SECONDS" -gt 0 ]]; then
            CREATED_DATE=$(aws scheduler get-schedule-group --name "$GROUP_NAME" \
                --query 'CreationDate' --output text 2>/dev/null || echo "")
            if [[ -n "$CREATED_DATE" && "$CREATED_DATE" != "None" ]]; then
                CREATED_TS=$(iso_to_epoch "$CREATED_DATE")
                if [[ -n "$CREATED_TS" && $(( NOW - CREATED_TS )) -lt "$MIN_AGE_SECONDS" ]]; then
                    echo "  Skipping tagged schedule group (too young): $GROUP_NAME"
                    continue
                fi
            fi
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

        if [[ "$MIN_AGE_SECONDS" -gt 0 ]]; then
            CREATED_DATE=$(aws scheduler get-schedule-group --name "$GROUP_NAME" \
                --query 'CreationDate' --output text 2>/dev/null || echo "")
            if [[ -n "$CREATED_DATE" && "$CREATED_DATE" != "None" ]]; then
                CREATED_TS=$(iso_to_epoch "$CREATED_DATE")
                if [[ -n "$CREATED_TS" && $(( NOW - CREATED_TS )) -lt "$MIN_AGE_SECONDS" ]]; then
                    echo "  Skipping Brighter schedule group (too young): $GROUP_NAME"
                    continue
                fi
            fi
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

# --- Fallback: clean up untagged test resources by naming convention ---
# Most test fixtures are not tagged with Environment=Test -- the AWS gateway only stamps
# Source=Brighter -- so the Tagging API query above misses them. We therefore also sweep
# by name, matching the two naming conventions the AWS test suites use.
#
# 1. Hand-written fixtures name resources <TestPrefix>-<GUID>, truncated to 45 chars.
# 2. Generated MessageGateway tests (Paramore.Brighter.Test.Generator) name resources
#    <transport>-<type>[-ch]-<32 hex GUID>, e.g. sqs-fifo-019f8f426d6378db9404c3550dd9c3c1.fifo.
#    See tests/Paramore.Brighter.AWS.Tests/MessagingGateway/*MessageGatewayProvider.cs and the
#    matching files under tests/Paramore.Brighter.AWS.V4.Tests.
#
# Both patterns are anchored at the start only, so derived resources that append a suffix
# (-DLQ, -Invalid, -dlq.fifo, .fifo) are matched by the same rule as their parent.
TEST_PREFIXES="Producer-Send-Tests|Producer-Requeue-Tests|Producer-DLQ-Tests|Producer-Scheduler-Tests|Producer-Scheduler-Async-Tests|Producer-Fire-Scheduler-Tests|Producer-Fire-Scheduler-Async-Tests|Producer-Tag-Tests|Producer-FSR-Tests|Producer-FSRA-Tests|Consumer-Requeue-Tests|Consumer-DLQ-Tests|Consumer-DLQ-Fifo|Consumer-Fallback-Tests|Consumer-Invalid-Tests|Consumer-NoChan-Tests|Buffered-Consumer-Tests|Buffered-Scheduler-Tests|Buffered-Scheduler-Async-Tests|Buffered-FSR-Tests|Redrive-Tests|Redrive-DLQ-Tests|Raw-Msg-Delivery-Tests|DLQ-Reader|Invalid-Reader"

# The 32-hex GUID makes this pattern specific enough that it cannot collide with a
# hand-named resource; it is the only thing standing between a real queue and deletion.
# Note the asymmetry with TEST_PREFIXES above, which carries no such requirement and is anchored
# at the start only: a queue named DLQ-Reader-orders would be swept by it. That is tolerable in a
# dedicated test account and nowhere else.
GENERATED_TEST_PATTERN="(sqs|sns)-(std|fifo)(-ch)?-[0-9a-f]{32}"

# A resource is treated as a test leftover if its name matches either convention.
TEST_NAME_PATTERN="^($TEST_PREFIXES|$GENERATED_TEST_PATTERN)"

echo ""
echo "Scanning for untagged test resources by naming convention ..."

# Deletions run in parallel. A backlog of leaked resources runs to tens of thousands, and one
# AWS API call at a time does not get through that inside the cleanup workflow's timeout.
PARALLELISM="${CLEANUP_PARALLELISM:-16}"

# MIN_AGE_SECONDS and NOW are defined near the top of the file so the tag sweep can share them.

# Clean untagged SNS topics.
# SNS list-topics returns a NextToken, so the CLI's default auto-pagination sees every topic.
ALL_TOPICS=$(aws sns list-topics --query 'Topics[*].TopicArn' --output text 2>&1)
SNS_LIST_EXIT=$?
if [[ $SNS_LIST_EXIT -ne 0 ]]; then
    echo "ERROR: Failed to list SNS topics (exit code $SNS_LIST_EXIT)."
    echo "  Ensure the caller has sns:ListTopics permission."
    echo "  Response: $ALL_TOPICS"
    exit 1
fi
MATCHED_TOPICS=()
for topic_arn in $ALL_TOPICS; do
    [[ -z "$topic_arn" || "$topic_arn" == "None" ]] && continue
    # The topic name is the last ARN segment and cannot itself contain a colon.
    if [[ "${topic_arn##*:}" =~ $TEST_NAME_PATTERN ]]; then
        MATCHED_TOPICS+=("$topic_arn")
    fi
done

if [[ ${#MATCHED_TOPICS[@]} -gt 0 ]]; then
    if $DRY_RUN; then
        for topic_arn in "${MATCHED_TOPICS[@]}"; do
            echo "  [DRY RUN] Would delete untagged test topic: ${topic_arn##*:}"
        done
    else
        # Deleting a topic deletes its subscriptions with it, so there is no need to unsubscribe
        # first -- and skipping that saves an API call per topic.
        printf '%s\n' "${MATCHED_TOPICS[@]}" \
            | xargs -P "$PARALLELISM" -I {} sh -c '
                if aws sns delete-topic --topic-arn "$1" >/dev/null 2>&1; then
                    echo "  Deleted untagged test topic: ${1##*:}"
                else
                    echo "    WARNING: failed to delete topic ${1##*:}"
                fi' _ {}
    fi
fi
echo "  Found ${#MATCHED_TOPICS[@]} untagged test topic(s)"

# Clean untagged SQS queues.
# --page-size is required: without it SQS returns at most 1000 queues and no NextToken, so the
# CLI has nothing to paginate on and the rest are silently invisible.
ALL_QUEUES=$(aws sqs list-queues --page-size 1000 --query 'QueueUrls[*]' --output text 2>&1)
SQS_LIST_EXIT=$?
if [[ $SQS_LIST_EXIT -ne 0 ]]; then
    echo "ERROR: Failed to list SQS queues (exit code $SQS_LIST_EXIT)."
    echo "  Ensure the caller has sqs:ListQueues permission."
    echo "  Response: $ALL_QUEUES"
    exit 1
fi
MATCHED_QUEUES=()
for queue_url in $ALL_QUEUES; do
    [[ -z "$queue_url" || "$queue_url" == "None" ]] && continue
    if [[ "${queue_url##*/}" =~ $TEST_NAME_PATTERN ]]; then
        MATCHED_QUEUES+=("$queue_url")
    fi
done

# Drop the ones that are too young to be certain about. CreatedTimestamp costs a call per
# queue, so the lookups run at the same parallelism as the deletions.
if [[ ${#MATCHED_QUEUES[@]} -gt 0 && "$MIN_AGE_SECONDS" -gt 0 ]]; then
    OLD_ENOUGH=()
    while IFS= read -r queue_url; do
        [[ -n "$queue_url" ]] && OLD_ENOUGH+=("$queue_url")
    done < <(printf '%s\n' "${MATCHED_QUEUES[@]}" \
        | xargs -P "$PARALLELISM" -I {} sh -c '
            created=$(aws sqs get-queue-attributes --queue-url "$1" \
                --attribute-names CreatedTimestamp \
                --query "Attributes.CreatedTimestamp" --output text 2>/dev/null || echo "")
            case "$created" in
                ""|None)
                    # Age unknown — skip rather than risk deleting a resource a live run may be using.
                    ;;
                *)
                    if [ $(( $2 - created )) -ge "$3" ]; then
                        echo "$1"
                    fi
                    ;;
            esac' _ {} "$NOW" "$MIN_AGE_SECONDS")

    SKIPPED_QUEUES=$(( ${#MATCHED_QUEUES[@]} - ${#OLD_ENOUGH[@]} ))
    if [[ $SKIPPED_QUEUES -gt 0 ]]; then
        echo "  Skipped $SKIPPED_QUEUES queue(s) created in the last $(( MIN_AGE_SECONDS / 60 )) minute(s); a test run may still be using them"
    fi
    MATCHED_QUEUES=(${OLD_ENOUGH[@]+"${OLD_ENOUGH[@]}"})
fi

if [[ ${#MATCHED_QUEUES[@]} -gt 0 ]]; then
    if $DRY_RUN; then
        for queue_url in "${MATCHED_QUEUES[@]}"; do
            echo "  [DRY RUN] Would delete untagged test queue: ${queue_url##*/}"
        done
    else
        printf '%s\n' "${MATCHED_QUEUES[@]}" \
            | xargs -P "$PARALLELISM" -I {} sh -c '
                if aws sqs delete-queue --queue-url "$1" >/dev/null 2>&1; then
                    echo "  Deleted untagged test queue: ${1##*/}"
                else
                    echo "    WARNING: failed to delete queue ${1##*/}"
                fi' _ {}
    fi
fi
echo "  Found ${#MATCHED_QUEUES[@]} untagged test queue(s)"

echo ""
echo "Cleanup complete."
exit 0
