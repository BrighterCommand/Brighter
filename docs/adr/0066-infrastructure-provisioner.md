---
id: 0066-infrastructure-provisioner
title: "Infrastructure Provisioner for Messaging Gateways"
status: Proposed
author:
  - "Rafael Lillo"
created: 2026-07-28
summary: "Introduces IAmAInfrastructureProvisioner with per-gateway Assume/Validate/Create/CreateOrUpdate implementations, attached to Publication and Subscription, to standardize how gateways provision broker infrastructure. Ships opt-in in V10 alongside deprecated legacy provisioning members; becomes the single mechanism in V11."
tags:
  - "transports"
  - "provisioning"
  - "api-design"
---

# 66. Infrastructure Provisioner for Messaging Gateways

Date: 2026-07-28

## Status

Proposed

## Context

Brighter supports multiple messaging gateways (RabbitMQ, Kafka, AWS SNS/SQS, Azure Service Bus, GCP Pub/Sub, Redis, MsSql, Postgres, RocketMQ; MQTT excluded — no broker-side infrastructure). Today, the infrastructure a gateway depends on — exchanges, topics, queues, subscriptions, dead-letter queues — is provisioned inconsistently:

- Some gateways auto-create infrastructure at channel open time, others assume it exists and fail at runtime.
- DevOps teams must hand-maintain gateway-specific scripts or Terraform to stand up infrastructure outside the application.
- There is no unified way to **validate** that required infrastructure exists before a consumer starts, which makes startup failures hard to diagnose (missing topic vs. wrong credentials vs. network issue).

We want a single, gateway-agnostic abstraction for provisioning, with **distinct implementations per gateway** — Assume, Validate, Create, and CreateOrUpdate — rather than one implementation switching behavior internally. Not every gateway/side ships all four: each ships only the strategies the broker actually supports.

## Decision

Introduce a narrow interface, `IAmAInfrastructureProvisioner`, in `Paramore.Brighter`, taking the gateway it operates on:

```csharp
/// <summary>
/// Provisions (or checks) the infrastructure — exchanges, topics, queues,
/// subscriptions — required by a messaging gateway. Each gateway ships
/// implementations of the strategies it supports: Assume, Validate,
/// Create, CreateOrUpdate.
/// </summary>
public interface IAmAInfrastructureProvisioner
{
    /// <summary>Executes the provisioning strategy synchronously.</summary>
    /// <param name="gateway">The messaging gateway whose infrastructure is provisioned.</param>
    void Provision(IAmAMessageGateway gateway);

    /// <summary>Executes the provisioning strategy asynchronously.</summary>
    /// <param name="gateway">The messaging gateway whose infrastructure is provisioned.</param>
    Task ProvisionAsync(IAmAMessageGateway gateway, CancellationToken cancellationToken = default);
}
```

The interface stays minimal by design: **everything a given strategy needs is a property of that implementation**, configured at construction/registration time — not parameters on the interface.

### Strategy implementations per gateway — short names

Naming is `{Gateway}[{Side}]{Strategy}`, keeping names short (no `InfrastructureProvisioner` suffix on classes).

Each class implements `IAmAInfrastructureProvisioner` and carries **only the properties its strategy needs**:

- **Assume** — does **nothing**; a pure no-op. It simply declares "the infrastructure exists and is managed externally" (e.g. Terraform) and returns immediately — no connectivity check, no SDK calls. Zero properties.
- **Validate** — a few properties: the expected resource (topic/queue/subscription) to check, and how to report findings. Read-only; never mutates.
- **Create** — the most properties: full resource specifications (DLQs, policies, delivery settings, tags, etc.) needed to materialize the infrastructure. Create-only: existing resources are never modified.
- **CreateOrUpdate** — same property surface as Create, plus update behavior: existing resources are **converged** to the declared configuration. Only implemented where the broker supports in-place updates; opt-in, for dev/test or app-owned infrastructure. Production guidance remains Assume/Validate.

**General rule:** whenever the publication side and the subscription side own **different resource types**, the gateway gets two sets — one per side. Where both sides share a single resource, one set suffices. Each set ships only the strategies the broker supports (Assume / Validate / Create / CreateOrUpdate).

| Gateway (package) | Side | Assume | Validate | Create | CreateOrUpdate | Resources handled |
|---|---|---|---|---|---|---|
| RabbitMQ (`RMQ.Sync` / `RMQ.Async`) | Publication | `RmqExchangeAssume` | `RmqExchangeValidate` | `RmqExchangeCreate` | — (exchange properties immutable) | exchanges, type, durability |
| | Subscription | `RmqQueueAssume` | `RmqQueueValidate` | `RmqQueueCreate` | `RmqQueueCreateOrUpdate` (policies/re-declare where legal) | queues, bindings to exchanges, DLX, TTL, quorum |
| Kafka | Both (shared) | `KafkaAssume` | `KafkaValidate` | `KafkaCreate` | `KafkaCreateOrUpdate` (AlterConfigs; no partition decrease) | topics (AdminClient), partitions, replication factor |
| AWS SNS (`AWSSQS` / `AWSSQS.V4`) | Publication | `SnsAssume` | `SnsValidate` | `SnsCreate` | `SnsCreateOrUpdate` (SetTopicAttributes, Tag/Untag) | topics, FIFO, KMS, tags |
| AWS SQS (`AWSSQS` / `AWSSQS.V4`) | Subscription | `SqsAssume` | `SqsValidate` | `SqsCreate` | `SqsCreateOrUpdate` (SetQueueAttributes, Tag/UntagQueue) | queues, DLQs, redrive, SNS subscription wiring |
| Azure Service Bus | Publication | `AsbTopicAssume` | `AsbTopicValidate` | `AsbTopicCreate` | `AsbTopicCreateOrUpdate` | topics, partitioning, duplicate detection |
| | Subscription | `AsbSubscriptionAssume` | `AsbSubscriptionValidate` | `AsbSubscriptionCreate` | `AsbSubscriptionCreateOrUpdate` | subscriptions, rules/filters, queues (for direct), DLQ settings, lock duration |
| GCP Pub/Sub | Publication | `GcpTopicAssume` | `GcpTopicValidate` | `GcpTopicCreate` | `GcpTopicCreateOrUpdate` (UpdateTopic) | topics, message retention, schema, KMS |
| | Subscription | `GcpSubscriptionAssume` | `GcpSubscriptionValidate` | `GcpSubscriptionCreate` | `GcpSubscriptionCreateOrUpdate` (UpdateSubscription) | subscriptions, ack deadline, dead-letter policy, retry policy, filters, ordering |
| Redis | Publication | `RedisStreamAssume` | `RedisStreamValidate` | `RedisStreamCreate` | `RedisStreamCreateOrUpdate` (XADD MAXLEN / XSETID where applicable) | streams (MKSTREAM), max length / trimming |
| | Subscription | `RedisGroupAssume` | `RedisGroupValidate` | `RedisGroupCreate` | — (no group update API) | consumer groups on streams |
| MsSql | Both (shared) | `MsSqlAssume` | `MsSqlValidate` | `MsSqlCreate` | `MsSqlCreateOrUpdate` (idempotent ALTER/migration DDL) | queue tables, indexes, stored procs (DDL scripts) |
| Postgres | Both (shared) | `PostgresAssume` | `PostgresValidate` | `PostgresCreate` | `PostgresCreateOrUpdate` (idempotent ALTER/migration DDL) | queue tables, indexes, LISTEN/NOTIFY triggers (DDL scripts) |
| RocketMQ | Publication | `RocketMqTopicAssume` | — | — | — | **Assume only** — topics cannot be created in flight by the client |
| | Subscription | `RocketMqGroupAssume` | `RocketMqGroupValidate` | `RocketMqGroupCreate` | — | consumer groups, retry/DLQ topics |

**CreateOrUpdate rules:**
- Same property surface as the corresponding `Create` class; the difference is purely behavioral (converge vs. leave-alone).
- Only ships where the broker exposes a real update API (see table). Where updates are impossible or unsafe (RabbitMQ exchange re-declaration with different type, Redis consumer groups, RocketMQ), the strategy is omitted rather than faked.
- Updates are **additive/convergent, never destructive**: CreateOrUpdate never deletes resources, removes subscriptions, or drops data (e.g. no Kafka partition decrease, no queue purge). Destructive change belongs to IaC.
- Still idempotent: running it repeatedly converges to the same state.

### Which providers need the split, and which don't

**Need the split (different resources per side):**
- **GCP Pub/Sub** — yes: `GcpTopic*` (publication) vs `GcpSubscription*` (subscription). Topics and subscriptions are fully independent resources with different property sets (schema/KMS vs ack deadline/dead-letter/retry), managed by different admin clients (`PublisherServiceApiClient` vs `SubscriberServiceApiClient`).
- **AWS** — split: `Sns*` / `Sqs*`.
- **Azure Service Bus** — yes: `AsbTopic*` (publication) vs `AsbSubscription*` (subscription; also covers plain queues for direct-to-queue scenarios).
- **RabbitMQ** — yes: `RmqExchange*` (publication) vs `RmqQueue*` (subscription). Exchanges and queues/bindings are distinct declarations with distinct properties; a publication shouldn't need queue/DLX settings.
- **Redis** — yes, lightly: `RedisStream*` (publication, incl. trimming policy) vs `RedisGroup*` (subscription, consumer groups only).
- **RocketMQ** — yes: `RocketMqTopic*` vs `RocketMqGroup*`.

**Don't need the split (one shared resource):**
- **Kafka** — the topic *is* the infrastructure for both sides; consumer groups are implicit and not provisionable. One set.
- **MsSql / Postgres** — both sides share the same queue table; one set running idempotent DDL.

### Per-gateway notes (checked against the actual gateway packages in the repo)

- **AWS SNS vs SQS — separate provisioners.** The two services have genuinely different resource models: SNS owns topics, subscriptions, and topic-level policies/encryption; SQS owns queues, queue attributes, DLQs and redrive policies. A combined implementation would force both SDK clients into every scenario. Typically a **publication uses the SNS provisioner, a subscription uses the SQS provisioner** (with `SqsCreate` optionally subscribing its queue to the SNS topic). The provisioner classes live once in the AWS gateway code and are shared by the `AWSSQS` (SDK v3) and `AWSSQS.V4` packages, parameterized by the SDK-version-specific client — same pattern as the gateway itself.
- **RabbitMQ — one provisioner set, shared by `RMQ.Sync` and `RMQ.Async`.** The exchange/queue split applies to both packages identically; the sync/async difference is in message flow, not infrastructure.
- **Azure Service Bus — split into `AsbTopic*` / `AsbSubscription*`.** One admin SDK, but topic properties (partitioning, duplicate detection) and subscription properties (rules, lock duration, DLQ) are disjoint enough to warrant separate classes rather than one bag.
- **GCP Pub/Sub — split into `GcpTopic*` / `GcpSubscription*`.** Mirrors the AWS split: separate admin clients and disjoint property sets.
- **Kafka — Create is thin.** Only topics (with partitions/replication); consumer groups are created implicitly by the broker on first consume, so `KafkaValidate` can't validate them meaningfully. Validation depth is limited to topic existence and config via `AdminClient`.
- **MsSql / Postgres — Create runs DDL.** "Infrastructure" is queue tables/indexes/triggers; `MsSqlCreate`/`PostgresCreate` apply idempotent DDL scripts (`IF NOT EXISTS`), `Validate` checks table/schema presence. Assume remains a no-op.
- **MQTT — out of scope.** MQTT brokers hold no durable per-topic infrastructure to provision, so the MQTT gateway is explicitly excluded from this ADR; it keeps its current behavior and gets no provisioner set.
- **Redis — streams + consumer groups.** `RedisStreamCreate` issues `XADD`/`XGROUP CREATE ... MKSTREAM` (idempotent via `BUSYGROUP` tolerance); `RedisStreamValidate` uses `XINFO`.
- **RocketMQ — topic side is Assume-only.** RocketMQ topics cannot be created in flight from the client (broker-side `autoCreateTopicEnable` is discouraged/disabled in production and not a supported provisioning path for Brighter), so the publication side ships `RocketMqTopicAssume` only — no Validate, no Create. The subscription side (consumer groups, retry/DLQ topics) keeps Validate/Create via the admin API. This also establishes the precedent that **a side may ship a subset of the strategies** when the broker doesn't support the operation.

### Current AWS behavior (baseline, verified against `AWSSQS.V4`)

Today's `AWSMessagingGateway` is driven by an `OnMissingChannel` setting (`Create` / `Validate` / `Assume` — the conceptual ancestor of this ADR's strategies) and is **create-only, never update**:

- `CreateTopic` is called because it is idempotent (returns the existing ARN), with attributes/tags supplied at call time.
- `CreateQueue` is called only when the queue is missing.
- There are **no** `SetQueueAttributes`, `SetTopicAttributes`, `TagResource`, or `TagQueue` calls — Brighter **does not update or converge** existing SNS/SQS resources. If a topic/queue already exists with different attributes, the current code silently keeps the existing configuration.

**Decision carried into this ADR:** the `*Create` implementations preserve this **create-only, no-in-place-update** semantics — they create what is missing and leave existing resources untouched. This keeps `Create` backward-compatible and avoids Brighter silently mutating infrastructure.

For users who *do* want Brighter to manage infrastructure actively, a fourth strategy ships alongside it: **`*CreateOrUpdate`** — creates missing resources and **updates existing ones** to the declared configuration (e.g. `SnsCreateOrUpdate` uses `SetTopicAttributes`/`TagResource`; `SqsCreateOrUpdate` uses `SetQueueAttributes`/`TagQueue`). It is strictly opt-in, only implemented where the broker supports updates, and never destructive. Drift detection remains `*Validate`'s job; `CreateOrUpdate` is the opt-in cure.

## Sample: AWS SNS & SQS

SNS and SQS get separate provisioner sets, used on different sides of the message flow. **Each provisioner instance serves exactly one `Publication` or one `Subscription`** — it is constructed for (and attached to) that single publication/subscription, so it provisions the one topic or one queue that publication/subscription routes to. There are no "all topics" / "all queues" god-provisioners; two subscriptions to different queues get two `SqsCreate` instances, each with its own settings.

### SNS (publication side — one provisioner per Publication)

```csharp
using Paramore.Brighter;

namespace Paramore.Brighter.MessagingGateway.AWSSNS;

/// <summary>
/// Assumes this publication's SNS topic already exists (managed externally,
/// e.g. Terraform). Does nothing — a pure no-op marker.
/// </summary>
public sealed class SnsAssume : IAmAInfrastructureProvisioner
{
    public void Provision(IAmAMessageGateway gateway) { }

    public Task ProvisionAsync(IAmAMessageGateway gateway, CancellationToken ct = default)
        => Task.CompletedTask;
}

/// <summary>
/// Validates that this publication's SNS topic exists and is correctly
/// configured. Never mutates anything.
/// </summary>
public sealed class SnsValidate : IAmAInfrastructureProvisioner
{
    /// <summary>The topic this publication routes to; must exist.</summary>
    public required string Topic { get; init; }

    /// <summary>Whether topic attributes (FIFO, KMS encryption) must match the publication config.</summary>
    public bool CheckAttributes { get; set; } = true;

    public void Provision(IAmAMessageGateway gateway)
        => ProvisionAsync(gateway).GetAwaiter().GetResult();

    public async Task ProvisionAsync(IAmAMessageGateway gateway, CancellationToken ct = default)
    {
        // GetTopicAttributes for Topic. Throws a ProvisioningException if the
        // topic is missing or (when CheckAttributes) misconfigured.
    }
}

/// <summary>
/// Creates this publication's SNS topic if missing. Idempotent (CreateTopic
/// returns the existing ARN). Requires SNS write permissions.
/// </summary>
public sealed class SnsCreate : IAmAInfrastructureProvisioner
{
    /// <summary>The topic this publication routes to.</summary>
    public required string Topic { get; init; }

    /// <summary>Create the topic as a FIFO topic.</summary>
    public bool UseFifo { get; set; }

    /// <summary>KMS key id for topic server-side encryption (optional).</summary>
    public string? KmsKeyId { get; set; }

    /// <summary>Tags applied to the created topic.</summary>
    public IDictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();

    public void Provision(IAmAMessageGateway gateway)
        => ProvisionAsync(gateway).GetAwaiter().GetResult();

    public async Task ProvisionAsync(IAmAMessageGateway gateway, CancellationToken ct = default)
    {
        // CreateTopic for Topic (idempotent — returns existing ARN),
        // with FIFO/KMS attributes and Tags supplied at creation time.
        // Create-only: an existing topic is left untouched (no
        // SetTopicAttributes/TagResource), matching current Brighter behavior.
    }
}
```

### SQS (subscription side — one provisioner per Subscription)

```csharp
namespace Paramore.Brighter.MessagingGateway.AWSSQS;

/// <summary>
/// Assumes this subscription's SQS queue already exists. Does nothing — a
/// pure no-op marker.
/// </summary>
public sealed class SqsAssume : IAmAInfrastructureProvisioner
{
    public void Provision(IAmAMessageGateway gateway) { }

    public Task ProvisionAsync(IAmAMessageGateway gateway, CancellationToken ct = default)
        => Task.CompletedTask;
}

/// <summary>
/// Validates that this subscription's SQS queue (and DLQ) exists, is correctly
/// configured, and is subscribed to the expected SNS topic.
/// Never mutates anything.
/// </summary>
public sealed class SqsValidate : IAmAInfrastructureProvisioner
{
    /// <summary>The queue this subscription consumes from; must exist.</summary>
    public required string Queue { get; init; }

    /// <summary>Whether queue attributes (visibility timeout, redrive policy) must match the subscription config.</summary>
    public bool CheckAttributes { get; set; } = true;

    /// <summary>The SNS topic the queue must be subscribed to (optional check).</summary>
    public string? SubscribedTopic { get; init; }

    public void Provision(IAmAMessageGateway gateway)
        => ProvisionAsync(gateway).GetAwaiter().GetResult();

    public async Task ProvisionAsync(IAmAMessageGateway gateway, CancellationToken ct = default)
    {
        // GetQueueUrl / GetQueueAttributes for Queue; ListSubscriptionsByTopic
        // when SubscribedTopic is set. Throws a ProvisioningException listing
        // every mismatch found.
    }
}

/// <summary>
/// Creates this subscription's SQS queue and DLQ if missing, attaches the
/// redrive policy, and optionally subscribes the queue to its SNS topic.
/// Idempotent. Requires SQS write permissions (plus SNS Subscribe if wiring).
/// </summary>
public sealed class SqsCreate : IAmAInfrastructureProvisioner
{
    // ---- Queue ----
    /// <summary>The queue this subscription consumes from.</summary>
    public required string Queue { get; init; }

    public TimeSpan VisibilityTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan MessageRetentionPeriod { get; set; } = TimeSpan.FromDays(4);
    public TimeSpan ReceiveMessageWaitTime { get; set; } = TimeSpan.FromSeconds(20); // long polling
    public bool UseFifo { get; set; }

    // ---- Dead-letter queue ----
    /// <summary>Create a DLQ named "{Queue}-DLQ" and attach a redrive policy.</summary>
    public bool CreateDeadLetterQueue { get; set; } = true;
    public int MaxReceiveCount { get; set; } = 3;

    // ---- SNS wiring ----
    /// <summary>The SNS topic to subscribe the queue to (null = plain queue, no SNS).</summary>
    public string? SubscribeToTopic { get; init; }
    public bool RawMessageDelivery { get; set; } = true;

    /// <summary>Tags applied to the created queue.</summary>
    public IDictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();

    public void Provision(IAmAMessageGateway gateway)
        => ProvisionAsync(gateway).GetAwaiter().GetResult();

    public async Task ProvisionAsync(IAmAMessageGateway gateway, CancellationToken ct = default)
    {
        // 1. CreateQueue for Queue and its DLQ, with attributes above, only
        //    when missing. Existing queues are left untouched (create-only,
        //    matching current Brighter behavior).
        // 2. Redrive policy and tags are applied as part of creation.
        // 3. When SubscribeToTopic is set: SNS Subscribe queue ARN → topic
        //    ARN with RawMessageDelivery; queue access policy allowing the
        //    topic to send (subscription creation is idempotent).
        // No SetQueueAttributes/TagQueue on pre-existing resources — drift is
        // surfaced by SqsValidate instead of being silently corrected.
    }
}
```

Note the separation of concerns: `SnsCreate` knows nothing about queues; `SqsCreate` only touches SNS to subscribe. A publication that produces directly to an SQS queue can attach `SqsCreate` with no SNS properties at all. And because each instance is scoped to one publication/subscription, settings are per-route: two subscriptions to different queues each get their own `SqsCreate` with independent visibility timeouts, DLQ policies, and tags.

### CreateOrUpdate variants

Where updates are supported, each `Create` class has a `CreateOrUpdate` sibling with the same properties and different behavior:

```csharp
/// <summary>
/// Creates this publication's SNS topic when missing AND updates an existing
/// topic to the declared configuration. Opt-in; never destructive.
/// </summary>
public sealed class SnsCreateOrUpdate : IAmAInfrastructureProvisioner
{
    // Same properties as SnsCreate: Topic, UseFifo, KmsKeyId, Tags.

    public void Provision(IAmAMessageGateway gateway)
        => ProvisionAsync(gateway).GetAwaiter().GetResult();

    public async Task ProvisionAsync(IAmAMessageGateway gateway, CancellationToken ct = default)
    {
        // 1. CreateTopic for Topic (idempotent).
        // 2. For an EXISTING topic: SetTopicAttributes where the current value
        //    differs from the declared one (e.g. KmsMasterKeyId).
        //    Immutable properties (FIFO on a standard topic) are reported as
        //    errors, not forced.
        // 3. TagResource/UntagResource to converge Tags.
    }
}

/// <summary>
/// Creates this subscription's SQS queue when missing AND updates an existing
/// queue to the declared configuration (attributes, redrive policy, tags).
/// Opt-in; never destructive — never purges or deletes the queue.
/// </summary>
public sealed class SqsCreateOrUpdate : IAmAInfrastructureProvisioner
{
    // Same properties as SqsCreate.

    public void Provision(IAmAMessageGateway gateway)
        => ProvisionAsync(gateway).GetAwaiter().GetResult();

    public async Task ProvisionAsync(IAmAMessageGateway gateway, CancellationToken ct = default)
    {
        // 1. CreateQueue for Queue/DLQ when missing.
        // 2. For an EXISTING queue: GetQueueAttributes, then SetQueueAttributes
        //    for any attribute that differs (visibility timeout, retention,
        //    redrive policy, access policy).
        // 3. Subscribe wiring as in SqsCreate (idempotent).
        // 4. TagQueue/UntagQueue to converge Tags.
        // Immutable differences (standard vs FIFO) are reported, not forced.
    }
}
```

Immutable or unsafe changes (e.g. converting a standard queue to FIFO, decreasing Kafka partitions) are **reported, never applied** — CreateOrUpdate converges what can be converged and fails loudly on the rest.

### Wiring: provisioner on Publication and Subscription

The provisioner is attached where provisioning decisions actually belong — the `Publication` (producer side) and the `Subscription` (consumer side):

```csharp
public class Publication
{
    // ... existing members ...

    /// <summary>
    /// How this publication's infrastructure is provisioned before the
    /// producer is used. Null in V10 (not yet mandatory).
    /// </summary>
    public IAmAInfrastructureProvisioner? Provisioner { get; set; }
}

public class Subscription
{
    // ... existing members ...

    /// <summary>
    /// How this subscription's infrastructure is provisioned before the
    /// channel is opened. Null in V10 (not yet mandatory).
    /// </summary>
    public IAmAInfrastructureProvisioner? Provisioner { get; set; }
}
```

Because the provisioner is a property on `Publication`/`Subscription`, users can also **mix strategies in one app**: e.g. `SqsCreate` for a dev subscription, `SnsAssume` for a production publication managed by Terraform — no global mode switch.

#### Workflow

The workflow stays the same as today — provisioning still happens at producer creation and at channel open — but instead of **each provider doing it its own way** (ad-hoc inline code scattered across gateways), every gateway follows **one pattern**: resolve the provisioner from the `Publication`/`Subscription` and call the interface.

```
Producer path:   build producer → publication.Provisioner?.ProvisionAsync(gateway) → send
Consumer path:   open channel   → subscription.Provisioner?.ProvisionAsync(gateway) → receive
```

- Gateways implement the pattern once in their channel/producer factory base; no per-gateway provisioning logic anymore.

#### Default provisioner resolution

If the user doesn't supply a provisioner, the gateway **creates one from the current configuration** instead of running separate legacy logic:

```csharp
var provisioner = subscription.Provisioner
    ?? gateway.CreateDefaultProvisioner(subscription); // built from existing config

await provisioner.ProvisionAsync(gateway);
```

- Each gateway implements `CreateDefaultProvisioner(Publication|Subscription)`, which maps the existing (V10-deprecated) config onto the matching implementation — e.g. if the legacy config says "create queues on startup", it builds an `SqsCreate` populated from those settings; if it says nothing, it builds an `SqsAssume`.
- This means there is **only ever one execution path** — `IAmAInfrastructureProvisioner` — even for users who never touch the new API. The legacy config becomes just another way to construct a provisioner, which keeps V10 safe and makes V11 removal a matter of deleting the mapping code, not untangling two provisioning pipelines.

### Migration plan: V10 → V11

**V10 (non-breaking, opt-in)**
- `IAmAInfrastructureProvisioner` and the per-gateway `*Assume` / `*Validate` / `*Create` / `*CreateOrUpdate` implementations ship as new public API.
- `Publication.Provisioner` / `Subscription.Provisioner` are added as **optional** (nullable). Not mandatory — existing code keeps working unchanged.
- The existing per-gateway provisioning members on `Publication`/`Subscription` that become unnecessary (e.g. flags like "create topic/queue on startup", inline infrastructure settings now owned by `*Create`-style implementations) are marked `[Obsolete]` with a message pointing at the corresponding provisioner implementation.

**V11 (breaking cleanup)**
- The `[Obsolete]` properties are **removed** from `Publication`/`Subscription`.
- `Provisioner` becomes the single, standard mechanism; gateways drop their legacy inline provisioning code paths entirely.

This gives users a full major version to migrate: set a provisioner, delete usage of the deprecated flags, done.

### Naming

- Interface follows Brighter's `IAmA*` convention (`IAmAChannelFactory`, `IAmAMessageProducer`, `IAmACommandProcessor`).
- Class names stay short: `RmqQueueCreate`, `SnsValidate`, etc. — the strategy is the name; the implemented interface supplies the "infrastructure provisioner" context.
- "Provisioner" rather than "Manager" signals narrow scope: assume / validate / create / create-or-update only. Destruction and drift reconciliation are explicitly out of scope.

## Consequences

**Positive**
- One consistent lifecycle for gateway infrastructure across all transports.
- Fast, diagnosable startup failures via the Validate implementations.
- Idempotent Create implementations simplify local dev and integration testing.
- Assume implementations are no-ops supporting "infra managed by Terraform" environments — no SDK calls, no credentials required for provisioning at all, and creation code is not even present.
- Per-strategy properties keep each class's surface proportional to its job; no god-object options bag shared by all four strategies.
- CreateOrUpdate offers an opt-in convergence path for teams that want Brighter to actively manage dev/test infrastructure, without changing Create's backward-compatible behavior.

**Negative / Risks**
- Class count: up to 4 classes × sides × gateways (a shared abstract base per gateway can hold common resource-descriptor logic to keep them thin).
- V10 carries dual provisioning paths (legacy inline + provisioner) until V11 removes the legacy ones — gateways must be careful the two never both run. (Mitigated by default-provisioner resolution: legacy config is translated into a provisioner, so only one pipeline ever executes.)
- Some admin APIs (e.g. older Kafka clients) have limited validation capability, so Validate depth will vary per gateway.
- New dependency on admin APIs (AWS SDK SNS/SQS, Confluent AdminClient, etc.) increases package surface for each gateway package.
- `Create`/`CreateOrUpdate` with broad cloud permissions is a security footgun — `CreateOrUpdate` doubly so, since it mutates existing resources. Documentation must recommend least-privilege, opt-in use, and default `Assume`/`Validate` in production.

## Alternatives Considered

1. **One interface with `AssumeAsync`/`ValidateAsync`/`CreateAsync` methods** — rejected: forces every implementation to carry all strategy code paths (and their SDK dependencies/permissions), invites internal `switch` behavior, and blurs least-privilege boundaries. Separate implementations of a small interface is cleaner.
2. **One options bag on the interface** — rejected: `Create`'s large property surface would pollute `Assume`/`Validate`; per-implementation properties keep each class honest.
3. **Keep per-gateway ad-hoc provisioning** — rejected: inconsistent behavior and poor diagnostics; the problem this ADR solves.
4. **Infrastructure-as-Code only (document Terraform/Pulumi)** — rejected as the sole answer: still the recommended production path (Assume), but doesn't help the dev/test inner loop or startup validation.
5. **Single generic provisioner driven by declarative config** — rejected: each broker's resource model differs too much; a leaky abstraction would result.

## Open Questions

- Should there be a shared abstract base per gateway (holding the resource descriptor) that the strategy implementations derive from?
- Do we expose a CLI/health-check endpoint that runs the registered provisioner on demand?
- Should the sync `Provision` exist at all for gateways whose SDKs are async-only (AWS SDK is async)? Keep it for interface parity with the gateway, or drop it?
