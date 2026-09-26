#!/bin/bash
# Exercises the real cleanup script with local AWS CLI responses, never real AWS.
# The MIT License (MIT)
# Copyright (c) 2026 Avtandil Ushikishvili <a.ushikishvili@gmail.com>
#
# Permission is hereby granted, free of charge, to any person obtaining a copy
# of this software and associated documentation files (the "Software"), to deal
# in the Software without restriction, including without limitation the rights
# to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
# copies of the Software, and to permit persons to whom the Software is
# furnished to do so, subject to the following conditions:
#
# The above copyright notice and this permission notice shall be included in
# all copies or substantial portions of the Software.
#
# THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
# IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
# FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
# AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
# LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
# OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
# THE SOFTWARE.
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CLEANUP_SCRIPT="${1:-$SCRIPT_DIR/clean_failed_tests_aws_assets.sh}"
TEST_ROOT=$(mktemp -d "${TMPDIR:-/tmp}/brighter-cleanup-tests.XXXXXX") || exit 1
trap 'rm -rf -- "$TEST_ROOT"' EXIT
mkdir "$TEST_ROOT/bin"

# A closed PATH plus an empty environment prevents falling back to a real AWS executable.
for utility in bash sh cat date tr wc grep xargs mktemp rm mkdir awk python3; do
    ln -s "$(command -v "$utility")" "$TEST_ROOT/bin/$utility" || exit 1
done
ln -s "$SCRIPT_DIR/tests/fixtures/aws-cleanup/InMemoryAwsCli.sh" "$TEST_ROOT/bin/aws" || exit 1
PASS=0
FAIL=0
TOPIC='arn:aws:sns:eu-west-1:000000000000:Producer-Send-Tests-topic'
QUEUE='https://sqs.eu-west-1.amazonaws.com/000000000000/Producer-Send-Tests-queue'
GROUP='arn:aws:scheduler:eu-west-1:000000000000:schedule-group/cleanup-group'
BUCKET='brightertestbucket-11111111-1111-1111-1111-111111111111'
OTHER_BUCKET='brightertestbucket-22222222-2222-2222-2222-222222222222'

arrange() {
    CASE_NAME="$1"
    CASE_DIR=$(mktemp -d "$TEST_ROOT/case.XXXXXX") || exit 1
    : > "$CASE_DIR/calls"
    LIMIT=0
    AGE=0
    CASE_TEMP="$CASE_DIR"
}

response() {
    printf '%s' "$2" > "$CASE_DIR/$1.stdout"
    printf '%s' "${3:-}" > "$CASE_DIR/$1.stderr"
    printf '%s' "${4:-0}" > "$CASE_DIR/$1.status"
}

aws_error() {
    response "$1" '' "An error occurred ($2) when calling the $3 operation: ${4:-simulated failure}" 254
}

run_cleanup() {
    local environment=("PATH=$TEST_ROOT/bin" "TMPDIR=$CASE_TEMP" "AWS_CLEANUP_FIXTURES=$CASE_DIR"
        "CLEANUP_MIN_AGE_SECONDS=$AGE" "CLEANUP_PARALLELISM=${PARALLELISM:-4}" "AWS_REGION=eu-west-1")
    if [[ "$LIMIT" != unset ]]; then
        environment+=("CLEANUP_MAX_DELETE_FAILURES=$LIMIT")
    fi
    env -i "${environment[@]}" bash "$CLEANUP_SCRIPT" "$@" > "$CASE_DIR/output" 2>&1
    STATUS=$?
    OUTPUT=$(cat "$CASE_DIR/output")
    if [[ -f "$CASE_DIR/unexpected" ]]; then
        echo "FAIL: $CASE_NAME: unexpected AWS command"
        FAIL=$((FAIL + 1))
    fi
}

assert_equal() {
    if [[ "$1" == "$2" ]]; then
        PASS=$((PASS + 1))
    else
        echo "FAIL: $CASE_NAME: $3 (expected '$1', got '$2')"
        echo "$OUTPUT"
        FAIL=$((FAIL + 1))
    fi
}

assert_contains() {
    if [[ "$OUTPUT" == *"$1"* ]]; then
        PASS=$((PASS + 1))
    else
        echo "FAIL: $CASE_NAME: missing '$1'"
        echo "$OUTPUT"
        FAIL=$((FAIL + 1))
    fi
}

assert_no_mutations() {
    local mutations
    mutations=$(grep -Ec ' (delete-|unsubscribe|tag-resource)|^s3 (rm|rb) ' "$CASE_DIR/calls" || true)
    assert_equal 0 "$mutations" 'no AWS mutations'
}

# Arrange / Act / Assert: every case executes the public script entry point.
arrange 'empty sweep'
run_cleanup
assert_equal 0 "$STATUS" 'exit status'
assert_contains 'Deletion summary: attempted=0 succeeded=0 already_absent=0 failed=0'
assert_no_mutations

for PARALLELISM in 1 4 16; do
    arrange "mixed parallel outcomes at parallelism $PARALLELISM"
    topics=""
    for ((number = 1; number <= 24; number++)); do
        topics="$topics $TOPIC-$number"
        if [[ $((number % 3)) -eq 0 ]]; then
            aws_error "sns.delete-topic.Producer-Send-Tests-topic-$number" Throttled DeleteTopic
        fi
    done
    response sns.list-topics "$topics"
    response sqs.list-queues "$QUEUE"
    run_cleanup
    assert_equal 1 "$STATUS" 'fail despite successful later queue deletion'
    assert_contains 'Deletion summary: attempted=25 succeeded=17 already_absent=0 failed=8'
    assert_contains 'Throttled'
    assert_equal 1 "$(grep -c 'sqs delete-queue' "$CASE_DIR/calls")" 'continued cleanup'
done
PARALLELISM=4

for LIMIT_VALUE in 1 2 08 999999999; do
    arrange "threshold $LIMIT_VALUE with two failures"
    LIMIT="$LIMIT_VALUE"
    response sns.list-topics "$TOPIC-1 $TOPIC-2"
    aws_error sns.delete-topic AuthorizationError DeleteTopic
    run_cleanup
    expected=0
    [[ "$LIMIT" == 1 ]] && expected=1
    assert_equal "$expected" "$STATUS" 'threshold comparison'
    assert_contains 'Deletion summary: attempted=2 succeeded=0 already_absent=0 failed=2'
done

for invalid in '' -1 abc 1.5 1e3 999999999999999999999999999999; do
    arrange "invalid threshold '$invalid'"
    LIMIT="$invalid"
    run_cleanup
    assert_equal 1 "$STATUS" 'refuse unsafe configuration'
    assert_equal '' "$(cat "$CASE_DIR/calls")" 'validate before any AWS calls'
done

for code in AWS.SimpleQueueService.NonExistentQueue QueueDoesNotExist; do
    arrange "known already-absent queue: $code"
    response sqs.list-queues "$QUEUE"
    aws_error sqs.delete-queue "$code" DeleteQueue
    run_cleanup
    assert_equal 0 "$STATUS" 'already absent is benign'
    assert_contains 'Deletion summary: attempted=1 succeeded=0 already_absent=1 failed=0'
done

arrange 'authorization error mentioning a missing resource'
LIMIT='unset'
response sqs.list-queues "$QUEUE"
aws_error sqs.delete-queue AccessDenied DeleteQueue 'QueueDoesNotExist: not found or no access'
run_cleanup
assert_equal 1 "$STATUS" 'do not classify error-message substrings as absence'
assert_contains 'Deletion summary: attempted=1 succeeded=0 already_absent=0 failed=1'
assert_contains 'AccessDenied'

arrange 'an error for a different operation is not treated as absence'
response sqs.list-queues "$QUEUE"
aws_error sqs.delete-queue QueueDoesNotExist GetQueueUrl
run_cleanup
assert_equal 1 "$STATUS" 'operation must match'
assert_contains 'Deletion summary: attempted=1 succeeded=0 already_absent=0 failed=1'

arrange 'an unstructured CLI error is a genuine failure'
response sqs.list-queues "$QUEUE"
response sqs.delete-queue '' 'Could not connect: resource not found' 1
run_cleanup
assert_equal 1 "$STATUS" 'do not infer absence from arbitrary text'
assert_contains 'Could not connect: resource not found'

arrange 'SNS already-absent responses'
response resourcegroupstaggingapi.get-resources "$TOPIC"
response sns.list-subscriptions-by-topic "$TOPIC:subscription-id"
aws_error sns.unsubscribe NotFound Unsubscribe
aws_error sns.delete-topic NotFound DeleteTopic
run_cleanup
assert_equal 0 "$STATUS" 'absent subscriptions and topics are benign'
assert_contains 'Deletion summary: attempted=2 succeeded=0 already_absent=2 failed=0'

arrange 'pending and empty subscription markers are not ARNs'
response resourcegroupstaggingapi.get-resources "$TOPIC"
response sns.list-subscriptions-by-topic 'PendingConfirmation None'
run_cleanup
assert_equal 0 "$STATUS" 'markers are skipped'
assert_equal 0 "$(grep -c 'sns unsubscribe' "$CASE_DIR/calls" || true)" 'no marker used as ARN'
assert_contains 'Deletion summary: attempted=1 succeeded=1 already_absent=0 failed=0'

arrange 'tag and name passes count attempts rather than distinct resources'
response resourcegroupstaggingapi.get-resources "$TOPIC"
response sns.list-topics "$TOPIC"
run_cleanup
assert_equal 0 "$STATUS" 'duplicate discovery is safe'
assert_contains 'Deletion summary: attempted=2 succeeded=2 already_absent=0 failed=0'

arrange 'serial topic, subscription, queue, schedule and group failures'
response resourcegroupstaggingapi.get-resources "$TOPIC arn:aws:sqs:eu-west-1:000000000000:Producer-Send-Tests-queue"
response resourcegroupstaggingapi.get-resources.brighter "$GROUP"
response sns.list-subscriptions-by-topic "$TOPIC:subscription-id"
response scheduler.list-schedules 'schedule-one'
aws_error sns.unsubscribe AuthorizationError Unsubscribe
aws_error sns.delete-topic AuthorizationError DeleteTopic
aws_error sqs.delete-queue RequestThrottled DeleteQueue
aws_error scheduler.delete-schedule AccessDeniedException DeleteSchedule
aws_error scheduler.delete-schedule-group ConflictException DeleteScheduleGroup
run_cleanup
assert_equal 1 "$STATUS" 'serial failures propagate'
assert_contains 'Deletion summary: attempted=5 succeeded=0 already_absent=0 failed=5'

arrange 'scheduler already-absent responses'
response resourcegroupstaggingapi.get-resources.brighter "$GROUP"
response scheduler.list-schedules 'schedule-one'
aws_error scheduler.delete-schedule ResourceNotFoundException DeleteSchedule
aws_error scheduler.delete-schedule-group ResourceNotFoundException DeleteScheduleGroup
run_cleanup
assert_equal 0 "$STATUS" 'absent schedules and groups are benign'
assert_contains 'Deletion summary: attempted=2 succeeded=0 already_absent=2 failed=0'

arrange 'failed subscription listing does not create bogus delete calls'
response resourcegroupstaggingapi.get-resources "$TOPIC"
aws_error sns.list-subscriptions-by-topic AuthorizationError ListSubscriptionsByTopic
LIMIT=999
run_cleanup
assert_equal 1 "$STATUS" 'discovery failures are not covered by deletion threshold'
assert_equal 0 "$(grep -c 'sns unsubscribe' "$CASE_DIR/calls" || true)" 'no diagnostic words used as ARNs'

arrange 'failed schedule listing does not create bogus delete calls'
response resourcegroupstaggingapi.get-resources.brighter "$GROUP"
aws_error scheduler.list-schedules AccessDeniedException ListSchedules
run_cleanup
assert_equal 1 "$STATUS" 'failed schedule discovery'
assert_equal 0 "$(grep -c 'scheduler delete-schedule ' "$CASE_DIR/calls" || true)" 'no diagnostic words used as schedule names'

arrange 'failed queue URL lookup is not an already-deleted queue'
response resourcegroupstaggingapi.get-resources 'arn:aws:sqs:eu-west-1:000000000000:Producer-Send-Tests-queue'
aws_error sqs.get-queue-url AccessDenied GetQueueUrl 'NonExistentQueue may require permissions'
run_cleanup
assert_equal 1 "$STATUS" 'failed queue lookup'
assert_no_mutations

arrange 'failed additional tag query is not a list of group ARNs'
aws_error resourcegroupstaggingapi.get-resources.brighter AccessDeniedException GetResources
run_cleanup
assert_equal 1 "$STATUS" 'failed additional discovery'
assert_no_mutations

arrange 'dry-run never deletes or stamps'
response resourcegroupstaggingapi.get-resources "$TOPIC arn:aws:sqs:eu-west-1:000000000000:Producer-Send-Tests-queue"
response resourcegroupstaggingapi.get-resources.brighter "$GROUP"
response sns.list-topics "$TOPIC"
response sqs.list-queues "$QUEUE"
run_cleanup --dry-run
assert_equal 0 "$STATUS" 'dry-run status'
assert_no_mutations
assert_contains 'Deletion summary: attempted=0 succeeded=0 already_absent=0 failed=0'

arrange 'young resources are not counted as deletion attempts'
AGE=3600
response sns.list-topics "$TOPIC"
response sqs.list-queues "$QUEUE"
response sqs.get-queue-attributes "$(date +%s)"
response sns.list-tags-for-resource "$(date +%s)"
run_cleanup
assert_equal 0 "$STATUS" 'young resources are safely deferred'
assert_no_mutations
assert_contains 'Deletion summary: attempted=0 succeeded=0 already_absent=0 failed=0'

arrange 'first-seen topic in dry-run is not stamped'
AGE=3600
response sns.list-topics "$TOPIC"
response sns.list-tags-for-resource None
run_cleanup --dry-run
assert_equal 0 "$STATUS" 'first-seen topic is deferred'
assert_no_mutations
assert_contains 'Deletion summary: attempted=0 succeeded=0 already_absent=0 failed=0'

arrange 'late discovery failure still reports completed deletion outcomes'
response resourcegroupstaggingapi.get-resources "$TOPIC"
aws_error sqs.list-queues AccessDenied ListQueues
run_cleanup
assert_equal 1 "$STATUS" 'preserve discovery exit status'
assert_contains 'Deletion summary: attempted=1 succeeded=1 already_absent=0 failed=0'

arrange 'missing result storage fails before AWS calls'
CASE_TEMP="$CASE_DIR/missing-directory"
run_cleanup
assert_equal 1 "$STATUS" 'cannot initialize result storage'
assert_equal '' "$(cat "$CASE_DIR/calls")" 'no AWS calls without result storage'

arrange 'worker failure is not covered by the deletion threshold'
response sns.list-topics "$TOPIC"
LIMIT=999
printf '#!/bin/bash\nexit 42\n' > "$CASE_DIR/xargs"
chmod +x "$CASE_DIR/xargs"
rm "$TEST_ROOT/bin/xargs"
ln -s "$CASE_DIR/xargs" "$TEST_ROOT/bin/xargs"
run_cleanup
assert_equal 1 "$STATUS" 'worker failure propagates'
assert_contains 'Topic deletion workers did not complete successfully'
rm "$TEST_ROOT/bin/xargs"
ln -s "$(command -v xargs)" "$TEST_ROOT/bin/xargs"

for path in serial parallel; do
    arrange "$path result-recording failure is fatal"
    LIMIT=999
    : > "$CASE_DIR/fail-recording"
    if [[ "$path" == serial ]]; then
        response resourcegroupstaggingapi.get-resources "$TOPIC"
    else
        response sns.list-topics "$TOPIC"
    fi
    run_cleanup
    assert_equal 1 "$STATUS" 'result write failure propagates'
    assert_contains 'Could not record a cleanup result'
done

arrange 'completed run removes its private result file'
response sns.list-topics "$TOPIC"
run_cleanup
assert_equal 0 "$STATUS" 'successful cleanup'
remaining_results=0
for result in "$CASE_DIR"/brighter-aws-cleanup.*; do
    [[ -e "$result" ]] && remaining_results=$((remaining_results + 1))
done
assert_equal 0 "$remaining_results" 'temporary results are removed'

arrange 'old nonempty test bucket is emptied before deletion'
AGE=3600
response s3api.list-buckets "$(printf '%s\t%s\n' "$BUCKET" '2001-09-09T01:46:40Z')"
: > "$CASE_DIR/$BUCKET.objects"
run_cleanup
assert_equal 0 "$STATUS" 'successful S3 cleanup'
assert_equal 1 "$(grep -c "s3api delete-bucket .*${BUCKET}" "$CASE_DIR/calls" || true)" 'bucket deletion attempted'
assert_equal true "$([[ -f "$CASE_DIR/$BUCKET.deleted" ]] && echo true || echo false)" 'bucket actually deleted after emptying'

arrange 'all old buckets in the aggregated listing are cleaned'
AGE=3600
response s3api.list-buckets "$(printf '%s\t%s\n' "$BUCKET" '2001-09-09T01:46:40Z' "$OTHER_BUCKET" '2001-09-09T01:46:40+00:00')"
run_cleanup
assert_equal 0 "$STATUS" 'multiple buckets handled'
assert_equal true "$([[ -f "$CASE_DIR/$BUCKET.deleted" && -f "$CASE_DIR/$OTHER_BUCKET.deleted" ]] && echo true || echo false)" 'both buckets deleted'

for created in "$(date -u '+%Y-%m-%dT%H:%M:%SZ')" '2999-01-01T00:00:00Z' 'not-a-date' None; do
    arrange "bucket with protected or unreadable age: $created"
    AGE=3600
    response s3api.list-buckets "$(printf '%s\t%s\n' "$BUCKET" "$created")"
    run_cleanup
    assert_no_mutations
done

arrange 'unrelated bucket names are never swept'
AGE=3600
response s3api.list-buckets "$(printf '%s\t%s\n' 'production-data' '2001-09-09T01:46:40Z' 'not-brightertestbucket-11111111-1111-1111-1111-111111111111' '2001-09-09T01:46:40Z')"
run_cleanup
assert_equal 0 "$STATUS" 'unrelated buckets ignored'
assert_no_mutations

arrange 'S3 dry-run lists candidates but never empties or deletes'
AGE=3600
response s3api.list-buckets "$(printf '%s\t%s\n' "$BUCKET" '2001-09-09T01:46:40Z')"
: > "$CASE_DIR/$BUCKET.objects"
run_cleanup --dry-run
assert_equal 0 "$STATUS" 'S3 dry-run status'
assert_contains "$BUCKET"
assert_no_mutations
assert_equal true "$([[ -f "$CASE_DIR/$BUCKET.objects" ]] && echo true || echo false)" 'objects untouched'

arrange 'failed S3 discovery cannot silently succeed'
LIMIT=999
aws_error s3api.list-buckets AccessDenied ListBuckets
run_cleanup
assert_equal 1 "$STATUS" 'discovery errors bypass deletion tolerance'
assert_contains AccessDenied
assert_no_mutations

arrange 'failed object removal is reported and leaves the bucket'
response s3api.list-buckets "$(printf '%s\t%s\n' "$BUCKET" '2001-09-09T01:46:40Z')"
: > "$CASE_DIR/$BUCKET.objects"
response s3.rm '' 'AccessDenied: cannot delete object' 1
run_cleanup
assert_equal 1 "$STATUS" 'object removal failure is fatal by default'
assert_contains AccessDenied
assert_equal false "$([[ -f "$CASE_DIR/$BUCKET.deleted" ]] && echo true || echo false)" 'nonempty bucket not deleted'

arrange 'bucket deletion failure does not prevent later cleanup'
response s3api.list-buckets "$(printf '%s\t%s\n' "$BUCKET" '2001-09-09T01:46:40Z' "$OTHER_BUCKET" '2001-09-09T01:46:40Z')"
aws_error "s3api.delete-bucket.$BUCKET" AccessDenied DeleteBucket
run_cleanup
assert_equal 1 "$STATUS" 'bucket deletion failure propagated'
assert_contains AccessDenied
assert_equal true "$([[ -f "$CASE_DIR/$OTHER_BUCKET.deleted" ]] && echo true || echo false)" 'later bucket still cleaned'

arrange 'bucket removed concurrently is benign'
response s3api.list-buckets "$(printf '%s\t%s\n' "$BUCKET" '2001-09-09T01:46:40Z')"
aws_error s3api.delete-bucket NoSuchBucket DeleteBucket
run_cleanup
assert_equal 0 "$STATUS" 'already-absent bucket does not fail cleanup'
assert_contains 'Already absent'

arrange 'versioned or otherwise nonempty bucket remains a visible failure'
response s3api.list-buckets "$(printf '%s\t%s\n' "$BUCKET" '2001-09-09T01:46:40Z')"
aws_error s3api.delete-bucket BucketNotEmpty DeleteBucket
run_cleanup
assert_equal 1 "$STATUS" 'remaining versions cannot be reported as deleted'
assert_contains BucketNotEmpty

arrange 'S3 deletion failures honour the configured threshold'
LIMIT=1
response s3api.list-buckets "$(printf '%s\t%s\n' "$BUCKET" '2001-09-09T01:46:40Z')"
aws_error s3api.delete-bucket AccessDenied DeleteBucket
run_cleanup
assert_equal 0 "$STATUS" 'one tolerated deletion failure'
assert_contains 'failed=1 (failure limit=1)'

echo "Offline cleanup tests: $PASS passed, $FAIL failed."
[[ "$FAIL" -eq 0 ]]
