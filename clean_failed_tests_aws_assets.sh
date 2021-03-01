#!/bin/bash

topics=$(aws sns list-topics | jq -r '.Topics | .[] | .TopicArn')
while IFS= read -r line ; do 
    aws sns delete-topic --topic-arn $line
done <<< "$topics"


queues=$(aws sqs list-queues | jq -r '.QueueUrls | .[]')
while IFS= read -r line ; do 
    aws sqs delete-queue --queue-url $line
done <<< "$queues"


