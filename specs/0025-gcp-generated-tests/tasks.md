# Tasks: GCP Pub/Sub Generated Tests Migration

## Phase 1: Configuration & Providers

- [ ] T001 [US1] Update `tests/Paramore.Brighter.Gcp.Tests/test-configuration.json` — Add `MessagingGateways` dictionary with `Pull` and `Stream` variants. Preserve existing `Outboxes` section. Set capability flags: DLQ=true, Requeue=true, ValidateBroker=true, PartitionKey=true, PublishConfirmation=false, DelayedMessages=false. Reference: `tests/Paramore.Brighter.RMQ.Async.Tests/test-configuration.json`

- [ ] T002 [P] [US2] Create `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/GcpPullMessageGatewayProvider.cs` — Implement `IAmAMessageGatewayProactorProvider` and `IAmAMessageGatewayReactorProvider`. Use `GatewayFactory` singleton for connection. Create `GcpPubSubSubscription` with `SubscriptionMode.Pull`. Support DLQ via `DeadLetterPolicy`, broker validation via `OnMissingChannel`, requeue, ordering keys, cleanup. Reference: `tests/Paramore.Brighter.RMQ.Async.Tests/MessagingGateway/RmqClassicMessageGatewayProvider.cs` and `tests/Paramore.Brighter.Kafka.Tests/MessagingGateway/KafkaMessageGatewayProvider.cs`. Extract setup patterns from existing manual tests in `Pull/Proactor/`.

- [ ] T003 [P] [US3] Create `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/GcpStreamMessageGatewayProvider.cs` — Same as T002 but with `SubscriptionMode.Stream`. Extract setup patterns from existing manual tests in `Stream/Proactor/`.

- [ ] T004 [P] [US1] Create `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/GcpMessageAssertion.cs` (if needed) — Check if GCP message headers need custom assertion beyond the default. If ordering keys, timestamps, or other GCP-specific headers differ, implement custom `IAmAMessageAssertion`. Reference: `tests/Paramore.Brighter.RMQ.Async.Tests/MessagingGateway/RmqMessageAssertion.cs`.

**Parallel:** T002 + T003 + T004 can run together after T001.

## Phase 2: Generate & Validate (MVP)

- [ ] T005 [US1] Run generator: `cd tests/Paramore.Brighter.Gcp.Tests && dotnet run --project ../../tools/Paramore.Brighter.Test.Generator`. Verify `MessagingGateway/Generated/Pull/{Proactor,Reactor}/` and `MessagingGateway/Generated/Stream/{Proactor,Reactor}/` directories are created with ~10 test files each (~40 total).

- [ ] T006 [US1] Build verification: `dotnet build tests/Paramore.Brighter.Gcp.Tests/`. Fix any compilation errors in providers or generated files.

- [ ] T007 [US1] Run generated tests against GCP Pub/Sub emulator. Compare results with manual tests. Fix provider issues. This is the MVP checkpoint — generated tests pass alongside manual tests.

## Phase 3: Cleanup

- [ ] T008 [US4] Delete manual messaging gateway tests: remove `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Pull/` and `tests/Paramore.Brighter.Gcp.Tests/MessagingGateway/Stream/` directories (20 files total). Keep `Helper/` and `TestDoubles/`.

- [ ] T009 [US4] Final validation: full GCP test suite run (generated messaging + generated outbox). Verify `generate-test.sh` runs cleanly. Confirm no regressions.

## Dependency Chain

```
T001 ──┬── T002 ──┐
       ├── T003 ──┤
       └── T004 ──┴── T005 → T006 → T007 → T008 → T009
```
