# Requirements: SQS Cleanup

> **Note**: This document captures user requirements and needs. Technical design decisions and implementation details should be documented in an Architecture Decision Record (ADR) in `docs/adr/`.

## Problem Statement

As a **maintainer of Brighter**, I would like **AWS test resources (SQS queues, SNS topics, SNS subscriptions) to be reliably cleaned up after test runs**, so that **orphaned resources do not accumulate in the AWS account, eventually hitting account limits and requiring manual intervention via `clean_failed_tests_aws_assets.sh`**.

Currently, the 96+ AWS test classes in `tests/Paramore.Brighter.AWS.Tests` and `tests/Paramore.Brighter.AWS.V4.Tests` rely on xUnit's `IDisposable`/`IAsyncDisposable` to clean up resources. When tests crash, time out, or the test process is forcibly terminated (e.g., by CI's 8-minute timeout), the Dispose methods never execute, leaving orphaned queues, topics, and subscriptions in the AWS account.

The only recovery mechanism is the manual `clean_failed_tests_aws_assets.sh` script, which is a nuclear option that deletes **all** SNS topics, SQS queues, and SNS subscriptions in the account.

## Proposed Solution

A three-pronged approach:

1. **Improve test-level cleanup resilience** - Ensure AWS tests handle failure paths more robustly so fewer resources are orphaned in the first place.
2. **Improve the cleanup mechanism** - Evaluate whether the current shell script is the best approach, or whether a more targeted, safer cleanup tool should be used.
3. **Automate cleanup in CI** - Run cleanup as part of the CI pipeline so orphaned resources are removed automatically after test failures.

## Requirements

### Functional Requirements

#### FR1: Robust Test Cleanup
- FR1.1: AWS test classes must clean up resources even when test assertions fail.
- FR1.2: Resource cleanup should be resilient to partial failures (e.g., if topic deletion fails, queue deletion should still be attempted).
- FR1.3: Test resource names should include identifiable tags so they can be distinguished from production resources.

#### FR2: Cleanup Tooling
- FR2.1: A cleanup mechanism must be able to identify and remove test-created resources without affecting non-test resources.
- FR2.2: The cleanup mechanism should support a dry-run mode to preview what would be deleted.
- FR2.3: The cleanup mechanism should handle pagination for accounts with many resources.
- FR2.4: The cleanup mechanism should be idempotent - safe to run multiple times.

#### FR3: CI Integration
- FR3.1: Cleanup must run automatically in CI after AWS test steps, regardless of test pass/fail status.
- FR3.2: Cleanup in CI should target only resources created by the current or recent test runs, not all resources in the account.
- FR3.3: Cleanup failures in CI should be logged but not fail the overall build.

### Non-functional Requirements

- **Safety**: Cleanup must never delete non-test resources. A tagging strategy must clearly differentiate test resources.
- **Observability**: Cleanup operations should log what was deleted and any errors encountered.
- **Performance**: Cleanup should complete within a reasonable time (under 2 minutes) to avoid extending CI run times significantly.
- **Compatibility**: The solution must work with both LocalStack (CI local testing) and real AWS accounts.

### Constraints and Assumptions

- **Constraints**:
  - Tests generate unique resource names using GUIDs (e.g., `Producer-Send-Tests-{guid}`). We may need to add the ability to tag resources, if required.
  - The CI workflow has an 8-minute timeout per job.
  - Real AWS tests only run on `main`/`master` pushes and non-fork PRs.
  - The cleanup script currently deletes everything indiscriminately - this is unsafe if the AWS account contains non-test resources.

- **Assumptions**:
  - The AWS account used for CI testing is dedicated to testing (no production resources).
  - AWS credentials in CI have permissions to list and delete SQS, SNS, and S3 resources.
  - LocalStack containers are ephemeral and do not need explicit cleanup.

### Out of Scope

- Cleanup of AWS resources other than SQS queues, SNS topics, and SNS subscriptions (e.g., S3 buckets, EventBridge schedulers are out of scope for this iteration).
- Changes to the LocalStack testing approach (LocalStack is already ephemeral).
- AWS resource cost optimization beyond cleanup.
- Changes to test logic or test coverage (only cleanup behavior is in scope).

## Acceptance Criteria

- **AC1**: All AWS test classes clean up their resources in Dispose/DisposeAsync, even when the test itself fails.
- **AC2**: A cleanup tool exists that can selectively remove test resources based on tagging, with dry-run support.
- **AC3**: The CI workflow includes a cleanup step that runs after AWS tests, using `if: always()` to ensure it executes regardless of test outcome.
- **AC4**: After a CI run with test failures, no orphaned test resources remain in the AWS account.
- **AC5**: The cleanup tool does not delete resources that don't match the test naming convention.

## Additional Context

### Current Cleanup Script

The existing `clean_failed_tests_aws_assets.sh` script:
```bash
# Deletes ALL topics, queues, and subscriptions - no filtering
topics=$(aws sns list-topics | jq -r '.Topics | .[] | .TopicArn')
queues=$(aws sqs list-queues | jq -r '.QueueUrls | .[]')
subscriptions=$(aws sns list-subscriptions | jq -r '.Subscriptions | .[] | .SubscriptionArn')
```

### Current Test Cleanup Pattern

All 96+ test classes follow this pattern:
```csharp
public class SomeAwsTest : IDisposable, IAsyncDisposable
{
    public void Dispose()
    {
        _channelFactory.DeleteTopicAsync().Wait();
        _channelFactory.DeleteQueueAsync().Wait();
        _messageProducer.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
        await _messageProducer.DisposeAsync();
    }
}
```

### CI Workflow Structure

- `localstack-ci` job: Uses LocalStack container (ephemeral, no cleanup needed)
- `aws-ci` job: Uses real AWS account, no cleanup step after tests
- `aws-scheduler-ci` job: Uses real AWS account, no cleanup step after tests
