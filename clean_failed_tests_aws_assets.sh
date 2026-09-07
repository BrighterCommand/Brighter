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
#   CLEANUP_PARALLELISM      concurrent deletions in the name sweep (default 16)
#   CLEANUP_MIN_AGE_SECONDS  resources younger than this are left alone (default 3600; 0 disables).
#                            Must be a whole number of seconds; anything else is refused.
#
# IAM: as well as the delete and list calls, the age guard needs sqs:GetQueueAttributes and --
# because SNS reports no creation time -- sns:ListTagsForResource and sns:TagResource, which it
# uses to stamp a topic on first sight and read that stamp back on a later sweep.

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
# Applied in both the tag sweep and the name sweep, to queues and to topics, so that a CI job
# still in flight does not have its resources deleted out from under it. The sweep fires on every
# CI completion as well as on a schedule, and CI declares no concurrency group, so the run that
# triggered a sweep is never the only one in the account.
#
# The guard is the only thing standing between the sweep and a live run's resources, and this
# script deliberately omits `set -e`, so a value bash cannot compare would evaluate false and
# silently disable it. Refuse to run instead.
MIN_AGE_SECONDS="${CLEANUP_MIN_AGE_SECONDS:-3600}"
if [[ ! "$MIN_AGE_SECONDS" =~ ^[0-9]+$ ]]; then
    echo "ERROR: CLEANUP_MIN_AGE_SECONDS must be a whole number of seconds (got '$MIN_AGE_SECONDS')."
    echo "  It guards a live test run's resources against this sweep. Disabling it by accident is"
    echo "  worse than not sweeping at all, so nothing is deleted."
    exit 1
fi
NOW=$(date +%s)

# Stamped on a matched SNS topic the first time a sweep sees it; see topic_is_too_young.
FIRST_SEEN_TAG="BrighterSweepFirstSeen"

# True when a queue is young enough that a run still in flight may be using it. An age that
# cannot be read counts as young: declining to delete costs a deferred leak, deleting wrongly
# costs somebody else's build.
queue_is_too_young() {
    local queue_url="$1" created
    [[ "$MIN_AGE_SECONDS" -gt 0 ]] || return 1

    created=$(aws sqs get-queue-attributes --queue-url "$queue_url" \
        --attribute-names CreatedTimestamp \
        --query "Attributes.CreatedTimestamp" --output text 2>/dev/null || echo "")
    [[ "$created" =~ ^[0-9]+$ ]] || return 0

    [[ $(( NOW - created )) -lt "$MIN_AGE_SECONDS" ]]
}

# True when a topic may belong to a run still in flight.
#
# SNS reports no creation time -- there is no such attribute, and the tagging API does not carry
# one -- so a topic's age cannot be read, only established. The first sweep to match a topic
# stamps it with FIRST_SEEN_TAG and leaves it; a later sweep deletes it once that stamp is
# MIN_AGE_SECONDS old. A topic a live run created moments ago therefore always survives its first
# sweep, and nothing is deferred forever -- the sweep after the window takes it.
#
# Needs sns:ListTagsForResource and sns:TagResource. Without TagResource no stamp ever lands and
# every topic is deferred indefinitely, which is the leak this script exists to stop, so that
# failure is reported rather than swallowed.
#
# A tag read that fails -- most likely throttling, since SNS's tagging APIs are documented at
# around 10 TPS per account and this runs at CLEANUP_PARALLELISM -- must not be confused with a
# topic that has no stamp yet: re-stamping on every failed read would reset the clock every
# sweep and defer the topic forever. So a failed read defers and leaves any existing stamp
# alone. The CLI distinguishes the two: a successful query with no such tag prints None, a
# failed call prints nothing and exits non-zero.
topic_is_too_young() {
    local topic_arn="$1" first_seen
    [[ "$MIN_AGE_SECONDS" -gt 0 ]] || return 1

    if ! first_seen=$(aws sns list-tags-for-resource --resource-arn "$topic_arn" \
        --query "Tags[?Key=='$FIRST_SEEN_TAG'].Value | [0]" --output text 2>/dev/null); then
        echo "    WARNING: could not read tags on ${topic_arn##*:} (needs sns:ListTagsForResource, or the call was throttled); deferring without stamping" >&2
        return 0
    fi

    if [[ "$first_seen" =~ ^[0-9]+$ ]]; then
        [[ $(( NOW - first_seen )) -lt "$MIN_AGE_SECONDS" ]]
        return
    fi

    # First sighting -- the read succeeded and carried no stamp. Record it and defer. A dry run
    # writes nothing, so it previews the same deferral the next real run would make.
    if ! $DRY_RUN; then
        aws sns tag-resource --resource-arn "$topic_arn" \
            --tags "Key=$FIRST_SEEN_TAG,Value=$NOW" >/dev/null 2>&1 \
            || echo "    WARNING: could not stamp $FIRST_SEEN_TAG on ${topic_arn##*:} (needs sns:TagResource); it will be deferred again next sweep" >&2
    fi
    return 0
}

# The name sweep runs these checks in parallel, each in its own bash subshell.
export -f queue_is_too_young topic_is_too_young
export MIN_AGE_SECONDS NOW FIRST_SEEN_TAG DRY_RUN

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
        if topic_is_too_young "$arn"; then
            if $DRY_RUN; then
                echo "  [DRY RUN] Would skip (too young): ${arn##*:}"
            else
                echo "  Skipping tagged topic (too young): ${arn##*:}"
            fi
            continue
        fi

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

        # Resolved and age-checked before the dry-run branch, so that the preview reports the
        # same decision the real run would reach rather than a longer list than it would act on.
        QUEUE_URL=$(aws sqs get-queue-url --queue-name "$QUEUE_NAME" --query 'QueueUrl' --output text 2>&1 || echo "")
        if [[ -z "$QUEUE_URL" || "$QUEUE_URL" == *"NonExistentQueue"* ]]; then
            echo "  Queue already gone: $QUEUE_NAME"
            continue
        fi

        if queue_is_too_young "$QUEUE_URL"; then
            if $DRY_RUN; then
                echo "  [DRY RUN] Would skip (too young): $QUEUE_NAME"
            else
                echo "  Skipping tagged queue (too young): $QUEUE_NAME"
            fi
            continue
        fi

        if $DRY_RUN; then
            echo "  [DRY RUN] Would delete queue: $QUEUE_NAME ($arn)"
        else
            echo "  Deleting queue: $QUEUE_NAME ($QUEUE_URL)"
            aws sqs delete-queue --queue-url "$QUEUE_URL" 2>&1 || echo "    WARNING: failed to delete queue $QUEUE_NAME"
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
TEST_PREFIXES="Producer-Send-Tests|Producer-Requeue-Tests|Producer-DLQ-Tests|Producer-Scheduler-Tests|Producer-Scheduler-Async-Tests|Producer-Fire-Scheduler-Tests|Producer-Fire-Scheduler-Async-Tests|Producer-Tag-Tests|Producer-FSR-Tests|Producer-FSRA-Tests|Consumer-Requeue-Tests|Consumer-DLQ-Tests|Consumer-DLQ-Async|Consumer-DLQ-Fifo|Consumer-Fallback-Tests|Consumer-Invalid-Tests|Consumer-NoChan-Tests|Buffered-Consumer-Tests|Buffered-Scheduler-Tests|Buffered-Scheduler-Async-Tests|Buffered-FSR-Tests|Redrive-Tests|Redrive-DLQ-Tests|Raw-Msg-Delivery-Tests|DLQ-Reader|Invalid-Reader"

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

# MIN_AGE_SECONDS, NOW and the two age predicates are defined near the top of the file so the tag
# sweep can share them.

# Splits the names read from stdin into OLD_ENOUGH and TOO_YOUNG using the named predicate. Each
# check costs an AWS call, so they run at the same parallelism as the deletions.
partition_by_age() {
    local predicate="$1" status name
    OLD_ENOUGH=()
    TOO_YOUNG=()
    while read -r status name; do
        case "$status" in
            young) TOO_YOUNG+=("$name") ;;
            old)   OLD_ENOUGH+=("$name") ;;
        esac
    done < <(xargs -P "$PARALLELISM" -I {} bash -c \
        'if "$2" "$1"; then echo "young $1"; else echo "old $1"; fi' _ {} "$predicate")
}

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

# Drop the ones that are not yet known to be old enough. Unlike a queue, a topic has no
# creation time to read, so the first sweep to see one stamps it and defers; see
# topic_is_too_young.
MATCHED_TOPIC_COUNT=${#MATCHED_TOPICS[@]}
if [[ ${#MATCHED_TOPICS[@]} -gt 0 && "$MIN_AGE_SECONDS" -gt 0 ]]; then
    partition_by_age topic_is_too_young < <(printf '%s\n' "${MATCHED_TOPICS[@]}")

    if [[ ${#TOO_YOUNG[@]} -gt 0 ]]; then
        if $DRY_RUN; then
            for topic_arn in "${TOO_YOUNG[@]}"; do
                echo "  [DRY RUN] Would skip (too young): ${topic_arn##*:}"
            done
        else
            echo "  Deferred ${#TOO_YOUNG[@]} topic(s) not yet known to be older than $(( MIN_AGE_SECONDS / 60 )) minute(s); a test run may still be using them"
        fi
    fi

    MATCHED_TOPICS=(${OLD_ENOUGH[@]+"${OLD_ENOUGH[@]}"})
fi

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
echo "  Matched $MATCHED_TOPIC_COUNT untagged test topic(s), acted on ${#MATCHED_TOPICS[@]}"

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

# Drop the ones that are too young to be certain about. The filter runs in both modes: a dry run
# that quietly omitted them would show a developer who had just run the tests an empty list,
# which is the opposite of what the flag is for.
MATCHED_QUEUE_COUNT=${#MATCHED_QUEUES[@]}
if [[ ${#MATCHED_QUEUES[@]} -gt 0 && "$MIN_AGE_SECONDS" -gt 0 ]]; then
    partition_by_age queue_is_too_young < <(printf '%s\n' "${MATCHED_QUEUES[@]}")

    if [[ ${#TOO_YOUNG[@]} -gt 0 ]]; then
        if $DRY_RUN; then
            for queue_url in "${TOO_YOUNG[@]}"; do
                echo "  [DRY RUN] Would skip (too young): ${queue_url##*/}"
            done
        else
            echo "  Skipped ${#TOO_YOUNG[@]} queue(s) created in the last $(( MIN_AGE_SECONDS / 60 )) minute(s); a test run may still be using them"
        fi
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
echo "  Matched $MATCHED_QUEUE_COUNT untagged test queue(s), acted on ${#MATCHED_QUEUES[@]}"

echo ""
echo "Cleanup complete."
exit 0
