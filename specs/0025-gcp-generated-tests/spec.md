# Spec 0025: GCP Pub/Sub Generated Tests

**Status:** Draft  
**Created:** 2026-03-19  
**Transport:** GCP Pub/Sub (Pull + Stream modes)

## Summary

Migrate the 20 manually-written GCP Pub/Sub messaging gateway tests to the existing Liquid template-based test generator. GCP has two delivery modes — **Pull** and **Stream** — each requiring both Proactor (async) and Reactor (sync) test variants. The outbox tests (Firestore, Spanner) are already generated; this spec covers only the messaging gateway tests.

## Background

The Brighter test generator (`tools/Paramore.Brighter.Test.Generator`) uses Liquid templates and `test-configuration.json` to auto-generate consistent test suites across transports. Redis, RabbitMQ, and Kafka already use this system. GCP currently has:

- **Generated:** Outbox tests (Firestore, SpannerBinary, SpannerText)
- **Manual:** 20 messaging gateway tests (5 behaviors × 2 modes × 2 patterns)

## User Scenarios

### US1: Consistent Test Generation (P1)

**As a** contributor  
**I want** GCP messaging gateway tests to be generated from the same templates as other transports  
**So that** test behavior is consistent and new template tests automatically apply to GCP

**Acceptance Criteria:**
- Given the test generator is run, when `test-configuration.json` includes MessagingGateways for Pull and Stream, then generated test files appear in `MessagingGateway/Generated/`
- Given a new template is added, when the generator is re-run, then GCP gains the new test without manual effort

### US2: Pull Mode Provider (P1)

**As a** contributor  
**I want** a Pull-mode provider implementing `IAmAMessageGatewayProactorProvider` and `IAmAMessageGatewayReactorProvider`  
**So that** the generator can create Pull-mode tests

**Acceptance Criteria:**
- Provider creates `GcpMessageProducer`, channels using `GcpPubSubChannelFactory`, and subscriptions with `SubscriptionMode.Pull`
- Provider supports DLQ setup, broker validation, requeue, partition key (ordering key), and cleanup

### US3: Stream Mode Provider (P1)

**As a** contributor  
**I want** a Stream-mode provider implementing both interfaces  
**So that** the generator can create Stream-mode tests

**Acceptance Criteria:**
- Same as US2 but with `SubscriptionMode.Stream`

### US4: Manual Test Removal (P2)

**As a** contributor  
**I want** the 20 manual messaging gateway tests removed after generated tests pass  
**So that** we avoid test duplication and maintenance burden

### US5: Extended Coverage (P3)

**As a** contributor  
**I want** GCP to gain template tests it doesn't currently have (multi-thread, activity context, assume channel, partition key)  
**So that** GCP has the same coverage depth as other transports

## Functional Requirements

### FR-001: test-configuration.json Update

Add a `MessagingGateways` dictionary (multi-variant format) to the existing GCP config:

```json
{
  "Namespace": "Paramore.Brighter.Gcp.Tests",
  "MessagingGateways": {
    "Pull": {
      "Publication": "Paramore.Brighter.MessagingGateway.GcpPubSub.GcpPubSubPublication",
      "Subscription": "Paramore.Brighter.MessagingGateway.GcpPubSub.GcpPubSubSubscription",
      "MessageGatewayProvider": "Paramore.Brighter.Gcp.Tests.MessagingGateway.GcpPullMessageGatewayProvider",
      "Category": "GcpPubSub",
      "HasSupportToPublishConfirmation": false,
      "HasSupportToDeadLetterQueue": true,
      "HasSupportToDelayedMessages": false,
      "HasSupportToPartitionKey": true,
      "HasSupportToValidateBrokerExistence": true,
      "HasSupportToRequeue": true
    },
    "Stream": {
      "Publication": "Paramore.Brighter.MessagingGateway.GcpPubSub.GcpPubSubPublication",
      "Subscription": "Paramore.Brighter.MessagingGateway.GcpPubSub.GcpPubSubSubscription",
      "MessageGatewayProvider": "Paramore.Brighter.Gcp.Tests.MessagingGateway.GcpStreamMessageGatewayProvider",
      "Category": "GcpPubSub",
      "HasSupportToPublishConfirmation": false,
      "HasSupportToDeadLetterQueue": true,
      "HasSupportToDelayedMessages": false,
      "HasSupportToPartitionKey": true,
      "HasSupportToValidateBrokerExistence": true,
      "HasSupportToRequeue": true
    }
  },
  "Outboxes": { "...existing..." }
}
```

### FR-002: Provider Implementations

Two provider classes, each implementing both `IAmAMessageGatewayProactorProvider` and `IAmAMessageGatewayReactorProvider`:

| Class | File | Mode |
|-------|------|------|
| `GcpPullMessageGatewayProvider` | `MessagingGateway/GcpPullMessageGatewayProvider.cs` | `SubscriptionMode.Pull` |
| `GcpStreamMessageGatewayProvider` | `MessagingGateway/GcpStreamMessageGatewayProvider.cs` | `SubscriptionMode.Stream` |

Each provider must:
- Use the existing `GatewayFactory` singleton for connection config
- Create `GcpPubSubPublication` and `GcpPubSubSubscription` with appropriate mode
- Support `DeadLetterPolicy` configuration for DLQ tests
- Support `OnMissingChannel.Validate` / `Assume` / `Create` modes
- Implement cleanup (delete topics/subscriptions after tests)
- Set ordering key support for partition key tests

### FR-003: Generated Test Output

After running the generator, the following directory structure should be created:

```
tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/
├── Generated/
│   ├── Pull/
│   │   ├── Proactor/   (~10 generated test files)
│   │   └── Reactor/    (~10 generated test files)
│   └── Stream/
│       ├── Proactor/   (~10 generated test files)
│       └── Reactor/    (~10 generated test files)
├── GcpPullMessageGatewayProvider.cs    (NEW)
├── GcpStreamMessageGatewayProvider.cs  (NEW)
├── Pull/         (EXISTING manual - to be deleted in Phase 3)
│   ├── Proactor/
│   └── Reactor/
└── Stream/       (EXISTING manual - to be deleted in Phase 3)
    ├── Proactor/
    └── Reactor/
```

### FR-004: GCP Capability Flags

| Capability | Value | Rationale |
|-----------|-------|-----------|
| HasSupportToPublishConfirmation | false | No explicit publish confirmation event |
| HasSupportToDeadLetterQueue | true | Native GCP DLQ via DeadLetterPolicy |
| HasSupportToDelayedMessages | false | Requires external scheduler, not native |
| HasSupportToPartitionKey | true | Maps to GCP ordering keys |
| HasSupportToValidateBrokerExistence | true | Supports OnMissingChannel.Validate |
| HasSupportToRequeue | true | Pull: ModifyAckDeadline(0), Stream: Reject() |

## GCP Manual Test → Template Mapping

| Manual Test | Generator Template |
|------------|-------------------|
| `When_posting_a_message_via_the_messaging_gateway` | `When_posting_a_message_via_the_messaging_gateway_should_be_received` |
| `When_a_message_consumer_reads_multiple_messages` | `When_a_message_consumer_reads_multiple_messages_should_receive_all_messages` |
| `When_queues_missing_verify_throws` | `When_infrastructure_missing_and_validate_channel_should_throw_exception` |
| `When_requeueing_a_message` | `When_requeing_a_failed_message_should_receive_message_again` |
| `When_requeueing_redrives_to_the_dlq` | `When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue` |

## New Coverage from Templates (not in manual tests)

- `When_multiple_threads_try_to_post_a_message_at_the_same_time_should_not_throw_exception`
- `When_sending_a_message_should_propagate_activity_context`
- `When_infrastructure_missing_and_assume_channel_should_throw_exception`
- `When_posting_a_message_but_no_broker_created_should_throw_exception`
- `When_posting_a_message_with_partition_key_via_the_messaging_gateway_should_be_received`

## Success Criteria

- **SC-001:** Generator produces ~40 test files (10 per variant×pattern combo)
- **SC-002:** All generated tests pass against GCP Pub/Sub emulator
- **SC-003:** Manual tests in `Pull/` and `Stream/` directories are deleted
- **SC-004:** No regression in GCP outbox generated tests
- **SC-005:** `generate-test.sh` / `generate-test.ps1` work without modification

## Assumptions

- GCP Pub/Sub emulator is available via Docker Compose (`docker-compose.yaml`)
- The existing `GatewayFactory` singleton pattern is reused (not replaced)
- Template Liquid files do not need modification for GCP (all GCP differences are in provider + config)
- The `MessageAssertion` class may need to be created if GCP message assertions differ from defaults

## Edge Cases

- **Ordering key tests** require enabling ordering on the subscription; provider must handle this
- **DLQ tests** need the emulator to support dead letter policies (verify emulator compatibility)
- **Stream mode cleanup** may require different teardown than Pull mode
- **Concurrent tests** — ensure topic/subscription names are unique per test to avoid interference
