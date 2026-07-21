# Ralph Tasks: 0036-universal-transport-conformance-tests

> Auto-generated for unattended TDD execution. Each task is self-contained with all context a fresh Claude session needs.

## Spec Context

- **Spec**: 0036-universal-transport-conformance-tests
- **Requirements**: specs/0036-universal-transport-conformance-tests/requirements.md
- **ADRs**: docs/adr/0066-conformance-test-provider-and-ungating.md, docs/adr/0067-conformance-rollout-and-deferral-governance.md

## Execution Notes

- **Container runtime is Podman**, so the broker command is `docker compose` (two words) — never `docker-compose`. Per-transport compose files live at the repo root: `docker-compose-kafka.yaml`, `docker-compose-redis.yaml`, `docker-compose-postgres.yaml`, `docker-compose-mssql.yaml`, `docker-compose-rmq.yaml`, `docker-compose-rocketmq.yaml`, `docker-compose-aws.yaml` / `docker-compose-localstack.yaml`, `docker-compose-mqtt.yaml`. There is **no** `docker-compose-gcp.yaml` and **no** ASB compose file (GCP uses its Pub/Sub emulator/project; ASB is a cloud service). **The task agent starts the container itself** — every per-transport conformance/onboarding task's RALPH-VERIFY begins by bringing the broker up.
- **Flag-and-move-on rule (from ADR 0067 + owner rulings).** A per-transport conformance/onboarding task must NOT stall the loop when a broker/emulator/CI-infrastructure/upstream dependency blocks it (e.g. RocketMQ's upstream client, ASB cloud CI). Its completion condition then includes the fallback: (1) set that configuration's ledger cell(s) in `specs/0036-universal-transport-conformance-tests/conformance-status.md` to `Deferred -> #NNNN (sign-off: @maintainer)`; (2) add the greppable in-code marker to the deferred generated test(s), `Skip = "Deferred: #NNNN — <behaviour> not yet conformant for <transport> (maintainer sign-off)"` (ADR 0067); (3) continue.
- **`#NNNN` is a documented pre-audit placeholder.** Deferred ledger cells and Skip markers carry the literal token `#NNNN` through Phases 1–5. It is reconciled to a real linked issue number by the FIRST task of Phase 6 ("Raise follow-up issues …"), which runs BEFORE the two Phase 6 audit tasks — so by the time the audit asserts the `Deferred: #<digits>` pattern, every marker carries real digits. Do not expect `#NNNN` to be a real issue number until that reconciliation task has run.
- **Generator build/regenerate.** Build: `dotnet build tools/Paramore.Brighter.Test.Generator`. Regenerate one project (CWD is the output root): `cd tests/<Project> && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator`. Regenerate all: `./generate-test.sh`. **The generator never deletes stale generated files** — every template deletion needs a paired manual sweep of `tests/Paramore.Brighter.*.Tests/**/Generated/`.
- **Do NOT run `./generate-test.sh` during Phase 0** until every provider has migrated (a full regenerate would break un-migrated projects against the new FR-1 signature). Regenerate per project, in the migration task for that project.
- Generator-behaviour TDD home: `tests/Paramore.Brighter.Test.Generator.Tests/`.
- **TDD in ralph mode — owner-sanctioned exception to the interactive `/test-first` gate.** CLAUDE.md's interactive `/test-first` workflow (write test → STOP for human IDE approval → implement) assumes a human in the loop. This spec's behaviour is produced by Liquid templates and asserted by the generator test project against the emitted `.cs` content, and it runs unattended — so the human approval checkpoint is intentionally not used here. TDD is still enforced structurally: each behavioural task names its failing test (a generator xUnit test asserting emitted content, or the audit tests in Phase 6) and its RALPH-VERIFY runs it. This deviation from the interactive gate is deliberate and recorded, not a silent skip.

## Tasks

### Phase 0 — Seams that unblock everything

- [x] **Seed the conformance ledger**
  - **Behavior**: A new checked-in markdown matrix records per-configuration conformance state and is the gate on the terminal cleanup. It has one row per wired gateway configuration (20) plus one placeholder row per un-onboarded transport (3), so all twelve targeted transports are represented; 11 columns, one per canonical behaviour (FR-2, FR-4, FR-5, FR-6, FR-7, FR-8, FR-9, FR-15, FR-16, FR-17, FR-22); every cell seeded `Unknown`. Cell vocabulary: `Unknown` (transient, fix-phase only) / `Pass` / `Fixed (#PR/commit)` / `Deferred -> #NNNN (sign-off: @maintainer)`. Placeholder-row cells may only ever resolve to `Deferred`.
  - **Test file**: `specs/0036-universal-transport-conformance-tests/conformance-status.md` (artifact under test; no xUnit test)
  - **Test should verify**:
    - Exactly these 23 rows exist: `AWS / SnsStandard`, `AWS / SnsFifo`, `AWS / SqsStandard`, `AWS / SqsFifo`, `AWS.V4 / SnsStandard`, `AWS.V4 / SnsFifo`, `AWS.V4 / SqsStandard`, `AWS.V4 / SqsFifo`, `GCP / Pull`, `GCP / PullOrdering`, `GCP / Stream`, `GCP / StreamOrdering`, `Kafka / Standard`, `Kafka / PartitionKey`, `MSSQL / MSSQLMessagingGateway`, `PostgresSQL / PostgresMessagingGateway`, `Redis / RedisMessagingGateway`, `RMQ.Async / Classic`, `RMQ.Async / Quorum`, `RocketMQ / RocketMQMessagingGateway`, `AzureServiceBus / (not yet declared)`, `MQTT / (not yet declared)`, `RMQ.Sync / (not yet declared)`.
    - The four singular-section configs (Redis, MSSQL, PostgresSQL, RocketMQ) are named by `CollectionName`; the project token is the test-project name from FR-13 (e.g. `PostgresSQL`, not `Postgres`).
    - Known FR-2 non-conformances are pre-seeded in the FR-2 column: `GCP / Pull|PullOrdering|Stream|StreamOrdering` and `RocketMQ / RocketMQMessagingGateway` carry an `Unknown` annotated with the greppable literal token `known FR-2 gap` (exactly these five cells; GCP redelivers immediately; RocketMQ no-op held by native invisibility timeout).
  - **Implementation files**:
    - `specs/0036-universal-transport-conformance-tests/conformance-status.md` - create the 23×11 matrix with the header legend and cell vocabulary above; annotate exactly the five known-FR-2-gap cells with the literal `known FR-2 gap`.
  - **RALPH-VERIFY**: `f=specs/0036-universal-transport-conformance-tests/conformance-status.md; test -f "$f" && [ "$(grep -c 'not yet declared' "$f")" -eq 3 ] && [ "$(grep -c 'known FR-2 gap' "$f")" -eq 5 ] && for r in 'AWS / SnsStandard' 'AWS / SnsFifo' 'AWS / SqsStandard' 'AWS / SqsFifo' 'AWS.V4 / SnsStandard' 'AWS.V4 / SnsFifo' 'AWS.V4 / SqsStandard' 'AWS.V4 / SqsFifo' 'GCP / Pull' 'GCP / PullOrdering' 'GCP / Stream' 'GCP / StreamOrdering' 'Kafka / Standard' 'Kafka / PartitionKey' 'MSSQL / MSSQLMessagingGateway' 'PostgresSQL / PostgresMessagingGateway' 'Redis / RedisMessagingGateway' 'RMQ.Async / Classic' 'RMQ.Async / Quorum' 'RocketMQ / RocketMQMessagingGateway' 'AzureServiceBus / (not yet declared)' 'MQTT / (not yet declared)' 'RMQ.Sync / (not yet declared)'; do grep -qF "$r" "$f" || { echo "MISSING ROW: $r"; exit 1; }; done && for c in FR-2 FR-4 FR-5 FR-6 FR-7 FR-8 FR-9 FR-15 FR-16 FR-17 FR-22; do grep -qF "$c" "$f" || { echo "MISSING COLUMN: $c"; exit 1; }; done`
  - **References**: requirements FR-21, AC-24; ADR 0067 "Key Components → conformance ledger", "Naming a singular-section configuration", "Placeholder rows"; config keys verified in `tests/Paramore.Brighter.{AWS,AWS.V4,Gcp,Kafka}.Tests/test-configuration.json` and `CollectionName` in `tests/Paramore.Brighter.{Redis,MSSQL,PostgresSQL,RocketMQ}.Tests/test-configuration.json`.

- [ ] **Narrow SkipTest to a closed legacy-template list (ADR 0066 step A)**
  - **Behavior**: `MessagingGatewayGenerator.SkipTest` consults the three capability gates only when the template filename is one of exactly four legacy templates; any other template generates regardless of flag values, so canonical templates are ungated by construction — not by naming. This must not rely on substring naming (a canonical delayed-requeue name naturally contains `requeuing` and `with_delay`).
  - **Test file**: `tests/Paramore.Brighter.Test.Generator.Tests/MessagingGatewayGenerator/When_gate_flags_are_false_should_skip_only_legacy_templates.cs`
  - **Test should verify**:
    - Given a config with all three gates `false`, `SkipTest` returns `true` for the four legacy names (`When_reading_a_delayed_message_via_the_messaging_gateway_should_delay_delivery`, `When_requeuing_a_failed_message_should_receive_message_again`, `When_requeuing_a_failed_message_with_delay_should_receive_message_again`, `When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue`).
    - For a hypothetical canonical name containing both `requeuing` and `with_delay` (not on the list), `SkipTest` returns `false` even with all gates `false`.
    - The retained gates (`confirming_posting`, `no_broker_created`, `assume_channel`/`validate_channel`) still behave as today.
  - **Implementation files**:
    - `tools/Paramore.Brighter.Test.Generator/Generators/MessagingGatewayGenerator.cs` - add a `private static readonly string[] LegacyGatedTemplates` with the four names; guard the four gate branches at lines 122, 127, 132, 145 so they are reachable only when `fileName` matches an entry in that list. Leave the three unrelated gate branches untouched.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && dotnet test tests/Paramore.Brighter.Test.Generator.Tests --filter "FullyQualifiedName~MessagingGatewayGenerator"`
  - **References**: requirements FR-10(1),(2),(3), AC-10(a); ADR 0066 "SkipTest's four gate branches — Step A", "the substring-matching hazard"; verified branch lines 122/127/132/145 in `MessagingGatewayGenerator.cs`.

- [ ] **Extend the FR-1 provider interface templates (both variants) and edit the exhaustion template to drop the positional bool**
  - **Behavior**: The two generated provider interface templates gain the full canonical surface, and `CreateSubscription`'s `bool setupDeadLetterQueue` is removed. Because the still-live exhaustion template passes that flag **positionally** as a bare `true` fourth argument (invisible to a name search), its `.liquid` source must be edited in this same change or six test projects fail to compile on regeneration. No project is regenerated in this task (template-only change, verified via the generator test project), so nothing breaks yet.
  - **Test file**: `tests/Paramore.Brighter.Test.Generator.Tests/MessagingGatewayGenerator/When_generating_provider_interface_should_expose_canonical_surface.cs`
  - **Test should verify**:
    - The emitted `IAmAMessageGatewayReactorProvider.cs` and `IAmAMessageGatewayProactorProvider.cs` declare `CreateSubscription(RoutingKey routingKey, ChannelName channelName, OnMissingChannel makeChannel, RoutingKey? deadLetterRoutingKey = null, RoutingKey? invalidMessageRoutingKey = null)` and contain no `setupDeadLetterQueue`.
    - Both declare `GetMessageFromInvalidChannel` (Reactor sync; Proactor `...Async` returning `Task<Message>` with `CancellationToken`) alongside the existing `GetMessageFromDeadLetterQueue`, and a `RejectionMetadataKeys RejectionMetadataKeys { get; }` property; the XML doc states both read members poll bounded and return `MessageType.MT_NONE` when empty or when the subscription does not configure that channel, never throwing/blocking.
    - The emitted exhaustion template copy calls `CreateSubscription(...)` with no positional `true`, passing `deadLetterRoutingKey:` explicitly. This preserves the DLQ-setup behaviour the positional `true` provided (the exhaustion test asserts requeue-to-DLQ), so the still-live exhaustion test keeps its DLQ against the broker until Phase 5 deletes the template. Do NOT drop the DLQ args — that would remove the DLQ and break the running test.
  - **Implementation files**:
    - `tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/Reactor/IAmAMessageGatewayReactorProvider.cs.liquid` - replace the `bool setupDeadLetterQueue = false` param (line 45) with the two nullable `RoutingKey?` params; add `GetMessageFromInvalidChannel`; add `RejectionMetadataKeys` property; document the MT_NONE contract.
    - `tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/Proactor/IAmAMessageGatewayProactorProvider.cs.liquid` - mirror with async members + `CancellationToken`; `RejectionMetadataKeys` stays a plain property.
    - `tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/Reactor/When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue.cs.liquid` - line 48: remove the bare positional `true`, pass the DLQ routing key explicitly.
    - `tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/Proactor/When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue.cs.liquid` - same edit.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && dotnet test tests/Paramore.Brighter.Test.Generator.Tests --filter "FullyQualifiedName~MessagingGatewayGenerator"`
  - **References**: requirements FR-1(1),(2),(3),(5),(6), AC-1; ADR 0066 "Architecture Overview" (interface sketch), "Read-member contract", "Implementation Approach → before→after"; the positional-`true` trap verified at line 48 of the Reactor exhaustion `.liquid`.

- [ ] **Add the Shared/RejectionMetadataKeys template and once-per-config generation mode**
  - **Behavior**: A new third template directory `Shared/` holds `RejectionMetadataKeys.cs.liquid`, a `sealed record RejectionMetadataKeys(string OriginalTopic, string OriginalType, string RejectionReason, string RejectionMessage, string RejectionTimestamp)`. The generator gains a generation mode that emits it **once per gateway configuration** (not once per variant) to `Generated/RejectionMetadataKeys.cs` in the parent namespace `{{ Namespace }}.MessagingGateway{{ Prefix }}` — a sibling of `Reactor/` and `Proactor/`, both of which already have the parent namespace in scope.
  - **Test file**: `tests/Paramore.Brighter.Test.Generator.Tests/MessagingGatewayGenerator/When_generating_gateway_should_emit_rejection_metadata_keys_once_per_config.cs`
  - **Test should verify**:
    - For a single-gateway config, exactly one `Generated/RejectionMetadataKeys.cs` is emitted (not one per Reactor/Proactor variant).
    - The emitted record is `sealed`, has the five members in order, and lives in namespace `{{ Namespace }}.MessagingGateway{{ Prefix }}`.
    - For a `MessagingGateways` (plural) config with N entries, one record is emitted per entry (per configuration), each in its own prefixed parent namespace.
  - **Implementation files**:
    - `tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/Shared/RejectionMetadataKeys.cs.liquid` - new template.
    - `tools/Paramore.Brighter.Test.Generator/Generators/MessagingGatewayGenerator.cs` - in `GenerateAsync`, after the Reactor/Proactor emits, emit the Shared record once per configuration to `MessagingGateway/<prefix>/Generated/RejectionMetadataKeys.cs`.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && dotnet test tests/Paramore.Brighter.Test.Generator.Tests --filter "FullyQualifiedName~MessagingGatewayGenerator"`
  - **References**: requirements FR-1(5), FR-8, C-2; ADR 0066 "Where the record lives", "What a provider returns for a field its gateway does not stamp" (`string.Empty`, never null), "Key Components" (third `Shared/` directory + once-per-config mode).

- [ ] **Migrate the Kafka providers to the FR-1 surface and regenerate Kafka (reference provider first)**
  - **Behavior**: The two Kafka providers implement the post-FR-1 interface (routing-key params, `GetMessageFromInvalidChannel`, `RejectionMetadataKeys` returning Kafka's PascalCase key strings), and Kafka's Generated tree is regenerated so its interface copies + Shared record + edited exhaustion copies are current. Kafka compiles (AC-1). This is the reference migration that proves the interface shape before the other projects follow.
  - **Test file**: `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Standard/Generated/Reactor/IAmAMessageGatewayReactorProvider.cs` (regenerated artifact; verification is compilation)
  - **Test should verify**:
    - `dotnet build tests/Paramore.Brighter.Kafka.Tests` succeeds with the new signature.
    - `KafkaMessageGatewayProvider` and `KafkaPartitionKeyMessageGatewayProvider` return `RejectionMetadataKeys("OriginalTopic","OriginalType","RejectionReason","RejectionMessage","RejectionTimestamp")` (PascalCase, from `src/Paramore.Brighter.MessagingGateway.Kafka/HeaderNames.cs`).
    - No `setupDeadLetterQueue` remains anywhere under `tests/Paramore.Brighter.Kafka.Tests`.
  - **Implementation files**:
    - `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/KafkaMessageGatewayProvider.cs` - implement new members; migrate `CreateSubscription`.
    - `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/KafkaPartitionKeyMessageGatewayProvider.cs` - same.
    - `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/**/Generated/**` - regenerate (interface copies, RejectionMetadataKeys.cs, exhaustion copies).
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && (cd tests/Paramore.Brighter.Kafka.Tests && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator) && dotnet build tests/Paramore.Brighter.Kafka.Tests`
  - **References**: requirements FR-1(6), AC-1; ADR 0066 "Key Components" (per-transport providers), Kafka key names in `src/Paramore.Brighter.MessagingGateway.Kafka/HeaderNames.cs`; regenerate steps in `.agent_instructions/generated_tests.md`.

- [ ] **Migrate the AWS (V3) providers to the FR-1 surface and regenerate AWS**
  - **Behavior**: The four AWS providers (SnsStandard, SnsFifo, SqsStandard, SqsFifo) implement the post-FR-1 interface — routing-key params, `GetMessageFromInvalidChannel`, `RejectionMetadataKeys` returning SQS camelCase key strings (`originalTopic`, `originalMessageType`, `rejectionReason`, `rejectionMessage`, `rejectionTimestamp`; `string.Empty` for any field the gateway does not stamp) — and the AWS Generated tree is regenerated. AWS compiles. (If this exceeds one iteration, split per provider; land SqsStandard first.)
  - **Test file**: `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/Generated/**` (regenerated artifacts; verification is compilation)
  - **Test should verify**:
    - `dotnet build tests/Paramore.Brighter.AWS.Tests` succeeds.
    - No `setupDeadLetterQueue` remains under `tests/Paramore.Brighter.AWS.Tests`.
    - Each provider returns SQS camelCase keys via `RejectionMetadataKeys`.
  - **Implementation files**:
    - `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/{SnsStandard,SnsFifo,SqsStandard,SqsFifo}MessageGatewayProvider.cs` - implement new members; migrate `CreateSubscription`.
    - `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/**/Generated/**` - regenerate.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && (cd tests/Paramore.Brighter.AWS.Tests && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator) && dotnet build tests/Paramore.Brighter.AWS.Tests`
  - **References**: requirements FR-1(6), AC-1; ADR 0066 "What a provider returns for a field its gateway does not stamp"; SQS keys via `SqsMessageConsumer.RefreshMetadata` in `src/Paramore.Brighter.MessagingGateway.AWSSQS`.

- [ ] **Migrate the AWS.V4 providers to the FR-1 surface and regenerate AWS.V4**
  - **Behavior**: The four AWS.V4 providers implement the post-FR-1 interface with SQS camelCase `RejectionMetadataKeys`; AWS.V4 Generated tree regenerated; AWS.V4 compiles. Split per provider if needed, SqsStandard first.
  - **Test file**: `tests/Paramore.Brighter.AWS.V4.Tests/MessagingGateway/Generated/**` (regenerated artifacts; verification is compilation)
  - **Test should verify**:
    - `dotnet build tests/Paramore.Brighter.AWS.V4.Tests` succeeds; no `setupDeadLetterQueue` remains.
  - **Implementation files**:
    - `tests/Paramore.Brighter.AWS.V4.Tests/MessagingGateway/{SnsStandard,SnsFifo,SqsStandard,SqsFifo}MessageGatewayProvider.cs` - migrate.
    - `tests/Paramore.Brighter.AWS.V4.Tests/MessagingGateway/**/Generated/**` - regenerate.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && (cd tests/Paramore.Brighter.AWS.V4.Tests && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator) && dotnet build tests/Paramore.Brighter.AWS.V4.Tests`
  - **References**: requirements FR-1(6), AC-1; ADR 0066 "Key Components".

- [ ] **Migrate the GCP providers to the FR-1 surface and regenerate GCP**
  - **Behavior**: The four GCP providers (Pull, PullOrdering, Stream, StreamOrdering) implement the post-FR-1 interface and `RejectionMetadataKeys` (returning GCP's own key strings, `string.Empty` where a field is not stamped); GCP Generated tree regenerated; GCP compiles. This migration only makes GCP compile — GCP's FR-2 non-conformance is addressed in Phase 4.
  - **Test file**: `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Generated/**` (regenerated artifacts; verification is compilation)
  - **Test should verify**:
    - `dotnet build tests/Paramore.Brighter.Gcp.Tests` succeeds; no `setupDeadLetterQueue` remains.
  - **Implementation files**:
    - `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Gcp{Pull,PullOrdering,Stream,StreamOrdering}MessageGatewayProvider.cs` - migrate.
    - `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/**/Generated/**` - regenerate.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && (cd tests/Paramore.Brighter.Gcp.Tests && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator) && dotnet build tests/Paramore.Brighter.Gcp.Tests`
  - **References**: requirements FR-1(6), AC-1; ADR 0066 "Key Components", "Why there is no scheduler member" (GCP delay behaviour, for Phase 4 context).

- [ ] **Migrate the MSSQL provider to the FR-1 surface and regenerate MSSQL**
  - **Behavior**: `MsSqlMessageGatewayProvider` implements the post-FR-1 interface and `RejectionMetadataKeys`; MSSQL Generated tree regenerated; MSSQL compiles. Note MSSQL previously derived its DLQ routing key internally when `setupDeadLetterQueue` was true — it now receives `deadLetterRoutingKey` explicitly from the test.
  - **Test file**: `tests/Paramore.Brighter.MSSQL.Tests/MessagingGateway/Generated/**` (regenerated artifacts; verification is compilation)
  - **Test should verify**:
    - `dotnet build tests/Paramore.Brighter.MSSQL.Tests` succeeds; no `setupDeadLetterQueue` remains.
  - **Implementation files**:
    - `tests/Paramore.Brighter.MSSQL.Tests/MessagingGateway/MsSqlMessageGatewayProvider.cs` - migrate.
    - `tests/Paramore.Brighter.MSSQL.Tests/MessagingGateway/Generated/**` - regenerate.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && (cd tests/Paramore.Brighter.MSSQL.Tests && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator) && dotnet build tests/Paramore.Brighter.MSSQL.Tests`
  - **References**: requirements FR-1(6), AC-1; ADR 0066 "Implementation Approach" (explicit DLQ key removes hidden naming knowledge); ADR `0040-mssql-dlq-brighter-managed`.

- [ ] **Migrate the PostgresSQL provider to the FR-1 surface and regenerate PostgresSQL**
  - **Behavior**: `PostgresMessageGatewayProvider` implements the post-FR-1 interface and `RejectionMetadataKeys`; PostgresSQL Generated tree regenerated; PostgresSQL compiles.
  - **Test file**: `tests/Paramore.Brighter.PostgresSQL.Tests/MessagingGateway/Generated/**` (regenerated artifacts; verification is compilation)
  - **Test should verify**:
    - `dotnet build tests/Paramore.Brighter.PostgresSQL.Tests` succeeds; no `setupDeadLetterQueue` remains.
  - **Implementation files**:
    - `tests/Paramore.Brighter.PostgresSQL.Tests/MessagingGateway/PostgresMessageGatewayProvider.cs` - migrate.
    - `tests/Paramore.Brighter.PostgresSQL.Tests/MessagingGateway/Generated/**` - regenerate.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && (cd tests/Paramore.Brighter.PostgresSQL.Tests && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator) && dotnet build tests/Paramore.Brighter.PostgresSQL.Tests`
  - **References**: requirements FR-1(6), AC-1; ADR `0041-postgres-dlq-brighter-managed`.

- [ ] **Migrate the Redis provider to the FR-1 surface and regenerate Redis**
  - **Behavior**: `RedisMessageGatewayProvider` implements the post-FR-1 interface and `RejectionMetadataKeys` (Redis camelCase: `originalTopic`, `originalMessageType`, `rejectionReason`, `rejectionMessage`, `rejectionTimestamp`); Redis Generated tree regenerated; Redis compiles.
  - **Test file**: `tests/Paramore.Brighter.Redis.Tests/MessagingGateway/Generated/**` (regenerated artifacts; verification is compilation)
  - **Test should verify**:
    - `dotnet build tests/Paramore.Brighter.Redis.Tests` succeeds; no `setupDeadLetterQueue` remains.
    - Provider returns Redis camelCase keys.
  - **Implementation files**:
    - `tests/Paramore.Brighter.Redis.Tests/MessagingGateway/RedisMessageGatewayProvider.cs` - migrate.
    - `tests/Paramore.Brighter.Redis.Tests/MessagingGateway/Generated/**` - regenerate.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && (cd tests/Paramore.Brighter.Redis.Tests && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator) && dotnet build tests/Paramore.Brighter.Redis.Tests`
  - **References**: requirements FR-1(6), AC-1; Redis keys via `RedisMessageConsumer.RefreshMetadata`; ADR `0039-redis-dlq-brighter-managed`.

- [ ] **Migrate the RMQ.Async providers to the FR-1 surface and regenerate RMQ.Async**
  - **Behavior**: `RmqClassicMessageGatewayProvider` and `RmqQuorumMessageGatewayProvider` implement the post-FR-1 interface and `RejectionMetadataKeys`; RMQ.Async Generated tree regenerated; RMQ.Async compiles.
  - **Test file**: `tests/Paramore.Brighter.RMQ.Async.Tests/MessagingGateway/Generated/**` (regenerated artifacts; verification is compilation)
  - **Test should verify**:
    - `dotnet build tests/Paramore.Brighter.RMQ.Async.Tests` succeeds; no `setupDeadLetterQueue` remains.
  - **Implementation files**:
    - `tests/Paramore.Brighter.RMQ.Async.Tests/MessagingGateway/Rmq{Classic,Quorum}MessageGatewayProvider.cs` - migrate.
    - `tests/Paramore.Brighter.RMQ.Async.Tests/MessagingGateway/**/Generated/**` - regenerate.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && (cd tests/Paramore.Brighter.RMQ.Async.Tests && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator) && dotnet build tests/Paramore.Brighter.RMQ.Async.Tests`
  - **References**: requirements FR-1(6), AC-1; ADR 0066 "Key Components".

- [ ] **Migrate the RocketMQ provider to the FR-1 surface and regenerate RocketMQ**
  - **Behavior**: `RocketMqMessageGatewayProvider` implements the post-FR-1 interface and `RejectionMetadataKeys`; RocketMQ Generated tree regenerated; RocketMQ compiles. This is the last of the 20 provider migrations; after it, AC-1 is satisfiable via a full regenerate.
  - **Test file**: `tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/Generated/**` (regenerated artifacts; verification is compilation)
  - **Test should verify**:
    - `dotnet build tests/Paramore.Brighter.RocketMQ.Tests` succeeds; no `setupDeadLetterQueue` remains anywhere in the repo (`grep -rn setupDeadLetterQueue tests tools` returns nothing).
  - **Implementation files**:
    - `tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/RocketMqMessageGatewayProvider.cs` - migrate.
    - `tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/Generated/**` - regenerate.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && (cd tests/Paramore.Brighter.RocketMQ.Tests && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator) && dotnet build tests/Paramore.Brighter.RocketMQ.Tests && ! grep -rn setupDeadLetterQueue tests tools`
  - **References**: requirements FR-1(6), AC-1; ADR `0042-rocketmq-dlq-brighter-managed`.

### Phase 1 — Canonical templates (each behaviour, both variants, ungated by construction)

> **The Deferred Skip marker is ledger-driven, not hand-written (review finding #2).** The generator reads the conformance ledger and, for each (configuration × canonical behaviour), emits `Skip = "Deferred: #NNNN — …"` on the generated test **only when that cell is not yet `Pass`/`Fixed`**. This single mechanism is what makes the whole rollout coherent: a config is "proven" by flipping its ledger cell to `Pass`/`Fixed` and regenerating — the marker then drops by construction, so there is no hand-deletion and the Phase 5 full regenerate is safe (it re-derives every marker from the resolved ledger). The FIRST task below wires this mechanism; every canonical template then emits the Skip conditionally on the value the generator supplies. Each canonical template is emitted in BOTH Reactor and Proactor variants (FR-14), follows NFR-1 naming and NFR-2 bounded loops, and makes no mechanism assertion (AC-21). TDD is via the generator test project asserting the emitted `.cs` content. Do not add any name to `LegacyGatedTemplates`.

- [ ] **Wire the generator to emit the Deferred Skip conditionally from the conformance ledger**
  - **Behavior**: The generator loads `specs/0036-universal-transport-conformance-tests/conformance-status.md` (resolved relative to the repo root, located by walking up from the output CWD until the file is found), and exposes to each canonical template a per-(configuration × behaviour) Skip value. For the ledger cell of this configuration's row and this template's FR column: if the cell is `Pass` or `Fixed (…)`, the Skip value is empty (test runs); if the cell is `Deferred -> #<n> …`, the Skip value is `Deferred: #<n> — <behaviour> not yet conformant for <transport> (maintainer sign-off)`; if the cell is `Unknown` (transient fix-phase state), the Skip value uses the `#NNNN` placeholder. A canonical template↔FR-column map (each canonical template filename → its ledger column key: FR-2, FR-4, FR-5, FR-6, FR-7, FR-8, FR-9, FR-15, FR-16, FR-17, FR-22) lives in the generator. Canonical templates emit `{% if Skip != empty %}, Skip = "{{ Skip }}"{% endif %}` on the `[Fact]`/`[Theory]`. This applies ONLY to canonical templates, never legacy ones.
  - **Test file**: `tests/Paramore.Brighter.Test.Generator.Tests/CanonicalTemplates/When_ledger_marks_a_cell_should_emit_skip_only_when_not_proven.cs`
  - **Test should verify**:
    - Given a ledger cell `Pass` (or `Fixed`) for a (config, FR), the generated canonical test for that behaviour carries NO `Skip`.
    - Given a cell `Unknown`, the generated test carries `Skip = "Deferred: #NNNN — …"`.
    - Given a cell `Deferred -> #1234 (sign-off: @m)`, the generated test carries `Skip = "Deferred: #1234 — …"` (real number, not the placeholder).
    - The mechanism resolves the ledger from repo root regardless of the generator's CWD, and is applied to canonical templates only.
  - **Implementation files**:
    - `tools/Paramore.Brighter.Test.Generator/Generators/MessagingGatewayGenerator.cs` - load + parse the ledger; add the canonical-template→FR-column map; supply the per-cell `Skip` value to the canonical template render context.
    - `tools/Paramore.Brighter.Test.Generator/**` - repo-root resolution helper for the ledger path.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && dotnet test tests/Paramore.Brighter.Test.Generator.Tests --filter "FullyQualifiedName~CanonicalTemplates"`
  - **References**: requirements FR-21, AC-24, FR-13, AC-13; ADR 0067 "generate everywhere immediately … each carrying an audited deferral marker for every configuration not yet known to conform" (lines 156-168), "the marker is deleted in the same PR as the gateway fix" (interpreted as ledger-flip + regenerate); the conformance ledger is the single source of truth (FR-21).

- [ ] **Canonical plain requeue (FR-22)**
  - **Behavior**: Generate a canonical test proving a message requeued with no delay (`channel.Requeue(M)` / `RequeueAsync(M)`, equivalently `Requeue(M, null)`) returns `true` and is redelivered within a bounded retry loop. May migrate from the legacy `When_requeuing_a_failed_message_should_receive_message_again` template. Owns the no-delay call in both its spellings (omitted and explicit-null are the same call).
  - **Test file**: `tests/Paramore.Brighter.Test.Generator.Tests/CanonicalTemplates/When_generating_plain_requeue_should_emit_bounded_redelivery_both_variants.cs`
  - **Test should verify**:
    - Both a Reactor variant (driving `IAmAChannelSync`) and a Proactor variant (driving `IAmAChannelAsync`) are emitted; file names follow the `When_..._should_..._again` convention (NFR-1).
    - The emitted body calls `Requeue`/`RequeueAsync` with no positive delay, asserts the return is `true`, and asserts redelivery inside a bounded receive-retry loop (500ms poll up to 30s ceiling) — never a fixed sleep + single receive (AC-20).
    - The `[Fact]`/`[Theory]` emits the conditional `{% if Skip != empty %}, Skip = "{{ Skip }}"{% endif %}` so the ledger-driven mechanism (Phase 1 task 1) drives the Deferred marker; the template itself hard-codes no marker.
  - **Implementation files**:
    - `tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/Reactor/When_requeuing_a_failed_message_should_be_redelivered.cs.liquid` - new canonical template (name chosen so it is NOT on `LegacyGatedTemplates`).
    - `tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/Proactor/When_requeuing_a_failed_message_should_be_redelivered.cs.liquid` - async variant.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && dotnet test tests/Paramore.Brighter.Test.Generator.Tests --filter "FullyQualifiedName~CanonicalTemplates"`
  - **References**: requirements FR-22, FR-15 (scoping), AC-25, NFR-1, NFR-2, AC-20; ADR 0066 "Delete the broken template" (no-delay canonical templates are legitimate).

- [ ] **Canonical requeue-with-delay (FR-2, before-D / after-D arms)**
  - **Behavior**: Generate a canonical test proving that after `channel.Requeue(M, 5s)`: `Requeue` returns `true`; an immediate single bounded receive (before 5s) yields `MT_NONE` (the lower-bound arm); and a receive within the bounded retry loop after the delay yields a message with `M`'s body. No mechanism assertion. This is the arm GCP ×4 fail (immediate redelivery) and that RocketMQ passes-before / fails-after (no-op held by invisibility timeout).
  - **Test file**: `tests/Paramore.Brighter.Test.Generator.Tests/CanonicalTemplates/When_generating_requeue_with_delay_should_emit_before_and_after_arms_both_variants.cs`
  - **Test should verify**:
    - Both variants emitted; NFR-1 naming; passes a non-null positive `TimeSpan` (5s) to `Requeue`/`RequeueAsync`.
    - Before-`D` arm is a single bounded receive asserting `MT_NONE` (AC-20 exemption); after-`D` arm asserts arrival inside the bounded retry loop.
    - No reference to a scheduler / native-delay API / redrive policy (AC-21).
  - **Implementation files**:
    - `tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/Reactor/When_requeuing_a_failed_message_with_delay_should_redeliver_after_delay.cs.liquid` - new canonical template (NOT on `LegacyGatedTemplates`).
    - `tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/Proactor/When_requeuing_a_failed_message_with_delay_should_redeliver_after_delay.cs.liquid` - async variant.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && dotnet test tests/Paramore.Brighter.Test.Generator.Tests --filter "FullyQualifiedName~CanonicalTemplates"`
  - **References**: requirements FR-2, AC-2, NFR-2, AC-20, AC-21; ADR 0066 "Why there is no scheduler member" (GCP vs RocketMQ failure modes).

- [ ] **Canonical explicit-zero requeue (FR-15)**
  - **Behavior**: Generate a canonical test proving `channel.Requeue(M, TimeSpan.Zero)` behaves as an immediate plain requeue: `Requeue` returns `true`, the message is received on the **first** iteration of the plain-requeue bounded retry loop, and elapsed time from the call to receipt is less than 5s. Proves `TimeSpan.Zero` is neither special-cased into error/unbounded wait nor treated as a positive delay. Scoped to the explicit `TimeSpan.Zero` argument only.
  - **Test file**: `tests/Paramore.Brighter.Test.Generator.Tests/CanonicalTemplates/When_generating_zero_delay_requeue_should_emit_first_iteration_receipt_both_variants.cs`
  - **Test should verify**:
    - Both variants emitted; NFR-1 naming; the call uses `TimeSpan.Zero` explicitly.
    - The receipt assertion is a first-iteration arrival inside the retry loop (not an AC-20 negative arm) with an elapsed-time-under-5s assertion.
  - **Implementation files**:
    - `tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/Reactor/When_requeuing_a_failed_message_with_zero_delay_should_redeliver_immediately.cs.liquid` - new canonical template.
    - `tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/Proactor/When_requeuing_a_failed_message_with_zero_delay_should_redeliver_immediately.cs.liquid` - async variant.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && dotnet test tests/Paramore.Brighter.Test.Generator.Tests --filter "FullyQualifiedName~CanonicalTemplates"`
  - **References**: requirements FR-15, AC-16, NFR-2; ADR 0066 "Delete the broken template" (zero-boundary template is legitimate no-delay).

- [ ] **Canonical reject with delivery error → DLQ (FR-4)**
  - **Behavior**: Generate a canonical test proving `channel.Reject(M, new MessageRejectionReason(RejectionReason.DeliveryError, "..."))` on a channel with a dead-letter routing key routes `M` to the DLQ, carrying original-topic (equal to the data topic) and a rejection-reason entry. DLQ arrival is asserted inside the bounded retry loop via `GetMessageFromDeadLetterQueue`.
  - **Test file**: `tests/Paramore.Brighter.Test.Generator.Tests/CanonicalTemplates/When_generating_delivery_error_reject_should_emit_dlq_routing_both_variants.cs`
  - **Test should verify**:
    - Both variants emitted; NFR-1 name matches `When_rejecting_message_with_delivery_error_should_send_to_dlq`.
    - Subscription is created with a `deadLetterRoutingKey`; DLQ read uses the bounded read member; original-topic and rejection-reason are asserted.
  - **Implementation files**:
    - `tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/Reactor/When_rejecting_message_with_delivery_error_should_send_to_dlq.cs.liquid` - new.
    - `tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/Proactor/When_rejecting_message_with_delivery_error_should_send_to_dlq.cs.liquid` - async variant.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && dotnet test tests/Paramore.Brighter.Test.Generator.Tests --filter "FullyQualifiedName~CanonicalTemplates"`
  - **References**: requirements FR-4, AC-4, NFR-1, NFR-2; ADR `0047-message-rejection-routing-strategy` (fallback ladder); Kafka reference `..._delivery_error_should_send_to_dlq`.

- [ ] **Canonical reject unacceptable → invalid channel, not DLQ (FR-5)**
  - **Behavior**: Generate a canonical test proving `channel.Reject(M, Unacceptable)` on a channel configured with BOTH routing keys routes `M` to the invalid channel (reason `"Unacceptable"`, original-topic = data topic) and NOT to the DLQ. The "not on DLQ" arm is a single bounded receive of the DLQ asserting `MT_NONE` (AC-20 exemption); the invalid-channel arrival stays inside the retry loop.
  - **Test file**: `tests/Paramore.Brighter.Test.Generator.Tests/CanonicalTemplates/When_generating_unacceptable_reject_should_emit_invalid_channel_routing_both_variants.cs`
  - **Test should verify**:
    - Both variants emitted; NFR-1 name matches `When_rejecting_message_with_unacceptable_reason_should_send_to_invalid_channel`.
    - Subscription created with both `deadLetterRoutingKey` and `invalidMessageRoutingKey`; invalid-channel arrival asserted in the retry loop; DLQ absence is a single bounded `MT_NONE` receive.
  - **Implementation files**:
    - `tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/Reactor/When_rejecting_message_with_unacceptable_reason_should_send_to_invalid_channel.cs.liquid` - new.
    - `tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/Proactor/When_rejecting_message_with_unacceptable_reason_should_send_to_invalid_channel.cs.liquid` - async variant.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && dotnet test tests/Paramore.Brighter.Test.Generator.Tests --filter "FullyQualifiedName~CanonicalTemplates"`
  - **References**: requirements FR-5, AC-5, AC-20; ADR 0066 "Read-member contract" (MT_NONE for absence); Kafka reference `..._unacceptable_reason_should_send_to_invalid_channel`.

- [ ] **Canonical fallback: unacceptable, DLQ-only → DLQ (FR-6)**
  - **Behavior**: Generate a canonical test proving that on a channel configured with a DLQ only (no invalid channel), `channel.Reject(M, Unacceptable)` routes `M` to the DLQ with rejection-reason still `"Unacceptable"`.
  - **Test file**: `tests/Paramore.Brighter.Test.Generator.Tests/CanonicalTemplates/When_generating_unacceptable_dlq_only_should_emit_dlq_fallback_both_variants.cs`
  - **Test should verify**:
    - Both variants emitted; NFR-1 name matches `When_rejecting_message_with_unacceptable_and_no_invalid_channel_should_fallback_to_dlq`.
    - Subscription created with `deadLetterRoutingKey` only (`invalidMessageRoutingKey` null); DLQ arrival asserted in the retry loop with reason `"Unacceptable"`.
  - **Implementation files**:
    - `tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/Reactor/When_rejecting_message_with_unacceptable_and_no_invalid_channel_should_fallback_to_dlq.cs.liquid` - new.
    - `tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/Proactor/When_rejecting_message_with_unacceptable_and_no_invalid_channel_should_fallback_to_dlq.cs.liquid` - async variant.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && dotnet test tests/Paramore.Brighter.Test.Generator.Tests --filter "FullyQualifiedName~CanonicalTemplates"`
  - **References**: requirements FR-6, AC-6; ADR 0066 "Read-member contract"; ADR `0047-message-rejection-routing-strategy`.

- [ ] **Canonical no channels configured → acknowledge and continue (FR-7)**
  - **Behavior**: Generate a canonical test proving that on a channel with neither DLQ nor invalid channel and two queued messages `M1`, `M2`: receive `M1`; `channel.Reject(M1, DeliveryError)` returns `true` (message removed, not redelivered); the next receive yields `M2` without blocking. The `_and_log` suffix is retained per NFR-1 but logging is not asserted.
  - **Test file**: `tests/Paramore.Brighter.Test.Generator.Tests/CanonicalTemplates/When_generating_no_channels_reject_should_emit_ack_and_continue_both_variants.cs`
  - **Test should verify**:
    - Both variants emitted; NFR-1 name matches `When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log`.
    - Subscription created with neither routing key; `Reject` return asserted `true`; the follow-on `M2` receipt asserted inside the retry loop.
  - **Implementation files**:
    - `tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/Reactor/When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log.cs.liquid` - new.
    - `tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/Proactor/When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log.cs.liquid` - async variant.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && dotnet test tests/Paramore.Brighter.Test.Generator.Tests --filter "FullyQualifiedName~CanonicalTemplates"`
  - **References**: requirements FR-7, AC-7, NFR-1; Kafka reference `..._no_channels_configured_should_acknowledge_and_log`.

- [ ] **Canonical reject None/unspecified → DLQ, not invalid (FR-17)**
  - **Behavior**: Generate a canonical test proving `channel.Reject(M, new MessageRejectionReason(RejectionReason.None, "..."))` on a channel with a dead-letter routing key routes `M` to the DLQ (default arm of the fallback ladder) with reason `"None"` and original-topic = data topic, and NOT to the invalid channel (single bounded `MT_NONE` receive of the invalid channel).
  - **Test file**: `tests/Paramore.Brighter.Test.Generator.Tests/CanonicalTemplates/When_generating_none_reason_reject_should_emit_dlq_default_both_variants.cs`
  - **Test should verify**:
    - Both variants emitted; NFR-1 name matches `When_rejecting_message_with_unknown_reason_should_send_to_dlq` (matching Kafka reference).
    - Subscription created with both routing keys; DLQ arrival in retry loop; invalid-channel absence a single bounded `MT_NONE` receive (AC-20).
  - **Implementation files**:
    - `tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/Reactor/When_rejecting_message_with_unknown_reason_should_send_to_dlq.cs.liquid` - new.
    - `tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/Proactor/When_rejecting_message_with_unknown_reason_should_send_to_dlq.cs.liquid` - async variant.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && dotnet test tests/Paramore.Brighter.Test.Generator.Tests --filter "FullyQualifiedName~CanonicalTemplates"`
  - **References**: requirements FR-17, AC-18, AC-20; ADR `0047-message-rejection-routing-strategy`; Kafka reference `..._unknown_reason_should_send_to_dlq`.

- [ ] **Canonical rejection-metadata stamping (FR-8)**
  - **Behavior**: Generate a canonical test proving a message rejected to the DLQ carries the universal rejection-metadata semantic set in `Header.Bag`, read via `provider.RejectionMetadataKeys.*` (never hard-coded key strings): original topic (= data topic), original message type (`"MT_COMMAND"`), rejection reason (`"DeliveryError"`), rejection message (= description passed to `Reject`), and a parseable ISO-8601 timestamp within the last minute. A field whose provider key is `string.Empty` fails as a genuine non-conformance.
  - **Test file**: `tests/Paramore.Brighter.Test.Generator.Tests/CanonicalTemplates/When_generating_metadata_test_should_read_via_provider_keys_both_variants.cs`
  - **Test should verify**:
    - Both variants emitted (FR-14 closes the Kafka Reactor-only gap); NFR-1 name matches `When_rejecting_message_should_include_metadata`.
    - The emitted body reads the bag via `provider.RejectionMetadataKeys.OriginalTopic` etc., not literal strings; DLQ arrival asserted in the retry loop.
  - **Implementation files**:
    - `tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/Reactor/When_rejecting_message_should_include_metadata.cs.liquid` - new.
    - `tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/Proactor/When_rejecting_message_should_include_metadata.cs.liquid` - async variant.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && dotnet test tests/Paramore.Brighter.Test.Generator.Tests --filter "FullyQualifiedName~CanonicalTemplates"`
  - **References**: requirements FR-8, AC-8, FR-1(5); ADR 0066 "RejectionMetadataKeys", "What a provider returns for a field its gateway does not stamp"; Kafka reference `..._rejecting_message_should_include_metadata` (Reactor-only today).

- [ ] **Canonical delayed send (FR-9)**
  - **Behavior**: Generate a canonical test proving `producer.SendWithDelay(M, 5s)` is not receivable before the delay (single bounded `MT_NONE` receive) and is receivable after it (bounded retry loop). May migrate from the legacy `When_reading_a_delayed_message_via_the_messaging_gateway_should_delay_delivery` template; the canonical template — not the legacy one — satisfies FR-9.
  - **Test file**: `tests/Paramore.Brighter.Test.Generator.Tests/CanonicalTemplates/When_generating_delayed_send_should_emit_before_and_after_arms_both_variants.cs`
  - **Test should verify**:
    - Both variants emitted; NFR-1 naming; drives `SendWithDelay` on the producer surface.
    - Immediate receive is a single bounded `MT_NONE` check (AC-20); after-delay receive is in the retry loop.
  - **Implementation files**:
    - `tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/Reactor/When_sending_a_delayed_message_should_deliver_after_delay.cs.liquid` - new canonical (NOT on `LegacyGatedTemplates`).
    - `tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/Proactor/When_sending_a_delayed_message_should_deliver_after_delay.cs.liquid` - async variant.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && dotnet test tests/Paramore.Brighter.Test.Generator.Tests --filter "FullyQualifiedName~CanonicalTemplates"`
  - **References**: requirements FR-9, AC-9, AC-20; ADR 0067 stage sequencing (delayed send is canonical).

- [ ] **Canonical Nack redelivers, plus two-message variant (FR-16)**
  - **Behavior**: Generate a canonical test proving `channel.Nack(M)` / `NackAsync(M)` releases a received message for redelivery on a subsequent receive (distinct from `Acknowledge` which removes, and `Reject` which routes). A second variant with two queued messages proves the nacked `M` is redelivered and then `M2` is received (not blocked behind it). Redelivery asserted inside the bounded retry loop.
  - **Test file**: `tests/Paramore.Brighter.Test.Generator.Tests/CanonicalTemplates/When_generating_nack_test_should_emit_redelivery_and_two_message_variant_both_variants.cs`
  - **Test should verify**:
    - Both Reactor and Proactor variants emitted; NFR-1 name matches `When_nacking_a_message_it_should_be_redelivered`; the two-message case is present.
    - Redelivery assertions sit inside bounded retry loops (AC-20); no mechanism assertion (AC-21).
  - **Implementation files**:
    - `tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/Reactor/When_nacking_a_message_it_should_be_redelivered.cs.liquid` - new (covers single + two-message).
    - `tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/Proactor/When_nacking_a_message_it_should_be_redelivered.cs.liquid` - async variant.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && dotnet test tests/Paramore.Brighter.Test.Generator.Tests --filter "FullyQualifiedName~CanonicalTemplates"`
  - **References**: requirements FR-16, AC-17, NFR-2, AC-20; Kafka reference `When_nacking_a_message_it_should_be_redelivered` (+ two-message).

- [ ] **Reconcile the canonical set against the Kafka hand-written suite**
  - **Behavior**: Verify every behavioural row of the Kafka reference surface maps to a canonical template (FR-2, FR-4, FR-5, FR-6, FR-7, FR-8, FR-17, FR-16, FR-22/FR-9), and that the mechanism-only Kafka test (`..._requeues_with_delay_should_use_scheduler`) and transport-internal tests are deliberately NOT reproduced (OOS-2/OOS-3). This is a coverage checkpoint before proving the reference transport.
  - **Test file**: `tests/Paramore.Brighter.Test.Generator.Tests/CanonicalTemplates/When_reconciling_canonical_set_should_cover_kafka_reference_surface.cs`
  - **Test should verify**:
    - A canonical template exists for each Kafka reference behaviour in the Coverage Reconciliation table.
    - No canonical template asserts a scheduler / native mechanism (AC-21).
  - **Implementation files**:
    - `tests/Paramore.Brighter.Test.Generator.Tests/CanonicalTemplates/...` - reconciliation assertions over the template directory.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && dotnet test tests/Paramore.Brighter.Test.Generator.Tests --filter "FullyQualifiedName~CanonicalTemplates"`
  - **References**: requirements "Coverage Reconciliation (Kafka reference surface)", OOS-2, OOS-3; ADR 0066 "Why there is no scheduler member".

- [ ] **Generate the canonical suite into every wired project ("generate everywhere immediately")**
  - **Behavior**: With the canonical templates and the ledger-driven Skip mechanism in place, regenerate ALL twenty wired configurations so the canonical suite lands everywhere at once (ADR 0067 "generate everywhere immediately"). At this point every ledger cell is still `Unknown` (nothing proven yet except what Phase 2 will prove), so every generated canonical test lands **carrying its Deferred Skip** and the whole solution builds green with the canonical tests skipped. This is the baseline the fix phases then shed markers from — and it closes the gap where Phase 3/4 would otherwise `dotnet test` a project that has no canonical tests (review finding #1). Safe to `./generate-test.sh` now: all providers migrated (Phase 0 complete) and the legacy templates still gate via the narrowed `SkipTest`.
  - **Test file**: `tests/Paramore.Brighter.Test.Generator.Tests/CanonicalTemplates/When_generating_everywhere_should_emit_skipped_canonical_suite_in_all_wired_projects.cs` (structural assertion) — solution build is the integration gate
  - **Test should verify**:
    - After a full regenerate, every wired project's `MessagingGateway/**/Generated/` tree contains the canonical suite (both variants), each canonical test carrying a `Deferred: #NNNN` Skip (all cells `Unknown`).
    - `dotnet build Brighter.slnx` succeeds and no canonical test runs unskipped yet.
  - **Implementation files**:
    - `tests/Paramore.Brighter.Test.Generator.Tests/CanonicalTemplates/When_generating_everywhere_should_emit_skipped_canonical_suite_in_all_wired_projects.cs` - new structural test: after a full regenerate, assert every wired project's `MessagingGateway/**/Generated/` tree contains the canonical suite (both variants) and every canonical test carries a `Deferred: #NNNN` Skip (all cells `Unknown`). A build alone cannot see Skip markers, so this test is the real gate.
    - `tests/Paramore.Brighter.*.Tests/**/Generated/**` - regenerate all wired projects (`./generate-test.sh`).
  - **RALPH-VERIFY** (the `dotnet test` is essential — the solution build proves the tree compiles green, but only the structural test proves the canonical suite was emitted everywhere and each test carries a Skip, since a `Skip` attribute argument does not affect compilation): `dotnet build tools/Paramore.Brighter.Test.Generator && ./generate-test.sh && dotnet build Brighter.slnx && dotnet test tests/Paramore.Brighter.Test.Generator.Tests --filter "FullyQualifiedName~When_generating_everywhere"`
  - **References**: requirements FR-13, AC-13, FR-14 (both variants), FR-21; ADR 0067 "generate everywhere immediately … already carrying a Skip … for each configuration not yet proven" (lines 156-168), Architecture Overview stage (i).

### Phase 2 — Prove the reference (Kafka) (ADR 0067 step 3)

- [ ] **Prove the canonical suite against Kafka Standard and PartitionKey**
  - **Behavior**: Bring up the Kafka broker and run the generated canonical suite for both Kafka configurations in both variants against the running broker. When both variants of a behaviour pass for a configuration, flip that cell to `Pass` in the ledger and regenerate — the ledger-driven mechanism (Phase 1 task 1) drops that cell's `Deferred: #NNNN` marker by construction. If the broker/CI is blocked, apply flag-and-move-on: set the affected cells to `Deferred -> #NNNN (sign-off: @maintainer)` and regenerate so the marker carries the issue number, then continue.
  - **Test file**: `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/Standard/Generated/Reactor/*.cs` (generated canonical suite being exercised)
  - **Test should verify**:
    - Every canonical behaviour passes for `Kafka / Standard` and `Kafka / PartitionKey` in both variants against the broker.
    - Ledger rows `Kafka / Standard` and `Kafka / PartitionKey` read `Pass` across all 11 columns; their Skip markers are removed.
  - **Implementation files**:
    - `specs/0036-universal-transport-conformance-tests/conformance-status.md` - set both Kafka rows to `Pass` (or `Deferred` per fallback).
    - `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/**/Generated/**` - regenerate after flipping the Kafka cells to `Pass`, so the markers drop by construction.
  - **RALPH-VERIFY** (also asserts the two Kafka ledger rows carry no `Unknown` — the suite run and the ledger update must agree): `dotnet build tools/Paramore.Brighter.Test.Generator && (cd tests/Paramore.Brighter.Kafka.Tests && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator) && { docker compose -f docker-compose-kafka.yaml up -d || true; } && dotnet test tests/Paramore.Brighter.Kafka.Tests --filter "FullyQualifiedName~MessagingGateway" && grep -q -- 'Kafka /' specs/0036-universal-transport-conformance-tests/conformance-status.md && ! ( grep -- 'Kafka /' specs/0036-universal-transport-conformance-tests/conformance-status.md | grep -q Unknown )`
  - **References**: ADR 0067 "Implementation Approach" step 3 (Kafka mis-declared gates irrelevant), "Infra reality" (`Pass` = actually ran); requirements FR-21, AC-24, FR-14 (both variants).

### Phase 3 — DLQ-ADR transports (ADR 0067 step 4)

> **One task per ledger row (per gateway configuration), not per project.** FR-13/FR-21 make the gateway configuration the unit of conformance, so a multi-configuration transport is split into one task per configuration — each is independently verifiable (its own ledger row) and sized for one agent session. The canonical suite was already landed in every wired project (all cells `Unknown`, all canonical tests skipped) by the end-of-Phase-1 "generate everywhere" task; these tasks flip THIS configuration's ledger cells and **regenerate the project** so the ledger-driven mechanism drops that configuration's Skip markers.
>
> **Determining a cell's value.** A cell is only un-skipped once it reads `Pass`/`Fixed` and the project is regenerated — so to exercise a not-yet-proven behaviour the agent flips its cell to a candidate `Pass`, regenerates to un-skip it, and runs it against the broker; a behaviour that then fails is either fixed inline (→ `Fixed`) or flipped to `Deferred` (and regenerated so it re-skips). The final regenerate + `dotnet test` in the RALPH-VERIFY therefore runs exactly the `Pass`/`Fixed` behaviours and skips only the `Deferred` ones — a cell claiming `Pass` whose test actually fails against the broker fails the task.
>
> **Method for every task below:** bring up the broker; run the project's generated canonical suite (a fresh un-skip only happens after the cell flips to `Pass`/`Fixed` and the project is regenerated, so unproven configurations stay skipped and the suite passes); for each non-conformant behaviour of THIS configuration apply the fix-to-conform boundary — a localized low-risk gateway fix → set the cell `Fixed (#PR)`; otherwise flag-and-move-on to a signed-off `Deferred` row; then **regenerate this project** so the markers for now-`Pass`/`Fixed` cells drop and the marker for any `Deferred` cell carries its issue number. Resolve every cell of this configuration's row (no `Unknown` left). Flag-and-move-on (Deferred cell + Skip marker + continue) is the completion fallback whenever a broker/emulator/CI/upstream dependency blocks the task.
>
> **Every RALPH-VERIFY below** therefore (1) regenerates the project (`dotnet build tools/… && (cd tests/<Project> && dotnet run --no-build --project ../../tools/…)`) so the marker state matches the just-updated ledger, (2) brings up the broker **best-effort** and runs the suite, and (3) asserts the configuration's row **exists and** carries no `Unknown` (`grep -q -- '<row>' … && ! (grep -- '<row>' … | grep -q Unknown)`), so the suite run and the ledger update cannot silently drift apart (review findings #1, #3, #4). For multi-configuration transports, do `SqsStandard` first as the reference configuration, then the rest.
>
> **Broker-up is decoupled from the gate (`{ docker compose … up -d || true; }`).** Because CI-infrastructure inability is itself a first-class deferral ground (flag-and-move-on), a broker that genuinely cannot be stood up must still let the task reach its deferral path, not fail the `&&` chain at `docker compose`. The generator build/regenerate BEFORE the brace group still hard-gate (they must succeed); the brace group always returns success so the run continues to `dotnet test` + the no-`Unknown` grep, which remain the real gate — deferred/skipped tests keep the run green.
>
> **Each multi-configuration task's `dotnet test` is scoped to that configuration's generated namespace** (`--filter "FullyQualifiedName~MessagingGateway.<Config>."`, e.g. `~MessagingGateway.SqsStandard.` — the trailing dot keeps `Pull` from also matching `PullOrdering` etc.). This isolates the row's task from sibling configurations and pre-existing hand-written gateway tests (whose namespaces differ, e.g. `MessagingGateway.Sqs.Standard`), so a sibling flake cannot fail this row. Single-configuration transports (Redis, MSSQL, PostgresSQL, RocketMQ) have no sibling generated configs, so they run the project's gateway suite (`~MessagingGateway`).

- [ ] **Bring AWS (V3) / SqsStandard to conformance**
  - **Behavior**: Per the Phase 3 method, resolve the `AWS / SqsStandard` row against LocalStack/AWS, both variants. Localized reject/DLQ/metadata fixes in `src/Paramore.Brighter.MessagingGateway.AWSSQS` (e.g. lazy DLQ/invalid producer, stamp the metadata semantic set under SQS keys) → `Fixed (#PR)`; otherwise flag-and-move-on to a signed-off `Deferred`. This is the AWS reference configuration — do it first.
  - **Test file**: `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/SqsStandard/Generated/Reactor/*.cs`
  - **Test should verify**:
    - Every behaviour for `AWS / SqsStandard` resolves to `Pass`/`Fixed` (both variants) or carries a `Deferred: #NNNN` Skip + matching ledger cell; no silent skip.
  - **Implementation files**:
    - `src/Paramore.Brighter.MessagingGateway.AWSSQS/...` - localized fixes where in-boundary.
    - `specs/0036-universal-transport-conformance-tests/conformance-status.md` - resolve the `AWS / SqsStandard` row.
    - `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/SqsStandard/Generated/**` - drop markers for fixed behaviours.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && (cd tests/Paramore.Brighter.AWS.Tests && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator) && { docker compose -f docker-compose-localstack.yaml up -d || true; } && dotnet test tests/Paramore.Brighter.AWS.Tests --filter "FullyQualifiedName~MessagingGateway.SqsStandard." && grep -q -- 'AWS / SqsStandard' specs/0036-universal-transport-conformance-tests/conformance-status.md && ! ( grep -- 'AWS / SqsStandard' specs/0036-universal-transport-conformance-tests/conformance-status.md | grep -q Unknown )`
  - **References**: ADR 0067 step 4, "size/risk fix-to-conform boundary"; ADR `0038-aws-sqs-dlq-direct-send`; requirements FR-13, AC-13, FR-21.

- [ ] **Bring AWS (V3) / SqsFifo to conformance**
  - **Behavior**: Per the Phase 3 method, resolve the `AWS / SqsFifo` row against LocalStack/AWS, both variants. Localized fixes in `src/Paramore.Brighter.MessagingGateway.AWSSQS` → `Fixed (#PR)`; otherwise a signed-off `Deferred`.
  - **Test file**: `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/SqsFifo/Generated/Reactor/*.cs`
  - **Test should verify**:
    - Every behaviour for `AWS / SqsFifo` resolves to `Pass`/`Fixed` (both variants) or carries a `Deferred: #NNNN` Skip + matching ledger cell; no silent skip.
  - **Implementation files**:
    - `src/Paramore.Brighter.MessagingGateway.AWSSQS/...` - localized fixes where in-boundary.
    - `specs/0036-universal-transport-conformance-tests/conformance-status.md` - resolve the `AWS / SqsFifo` row.
    - `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/SqsFifo/Generated/**` - drop markers for fixed behaviours.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && (cd tests/Paramore.Brighter.AWS.Tests && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator) && { docker compose -f docker-compose-localstack.yaml up -d || true; } && dotnet test tests/Paramore.Brighter.AWS.Tests --filter "FullyQualifiedName~MessagingGateway.SqsFifo." && grep -q -- 'AWS / SqsFifo' specs/0036-universal-transport-conformance-tests/conformance-status.md && ! ( grep -- 'AWS / SqsFifo' specs/0036-universal-transport-conformance-tests/conformance-status.md | grep -q Unknown )`
  - **References**: ADR 0067 step 4; ADR `0038-aws-sqs-dlq-direct-send`; requirements FR-13, AC-13, FR-21.

- [ ] **Bring AWS (V3) / SnsStandard to conformance**
  - **Behavior**: Per the Phase 3 method, resolve the `AWS / SnsStandard` row against LocalStack/AWS, both variants. Localized fixes in `src/Paramore.Brighter.MessagingGateway.AWSSQS` → `Fixed (#PR)`; otherwise a signed-off `Deferred`.
  - **Test file**: `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/SnsStandard/Generated/Reactor/*.cs`
  - **Test should verify**:
    - Every behaviour for `AWS / SnsStandard` resolves to `Pass`/`Fixed` (both variants) or carries a `Deferred: #NNNN` Skip + matching ledger cell; no silent skip.
  - **Implementation files**:
    - `src/Paramore.Brighter.MessagingGateway.AWSSQS/...` - localized fixes where in-boundary.
    - `specs/0036-universal-transport-conformance-tests/conformance-status.md` - resolve the `AWS / SnsStandard` row.
    - `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/SnsStandard/Generated/**` - drop markers for fixed behaviours.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && (cd tests/Paramore.Brighter.AWS.Tests && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator) && { docker compose -f docker-compose-localstack.yaml up -d || true; } && dotnet test tests/Paramore.Brighter.AWS.Tests --filter "FullyQualifiedName~MessagingGateway.SnsStandard." && grep -q -- 'AWS / SnsStandard' specs/0036-universal-transport-conformance-tests/conformance-status.md && ! ( grep -- 'AWS / SnsStandard' specs/0036-universal-transport-conformance-tests/conformance-status.md | grep -q Unknown )`
  - **References**: ADR 0067 step 4; ADR `0038-aws-sqs-dlq-direct-send`; requirements FR-13, AC-13, FR-21.

- [ ] **Bring AWS (V3) / SnsFifo to conformance**
  - **Behavior**: Per the Phase 3 method, resolve the `AWS / SnsFifo` row against LocalStack/AWS, both variants. Localized fixes in `src/Paramore.Brighter.MessagingGateway.AWSSQS` → `Fixed (#PR)`; otherwise a signed-off `Deferred`.
  - **Test file**: `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/SnsFifo/Generated/Reactor/*.cs`
  - **Test should verify**:
    - Every behaviour for `AWS / SnsFifo` resolves to `Pass`/`Fixed` (both variants) or carries a `Deferred: #NNNN` Skip + matching ledger cell; no silent skip.
  - **Implementation files**:
    - `src/Paramore.Brighter.MessagingGateway.AWSSQS/...` - localized fixes where in-boundary.
    - `specs/0036-universal-transport-conformance-tests/conformance-status.md` - resolve the `AWS / SnsFifo` row.
    - `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/SnsFifo/Generated/**` - drop markers for fixed behaviours.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && (cd tests/Paramore.Brighter.AWS.Tests && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator) && { docker compose -f docker-compose-localstack.yaml up -d || true; } && dotnet test tests/Paramore.Brighter.AWS.Tests --filter "FullyQualifiedName~MessagingGateway.SnsFifo." && grep -q -- 'AWS / SnsFifo' specs/0036-universal-transport-conformance-tests/conformance-status.md && ! ( grep -- 'AWS / SnsFifo' specs/0036-universal-transport-conformance-tests/conformance-status.md | grep -q Unknown )`
  - **References**: ADR 0067 step 4; ADR `0038-aws-sqs-dlq-direct-send`; requirements FR-13, AC-13, FR-21.

- [ ] **Bring AWS.V4 / SqsStandard to conformance**
  - **Behavior**: Per the Phase 3 method, resolve the `AWS.V4 / SqsStandard` row against LocalStack/AWS, both variants. Localized fixes in `src/Paramore.Brighter.MessagingGateway.AWSSQS.V4` → `Fixed (#PR)`; otherwise a signed-off `Deferred`. AWS.V4 reference configuration — do it first.
  - **Test file**: `tests/Paramore.Brighter.AWS.V4.Tests/MessagingGateway/SqsStandard/Generated/Reactor/*.cs`
  - **Test should verify**:
    - Every behaviour for `AWS.V4 / SqsStandard` resolves to `Pass`/`Fixed` (both variants) or carries a `Deferred: #NNNN` Skip + matching ledger cell; no silent skip.
  - **Implementation files**:
    - `src/Paramore.Brighter.MessagingGateway.AWSSQS.V4/...` - localized fixes where in-boundary.
    - `specs/0036-universal-transport-conformance-tests/conformance-status.md` - resolve the `AWS.V4 / SqsStandard` row.
    - `tests/Paramore.Brighter.AWS.V4.Tests/MessagingGateway/SqsStandard/Generated/**` - drop markers for fixed behaviours.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && (cd tests/Paramore.Brighter.AWS.V4.Tests && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator) && { docker compose -f docker-compose-localstack.yaml up -d || true; } && dotnet test tests/Paramore.Brighter.AWS.V4.Tests --filter "FullyQualifiedName~MessagingGateway.SqsStandard." && grep -q -- 'AWS.V4 / SqsStandard' specs/0036-universal-transport-conformance-tests/conformance-status.md && ! ( grep -- 'AWS.V4 / SqsStandard' specs/0036-universal-transport-conformance-tests/conformance-status.md | grep -q Unknown )`
  - **References**: ADR 0067 step 4; ADR `0038-aws-sqs-dlq-direct-send`; requirements FR-13, FR-21.

- [ ] **Bring AWS.V4 / SqsFifo to conformance**
  - **Behavior**: Per the Phase 3 method, resolve the `AWS.V4 / SqsFifo` row against LocalStack/AWS, both variants. Localized fixes in `src/Paramore.Brighter.MessagingGateway.AWSSQS.V4` → `Fixed (#PR)`; otherwise a signed-off `Deferred`.
  - **Test file**: `tests/Paramore.Brighter.AWS.V4.Tests/MessagingGateway/SqsFifo/Generated/Reactor/*.cs`
  - **Test should verify**:
    - Every behaviour for `AWS.V4 / SqsFifo` resolves to `Pass`/`Fixed` (both variants) or carries a `Deferred: #NNNN` Skip + matching ledger cell; no silent skip.
  - **Implementation files**:
    - `src/Paramore.Brighter.MessagingGateway.AWSSQS.V4/...` - localized fixes where in-boundary.
    - `specs/0036-universal-transport-conformance-tests/conformance-status.md` - resolve the `AWS.V4 / SqsFifo` row.
    - `tests/Paramore.Brighter.AWS.V4.Tests/MessagingGateway/SqsFifo/Generated/**` - drop markers for fixed behaviours.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && (cd tests/Paramore.Brighter.AWS.V4.Tests && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator) && { docker compose -f docker-compose-localstack.yaml up -d || true; } && dotnet test tests/Paramore.Brighter.AWS.V4.Tests --filter "FullyQualifiedName~MessagingGateway.SqsFifo." && grep -q -- 'AWS.V4 / SqsFifo' specs/0036-universal-transport-conformance-tests/conformance-status.md && ! ( grep -- 'AWS.V4 / SqsFifo' specs/0036-universal-transport-conformance-tests/conformance-status.md | grep -q Unknown )`
  - **References**: ADR 0067 step 4; ADR `0038-aws-sqs-dlq-direct-send`; requirements FR-13, FR-21.

- [ ] **Bring AWS.V4 / SnsStandard to conformance**
  - **Behavior**: Per the Phase 3 method, resolve the `AWS.V4 / SnsStandard` row against LocalStack/AWS, both variants. Localized fixes in `src/Paramore.Brighter.MessagingGateway.AWSSQS.V4` → `Fixed (#PR)`; otherwise a signed-off `Deferred`.
  - **Test file**: `tests/Paramore.Brighter.AWS.V4.Tests/MessagingGateway/SnsStandard/Generated/Reactor/*.cs`
  - **Test should verify**:
    - Every behaviour for `AWS.V4 / SnsStandard` resolves to `Pass`/`Fixed` (both variants) or carries a `Deferred: #NNNN` Skip + matching ledger cell; no silent skip.
  - **Implementation files**:
    - `src/Paramore.Brighter.MessagingGateway.AWSSQS.V4/...` - localized fixes where in-boundary.
    - `specs/0036-universal-transport-conformance-tests/conformance-status.md` - resolve the `AWS.V4 / SnsStandard` row.
    - `tests/Paramore.Brighter.AWS.V4.Tests/MessagingGateway/SnsStandard/Generated/**` - drop markers for fixed behaviours.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && (cd tests/Paramore.Brighter.AWS.V4.Tests && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator) && { docker compose -f docker-compose-localstack.yaml up -d || true; } && dotnet test tests/Paramore.Brighter.AWS.V4.Tests --filter "FullyQualifiedName~MessagingGateway.SnsStandard." && grep -q -- 'AWS.V4 / SnsStandard' specs/0036-universal-transport-conformance-tests/conformance-status.md && ! ( grep -- 'AWS.V4 / SnsStandard' specs/0036-universal-transport-conformance-tests/conformance-status.md | grep -q Unknown )`
  - **References**: ADR 0067 step 4; ADR `0038-aws-sqs-dlq-direct-send`; requirements FR-13, FR-21.

- [ ] **Bring AWS.V4 / SnsFifo to conformance**
  - **Behavior**: Per the Phase 3 method, resolve the `AWS.V4 / SnsFifo` row against LocalStack/AWS, both variants. Localized fixes in `src/Paramore.Brighter.MessagingGateway.AWSSQS.V4` → `Fixed (#PR)`; otherwise a signed-off `Deferred`.
  - **Test file**: `tests/Paramore.Brighter.AWS.V4.Tests/MessagingGateway/SnsFifo/Generated/Reactor/*.cs`
  - **Test should verify**:
    - Every behaviour for `AWS.V4 / SnsFifo` resolves to `Pass`/`Fixed` (both variants) or carries a `Deferred: #NNNN` Skip + matching ledger cell; no silent skip.
  - **Implementation files**:
    - `src/Paramore.Brighter.MessagingGateway.AWSSQS.V4/...` - localized fixes where in-boundary.
    - `specs/0036-universal-transport-conformance-tests/conformance-status.md` - resolve the `AWS.V4 / SnsFifo` row.
    - `tests/Paramore.Brighter.AWS.V4.Tests/MessagingGateway/SnsFifo/Generated/**` - drop markers for fixed behaviours.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && (cd tests/Paramore.Brighter.AWS.V4.Tests && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator) && { docker compose -f docker-compose-localstack.yaml up -d || true; } && dotnet test tests/Paramore.Brighter.AWS.V4.Tests --filter "FullyQualifiedName~MessagingGateway.SnsFifo." && grep -q -- 'AWS.V4 / SnsFifo' specs/0036-universal-transport-conformance-tests/conformance-status.md && ! ( grep -- 'AWS.V4 / SnsFifo' specs/0036-universal-transport-conformance-tests/conformance-status.md | grep -q Unknown )`
  - **References**: ADR 0067 step 4; ADR `0038-aws-sqs-dlq-direct-send`; requirements FR-13, FR-21.

- [ ] **Bring Redis to conformance**
  - **Behavior**: Run the generated canonical suite for `Redis / RedisMessagingGateway` against the Redis broker, both variants. Fix-to-conform inline where localized (Brighter-managed DLQ/invalid channel, metadata under Redis camelCase keys); otherwise flag-and-move-on to signed-off `Deferred`. Update the Redis ledger row.
  - **Test file**: `tests/Paramore.Brighter.Redis.Tests/MessagingGateway/Generated/Reactor/*.cs`
  - **Test should verify**:
    - Each behaviour resolves to `Pass`/`Fixed` (both variants) or carries a `Deferred: #NNNN` Skip + matching ledger cell.
  - **Implementation files**:
    - `src/Paramore.Brighter.MessagingGateway.Redis/...` - localized fixes where in-boundary.
    - `specs/0036-universal-transport-conformance-tests/conformance-status.md` - update the Redis row.
    - `tests/Paramore.Brighter.Redis.Tests/MessagingGateway/Generated/**` - drop markers for fixed behaviours.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && (cd tests/Paramore.Brighter.Redis.Tests && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator) && { docker compose -f docker-compose-redis.yaml up -d || true; } && dotnet test tests/Paramore.Brighter.Redis.Tests --filter "FullyQualifiedName~MessagingGateway" && grep -q -- 'Redis / RedisMessagingGateway' specs/0036-universal-transport-conformance-tests/conformance-status.md && ! ( grep -- 'Redis / RedisMessagingGateway' specs/0036-universal-transport-conformance-tests/conformance-status.md | grep -q Unknown )`
  - **References**: ADR 0067 step 4; ADR `0039-redis-dlq-brighter-managed`; requirements FR-13, FR-21.

- [ ] **Bring MSSQL to conformance**
  - **Behavior**: Run the generated canonical suite for `MSSQL / MSSQLMessagingGateway` against the MSSQL broker, both variants. Fix-to-conform inline where localized (Brighter-managed DLQ per ADR 0040); otherwise flag-and-move-on to signed-off `Deferred`. Update the MSSQL ledger row.
  - **Test file**: `tests/Paramore.Brighter.MSSQL.Tests/MessagingGateway/Generated/Reactor/*.cs`
  - **Test should verify**:
    - Each behaviour resolves to `Pass`/`Fixed` (both variants) or carries a `Deferred: #NNNN` Skip + matching ledger cell.
  - **Implementation files**:
    - `src/Paramore.Brighter.MessagingGateway.MsSql/...` - localized fixes where in-boundary.
    - `specs/0036-universal-transport-conformance-tests/conformance-status.md` - update the MSSQL row.
    - `tests/Paramore.Brighter.MSSQL.Tests/MessagingGateway/Generated/**` - drop markers for fixed behaviours.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && (cd tests/Paramore.Brighter.MSSQL.Tests && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator) && { docker compose -f docker-compose-mssql.yaml up -d || true; } && dotnet test tests/Paramore.Brighter.MSSQL.Tests --filter "FullyQualifiedName~MessagingGateway" && grep -q -- 'MSSQL / MSSQLMessagingGateway' specs/0036-universal-transport-conformance-tests/conformance-status.md && ! ( grep -- 'MSSQL / MSSQLMessagingGateway' specs/0036-universal-transport-conformance-tests/conformance-status.md | grep -q Unknown )`
  - **References**: ADR 0067 step 4; ADR `0040-mssql-dlq-brighter-managed`; requirements FR-13, FR-21.

- [ ] **Bring PostgresSQL to conformance**
  - **Behavior**: Run the generated canonical suite for `PostgresSQL / PostgresMessagingGateway` against the Postgres broker, both variants. Fix-to-conform inline where localized (Brighter-managed DLQ per ADR 0041; native delay column for FR-2); otherwise flag-and-move-on to signed-off `Deferred`. Update the PostgresSQL ledger row.
  - **Test file**: `tests/Paramore.Brighter.PostgresSQL.Tests/MessagingGateway/Generated/Reactor/*.cs`
  - **Test should verify**:
    - Each behaviour resolves to `Pass`/`Fixed` (both variants) or carries a `Deferred: #NNNN` Skip + matching ledger cell.
  - **Implementation files**:
    - `src/Paramore.Brighter.MessagingGateway.Postgres/...` - localized fixes where in-boundary.
    - `specs/0036-universal-transport-conformance-tests/conformance-status.md` - update the PostgresSQL row.
    - `tests/Paramore.Brighter.PostgresSQL.Tests/MessagingGateway/Generated/**` - drop markers for fixed behaviours.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && (cd tests/Paramore.Brighter.PostgresSQL.Tests && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator) && { docker compose -f docker-compose-postgres.yaml up -d || true; } && dotnet test tests/Paramore.Brighter.PostgresSQL.Tests --filter "FullyQualifiedName~MessagingGateway" && grep -q -- 'PostgresSQL / PostgresMessagingGateway' specs/0036-universal-transport-conformance-tests/conformance-status.md && ! ( grep -- 'PostgresSQL / PostgresMessagingGateway' specs/0036-universal-transport-conformance-tests/conformance-status.md | grep -q Unknown )`
  - **References**: ADR 0067 step 4; ADR `0041-postgres-dlq-brighter-managed`; requirements FR-13, FR-21.

- [ ] **Bring RMQ.Async / Classic to conformance**
  - **Behavior**: Per the Phase 3 method, resolve the `RMQ.Async / Classic` row against the RabbitMQ broker, both variants. RMQ has no per-transport DLQ ADR (reject/DLQ rests on native DLX + universal routing 0047/0045), so its fix may be larger — apply the size/risk boundary: localized fix in `src/Paramore.Brighter.MessagingGateway.RMQ.Async` → `Fixed (#PR)`; otherwise a signed-off `Deferred`. RMQ.Async reference configuration — do it first.
  - **Test file**: `tests/Paramore.Brighter.RMQ.Async.Tests/MessagingGateway/Classic/Generated/Reactor/*.cs`
  - **Test should verify**:
    - Every behaviour for `RMQ.Async / Classic` resolves to `Pass`/`Fixed` (both variants) or carries a `Deferred: #NNNN` Skip + matching ledger cell; no silent skip.
  - **Implementation files**:
    - `src/Paramore.Brighter.MessagingGateway.RMQ.Async/...` - localized fixes where in-boundary.
    - `specs/0036-universal-transport-conformance-tests/conformance-status.md` - resolve the `RMQ.Async / Classic` row.
    - `tests/Paramore.Brighter.RMQ.Async.Tests/MessagingGateway/Classic/Generated/**` - drop markers for fixed behaviours.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && (cd tests/Paramore.Brighter.RMQ.Async.Tests && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator) && { docker compose -f docker-compose-rmq.yaml up -d || true; } && dotnet test tests/Paramore.Brighter.RMQ.Async.Tests --filter "FullyQualifiedName~MessagingGateway.Classic." && grep -q -- 'RMQ.Async / Classic' specs/0036-universal-transport-conformance-tests/conformance-status.md && ! ( grep -- 'RMQ.Async / Classic' specs/0036-universal-transport-conformance-tests/conformance-status.md | grep -q Unknown )`
  - **References**: ADR 0067 step 4 (RMQ.Async native DLX, larger fix), Architecture Overview stage (ii); ADR `0047-message-rejection-routing-strategy`, `0045-provide-dlq-where-missing`; requirements FR-13, FR-21.

- [ ] **Bring RMQ.Async / Quorum to conformance**
  - **Behavior**: Per the Phase 3 method, resolve the `RMQ.Async / Quorum` row against the RabbitMQ broker, both variants. Same size/risk boundary as Classic: localized fix in `src/Paramore.Brighter.MessagingGateway.RMQ.Async` → `Fixed (#PR)`; otherwise a signed-off `Deferred`.
  - **Test file**: `tests/Paramore.Brighter.RMQ.Async.Tests/MessagingGateway/Quorum/Generated/Reactor/*.cs`
  - **Test should verify**:
    - Every behaviour for `RMQ.Async / Quorum` resolves to `Pass`/`Fixed` (both variants) or carries a `Deferred: #NNNN` Skip + matching ledger cell; no silent skip.
  - **Implementation files**:
    - `src/Paramore.Brighter.MessagingGateway.RMQ.Async/...` - localized fixes where in-boundary.
    - `specs/0036-universal-transport-conformance-tests/conformance-status.md` - resolve the `RMQ.Async / Quorum` row.
    - `tests/Paramore.Brighter.RMQ.Async.Tests/MessagingGateway/Quorum/Generated/**` - drop markers for fixed behaviours.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && (cd tests/Paramore.Brighter.RMQ.Async.Tests && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator) && { docker compose -f docker-compose-rmq.yaml up -d || true; } && dotnet test tests/Paramore.Brighter.RMQ.Async.Tests --filter "FullyQualifiedName~MessagingGateway.Quorum." && grep -q -- 'RMQ.Async / Quorum' specs/0036-universal-transport-conformance-tests/conformance-status.md && ! ( grep -- 'RMQ.Async / Quorum' specs/0036-universal-transport-conformance-tests/conformance-status.md | grep -q Unknown )`
  - **References**: ADR 0067 step 4 (RMQ.Async native DLX, larger fix), Architecture Overview stage (ii); ADR `0047-message-rejection-routing-strategy`, `0045-provide-dlq-where-missing`; requirements FR-13, FR-21.

- [ ] **Bring RocketMQ to conformance (expected FR-2 signed-off Deferred)**
  - **Behavior**: Run the generated canonical suite for `RocketMQ / RocketMQMessagingGateway` against the RocketMQ broker, both variants. Non-FR-2 behaviours fix-to-conform inline where localized. FR-2 is blocked upstream (`RocketMessageConsumer.Requeue` is a no-op; `ChangeInvisibleDuration` commented out pending an upstream RocketMQ C# client release), and may pass the before-`D` arm by accident (message held by native 30s invisibility timeout) — do not chase a green FR-2; flag-and-move-on to `Deferred -> #NNNN (sign-off: @maintainer)` with the Skip marker. Update the RocketMQ ledger row.
  - **Test file**: `tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/Generated/Reactor/*.cs`
  - **Test should verify**:
    - FR-2 cell reads `Deferred -> #NNNN (sign-off: @maintainer)` with a matching `Deferred: #NNNN` Skip on the FR-2 generated tests (both variants).
    - Other behaviours resolve to `Pass`/`Fixed` or a signed-off `Deferred`.
  - **Implementation files**:
    - `specs/0036-universal-transport-conformance-tests/conformance-status.md` - update the RocketMQ row (FR-2 Deferred).
    - `tests/Paramore.Brighter.RocketMQ.Tests/MessagingGateway/Generated/**` - keep FR-2 Skip markers; drop markers for any fixed behaviours.
    - `src/Paramore.Brighter.MessagingGateway.RocketMQ/...` - localized non-FR-2 fixes where in-boundary.
  - **RALPH-VERIFY** (the RocketMQ row must have no `Unknown` — FR-2 resolved to a signed-off `Deferred`, the rest `Pass`/`Fixed`): `dotnet build tools/Paramore.Brighter.Test.Generator && (cd tests/Paramore.Brighter.RocketMQ.Tests && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator) && { docker compose -f docker-compose-rocketmq.yaml up -d || true; } && dotnet test tests/Paramore.Brighter.RocketMQ.Tests --filter "FullyQualifiedName~MessagingGateway" && grep -q -- 'RocketMQ / RocketMQMessagingGateway' specs/0036-universal-transport-conformance-tests/conformance-status.md && ! ( grep -- 'RocketMQ / RocketMQMessagingGateway' specs/0036-universal-transport-conformance-tests/conformance-status.md | grep -q Unknown )`
  - **References**: ADR 0067 step 4 + "known FR-2 non-conformances" (RocketMQ upstream block); ADR 0066 "Why there is no scheduler member" (RocketMQ invisibility timeout); requirements FR-21 (seeded non-conformance).

### Phase 4 — Known-gap transports (ADR 0067 step 5)

> **GCP is split one task per configuration** (Pull, PullOrdering, Stream, StreamOrdering — four ledger rows). All four share the same FR-2 non-conformance (immediate redelivery: `ModifyAckDeadline(..., 0)` for Pull/PullOrdering; `gcpStreamMessage.Reject()` for Stream/StreamOrdering; timing governed by the subscription RetryPolicy, not the requeue delay). Each task independently decides whether a localized in-boundary fix in `src/Paramore.Brighter.MessagingGateway.GcpPubSub` can honour the delay → `Fixed (#PR)`, else flags its FR-2 cell to a signed-off `Deferred`, and resolves that configuration's other behaviours. There is no repo-root GCP compose file — provision the Pub/Sub emulator/project first; if infra cannot be stood up, flag-and-move-on. Do `GCP / Pull` first as the reference. The ledger grep is anchored (`… \|`) so `Pull` does not also match `PullOrdering` (nor `Stream` match `StreamOrdering`); the `dotnet test` filter is likewise scoped per configuration with a trailing dot (`~MessagingGateway.Pull.`, `~MessagingGateway.PullOrdering.`, etc.) so a sibling GCP configuration cannot fail this row's task.

- [ ] **Decide/attempt the GCP / Pull FR-2 fix**
  - **Behavior**: Per the GCP method above, resolve the `GCP / Pull` row against the Pub/Sub emulator/project, both variants. FR-2 → `Fixed (#PR)` if a localized delay-honouring fix is in-boundary, else a signed-off `Deferred`; other behaviours to `Pass`/`Fixed` or signed-off `Deferred`.
  - **Test file**: `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Pull/Generated/Reactor/*.cs`
  - **Test should verify**:
    - Every behaviour for `GCP / Pull` resolves to `Pass`/`Fixed` (both variants) or carries a `Deferred: #NNNN` Skip + matching ledger cell; no silent skip.
  - **Implementation files**:
    - `src/Paramore.Brighter.MessagingGateway.GcpPubSub/...` - FR-2 delay fix if in-boundary.
    - `specs/0036-universal-transport-conformance-tests/conformance-status.md` - resolve the `GCP / Pull` row.
    - `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Pull/Generated/**` - drop/keep markers accordingly.
  - **RALPH-VERIFY** (provision the emulator/project first; there is no `docker-compose-gcp.yaml`): `dotnet build tools/Paramore.Brighter.Test.Generator && (cd tests/Paramore.Brighter.Gcp.Tests && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator) && dotnet test tests/Paramore.Brighter.Gcp.Tests --filter "FullyQualifiedName~MessagingGateway.Pull." && grep -qE 'GCP / Pull[[:space:]]*\|' specs/0036-universal-transport-conformance-tests/conformance-status.md && ! ( grep -E 'GCP / Pull[[:space:]]*\|' specs/0036-universal-transport-conformance-tests/conformance-status.md | grep -q Unknown )`
  - **References**: ADR 0067 step 5 + "known FR-2 non-conformances" (GCP immediate redelivery); ADR 0066 "Why there is no scheduler member"; requirements FR-2, AC-2, FR-13, FR-21 (seeded non-conformance).

- [ ] **Decide/attempt the GCP / PullOrdering FR-2 fix**
  - **Behavior**: Per the GCP method above, resolve the `GCP / PullOrdering` row against the Pub/Sub emulator/project, both variants. FR-2 → `Fixed (#PR)` or signed-off `Deferred`; other behaviours resolved.
  - **Test file**: `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/PullOrdering/Generated/Reactor/*.cs`
  - **Test should verify**:
    - Every behaviour for `GCP / PullOrdering` resolves to `Pass`/`Fixed` (both variants) or carries a `Deferred: #NNNN` Skip + matching ledger cell; no silent skip.
  - **Implementation files**:
    - `src/Paramore.Brighter.MessagingGateway.GcpPubSub/...` - FR-2 delay fix if in-boundary.
    - `specs/0036-universal-transport-conformance-tests/conformance-status.md` - resolve the `GCP / PullOrdering` row.
    - `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/PullOrdering/Generated/**` - drop/keep markers accordingly.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && (cd tests/Paramore.Brighter.Gcp.Tests && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator) && dotnet test tests/Paramore.Brighter.Gcp.Tests --filter "FullyQualifiedName~MessagingGateway.PullOrdering." && grep -qE 'GCP / PullOrdering[[:space:]]*\|' specs/0036-universal-transport-conformance-tests/conformance-status.md && ! ( grep -E 'GCP / PullOrdering[[:space:]]*\|' specs/0036-universal-transport-conformance-tests/conformance-status.md | grep -q Unknown )`
  - **References**: ADR 0067 step 5 + "known FR-2 non-conformances"; ADR 0066 "Why there is no scheduler member"; requirements FR-2, AC-2, FR-13, FR-21.

- [ ] **Decide/attempt the GCP / Stream FR-2 fix**
  - **Behavior**: Per the GCP method above, resolve the `GCP / Stream` row against the Pub/Sub emulator/project, both variants. FR-2 → `Fixed (#PR)` or signed-off `Deferred`; other behaviours resolved.
  - **Test file**: `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Stream/Generated/Reactor/*.cs`
  - **Test should verify**:
    - Every behaviour for `GCP / Stream` resolves to `Pass`/`Fixed` (both variants) or carries a `Deferred: #NNNN` Skip + matching ledger cell; no silent skip.
  - **Implementation files**:
    - `src/Paramore.Brighter.MessagingGateway.GcpPubSub/...` - FR-2 delay fix if in-boundary.
    - `specs/0036-universal-transport-conformance-tests/conformance-status.md` - resolve the `GCP / Stream` row.
    - `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Stream/Generated/**` - drop/keep markers accordingly.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && (cd tests/Paramore.Brighter.Gcp.Tests && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator) && dotnet test tests/Paramore.Brighter.Gcp.Tests --filter "FullyQualifiedName~MessagingGateway.Stream." && grep -qE 'GCP / Stream[[:space:]]*\|' specs/0036-universal-transport-conformance-tests/conformance-status.md && ! ( grep -E 'GCP / Stream[[:space:]]*\|' specs/0036-universal-transport-conformance-tests/conformance-status.md | grep -q Unknown )`
  - **References**: ADR 0067 step 5 + "known FR-2 non-conformances"; ADR 0066 "Why there is no scheduler member"; requirements FR-2, AC-2, FR-13, FR-21.

- [ ] **Decide/attempt the GCP / StreamOrdering FR-2 fix**
  - **Behavior**: Per the GCP method above, resolve the `GCP / StreamOrdering` row against the Pub/Sub emulator/project, both variants. FR-2 → `Fixed (#PR)` or signed-off `Deferred`; other behaviours resolved.
  - **Test file**: `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/StreamOrdering/Generated/Reactor/*.cs`
  - **Test should verify**:
    - Every behaviour for `GCP / StreamOrdering` resolves to `Pass`/`Fixed` (both variants) or carries a `Deferred: #NNNN` Skip + matching ledger cell; no silent skip.
  - **Implementation files**:
    - `src/Paramore.Brighter.MessagingGateway.GcpPubSub/...` - FR-2 delay fix if in-boundary.
    - `specs/0036-universal-transport-conformance-tests/conformance-status.md` - resolve the `GCP / StreamOrdering` row.
    - `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/StreamOrdering/Generated/**` - drop/keep markers accordingly.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && (cd tests/Paramore.Brighter.Gcp.Tests && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator) && dotnet test tests/Paramore.Brighter.Gcp.Tests --filter "FullyQualifiedName~MessagingGateway.StreamOrdering." && grep -qE 'GCP / StreamOrdering[[:space:]]*\|' specs/0036-universal-transport-conformance-tests/conformance-status.md && ! ( grep -E 'GCP / StreamOrdering[[:space:]]*\|' specs/0036-universal-transport-conformance-tests/conformance-status.md | grep -q Unknown )`
  - **References**: ADR 0067 step 5 + "known FR-2 non-conformances"; ADR 0066 "Why there is no scheduler member"; requirements FR-2, AC-2, FR-13, FR-21.

- [ ] **Onboard MQTT: config + both providers (FR-20 step 1-2)**
  - **Behavior**: Add MQTT's generator wiring so the canonical templates generate for it: a `test-configuration.json` declaring its gateway configuration(s), and a `*MessageGatewayProvider.cs` implementing BOTH `IAmAMessageGatewayReactorProvider` and `IAmAMessageGatewayProactorProvider` against the post-FR-1 surface (routing-key params, `GetMessageFromInvalidChannel[Async]`, `RejectionMetadataKeys` from MQTT's own key strings, `string.Empty` where unstamped). It never carries `bool setupDeadLetterQueue`. MQTT compiles and generates. (Broker execution is the next task.)
  - **Test file**: `tests/Paramore.Brighter.MQTT.Tests/MessagingGateway/Generated/Reactor/IAmAMessageGatewayReactorProvider.cs` (regenerated artifact; verification is compilation + generation)
  - **Test should verify**:
    - `tests/Paramore.Brighter.MQTT.Tests/test-configuration.json` declares a `MessagingGateway`/`MessagingGateways` section; the provider implements both interfaces; `dotnet build tests/Paramore.Brighter.MQTT.Tests` succeeds.
    - The canonical templates are emitted for every MQTT configuration.
  - **Implementation files**:
    - `tests/Paramore.Brighter.MQTT.Tests/test-configuration.json` - new (no `HasSupportTo*` gate keys).
    - `tests/Paramore.Brighter.MQTT.Tests/MessagingGateway/MqttMessageGatewayProvider.cs` - new provider (both interfaces).
    - `tests/Paramore.Brighter.MQTT.Tests/MessagingGateway/**/Generated/**` - regenerate.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && (cd tests/Paramore.Brighter.MQTT.Tests && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator) && dotnet build tests/Paramore.Brighter.MQTT.Tests`
  - **References**: requirements FR-20(1),(2), AC-23, FR-13 mapping (`MQTT`→`MQTT`); ADR 0066 "provider implementations FR-20 adds"; ADR `0043-mqtt-dlq-brighter-managed`.

- [ ] **Onboard MQTT: run against a broker and record the ledger (FR-20 step 3)**
  - **Behavior**: Bring up the MQTT broker and run MQTT's generated canonical suite against it (not merely compile). Replace MQTT's placeholder ledger row with per-configuration rows; record `Pass`/`Fixed` where both variants pass against the broker, or flag-and-move-on to signed-off `Deferred` (with Skip markers) where infra/behaviour blocks. `Pass` requires the suite to actually run.
  - **Test file**: `tests/Paramore.Brighter.MQTT.Tests/MessagingGateway/Generated/Reactor/*.cs` (generated canonical suite)
  - **Test should verify**:
    - The `MQTT / (not yet declared)` placeholder row is replaced by per-configuration rows; each cell is `Pass`/`Fixed` (both variants, ran against broker) or `Deferred -> #NNNN` with matching Skip.
  - **Implementation files**:
    - `specs/0036-universal-transport-conformance-tests/conformance-status.md` - replace MQTT placeholder with per-config rows.
    - `tests/Paramore.Brighter.MQTT.Tests/MessagingGateway/**/Generated/**` - drop/keep markers accordingly.
    - `src/Paramore.Brighter.MessagingGateway.MQTT/...` - localized fixes where in-boundary.
  - **RALPH-VERIFY** (the placeholder row is gone and every replacement MQTT row has no `Unknown`): `dotnet build tools/Paramore.Brighter.Test.Generator && (cd tests/Paramore.Brighter.MQTT.Tests && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator) && { docker compose -f docker-compose-mqtt.yaml up -d || true; } && dotnet test tests/Paramore.Brighter.MQTT.Tests --filter "FullyQualifiedName~MessagingGateway" && ! grep -q 'MQTT / (not yet declared)' specs/0036-universal-transport-conformance-tests/conformance-status.md && ! grep -- 'MQTT /' specs/0036-universal-transport-conformance-tests/conformance-status.md | grep -q Unknown`
  - **References**: requirements FR-20(3), AC-23 (must run, not just compile), FR-21; ADR 0067 step 5, "Infra reality".

- [ ] **Onboard RMQ.Sync: config + both providers (FR-20 step 1-2)**
  - **Behavior**: Add RMQ.Sync's generator wiring: a `test-configuration.json` declaring its gateway configuration(s) and a `*MessageGatewayProvider.cs` implementing BOTH provider interfaces against the post-FR-1 surface (never `bool setupDeadLetterQueue`). RMQ.Sync compiles and generates. (Broker execution is the next task.)
  - **Test file**: `tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway/Generated/Reactor/IAmAMessageGatewayReactorProvider.cs` (regenerated artifact; verification is compilation + generation)
  - **Test should verify**:
    - `tests/Paramore.Brighter.RMQ.Sync.Tests/test-configuration.json` declares a gateway section; the provider implements both interfaces; `dotnet build tests/Paramore.Brighter.RMQ.Sync.Tests` succeeds; canonical templates emitted for every RMQ.Sync configuration.
  - **Implementation files**:
    - `tests/Paramore.Brighter.RMQ.Sync.Tests/test-configuration.json` - new (no gate keys).
    - `tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway/RmqSyncMessageGatewayProvider.cs` - new provider (both interfaces).
    - `tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway/**/Generated/**` - regenerate.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && (cd tests/Paramore.Brighter.RMQ.Sync.Tests && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator) && dotnet build tests/Paramore.Brighter.RMQ.Sync.Tests`
  - **References**: requirements FR-20(1),(2), AC-23, FR-13 mapping (`RMQ.Sync`→`RMQ.Sync`); ADR 0066 "provider implementations FR-20 adds".

- [ ] **Onboard RMQ.Sync: run against a broker and record the ledger (FR-20 step 3)**
  - **Behavior**: Bring up the RabbitMQ broker and run RMQ.Sync's generated canonical suite against it. Replace RMQ.Sync's placeholder ledger row with per-configuration rows; record `Pass`/`Fixed` (both variants, ran against broker) or flag-and-move-on to signed-off `Deferred`.
  - **Test file**: `tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway/Generated/Reactor/*.cs` (generated canonical suite)
  - **Test should verify**:
    - The `RMQ.Sync / (not yet declared)` placeholder row is replaced by per-configuration rows; each cell is `Pass`/`Fixed` or `Deferred -> #NNNN` with matching Skip.
  - **Implementation files**:
    - `specs/0036-universal-transport-conformance-tests/conformance-status.md` - replace RMQ.Sync placeholder with per-config rows.
    - `tests/Paramore.Brighter.RMQ.Sync.Tests/MessagingGateway/**/Generated/**` - drop/keep markers accordingly.
    - `src/Paramore.Brighter.MessagingGateway.RMQ.Sync/...` - localized fixes where in-boundary.
  - **RALPH-VERIFY** (the placeholder row is gone and every replacement RMQ.Sync row has no `Unknown`): `dotnet build tools/Paramore.Brighter.Test.Generator && (cd tests/Paramore.Brighter.RMQ.Sync.Tests && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator) && { docker compose -f docker-compose-rmq.yaml up -d || true; } && dotnet test tests/Paramore.Brighter.RMQ.Sync.Tests --filter "FullyQualifiedName~MessagingGateway" && ! grep -q 'RMQ.Sync / (not yet declared)' specs/0036-universal-transport-conformance-tests/conformance-status.md && ! grep -- 'RMQ.Sync /' specs/0036-universal-transport-conformance-tests/conformance-status.md | grep -q Unknown`
  - **References**: requirements FR-20(3), AC-23, FR-21; ADR 0067 step 5, "Infra reality".

- [ ] **Onboard AzureServiceBus: config + both providers (FR-20 step 1-2)**
  - **Behavior**: Add AzureServiceBus's generator wiring: a `test-configuration.json` declaring its gateway configuration(s) and a `*MessageGatewayProvider.cs` implementing BOTH provider interfaces against the post-FR-1 surface (never `bool setupDeadLetterQueue`; `RejectionMetadataKeys` from ASB's own key strings). Use the correct test project `tests/Paramore.Brighter.AzureServiceBus.Tests` — NOT `tests/Paramore.Brighter.Azure.Tests`. ASB compiles and generates. (Broker execution is the next task.)
  - **Test file**: `tests/Paramore.Brighter.AzureServiceBus.Tests/MessagingGateway/Generated/Reactor/IAmAMessageGatewayReactorProvider.cs` (regenerated artifact; verification is compilation + generation)
  - **Test should verify**:
    - `tests/Paramore.Brighter.AzureServiceBus.Tests/test-configuration.json` declares a gateway section; the provider implements both interfaces; `dotnet build tests/Paramore.Brighter.AzureServiceBus.Tests` succeeds; canonical templates emitted for every ASB configuration.
  - **Implementation files**:
    - `tests/Paramore.Brighter.AzureServiceBus.Tests/test-configuration.json` - new (no gate keys).
    - `tests/Paramore.Brighter.AzureServiceBus.Tests/MessagingGateway/AzureServiceBusMessageGatewayProvider.cs` - new provider (both interfaces).
    - `tests/Paramore.Brighter.AzureServiceBus.Tests/MessagingGateway/**/Generated/**` - regenerate.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && (cd tests/Paramore.Brighter.AzureServiceBus.Tests && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator) && dotnet build tests/Paramore.Brighter.AzureServiceBus.Tests`
  - **References**: requirements FR-20(1),(2), AC-23, FR-13 mapping + the `Azure.Tests` trap; ADR 0066 "provider implementations FR-20 adds".

- [ ] **Onboard AzureServiceBus: run against a broker or record a signed-off Deferred (FR-20 step 3)**
  - **Behavior**: Attempt to run ASB's generated canonical suite against a real broker (cloud instance/emulator). ASB is a cloud service with no container story in this repo, so infra is the likeliest block — apply flag-and-move-on: replace the placeholder ledger row with per-configuration rows and record `Deferred -> #NNNN (sign-off: @maintainer)` with Skip markers on infra grounds; the configuration stays in the target set and is never dropped. If infra can be stood up, record `Pass`/`Fixed` where both variants pass against the broker.
  - **Test file**: `tests/Paramore.Brighter.AzureServiceBus.Tests/MessagingGateway/Generated/Reactor/*.cs` (generated canonical suite)
  - **Test should verify**:
    - The `AzureServiceBus / (not yet declared)` placeholder row is replaced by per-configuration rows; each cell is `Pass`/`Fixed` (ran against broker) or `Deferred -> #NNNN` with matching Skip and infra-grounds sign-off.
  - **Implementation files**:
    - `specs/0036-universal-transport-conformance-tests/conformance-status.md` - replace ASB placeholder with per-config rows.
    - `tests/Paramore.Brighter.AzureServiceBus.Tests/MessagingGateway/**/Generated/**` - keep/drop markers accordingly.
    - `src/Paramore.Brighter.MessagingGateway.AzureServiceBus/...` - localized fixes only if a broker is available and in-boundary.
  - **RALPH-VERIFY** (no repo-root ASB compose file; if no broker is available, apply flag-and-move-on and the deferred behaviours carry Skip markers, so the run passes with them skipped — the placeholder row must still be gone and no replacement ASB row may read `Unknown`): `dotnet build tools/Paramore.Brighter.Test.Generator && (cd tests/Paramore.Brighter.AzureServiceBus.Tests && dotnet run --no-build --project ../../tools/Paramore.Brighter.Test.Generator) && dotnet build tests/Paramore.Brighter.AzureServiceBus.Tests && dotnet test tests/Paramore.Brighter.AzureServiceBus.Tests --filter "FullyQualifiedName~MessagingGateway" && ! grep -q 'AzureServiceBus / (not yet declared)' specs/0036-universal-transport-conformance-tests/conformance-status.md && ! grep -- 'AzureServiceBus /' specs/0036-universal-transport-conformance-tests/conformance-status.md | grep -q Unknown`
  - **References**: requirements FR-20(3), AC-23 ("Inability to provide CI infrastructure is a valid ground for deferral"), FR-21 placeholder rules; ADR 0067 "Negative" (ASB likely lands Deferred), step 5.

### Phase 5 — Terminal cleanup (ADR 0067 step 6 — GATED on the ledger having no Unknown cells)

> Precondition for every task in this phase: `specs/0036-universal-transport-conformance-tests/conformance-status.md` has NO `Unknown` cell — every cell reads `Pass`, `Fixed (#…)`, or `Deferred -> #NNNN (sign-off: @maintainer)`, and every one of the twelve transports is represented (placeholders resolved to signed-off Deferred). Order within the phase is load-bearing: templates + generated copies first, keys last.

- [ ] **Delete the four legacy templates and sweep their 80 generated copies**
  - **Behavior**: Delete the four legacy gated templates in BOTH variants and manually sweep all 80 checked-in generated copies (the generator never deletes stale files): 6 delayed-message, 36 plain-requeue, 6 with_delay, 32 exhaustion. After this, no legacy template or generated copy remains, and every remaining delayed-requeue template passes a non-null `TimeSpan` to `Requeue`/`RequeueAsync`.
  - **Test file**: `tests/Paramore.Brighter.Test.Generator.Tests/Cleanup/When_legacy_templates_deleted_should_leave_no_template_or_generated_copy.cs`
  - **Test should verify**:
    - None of the four legacy filenames exists under `tools/.../Templates/MessagingGateway/{Reactor,Proactor}/`.
    - No generated copy of any of the four remains under any `tests/Paramore.Brighter.*.Tests/**/Generated/` directory (AC-10(b), AC-12, AC-22).
    - No messaging-gateway template purporting to exercise delayed requeue calls `Requeue`/`RequeueAsync` without a non-null `TimeSpan` (AC-12).
  - **Implementation files**:
    - `tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/Reactor/{When_reading_a_delayed_message_...,When_requeuing_a_failed_message_should_receive_message_again,When_requeuing_a_failed_message_with_delay_...,When_requeuing_a_message_too_many_times_...}.cs.liquid` - delete.
    - `tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/Proactor/{same four}.cs.liquid` - delete.
    - `tests/Paramore.Brighter.*.Tests/**/Generated/**` - remove the 80 stale copies.
  - **RALPH-VERIFY** (matches the four legacy filenames EXACTLY — never the `*with_delay*` glob, which would also match the canonical FR-2 template `When_requeuing_a_failed_message_with_delay_should_redeliver_after_delay` and every regenerated copy of it; runs the Cleanup xUnit test as the gate; joins the build with `&&` so the deletion checks actually gate completion): `dotnet test tests/Paramore.Brighter.Test.Generator.Tests --filter "FullyQualifiedName~Cleanup" && ! find tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway -type f \( -name 'When_reading_a_delayed_message_via_the_messaging_gateway_should_delay_delivery.cs.liquid' -o -name 'When_requeuing_a_failed_message_should_receive_message_again.cs.liquid' -o -name 'When_requeuing_a_failed_message_with_delay_should_receive_message_again.cs.liquid' -o -name 'When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue.cs.liquid' \) -print | grep -q . && ! find tests -path '*/Generated/*' -type f \( -name 'When_reading_a_delayed_message_via_the_messaging_gateway_should_delay_delivery.cs' -o -name 'When_requeuing_a_failed_message_should_receive_message_again.cs' -o -name 'When_requeuing_a_failed_message_with_delay_should_receive_message_again.cs' -o -name 'When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue.cs' \) | grep -q . && dotnet build tools/Paramore.Brighter.Test.Generator`
  - **References**: requirements FR-10(3), FR-12, FR-19, AC-10(b), AC-12, AC-22; ADR 0066 "The generated tree" (80 copies: 6+36+6+32), "Step C", "the substring-matching hazard" (why the verify uses exact filenames, not `*with_delay*`); ADR 0067 step 6.

- [ ] **Remove the four gate branches and the closed legacy list from SkipTest**
  - **Behavior**: With the legacy templates gone, the four gate branches (keyed on three gates — `HasSupportToDelayedMessages` tested twice, for `delayed_message` and `with_delay`) gate nothing and are removed from `SkipTest`, along with the now-unused `LegacyGatedTemplates` list. The three unrelated retained gates (`confirming_posting`, `no_broker_created`, `assume_channel`/`validate_channel`) stay.
  - **Test file**: `tests/Paramore.Brighter.Test.Generator.Tests/MessagingGatewayGenerator/When_gates_retired_should_leave_no_branch_keyed_on_the_three_gates.cs`
  - **Test should verify**:
    - `SkipTest` has no branch referencing `HasSupportToDelayedMessages`, `HasSupportToDeadLetterQueue`, or `HasSupportToRequeue` (all four branches gone).
    - The retained gate branches still behave as before.
  - **Implementation files**:
    - `tools/Paramore.Brighter.Test.Generator/Generators/MessagingGatewayGenerator.cs` - delete the four gate branches (former lines 122/127/132/145) and the `LegacyGatedTemplates` array.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && dotnet test tests/Paramore.Brighter.Test.Generator.Tests --filter "FullyQualifiedName~MessagingGatewayGenerator" && ! grep -nE "HasSupportToDelayedMessages|HasSupportToDeadLetterQueue|HasSupportToRequeue" tools/Paramore.Brighter.Test.Generator/Generators/MessagingGatewayGenerator.cs`
  - **References**: requirements FR-10(4), AC-10(c); ADR 0066 "Step C", "four branches keyed on three gates".

- [ ] **Remove the three properties from MessagingGatewayConfiguration**
  - **Behavior**: Delete `HasSupportToDelayedMessages`, `HasSupportToDeadLetterQueue`, and `HasSupportToRequeue` from `MessagingGatewayConfiguration` (former lines 91/96/106). The generator builds; the retained flag properties are untouched.
  - **Test file**: `tests/Paramore.Brighter.Test.Generator.Tests/MessagingGatewayGenerator/When_gates_retired_should_absent_config_properties.cs`
  - **Test should verify**:
    - `MessagingGatewayConfiguration` has no `HasSupportToDelayedMessages`/`HasSupportToDeadLetterQueue`/`HasSupportToRequeue` members (AC-10(c)).
  - **Implementation files**:
    - `tools/Paramore.Brighter.Test.Generator/Configuration/MessagingGatewayConfiguration.cs` - remove the three properties.
  - **RALPH-VERIFY**: `dotnet build tools/Paramore.Brighter.Test.Generator && ! grep -nE "HasSupportToDelayedMessages|HasSupportToDeadLetterQueue|HasSupportToRequeue" tools/Paramore.Brighter.Test.Generator/Configuration/MessagingGatewayConfiguration.cs`
  - **References**: requirements FR-10(4), AC-10(c); ADR 0066 "Key Components" (config loses three properties).

- [ ] **Remove the three gate keys from every test-configuration.json (FR-11 — LAST)**
  - **Behavior**: Remove `HasSupportToDelayedMessages`, `HasSupportToDeadLetterQueue`, and `HasSupportToRequeue` from every `tests/Paramore.Brighter.*.Tests/test-configuration.json` (including the mis-declared PostgreSQL, AWS/AWS.V4 and Kafka values). This is the final step — done AFTER the legacy templates and their copies are gone — because removing a key before its template is deleted would ungate the legacy template (AC-11 is ordered). Then a full regenerate + solution build confirms nothing references the removed keys.
  - **Test file**: `specs/…`/config artifacts (verification is grep + full regenerate build; no new xUnit test)
  - **Test should verify**:
    - No `test-configuration.json` contains any of the three keys (AC-11).
    - `./generate-test.sh` followed by a solution build succeeds for every messaging-gateway test project.
  - **Implementation files**:
    - `tests/Paramore.Brighter.*.Tests/test-configuration.json` (all gateway configs) - remove the three keys.
    - `tests/Paramore.Brighter.*.Tests/**/Generated/**` - regenerate (full `./generate-test.sh`). Safe: all providers migrated and legacy templates deleted, and the canonical Skip markers are re-derived from the ledger (Pass/Fixed → no marker; Deferred → marker with the cell's issue number), so a full regenerate cannot clobber a proven configuration back to skipped.
  - **RALPH-VERIFY**: `! grep -rnE "HasSupportToDelayedMessages|HasSupportToDeadLetterQueue|HasSupportToRequeue" tests --include=test-configuration.json && dotnet build tools/Paramore.Brighter.Test.Generator && ./generate-test.sh && dotnet build Brighter.slnx`
  - **References**: requirements FR-11, AC-11 (ordered — keys last); ADR 0066 "The key removal must not precede the template deletion"; ADR 0067 step 6 (order within the step matters).

### Phase 6 — CI audit (ADR 0067 step 7)

> **Order within Phase 6 is load-bearing (review finding #6).** The audit tasks assert the `Deferred: #<digits>` pattern, but through Phases 1–5 Skip markers and Deferred ledger cells carry the literal `#NNNN` placeholder. So the "Raise follow-up issues" reconciliation task runs FIRST — it replaces every `#NNNN` with a real issue number — and only then do the two audit tasks run against a trail of real digits.

- [ ] **Raise follow-up issues for every Deferred ledger row (reconcile `#NNNN` first)**
  - **Behavior**: For each `Deferred -> #NNNN (sign-off: @maintainer)` ledger cell, ensure a named, linked follow-up issue exists (RocketMQ FR-2 upstream block, GCP FR-2 if deferred, ASB infra deferral, any lagging-variant deferrals, and OOS-2 supplementary scheduler tests for the six scheduler-capable gateways). Replace every literal `#NNNN` placeholder — in the ledger AND in the Skip markers — with the real issue number, so the audit tasks that follow see a trail of real digits. Regenerate if the markers are template-driven.
  - **Test file**: `specs/0036-universal-transport-conformance-tests/conformance-status.md` (artifact — every `#NNNN` placeholder is gone and every `#<n>` resolves to a real issue)
  - **Test should verify**:
    - No literal `#NNNN` placeholder remains in the ledger or in any Skip marker.
    - Every distinct `#<n>` in the ledger and in Skip markers corresponds to an existing issue (checked via `gh issue view`).
    - No `Deferred` cell references a placeholder/non-existent issue number.
  - **Implementation files**:
    - `specs/0036-universal-transport-conformance-tests/conformance-status.md` - finalize real issue numbers.
    - `tests/Paramore.Brighter.*.Tests/**/Generated/**` - align Skip marker issue numbers with the raised issues (regenerate if the markers are template-driven).
  - **RALPH-VERIFY**: `! grep -rn '#NNNN' specs/0036-universal-transport-conformance-tests/conformance-status.md tests --include=*.cs && for n in $(grep -ohE "#[0-9]+" specs/0036-universal-transport-conformance-tests/conformance-status.md | tr -d '#' | sort -u); do gh issue view "$n" >/dev/null 2>&1 || { echo "MISSING #$n"; exit 1; }; done`
  - **References**: requirements FR-13 (named, linked, signed-off follow-up), FR-21; ADR 0067 "Risks and Mitigations" (deferral list owned/audited), OOS-2 follow-up (scheduler-delegation tests).

- [ ] **Enforce the greppable linked-issue Skip convention**
  - **Behavior**: Implement a read-only in-tree audit that fails any messaging-gateway test (template or generated copy) whose `Skip` value does not match the required `Deferred: #<n>` pattern. A bare or reasonless `Skip` is a CI failure. The audit does not query the live issue tracker. (Runs after `#NNNN` reconciliation, so the pattern sees real digits.)
  - **Test file**: `tests/Paramore.Brighter.Test.Generator.Tests/ConformanceAudit/When_a_gateway_skip_is_not_a_deferred_marker_should_fail_audit.cs`
  - **Test should verify**:
    - A messaging-gateway test with `Skip = "Deferred: #1234 — …"` passes; a bare `Skip = "flaky"` (or any value not matching `Deferred: #<n>`) fails the audit.
    - The audit scans in-tree artifacts only (`tools/.../Templates/MessagingGateway/`, `tests/**/Generated/`), never the network.
  - **Implementation files**:
    - `tests/Paramore.Brighter.Test.Generator.Tests/ConformanceAudit/GatewaySkipConventionAudit.cs` (+ the test above) - scan messaging-gateway `.cs`/`.liquid` for `Skip = ` and assert the `Deferred: #<n>` pattern.
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Test.Generator.Tests --filter "FullyQualifiedName~ConformanceAudit"`
  - **References**: requirements FR-13, AC-13 ("no silent skip"); ADR 0067 "The greppable linked-issue Skip convention", "The CI audit check", step 7.

- [ ] **Cross-check Skip markers against the conformance ledger**
  - **Behavior**: Extend the audit to cross-check the in-tree trail: every `Skip = "Deferred: #NNNN …"` maps to a ledger row marked `Deferred -> #NNNN`, and every `Deferred` ledger row carries an issue link and a recorded sign-off entry. A `Skip` without a ledger row, or a `Deferred` row missing its issue link or sign-off, fails audit. The build does not query the tracker for issue open/closed state or re-verify sign-off provenance (that is the maintainer review gate's job).
  - **Test file**: `tests/Paramore.Brighter.Test.Generator.Tests/ConformanceAudit/When_a_skip_has_no_matching_deferred_ledger_row_should_fail_audit.cs`
  - **Test should verify**:
    - A `Deferred: #NNNN` Skip with a matching `Deferred -> #NNNN (sign-off: @…)` ledger row passes; a Skip with no matching row fails; a `Deferred` row missing its issue link or sign-off fails.
    - The audit reads only `conformance-status.md` and the in-tree test artifacts.
  - **Implementation files**:
    - `tests/Paramore.Brighter.Test.Generator.Tests/ConformanceAudit/LedgerSkipCrossCheckAudit.cs` (+ the test above) - parse the ledger and the Skip markers and cross-check both directions.
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Test.Generator.Tests --filter "FullyQualifiedName~ConformanceAudit"`
  - **References**: requirements FR-13, FR-21, AC-13, AC-24; ADR 0067 "The CI audit check", step 7, "does not query the live issue tracker".
