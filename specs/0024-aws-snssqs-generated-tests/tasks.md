# Tasks: AWS SNS/SQS Generated Tests

**Input**: Design documents from `specs/0024-aws-snssqs-generated-tests/`
**Prerequisites**: plan.md (required), spec.md (required)

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US5 from spec.md)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create generator configuration files and validate the existing compose/credential infrastructure

- [ ] T001 [P] [US4] Create `tests/Paramore.Brighter.AWS.Tests/test-configuration.json` with `MessagingGateways` dict containing four transport variants (SnsStandard, SnsFifo, SqsStandard, SqsFifo) — feature flags per FR-006/006a/007/007a, Category "AWS", Publication/Subscription types from `Paramore.Brighter.MessagingGateway.AWSSQS`, provider FQNs from `Paramore.Brighter.AWS.Tests.MessagingGateway`. Reference: `tests/Paramore.Brighter.RMQ.Async.Tests/test-configuration.json`
- [ ] T002 [P] [US4] Create `tests/Paramore.Brighter.AWS.V4.Tests/test-configuration.json` — same structure as T001 but namespace `Paramore.Brighter.AWS.V4.Tests`, Publication/Subscription from `Paramore.Brighter.MessagingGateway.AWSSQS.V4`, providers from `Paramore.Brighter.AWS.V4.Tests.MessagingGateway`
- [ ] T003 [US3] Verify `docker-compose-localstack.yaml` works with `podman compose` — run `podman compose -f docker-compose-localstack.yaml up -d`, validate health at `http://localhost:4566/_localstack/health` showing SNS/SQS/STS services ready

**Checkpoint**: Configuration files ready, LocalStack verified — provider implementation can begin

---

## Phase 2: SNS Standard Provider — MVP (Priority: P1) 🎯 MVP

**Goal**: Implement the first provider to validate the entire generated-test pattern end-to-end against LocalStack

**Independent Test**: Run generator, build project, execute `When_posting_a_message_via_the_messaging_gateway_should_be_received` test against LocalStack

### Implementation for SNS Standard

- [ ] T004 [US2] Create `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/SnsStandardMessageGatewayProvider.cs` — implement both `IAmAMessageGatewayProactorProvider` and `IAmAMessageGatewayReactorProvider` for SNS pub/sub with standard queues. Must include: unique routing key/channel name generation per instance, `SnsPublication` with `MakeChannels=Create`, `SqsSubscription` with `ChannelType.PubSub` and `RequeueCount=3`, producer/channel creation via `GatewayFactory.CreateFactory()`, DLQ support with separate DLQ routing key + consumer, `RequeueTrackingChannelAsync`/`RequeueTrackingChannelSync` decorators, cleanup methods. Reference: `tests/Paramore.Brighter.RMQ.Async.Tests/MessagingGateway/RmqClassicMessageGatewayProvider.cs` (structure), `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/Sns/Standard/Proactor/When_posting_a_message_via_the_messaging_gateway_async.cs` (AWS setup)
- [ ] T005 [US1] Run test generator for AWS.Tests — execute `cd tests/Paramore.Brighter.AWS.Tests && dotnet run --project ../../tools/Paramore.Brighter.Test.Generator/Paramore.Brighter.Test.Generator.csproj`. Verify generated files appear under `MessagingGateway/SnsStandard/Generated/{Proactor,Reactor}/`
- [ ] T006 [US1] Build `tests/Paramore.Brighter.AWS.Tests/Paramore.Brighter.AWS.Tests.csproj` and fix any compilation errors in generated code or provider
- [ ] T007 [US1] Run generated SNS Standard tests against LocalStack — `LOCALSTACK_SERVICE_URL=http://localhost:4566 dotnet test tests/Paramore.Brighter.AWS.Tests/ --filter "Category=AWS" --framework net10.0`. Fix any test failures

**Checkpoint**: SNS Standard provider + generated tests pass end-to-end against LocalStack — pattern validated

---

## Phase 3: Remaining Three Non-V4 Providers (Priority: P1)

**Goal**: Complete all four transport variant providers for the non-V4 project

**Independent Test**: Run full generated test suite against LocalStack for all four variants

### Implementation for SQS Standard

- [ ] T008 [P] [US2] Create `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/SqsStandardMessageGatewayProvider.cs` — same structure as SnsStandard but: `SqsPublication` (not `SnsPublication`), `SqsMessageProducer` (not `SnsMessageProducer`), `ChannelType.PointToPoint` (not `PubSub`), delayed messages supported. Reference: `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/Sqs/Standard/Proactor/When_posting_a_message_via_the_messaging_gateway_async.cs`

### Implementation for SNS FIFO

- [ ] T009 [P] [US2] Create `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/SnsFifoMessageGatewayProvider.cs` — same structure as SnsStandard but: routing key/channel name with `.fifo` suffix, `SnsAttributes { Type = SnsType.Fifo }` on publication, `SqsAttributes { Type = SqsType.Fifo, FifoThroughputLimit = FifoThroughputLimit.PerMessageGroupId }` on subscription, DLQ queue/topic names also with `.fifo` suffix. Reference: `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/Sns/Fifo/Proactor/When_posting_a_message_via_the_messaging_gateway_async.cs`

### Implementation for SQS FIFO

- [ ] T010 [P] [US2] Create `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/SqsFifoMessageGatewayProvider.cs` — combines SQS point-to-point pattern (T008) with FIFO attributes (T009): `SqsPublication` + `SqsMessageProducer`, `.fifo` suffix, FIFO attributes, `ChannelType.PointToPoint`. Reference: `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/Sqs/Fifo/Proactor/When_posting_a_message_via_the_messaging_gateway_async.cs`

### Regenerate and Verify

- [ ] T011 [US1] Regenerate all tests — `cd tests/Paramore.Brighter.AWS.Tests && dotnet run --project ../../tools/Paramore.Brighter.Test.Generator/Paramore.Brighter.Test.Generator.csproj`. Verify four variant directories exist under `MessagingGateway/{SnsStandard,SnsFifo,SqsStandard,SqsFifo}/Generated/{Proactor,Reactor}/`
- [ ] T012 [US1] Build `tests/Paramore.Brighter.AWS.Tests/Paramore.Brighter.AWS.Tests.csproj` and fix any compilation errors
- [ ] T013 [US1] Run all generated tests against LocalStack — `LOCALSTACK_SERVICE_URL=http://localhost:4566 dotnet test tests/Paramore.Brighter.AWS.Tests/ --filter "Category=AWS" --framework net10.0`. All four variant test suites must pass

**Checkpoint**: All four non-V4 providers + generated tests verified against LocalStack

---

## Phase 4: V4 Providers (Priority: P1)

**Goal**: Mirror all four providers into the V4 test project with namespace changes

**Independent Test**: Run V4 generated test suite against LocalStack

### Implementation for V4

- [ ] T014 [P] [US2] Create `tests/Paramore.Brighter.AWS.V4.Tests/MessagingGateway/SnsStandardMessageGatewayProvider.cs` — copy from non-V4, change namespace to `Paramore.Brighter.AWS.V4.Tests.MessagingGateway`, imports to `Paramore.Brighter.MessagingGateway.AWSSQS.V4`, helpers to `Paramore.Brighter.AWS.V4.Tests.Helpers.GatewayFactory`
- [ ] T015 [P] [US2] Create `tests/Paramore.Brighter.AWS.V4.Tests/MessagingGateway/SnsFifoMessageGatewayProvider.cs` — V4 version of SnsFifo provider with same namespace changes as T014
- [ ] T016 [P] [US2] Create `tests/Paramore.Brighter.AWS.V4.Tests/MessagingGateway/SqsStandardMessageGatewayProvider.cs` — V4 version of SqsStandard provider with same namespace changes as T014
- [ ] T017 [P] [US2] Create `tests/Paramore.Brighter.AWS.V4.Tests/MessagingGateway/SqsFifoMessageGatewayProvider.cs` — V4 version of SqsFifo provider with same namespace changes as T014

### Generate and Verify V4

- [ ] T018 [US1] Run test generator for V4 — `cd tests/Paramore.Brighter.AWS.V4.Tests && dotnet run --project ../../tools/Paramore.Brighter.Test.Generator/Paramore.Brighter.Test.Generator.csproj`
- [ ] T019 [US1] Build `tests/Paramore.Brighter.AWS.V4.Tests/Paramore.Brighter.AWS.V4.Tests.csproj` and fix compilation errors
- [ ] T020 [US1] Run all V4 generated tests against LocalStack — `LOCALSTACK_SERVICE_URL=http://localhost:4566 dotnet test tests/Paramore.Brighter.AWS.V4.Tests/ --filter "Category=AWS" --framework net10.0`

**Checkpoint**: Both AWS.Tests and AWS.V4.Tests fully passing against LocalStack

---

## Phase 5: Podman Validation (Priority: P1)

**Goal**: Full end-to-end validation using Podman compose

**Independent Test**: Clean restart of LocalStack via Podman, run both test suites

- [ ] T021 [US3] Full Podman compose cycle — `podman compose -f docker-compose-localstack.yaml down && podman compose -f docker-compose-localstack.yaml up -d`, wait for health check, run `dotnet test tests/Paramore.Brighter.AWS.Tests/ --filter "Category=AWS"` and `dotnet test tests/Paramore.Brighter.AWS.V4.Tests/ --filter "Category=AWS"`. Both must pass with zero failures

**Checkpoint**: Podman + LocalStack validated as first-class test runner

---

## Phase 6: Retire Handwritten Tests (Priority: P3)

**Goal**: Remove handwritten tests that are now covered by generated equivalents, preserving AWS-specific tests with no template coverage

**Independent Test**: Full test run after removal shows same or better pass rate

- [ ] T022 [US5] Map handwritten tests to generated equivalents — for each file under `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/{Sns,Sqs}/{Standard,Fifo}/{Proactor,Reactor}/`, determine if an equivalent generated test exists. Categorize as: REMOVE (covered by generated), PRESERVE (AWS-specific behavior not in templates, e.g., `When_customising_aws_client_config_async.cs`, `When_infrastructure_exists_can_verify_by_arn_async.cs`, `When_infrastructure_exists_can_verify_by_convention_async.cs`, `When_infrastructure_exists_can_verify_by_url*.cs`, `When_raw_message_delivery_disabled_async.cs`, DLQ rejection tests)
- [ ] T023 [US5] Remove handwritten test files categorized as REMOVE in T022 from `tests/Paramore.Brighter.AWS.Tests/MessagingGateway/{Sns,Sqs}/` directories
- [ ] T024 [US5] Remove handwritten test files from `tests/Paramore.Brighter.AWS.V4.Tests/MessagingGateway/{Sns,Sqs}/` directories — same mapping as T022 applied to V4
- [ ] T025 [US5] Run full test suite for both projects — `dotnet test tests/Paramore.Brighter.AWS.Tests/ --filter "Category=AWS"` and `dotnet test tests/Paramore.Brighter.AWS.V4.Tests/ --filter "Category=AWS"`. Zero test failures after cleanup

**Checkpoint**: Handwritten duplicates removed, only generated + AWS-specific tests remain

---

## Phase 7: Polish & Cross-Cutting Concerns

- [ ] T026 Commit all changes with descriptive message summarizing: new test-configuration.json files, 8 provider implementations, generated test infrastructure, Podman validation

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — T001 and T002 can run in parallel; T003 can run in parallel
- **Phase 2 (SNS Standard MVP)**: Depends on T001 (config), T003 (LocalStack verified)
- **Phase 3 (Remaining providers)**: Depends on T007 (SNS Standard validated) — T008, T009, T010 can run in parallel
- **Phase 4 (V4)**: Depends on T013 (non-V4 verified), T002 (V4 config) — T014–T017 can run in parallel
- **Phase 5 (Podman)**: Depends on T020 (V4 verified)
- **Phase 6 (Cleanup)**: Depends on T021 (Podman validated) — T023, T024 can run in parallel after T022
- **Phase 7 (Polish)**: Depends on T025 (final verification)

### User Story Dependencies

- **US4 (Config)**: No dependencies — can start immediately (T001, T002)
- **US2 (Providers)**: Depends on US4 config files being created
- **US1 (Generated tests)**: Depends on US2 providers being implemented
- **US3 (Podman)**: Depends on US1 generated tests passing
- **US5 (Cleanup)**: Depends on US3 Podman validation

### Parallel Opportunities

- T001 + T002 + T003: All setup tasks in parallel
- T008 + T009 + T010: Three remaining providers in parallel (different files)
- T014 + T015 + T016 + T017: All four V4 providers in parallel (different files)
- T023 + T024: Cleanup of both projects in parallel

---

## Parallel Example: Phase 3

```text
# Launch all remaining providers together (different files, no dependencies):
T008: Create SqsStandardMessageGatewayProvider.cs
T009: Create SnsFifoMessageGatewayProvider.cs
T010: Create SqsFifoMessageGatewayProvider.cs
```

## Parallel Example: Phase 4

```text
# Launch all V4 providers together:
T014: Create V4 SnsStandardMessageGatewayProvider.cs
T015: Create V4 SnsFifoMessageGatewayProvider.cs
T016: Create V4 SqsStandardMessageGatewayProvider.cs
T017: Create V4 SqsFifoMessageGatewayProvider.cs
```

---

## Implementation Strategy

### MVP First (Phase 1 + Phase 2)

1. Create test-configuration.json files (T001, T002)
2. Verify Podman + LocalStack (T003)
3. Implement SnsStandard provider (T004)
4. Generate, build, test (T005–T007)
5. **STOP and VALIDATE**: SNS Standard generated tests pass against LocalStack

### Incremental Delivery

1. Phase 1 + Phase 2 → SNS Standard working (MVP)
2. Phase 3 → All four variants working
3. Phase 4 → V4 parity achieved
4. Phase 5 → Podman workflow validated
5. Phase 6 → Handwritten test cleanup
6. Each phase adds value without breaking previous phases

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks
- [Story] label maps task to specific user story for traceability
- Provider implementations follow the established pattern from RMQ Classic/Quorum providers
- FIFO providers MUST append `.fifo` to all queue/topic names per AWS requirements (FR-012)
- All providers MUST generate unique resource names per instance to support parallel test execution (FR-004)
- The `RequeueTrackingChannel` decorator pattern from Redis/RMQ providers handles DLQ requeue counting
- Generated files are auto-generated — do NOT manually edit files under `Generated/` directories
