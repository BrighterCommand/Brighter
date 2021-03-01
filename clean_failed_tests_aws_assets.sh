#!/bin/bash

topics=$(aws sns list-topics | jq -r '.Topics | .[] | .TopicArn')
while IFS= read -r line ; do 
    aws sns delete-topic --topic-arn $line
done <<< "$topics"


queues=$(aws sqs list-queues | jq -r '.QueueUrls | .[]')
while IFS= read -r line ; do 
    aws sqs delete-queue --queue-url $line
done <<< "$queues"

subscriptions=$( aws sns list-subscriptions | jq -r '.Subscriptions | .[] | .SubscriptionArn')
while IFS= read -r line; do
    aws sns unsubscribe --subscription-arn $line
done <<< "$subscriptions"

