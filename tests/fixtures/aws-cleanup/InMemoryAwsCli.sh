#!/bin/bash
# Offline AWS CLI I/O substitute. It never invokes the real CLI or a network client.
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

: "${AWS_CLEANUP_FIXTURES:?Must be run by the offline cleanup test harness}"
printf '%s\n' "$*" >> "$AWS_CLEANUP_FIXTURES/calls"
key="$1.$2"
shift 2
resource=""
group=""
while [[ $# -gt 0 ]]; do
    case "$1" in
        --topic-arn|--subscription-arn|--queue-url|--queue-name|--name|--resource-arn)
            resource="$2"; shift ;;
        --group-name) group="$2"; shift ;;
        Key=Source,Values=Brighter) resource="brighter" ;;
    esac
    shift
done
resource="${resource:-$group}"
resource="${resource##*/}"
resource="${resource##*:}"
response="$AWS_CLEANUP_FIXTURES/$key"
if [[ -f "$response.$resource.status" ]]; then
    response="$response.$resource"
elif [[ "$key:$resource" == resourcegroupstaggingapi.get-resources:brighter ]]; then
    echo None
    exit 0
fi
if [[ -f "$response.status" ]]; then
    cat "$response.stdout"
    cat "$response.stderr" >&2
    exit "$(cat "$response.status")"
fi

case "$key" in
    resourcegroupstaggingapi.get-resources|sns.list-topics|sqs.list-queues|sns.list-subscriptions-by-topic|scheduler.list-schedules)
        echo None ;;
    sqs.get-queue-url)
        echo "https://sqs.eu-west-1.amazonaws.com/000000000000/$resource" ;;
    sqs.get-queue-attributes|sns.list-tags-for-resource)
        echo 1000000000 ;;
    scheduler.get-schedule-group)
        echo '2001-09-09T01:46:40Z' ;;
    sns.delete-topic|sns.unsubscribe|sqs.delete-queue|scheduler.delete-schedule|scheduler.delete-schedule-group|sns.tag-resource)
        if [[ -f "$AWS_CLEANUP_FIXTURES/fail-recording" ]]; then
            case "${CLEANUP_RESULTS:-}" in
                "$AWS_CLEANUP_FIXTURES"/brighter-aws-cleanup.*)
                    rm -f -- "$CLEANUP_RESULTS"
                    mkdir "$CLEANUP_RESULTS" ;;
                *) echo 'Unsafe result path in offline fixture' >&2; exit 99 ;;
            esac
        fi
        ;;
    *)
        printf '%s\n' "$key" >> "$AWS_CLEANUP_FIXTURES/unexpected"
        echo "Unexpected offline AWS command: $key" >&2
        exit 99 ;;
esac
