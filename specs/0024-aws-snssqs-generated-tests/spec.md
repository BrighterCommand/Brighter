# Feature Specification: AWS SNS/SQS Generated Tests

**Feature Branch**: `minor.aws-snssqs.generated-tests`
**Created**: 2026-03-12
**Status**: Draft
**Input**: User description: "Using Generated test in AWS SNS/SQS test, for testing use localstack + podman"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Replace Handwritten AWS Tests with Generated Tests (Priority: P1)

As a contributor to the Brighter framework, I want the AWS SNS/SQS messaging gateway tests to use the test generator framework so that tests stay consistent with other transport implementations and are easier to maintain.

The existing AWS SNS/SQS test projects (`Paramore.Brighter.AWS.Tests` and `Paramore.Brighter.AWS.V4.Tests`) contain ~270 handwritten test files covering Standard/FIFO × SNS/SQS × Proactor/Reactor scenarios. These tests duplicate the same behavioral patterns already captured in the Liquid templates used by Redis, Kafka, RMQ, and GCP test projects. Migrating to the test generator ensures consistency, reduces maintenance burden, and makes it trivial to add new test scenarios across all transports.

**Why this priority**: The generated tests are the core deliverable. Without them, nothing else in this spec matters.

**Independent Test**: Can be fully tested by running `dotnet run --project tools/Paramore.Brighter.Test.Generator/Paramore.Brighter.Test.Generator.csproj` from inside each AWS test project directory and verifying the generated files compile and execute against LocalStack.

**Acceptance Scenarios**:

1. **Given** the test generator is configured for `Paramore.Brighter.AWS.Tests`, **When** the generator runs, **Then** it produces Proactor and Reactor test files under `MessagingGateway/Generated/` for all four transport variants (SNS Standard, SNS FIFO, SQS Standard, SQS FIFO).
2. **Given** the test generator is configured for `Paramore.Brighter.AWS.V4.Tests`, **When** the generator runs, **Then** it produces equivalent generated tests referencing the V4 namespace and V4 provider implementations.
3. **Given** a generated test project, **When** `dotnet test` is run with LocalStack available, **Then** all generated tests pass.

---

### User Story 2 - Implement AWS Message Gateway Providers for Standard Queues (Priority: P1)

As a contributor, I need `IAmAMessageGatewayProactorProvider` and `IAmAMessageGatewayReactorProvider` implementations for each AWS transport variant so that the generated test templates can create the correct producers, channels, publications, and subscriptions at test runtime.

Four provider implementations are needed for the non-V4 project:
- **SNS Standard** – pub/sub via SNS topic + SQS subscription (standard queue)
- **SNS FIFO** – pub/sub via SNS FIFO topic + SQS FIFO subscription
- **SQS Standard** – point-to-point via SQS standard queue
- **SQS FIFO** – point-to-point via SQS FIFO queue

Four equivalent providers are needed for the V4 project (using V4 namespaces).

**Why this priority**: Providers are required by the generated tests — they are the glue between the generic templates and the AWS-specific infrastructure.

**Independent Test**: Each provider can be tested by running a single generated test (e.g., `When_posting_a_message_via_the_messaging_gateway_should_be_received`) against LocalStack.

**Acceptance Scenarios**:

1. **Given** an SNS Standard Proactor provider, **When** a publication and subscription are created, **Then** a message sent via the producer is received on the channel.
2. **Given** an SQS Standard Proactor provider, **When** a publication and subscription are created with `ChannelType.PointToPoint`, **Then** a message sent via the producer is received on the channel.
3. **Given** an SNS FIFO provider, **When** a publication and subscription are created with FIFO attributes, **Then** messages are received in order.
4. **Given** an SQS FIFO provider, **When** a publication and subscription are created with FIFO attributes, **Then** messages are received in order.

---

### User Story 3 - Use LocalStack with Podman for Local Test Execution (Priority: P1)

As a developer, I want to run the AWS SNS/SQS generated tests locally using Podman (not Docker) with LocalStack so that I do not need an AWS account or Docker Desktop to run the test suite.

The existing `docker-compose-localstack.yaml` starts a LocalStack container exposing SNS, SQS, STS, DynamoDB, IAM, and Scheduler services on port 4566. Tests detect LocalStack via the `LOCALSTACK_SERVICE_URL` environment variable and use fake credentials (`test`/`test`). Podman must be a first-class alternative to Docker for running this compose file.

**Why this priority**: LocalStack + Podman is the user's explicit requirement for the testing workflow.

**Independent Test**: Can be verified by starting LocalStack with `podman compose -f docker-compose-localstack.yaml up -d`, setting `LOCALSTACK_SERVICE_URL=http://localhost:4566`, and running the generated AWS tests.

**Acceptance Scenarios**:

1. **Given** Podman is installed and `podman compose -f docker-compose-localstack.yaml up -d` is executed, **When** LocalStack starts, **Then** the SNS, SQS, and STS services are available on `http://localhost:4566`.
2. **Given** `LOCALSTACK_SERVICE_URL=http://localhost:4566` is set, **When** `dotnet test` runs the generated AWS tests, **Then** all tests pass using fake credentials against LocalStack.
3. **Given** the test runner environment, **When** tests execute, **Then** each test creates and tears down its own queues/topics to avoid cross-test interference.

---

### User Story 4 - Configure test-configuration.json for AWS Transports (Priority: P2)

As a contributor, I want `test-configuration.json` files in the AWS test projects that describe the feature capabilities of each AWS transport variant so that the test generator produces the correct subset of tests.

AWS SNS/SQS supports: dead letter queues, requeue, delayed messages (SQS only), and broker existence validation. It does not natively support publish confirmations or partition keys. These flags control which Liquid templates are rendered.

**Why this priority**: Configuration drives which tests are generated — it is a prerequisite for Story 1, but lower priority because the shape of the configuration can be refined iteratively.

**Independent Test**: Can be verified by inspecting the generated output after running the generator and confirming that only applicable tests are produced (e.g., no `When_confirming_posting` test for AWS).

**Acceptance Scenarios**:

1. **Given** a `test-configuration.json` with `HasSupportToPublishConfirmation: false`, **When** the generator runs, **Then** no publish confirmation test is generated.
2. **Given** a `test-configuration.json` with `HasSupportToDeadLetterQueue: true`, **When** the generator runs, **Then** the DLQ requeue test is generated.
3. **Given** the AWS.Tests project with four gateway variants, **When** the generator runs, **Then** each variant gets its own prefixed set of generated tests.

---

### User Story 5 - Retire Handwritten Tests Replaced by Generated Tests (Priority: P3)

As a maintainer, I want to remove or deprecate the handwritten test files that are now covered by generated equivalents so that the test suite is not duplicated and maintenance cost is reduced.

**Why this priority**: Cleanup is important but should only happen after generated tests are verified to be comprehensive and passing.

**Independent Test**: Can be verified by comparing the test coverage (behavior, not line coverage) of the generated tests against the handwritten tests, and confirming all behavioral scenarios are covered before removing the handwritten files.

**Acceptance Scenarios**:

1. **Given** a list of generated tests covering a specific behavior, **When** the equivalent handwritten test exists, **Then** the handwritten test is removed.
2. **Given** handwritten tests that cover scenarios NOT present in the generated templates, **When** cleanup is performed, **Then** those handwritten tests are preserved as non-generated custom tests alongside the generated ones.

---

### Edge Cases

- What happens when LocalStack is not running but `LOCALSTACK_SERVICE_URL` is set? Tests should fail fast with a clear connection error rather than hanging.
- What happens when a test creates a FIFO queue/topic but the name does not end with `.fifo`? AWS requires the `.fifo` suffix — the provider must enforce this.
- What happens when two tests run in parallel and create resources with the same name? Each provider must generate unique queue/topic names per test instance.
- What happens when LocalStack resets mid-test-run? Tests should be independently idempotent — each test creates and tears down its own resources.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST generate Proactor and Reactor test files for four AWS transport variants: SNS Standard, SNS FIFO, SQS Standard, SQS FIFO.
- **FR-002**: System MUST produce generated tests in a `MessagingGateway/Generated/` directory within each AWS test project, following the same convention as Redis, RMQ, Kafka, and GCP test projects.
- **FR-003**: System MUST provide `IAmAMessageGatewayProactorProvider` and `IAmAMessageGatewayReactorProvider` implementations for each of the four transport variants, for both `AWS.Tests` and `AWS.V4.Tests` projects (8 provider classes total).
- **FR-004**: Each provider MUST create unique queue names and topic names per test instance to avoid cross-test interference during parallel execution.
- **FR-005**: System MUST include a `test-configuration.json` in each AWS test project that uses the `MessagingGateways` dictionary format (multiple gateway variants), with appropriate feature flags per transport variant.
- **FR-006**: AWS SNS Standard transport variant MUST set `HasSupportToPublishConfirmation: false`, `HasSupportToPartitionKey: false`, `HasSupportToDelayedMessages: false`, `HasSupportToDeadLetterQueue: true`, `HasSupportToValidateBrokerExistence: true`, `HasSupportToRequeue: true`.
- **FR-006a**: AWS SNS FIFO transport variant MUST set `HasSupportToPartitionKey: true` (maps to `MessageGroupId`), `HasSupportToPublishConfirmation: false`, `HasSupportToDelayedMessages: false`, `HasSupportToDeadLetterQueue: true`, `HasSupportToValidateBrokerExistence: true`, `HasSupportToRequeue: true`.
- **FR-007**: AWS SQS Standard transport variant MUST set `HasSupportToPublishConfirmation: false`, `HasSupportToPartitionKey: false`, `HasSupportToDelayedMessages: true`, `HasSupportToDeadLetterQueue: true`, `HasSupportToValidateBrokerExistence: true`, `HasSupportToRequeue: true`.
- **FR-007a**: AWS SQS FIFO transport variant MUST set `HasSupportToPartitionKey: true` (maps to `MessageGroupId`), `HasSupportToPublishConfirmation: false`, `HasSupportToDelayedMessages: true`, `HasSupportToDeadLetterQueue: true`, `HasSupportToValidateBrokerExistence: true`, `HasSupportToRequeue: true`.
- **FR-008**: All generated tests MUST be runnable against LocalStack using fake credentials (`test`/`test`) when the `LOCALSTACK_SERVICE_URL` environment variable is set.
- **FR-009**: The `docker-compose-localstack.yaml` MUST work with both `docker compose` and `podman compose` without modifications.
- **FR-010**: Each provider MUST properly clean up AWS resources (queues, topics, subscriptions) in its cleanup/dispose methods to prevent resource leaks in LocalStack.
- **FR-011**: Generated tests for the V4 project MUST reference V4-specific namespaces (`Paramore.Brighter.MessagingGateway.AWSSQS.V4`) and V4 provider implementations.
- **FR-012**: FIFO providers MUST ensure queue and topic names end with the `.fifo` suffix as required by AWS.
- **FR-013**: Handwritten tests that are functionally equivalent to generated tests SHOULD be removed after generated tests are verified passing. Handwritten tests covering behaviors not in the generated templates MUST be preserved.

### Key Entities

- **MessageGatewayProvider**: Per-variant implementation that creates publications, subscriptions, producers, and channels for test execution. Contains all AWS-specific configuration (endpoint URL, credentials, queue type, FIFO attributes).
- **test-configuration.json**: JSON configuration file consumed by the Test Generator tool. Defines namespace, message builder, message assertion, and a dictionary of gateway configurations with feature flags.
- **LocalStack**: Docker/Podman-hosted AWS service emulator that provides SNS, SQS, STS, IAM endpoints on a single port (4566). Used by tests via `LOCALSTACK_SERVICE_URL` environment variable.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All generated tests pass against LocalStack started via `podman compose -f docker-compose-localstack.yaml up -d` — zero test failures for both `AWS.Tests` and `AWS.V4.Tests`.
- **SC-002**: Running the test generator produces at least 10 test files per transport variant (4 variants × Proactor + Reactor = at least 80 generated test files per project).
- **SC-003**: The generated tests cover the same behavioral scenarios as the existing handwritten tests: message posting, message receiving, multiple message consumption, requeue, dead letter queue routing, infrastructure validation, and activity context propagation.
- **SC-004**: A new contributor can run the full AWS test suite locally by executing two commands: `podman compose -f docker-compose-localstack.yaml up -d` and `dotnet test tests/Paramore.Brighter.AWS.Tests/ --filter "Category=AWS"`.
- **SC-005**: The test generator can regenerate all AWS test files in under 10 seconds, enabling rapid iteration when templates change.

## Assumptions

- **Podman Compose Compatibility**: The existing `docker-compose-localstack.yaml` uses Docker Compose v3 format which is compatible with `podman compose`. No modifications to the compose file are needed for Podman.
- **LocalStack Free Tier**: All required AWS services (SNS, SQS, STS, IAM) are available in the free/community edition of LocalStack. No LocalStack Pro license is required.
- **Existing Credential Chain**: The `CredentialsChain` helper in the test projects already supports LocalStack via the `LOCALSTACK_SERVICE_URL` environment variable with fake credentials. This mechanism is reused without changes.
- **FIFO Support in LocalStack**: LocalStack supports FIFO queues and topics in its free edition. FIFO ordering guarantees are emulated.
- **Test Category**: Generated AWS tests use the `"AWS"` category trait, matching the existing convention in the handwritten tests.
- **No Template Changes**: The existing Liquid templates in `tools/Paramore.Brighter.Test.Generator/Templates/MessagingGateway/` are sufficient for AWS tests. If SQS delayed messages require template adjustments, that is in scope but assumed unlikely.
- **Generator Already Handles Multiple Gateways**: The `MessagingGateways` dictionary format (as used by `Paramore.Brighter.RMQ.Async.Tests`) supports generating prefixed test classes for multiple transport variants in a single test project.
