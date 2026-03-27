# 37. AWS Test Resource Cleanup

Date: 2026-02-07

## Status

Accepted

## Context

**Parent Requirement**: [specs/0002-sqs-cleanup/requirements.md](../../specs/0002-sqs-cleanup/requirements.md)

**Scope**: This ADR covers the complete approach to AWS test resource cleanup: tagging infrastructure, the cleanup tool, and CI integration.

### The Problem

The 96+ AWS integration test classes in `tests/Paramore.Brighter.AWS.Tests` and `tests/Paramore.Brighter.AWS.V4.Tests` create SQS queues, SNS topics, and SNS subscriptions. Each test class implements `IDisposable`/`IAsyncDisposable` to clean up its resources via `ChannelFactory.DeleteTopicAsync()` and `ChannelFactory.DeleteQueueAsync()`.

When the test process crashes, times out (8-minute CI limit), or is forcibly terminated, Dispose methods never execute. Orphaned resources accumulate until they hit AWS account limits, requiring manual cleanup via the `clean_failed_tests_aws_assets.sh` script.

### Forces

- **Safety**: The current cleanup script deletes *all* resources in the account indiscriminately. If the account ever contains non-test resources, they would be destroyed.
- **Reliability**: xUnit's Dispose is best-effort — it cannot run if the process is killed.
- **Operator visibility**: Orphaned resources are indistinguishable from intentional resources without access to the test source code.
- **Existing tag infrastructure**: `SqsAttributes` already supports `Dictionary<string, string>? Tags` and `AWSMessagingGateway.CreateQueueTags()` merges them with the default `Source=Brighter` tag. `SnsAttributes` has a `List<Tag> Tags` property but it is read-only and always returns an empty list. `CreateTopicAsync()` hardcodes only `Source=Brighter`.
- **CI time budget**: The 8-minute job timeout leaves limited room for a cleanup step.

### Current State of Tag Support

| Resource | Attributes Class | Tags Property | Configurable? | Applied During Creation? |
|----------|-----------------|---------------|---------------|-------------------------|
| SQS Queue | `SqsAttributes` | `Dictionary<string, string>?` | Yes, via constructor | Yes, via `CreateQueueTags()` |
| SNS Topic | `SnsAttributes` | `List<Tag>` | No — read-only, always `[]` | No — hardcoded `Source=Brighter` only |

Both V4 and non-V4 packages have identical implementations.

## Decision

We will adopt a **tag-based cleanup strategy**:

1. **Make `SnsAttributes.Tags` configurable** so topics can be tagged like queues already can.
2. **Wire `SnsAttributes.Tags` into `CreateTopicAsync()`** so tags are applied at creation time.
3. **Tag all test resources** with `Environment=Test` so operators can identify them.
4. **Replace the cleanup script** to filter by tag rather than by naming convention.
5. **Add a CI cleanup step** that runs after AWS tests regardless of outcome.

Tags are the right approach because:
- They are a first-class AWS concept designed for resource identification and lifecycle management.
- They are visible in the AWS Console, enabling operators to identify orphaned test resources without access to the codebase.
- The AWS Resource Groups Tagging API (`resourcegroupstaggingapi`) provides a unified way to find resources across services by tag.
- The infrastructure for SQS tagging already exists — we only need to close the gap for SNS.

### Architecture Overview

```
┌──────────────────────────────────────────────────────────────┐
│                      CI Pipeline                             │
│                                                              │
│  ┌──────────────────────┐    ┌────────────────────────────┐  │
│  │  AWS Tests            │    │  Cleanup Step              │  │
│  │                       │    │  (if: always())            │  │
│  │  Resources created    │──▶ │                            │  │
│  │  with tags:           │    │  1. Query resources by     │  │
│  │    Source=Brighter    │    │     tag: Environment=Test  │  │
│  │    Environment=Test   │    │  2. Delete subscriptions   │  │
│  │                       │    │  3. Delete topics          │  │
│  │  Each test cleans up  │    │  4. Delete queues          │  │
│  │  via Dispose          │    │  5. Log results            │  │
│  └──────────────────────┘    └────────────────────────────┘  │
│         │                                                    │
│         │ on crash                                           │
│         ▼                                                    │
│  ┌──────────────────────┐                                    │
│  │  Orphaned resources   │◀── Identifiable by tag,           │
│  │  (tagged)             │    cleaned up by step above       │
│  └──────────────────────┘                                    │
└──────────────────────────────────────────────────────────────┘
```

### Key Components

#### 1. `SnsAttributes` — Make Tags Configurable

**Role**: Information holder — knows the desired configuration for an SNS topic.

**Current** (both V4 and non-V4):
```csharp
public List<Tag> Tags => [];  // read-only, always empty
```

**Change**: Accept `List<Tag>?` in the constructor and store it. Use `List<Tag>` to match the AWS SDK's `CreateTopicRequest.Tags` type.

```csharp
public SnsAttributes(
    string? deliveryPolicy = null,
    string? policy = null,
    SqsType type = SqsType.Standard,
    bool contentBasedDeduplication = true,
    List<Tag>? tags = null)
{
    // ...existing assignments...
    Tags = tags ?? [];
}

public List<Tag> Tags { get; }
```

**Files affected**:
- `src/Paramore.Brighter.MessagingGateway.AWSSQS/SnsAttributes.cs`
- `src/Paramore.Brighter.MessagingGateway.AWSSQS.V4/SnsAttributes.cs`

#### 2. `AWSMessagingGateway.CreateTopicAsync()` — Apply Tags from SnsAttributes

**Role**: Service provider — responsible for creating AWS resources with the correct configuration.

**Current** (both V4 and non-V4):
```csharp
var createTopicRequest = new CreateTopicRequest(topicName)
{
    Attributes = attributes,
    Tags = [new Tag { Key = "Source", Value = "Brighter" }]  // hardcoded
};
```

**Change**: Add a `CreateTopicTags()` method that mirrors the existing `CreateQueueTags()` pattern — starts with the default `Source=Brighter` tag and merges in any tags from `SnsAttributes`.

```csharp
private List<Tag> CreateTopicTags(SnsAttributes? snsAttributes)
{
    var tags = new List<Tag> { new() { Key = "Source", Value = "Brighter" } };
    if (snsAttributes?.Tags == null) return tags;

    tags.AddRange(snsAttributes.Tags);
    return tags;
}
```

Then in `CreateTopicAsync()`:
```csharp
var createTopicRequest = new CreateTopicRequest(topicName)
{
    Attributes = attributes,
    Tags = CreateTopicTags(snsAttributes)
};
```

**Files affected**:
- `src/Paramore.Brighter.MessagingGateway.AWSSQS/AWSMessagingGateway.cs`
- `src/Paramore.Brighter.MessagingGateway.AWSSQS.V4/AWSMessagingGateway.cs`

#### 3. Test Classes — Add `Environment=Test` Tag

**Role**: Callers — responsible for declaring the intent that these are test resources.

Tests will pass an `Environment=Test` tag through their `SqsAttributes` and `SnsAttributes`:

```csharp
// SQS — already supported
new SqsAttributes(
    tags: new Dictionary<string, string> { { "Environment", "Test" } }
)

// SNS — enabled by change #1 above
new SnsAttributes(
    tags: [new Tag { Key = "Environment", Value = "Test" }]
)
```

All 96+ test classes that create AWS resources need this change. The tag is the same across all tests, so this is a mechanical addition to the subscription/publication construction in each test constructor.

**Files affected**:
- All test classes in `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/`
- All test classes in `tests/Paramore.Brighter.AWS.V4.Tests/MessagingGateway/`

#### 4. Cleanup Script — Filter by Tag

**Role**: Service provider — responsible for identifying and removing orphaned test resources.

Replace `clean_failed_tests_aws_assets.sh` with a version that uses the AWS Resource Groups Tagging API to find resources tagged with `Environment=Test`:

```bash
#!/bin/bash
# clean_failed_tests_aws_assets.sh
# Cleans up orphaned AWS test resources identified by Environment=Test tag

DRY_RUN=false
if [[ "$1" == "--dry-run" ]]; then
    DRY_RUN=true
    echo "[DRY RUN] No resources will be deleted"
fi

# Use Resource Groups Tagging API to find all resources tagged Environment=Test
RESOURCES=$(aws resourcegroupstaggingapi get-resources \
    --tag-filters Key=Environment,Values=Test \
    --resource-type-filters \
        sqs:queue \
        sns:topic \
        sns:subscription \
    --query 'ResourceTagMappingList[*].ResourceARN' \
    --output text)

# Process each resource by type...
# Delete subscriptions first, then topics, then queues
```

**Responsibilities**:
- **Knowing**: Which resources are test-created (via `Environment=Test` tag)
- **Doing**: Listing, filtering, and deleting orphaned test resources
- **Deciding**: Deletion order (subscriptions before topics)

**Behaviour**:
- Accept optional `--dry-run` flag to preview deletions without executing them
- Use `resourcegroupstaggingapi` to find tagged resources across SQS and SNS
- Delete subscriptions before topics (AWS requirement)
- Log each action
- Exit 0 even on individual deletion failures (log and continue)

#### 5. CI Workflow — Add Cleanup Step

Add a cleanup step to both `aws-ci` and `aws-scheduler-ci` jobs:

```yaml
- name: Cleanup orphaned test resources
  if: always()
  run: |
    chmod +x ./clean_failed_tests_aws_assets.sh
    ./clean_failed_tests_aws_assets.sh
  continue-on-error: true
```

Key properties:
- `if: always()` — runs regardless of test pass/fail/cancel
- `continue-on-error: true` — cleanup failures do not fail the build
- Runs after the test step, using the same AWS credentials already configured

### Technology Choices

- **AWS Resource Groups Tagging API**: Provides a single API to find resources across SQS and SNS by tag, avoiding separate list-and-filter calls per service.
- **Shell script (bash + aws-cli + jq)**: Retained for the cleanup tool. CI runners have `aws-cli` and `jq` pre-installed.
- **`Environment=Test` tag**: A standard convention that is self-documenting and visible in the AWS Console.

### Implementation Approach

1. **Make `SnsAttributes.Tags` configurable** (both V4 and non-V4)
2. **Add `CreateTopicTags()` to `AWSMessagingGateway`** and wire it into `CreateTopicAsync()` (both V4 and non-V4)
3. **Add `Environment=Test` tags to all test classes** that create SQS/SNS resources
4. **Replace `clean_failed_tests_aws_assets.sh`** with tag-based filtering and dry-run support
5. **Add cleanup step to CI** in both `aws-ci` and `aws-scheduler-ci` jobs

## Consequences

### Positive

- Orphaned test resources are automatically cleaned up after every CI run
- Resources are identifiable by operators in the AWS Console without access to the codebase
- The cleanup script is safe — it only deletes resources explicitly tagged as test resources
- Tag-based filtering is a first-class AWS concept, more robust than name-pattern heuristics
- The existing `SqsAttributes.Tags` pattern is extended consistently to `SnsAttributes`
- Dry-run mode allows manual verification before cleanup

### Negative

- Requires changes to all 96+ test classes to add the `Environment=Test` tag (mechanical but high-touch)
- Adds a constructor parameter to `SnsAttributes`, which is a public API change
- CI cleanup requires the `resourcegroupstaggingapi` IAM permission (likely already available, but must be verified)

### Risks and Mitigations

| Risk | Mitigation |
|------|------------|
| Breaking change to `SnsAttributes` constructor | New parameter is optional with default `null`; existing callers are unaffected |
| Missing IAM permissions for Resource Groups Tagging API | Verify permissions in CI; fall back to per-service list+filter if needed |
| Test classes miss the tag addition | Code review + grep verification; only tagged resources will be cleaned up, so missing a tag is safe (resource just won't be auto-cleaned) |
| AWS API rate limiting during cleanup | Cleanup handles errors gracefully; log and continue |
| Tag propagation to subscriptions | SNS subscriptions inherit the topic's context; script handles subscriptions per-topic |

## Alternatives Considered

### 1. GUID-Based Name Pattern Matching

Filter resources by detecting GUID patterns in resource names.

**Rejected because**: Name patterns are heuristic and require knowledge of the codebase to understand. Tags are explicit, operator-visible, and a standard AWS resource management practice. False positives are possible with GUID matching if non-test resources happen to contain UUID-like patterns.

### 2. C# Cleanup Tool

Write the cleanup tool as a .NET console application in the solution.

**Rejected because**: Adds build/publish complexity to the CI pipeline. The cleanup logic is straightforward list-filter-delete — shell scripting is the right level of abstraction.

### 3. AWS Lambda for Scheduled Cleanup

Deploy a Lambda function that runs on a schedule to clean up old resources.

**Rejected because**: Adds infrastructure to manage (Lambda, IAM roles, EventBridge schedule). Over-engineered for the problem. CI-integrated cleanup is simpler and runs at the right time.

### 4. SQS Message Retention / TTL-based Cleanup

Use SQS queue policies or CloudWatch alarms to auto-delete idle queues.

**Rejected because**: SQS does not support automatic queue deletion based on inactivity. SNS topics have no TTL mechanism.

## References

- Requirements: [specs/0002-sqs-cleanup/requirements.md](../../specs/0002-sqs-cleanup/requirements.md)
- AWS Resource Groups Tagging API: `get-resources` supports filtering by tag and resource type
- Existing tag support: `SqsAttributes.Tags` in `src/Paramore.Brighter.MessagingGateway.AWSSQS/SqsAttributes.cs` (line 120)
- SNS tag gap: `SnsAttributes.Tags` in `src/Paramore.Brighter.MessagingGateway.AWSSQS/SnsAttributes.cs` (line 70)
- Queue tag application: `AWSMessagingGateway.CreateQueueTags()` in `src/Paramore.Brighter.MessagingGateway.AWSSQS/AWSMessagingGateway.cs` (line 375)
- Topic tag hardcoding: `AWSMessagingGateway.CreateTopicAsync()` in `src/Paramore.Brighter.MessagingGateway.AWSSQS/AWSMessagingGateway.cs` (line 215)
