# Tasks: SQS Cleanup

**ADR**: [0037 - AWS Test Resource Cleanup](../../docs/adr/0037-aws-test-resource-cleanup.md)
**Requirements**: [requirements.md](requirements.md)

## Task Dependencies

```
Task 1 (SnsAttributes Tags) ──┐
                               ├──▶ Task 3 (SNS test tags)
Task 2 (CreateTopicTags)   ────┘
                               ├──▶ Task 5 (Cleanup script)
Task 4 (SQS test tags)    ────────▶ Task 5 (Cleanup script)
                                    Task 5 ──▶ Task 6 (CI integration)
```

---

## Phase 1: Make SNS Tags Configurable (production code)

- [ ] **TEST + IMPLEMENT: SnsAttributes accepts and stores custom tags**
  - **USE COMMAND**: `/test-first when creating SnsAttributes with custom tags they should be stored and retrievable`
  - Test location: `tests/Paramore.Brighter.AWS.Tests/`
  - Test file: `When_creating_sns_attributes_with_tags.cs`
  - Test should verify:
    - `SnsAttributes` constructed with `tags: [new Tag { Key = "Environment", Value = "Test" }]` stores them in the `Tags` property
    - `SnsAttributes` constructed without tags has `Tags` as `null`
    - `SnsAttributes.Empty` has `Tags` as `null`
    - Existing constructor parameters (deliveryPolicy, policy, type, contentBasedDeduplication) are unaffected
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add `List<Tag>? tags = null` parameter to `SnsAttributes` constructor in `src/Paramore.Brighter.MessagingGateway.AWSSQS/SnsAttributes.cs`
    - Change `Tags` from `public List<Tag> Tags => [];` to `public List<Tag>? Tags { get; }`
    - Store tags in constructor body: `Tags = tags;`
    - Apply identical changes to `src/Paramore.Brighter.MessagingGateway.AWSSQS.V4/SnsAttributes.cs`

- [ ] **TEST + IMPLEMENT: CreateTopicAsync applies custom tags from SnsAttributes**
  - **USE COMMAND**: `/test-first when creating an SNS topic with custom tags they should be applied alongside the default Source tag`
  - Test location: `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/Sns/Standard/Proactor/`
  - Test file: `When_creating_a_topic_with_custom_tags_async.cs`
  - Test should verify:
    - Create a topic via `SnsMessageProducer` with `SnsAttributes` containing `Environment=Test` tag
    - Use AWS SDK to list tags on the created topic ARN
    - Topic has both `Source=Brighter` (default) and `Environment=Test` (custom) tags
    - Clean up topic in Dispose
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add `CreateTopicTags(SnsAttributes? snsAttributes)` method to `src/Paramore.Brighter.MessagingGateway.AWSSQS/AWSMessagingGateway.cs` mirroring existing `CreateQueueTags()` pattern (line 375)
    - Replace hardcoded `Tags = [new Tag { Key = "Source", Value = "Brighter" }]` in `CreateTopicAsync()` (line 215) with `Tags = CreateTopicTags(snsAttributes)`
    - Apply identical changes to `src/Paramore.Brighter.MessagingGateway.AWSSQS.V4/AWSMessagingGateway.cs`

## Phase 2: Tag Test Resources with `Environment=Test`

- [ ] **TEST + IMPLEMENT: SNS test resources are tagged with Environment=Test**
  - **USE COMMAND**: `/test-first when an SNS PubSub test creates resources they should be tagged with Environment=Test`
  - Test location: `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/Sns/Standard/Proactor/`
  - Test file: `When_posting_a_message_resources_are_tagged_async.cs`
  - Test should verify:
    - Create a subscription and producer using the same pattern as existing SNS tests but with `Environment=Test` tags on both `SqsAttributes` and `SnsAttributes`
    - Post a message to confirm resources are functional
    - Use AWS SDK to verify the created topic has `Environment=Test` tag
    - Use AWS SDK to verify the created queue has `Environment=Test` tag
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Add `tags: new Dictionary<string, string> { { "Environment", "Test" } }` to all `SqsAttributes` constructors across all 238 test files in:
      - `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/` (119 files)
      - `tests/Paramore.Brighter.AWS.V4.Tests/MessagingGateway/` (119 files)
    - Add `tags: [new Tag { Key = "Environment", Value = "Test" }]` to all `SnsAttributes` constructors in SNS and SNS FIFO test files
    - For tests that don't currently specify `SqsAttributes`, add `queueAttributes: new SqsAttributes(tags: new Dictionary<string, string> { { "Environment", "Test" } })` to the subscription constructor
    - For SNS tests that don't currently specify `SnsAttributes`, add `topicAttributes: new SnsAttributes(tags: [new Tag { Key = "Environment", Value = "Test" }])` to the subscription constructor

- [ ] **TEST + IMPLEMENT: SQS point-to-point test resources are tagged with Environment=Test**
  - **USE COMMAND**: `/test-first when an SQS point-to-point test creates resources they should be tagged with Environment=Test`
  - Test location: `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/Sqs/Standard/Proactor/`
  - Test file: `When_posting_a_message_resources_are_tagged_async.cs`
  - Test should verify:
    - Create a subscription and producer using the same pattern as existing SQS tests but with `Environment=Test` tag on `SqsAttributes`
    - Post a message to confirm the queue is functional
    - Use AWS SDK to verify the created queue has `Environment=Test` tag
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Verify the tag additions from Task 3 are correct for SQS point-to-point tests
    - This task validates that the SQS tagging path (which already existed in production code via `CreateQueueTags()`) works end-to-end with the test tag

## Phase 3: Cleanup Script

- [ ] **TEST + IMPLEMENT: Cleanup script identifies and deletes only tagged test resources**
  - **USE COMMAND**: `/test-first when running the cleanup script it should delete resources tagged Environment=Test and leave untagged resources`
  - Test location: project root
  - Test file: `test_clean_failed_tests_aws_assets.sh` (bash test script)
  - Test should verify:
    - Script with `--dry-run` lists resources tagged `Environment=Test` without deleting
    - Script without `--dry-run` deletes SQS queues tagged `Environment=Test`
    - Script without `--dry-run` deletes SNS subscriptions before topics
    - Script without `--dry-run` deletes SNS topics tagged `Environment=Test`
    - Script exits 0 even when individual deletions fail
    - Script logs each deletion action
  - **⛔ STOP HERE - WAIT FOR USER APPROVAL in IDE before implementing**
  - Implementation should:
    - Replace `clean_failed_tests_aws_assets.sh` with a new version that:
      - Accepts `--dry-run` flag
      - Uses `aws resourcegroupstaggingapi get-resources --tag-filters Key=Environment,Values=Test` to find tagged resources
      - Categorizes resources by ARN type (sqs, sns topic, sns subscription)
      - Deletes in order: subscriptions, topics, queues
      - Logs each action with resource ARN
      - Continues on individual errors (log and proceed)
      - Exits 0 always

## Phase 4: CI Integration

- [ ] **IMPLEMENT: Add cleanup step to CI workflow after AWS test jobs**
  - Implementation should:
    - Add a new step to the `aws-ci` job in `.github/workflows/ci.yml` after the test step:
      ```yaml
      - name: Cleanup orphaned test resources
        if: always()
        run: |
          chmod +x ./clean_failed_tests_aws_assets.sh
          ./clean_failed_tests_aws_assets.sh
        continue-on-error: true
      ```
    - Add the same step to the `aws-scheduler-ci` job
    - No cleanup step needed for `localstack-ci` (ephemeral containers)
  - Verification:
    - Review the workflow YAML for correct `if: always()` placement
    - Confirm `continue-on-error: true` is set so cleanup failures don't fail the build
    - Confirm the step appears after the test step but uses the same AWS credentials

## Summary

| Phase | Task | Description | Depends On |
|-------|------|-------------|------------|
| 1 | 1 | Make `SnsAttributes.Tags` configurable | — |
| 1 | 2 | Wire `SnsAttributes.Tags` into `CreateTopicAsync()` | Task 1 |
| 2 | 3 | Tag SNS test resources with `Environment=Test` | Tasks 1, 2 |
| 2 | 4 | Tag SQS test resources with `Environment=Test` | — |
| 3 | 5 | Replace cleanup script with tag-based filtering | Tasks 3, 4 |
| 4 | 6 | Add cleanup step to CI workflow | Task 5 |
