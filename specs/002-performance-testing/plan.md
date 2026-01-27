# Memory Leak Testing Implementation Plan

## Overview

This plan implements the memory leak testing infrastructure described in ADR-0036. The implementation is divided into 4 phases, each with clear goals, tasks, and verification criteria.

---

## Phase 1: Foundation (Week 1)

**Goal**: Basic infrastructure and 2-3 quick tests working locally

### Tasks

#### 1.1 Package Management Setup
- [ ] Add `JetBrains.dotMemoryUnit` version 3.2.20220510 to Directory.Packages.props
- [ ] Add `Microsoft.AspNetCore.Mvc.Testing` to Directory.Packages.props (version per framework)
- [ ] Verify package resolution with `dotnet restore`

#### 1.2 Create Test Project
- [ ] Create directory: `tests/Paramore.Brighter.MemoryLeak.Tests/`
- [ ] Create `Paramore.Brighter.MemoryLeak.Tests.csproj`
  - Target frameworks: `$(BrighterTestTargetFrameworks)`
  - Package references: Microsoft.NET.Test.Sdk, xUnit, GitHubActionsTestLogger, coverlet.collector
  - Package references: JetBrains.dotMemoryUnit, Microsoft.AspNetCore.Mvc.Testing
  - Package reference: Microsoft.Extensions.TimeProvider.Testing
  - Project references: GreetingsWeb, SalutationAnalytics, Paramore.Brighter, Paramore.Brighter.ServiceActivator
- [ ] Verify project builds: `dotnet build tests/Paramore.Brighter.MemoryLeak.Tests/`

#### 1.3 Infrastructure - Base Classes
- [ ] Create directory: `tests/Paramore.Brighter.MemoryLeak.Tests/Infrastructure/`
- [ ] Implement `MemoryLeakTestBase.cs`
  - `[DotMemoryUnit(FailIfRunWithoutSupport = false)]` attribute
  - Constructor initializes dotMemory output
  - Method: `AssertNoLeakedHandlers<THandler>()`
  - Method: `AssertDbContextsDisposed<TContext>()`
  - Method: `AssertMemoryGrowthWithinBounds(Action, int iterations, long maxBytes, string name)`
  - Implement IDisposable with proper GC.Collect pattern
- [ ] Verify base class compiles

#### 1.4 Infrastructure - WebAPI Test Server
- [ ] Implement `WebApiTestServer.cs`
  - Inherit from `WebApplicationFactory<GreetingsWeb.Startup>`
  - Override `ConfigureWebHost` with test configuration
  - Configure SQLite: `Data Source=:memory:`
  - Configure RabbitMQ: `amqp://guest:guest@localhost:5672`
  - Configure environment variables: BRIGHTER_TRANSPORT=rmq, DATABASE_TYPE_ENV=sqlite
  - Helper method: `SendGreetingAsync(string name, string greeting)` returns HttpResponseMessage
  - Helper method: `CreatePersonAsync(string name)` returns HttpResponseMessage
  - Implement IDisposable
- [ ] Verify WebApiTestServer compiles

#### 1.5 Infrastructure - Load Generator
- [ ] Implement `LoadGenerator.cs`
  - Constructor takes WebApiTestServer
  - Class: `LoadTestResult` with SuccessCount, FailureCount, TotalRequests properties
  - Method: `RunLoadAsync(int totalRequests, int concurrentRequests, CancellationToken ct = default)`
  - Use SemaphoreSlim for concurrency control
  - Return LoadTestResult with statistics
- [ ] Verify LoadGenerator compiles

#### 1.6 Quick Test - Handler Lifecycle
- [ ] Create directory: `tests/Paramore.Brighter.MemoryLeak.Tests/Quick/`
- [ ] Implement `ApiHandlerLifecycleTests.cs`
  - Inherit from MemoryLeakTestBase
  - `[Trait("Category", "MemoryLeak")]` and `[Trait("Speed", "Quick")]`
  - Test: `When_processing_commands_handlers_should_be_disposed`
    - 1000 POST requests via LoadGenerator
    - Assert 0 leaked handler instances after GC
  - Test: `When_processing_commands_memory_should_not_grow_unbounded`
    - Warmup: 100 requests
    - Baseline memory checkpoint
    - Process 500 more requests
    - Assert memory growth < 10MB
- [ ] Run test locally: `dotnet test --filter "FullyQualifiedName~ApiHandlerLifecycleTests"`

#### 1.7 Quick Test - DbContext Lifecycle
- [ ] Implement `DbContextLifecycleTests.cs`
  - Inherit from MemoryLeakTestBase
  - `[Trait("Category", "MemoryLeak")]` and `[Trait("Speed", "Quick")]`
  - Test: `When_processing_database_operations_dbcontexts_should_be_disposed`
    - 500 requests that exercise DbContext
    - Assert 0 undisposed DbContext instances after GC
- [ ] Run test locally: `dotnet test --filter "FullyQualifiedName~DbContextLifecycleTests"`

#### 1.8 Phase 1 Verification
- [ ] Start RabbitMQ: `docker run -d -p 5672:5672 brightercommand/rabbitmq:3.13-management-delay`
- [ ] Run all quick tests: `dotnet test --filter "Speed=Quick"`
- [ ] Verify all tests pass
- [ ] Verify dotMemory assertions execute (check test output)
- [ ] Document any threshold adjustments needed

**Phase 1 Deliverables:**
- Test project infrastructure compiling and working
- 2 quick test scenarios passing locally
- Clear understanding of dotMemory Unit behavior
- Foundation for remaining tests

---

## Phase 2: Quick Tests + CI (Week 2)

**Goal**: All quick tests running in CI on PRs

### Tasks

#### 2.1 Quick Test - Command Processor
- [ ] Implement `CommandProcessorMemoryTests.cs`
  - Inherit from MemoryLeakTestBase
  - `[Trait("Category", "MemoryLeak")]` and `[Trait("Speed", "Quick")]`
  - Test: `When_sending_commands_via_processor_memory_remains_stable`
    - Use AssertMemoryGrowthWithinBounds helper
    - 1000 command iterations
    - Assert < 5MB growth
- [ ] Run test locally and verify pass

#### 2.2 Quick Test - Message Producer
- [ ] Implement `MessageProducerMemoryTests.cs`
  - Inherit from MemoryLeakTestBase
  - `[Trait("Category", "MemoryLeak")]` and `[Trait("Speed", "Quick")]`
  - Test: `When_publishing_events_producer_connections_should_not_leak`
    - 500 greeting posts (triggers event publishing)
    - Wait for async publishing to complete (2 seconds)
    - Assert < 50 RabbitMQ connection-related objects
- [ ] Run test locally and verify pass

#### 2.3 Quick Test - Consumer Basics
- [ ] Implement `Infrastructure/ConsumerTestHost.cs`
  - Wrap IHost for SalutationAnalytics consumer
  - Method: `StartAsync()` to start ServiceActivator
  - Method: `StopAsync()` to stop gracefully
  - Implement IDisposable
- [ ] Implement `ConsumerBasicMemoryTests.cs`
  - Inherit from MemoryLeakTestBase
  - `[Trait("Category", "MemoryLeak")]` and `[Trait("Speed", "Quick")]`
  - Test: `When_consuming_messages_handlers_should_be_disposed`
    - Start consumer host
    - Publish 500 messages via API
    - Wait for consumption
    - Assert 0 leaked consumer handler instances
- [ ] Run test locally and verify pass

#### 2.4 Verify All Quick Tests
- [ ] Run all quick tests: `dotnet test --filter "Speed=Quick"`
- [ ] Verify execution time < 10 minutes
- [ ] Document actual memory measurements vs thresholds
- [ ] Adjust thresholds if needed based on real data

#### 2.5 CI Integration - Add Quick Test Job
- [ ] Modify `.github/workflows/ci.yml`
  - Add new job `memory-leak-quick` after existing test jobs
  - `needs: [build]`
  - `timeout-minutes: 15`
  - `runs-on: ubuntu-latest`
  - Service: RabbitMQ (brightercommand/rabbitmq:3.13-management-delay)
  - Steps:
    - Checkout code
    - Setup dotnet (8.0.x, 9.0.x, 10.0.x)
    - Install dependencies: `dotnet restore`
    - Run tests: `dotnet test ./tests/Paramore.Brighter.MemoryLeak.Tests/*.csproj --filter "Category=MemoryLeak&Speed=Quick" --configuration Release --logger "console;verbosity=normal" --logger GitHubActions --blame -v n`
    - Upload memory snapshots on failure: Upload `**/*.dmw` files, 7 day retention

#### 2.6 Test CI Integration
- [ ] Push branch to GitHub
- [ ] Create draft PR to trigger CI
- [ ] Verify memory-leak-quick job appears
- [ ] Verify job completes successfully
- [ ] Verify job runs in parallel with other test jobs
- [ ] Verify execution time < 15 minutes

#### 2.7 Threshold Tuning
- [ ] Review actual memory growth from CI runs
- [ ] Adjust thresholds if there are false positives
- [ ] Document rationale for any threshold changes
- [ ] Re-run CI to verify adjustments

#### 2.8 Documentation
- [ ] Add comment to ci.yml explaining memory-leak-quick job
- [ ] Document false positive handling procedures
- [ ] Update specs/002-performance-testing/.currenttask

**Phase 2 Deliverables:**
- All 5 quick test scenarios implemented and passing
- CI job running successfully on PRs
- Thresholds tuned based on real CI data
- Clear documentation for contributors

---

## Phase 3: Soak Tests (Week 3)

**Goal**: Long-running tests working locally and in nightly workflow

### Tasks

#### 3.1 Soak Test Infrastructure
- [ ] Verify `ConsumerTestHost.cs` works for long-running scenarios
- [ ] Add memory checkpoint logging helper to MemoryLeakTestBase
  - Method: `LogMemoryCheckpoint(string label, long memoryBytes, int operationCount)`
- [ ] Test long-running consumer host locally (5 min test)

#### 3.2 Soak Test - API Under Load
- [ ] Create directory: `tests/Paramore.Brighter.MemoryLeak.Tests/Soak/`
- [ ] Implement `ApiUnderLoadTests.cs`
  - Inherit from MemoryLeakTestBase
  - `[Trait("Category", "MemoryLeak")]` and `[Trait("Speed", "Soak")]`
  - Test: `When_api_runs_under_sustained_load_memory_remains_stable`
    - `[Fact(Timeout = 3600000)]` - 60 min timeout
    - Warmup: 100 requests
    - Establish baseline memory
    - Run for 30 minutes with continuous load (1000 requests per batch, 20 concurrent)
    - Log memory checkpoints every 5 minutes
    - Assert total growth < 50MB
    - Assert growth per request < 1KB
    - Output all checkpoints to console
- [ ] Run locally with shortened duration (5 min) to verify structure
- [ ] Document expected full run time and memory patterns

#### 3.3 Soak Test - Continuous Consumer
- [ ] Implement `ContinuousConsumerTests.cs`
  - Inherit from MemoryLeakTestBase
  - `[Trait("Category", "MemoryLeak")]` and `[Trait("Speed", "Soak")]`
  - Test: `When_consumer_processes_messages_continuously_memory_remains_stable`
    - `[Fact(Timeout = 3600000)]` - 60 min timeout
    - Start consumer host
    - Establish baseline memory
    - Publish messages continuously for 30 minutes (batches of 100)
    - Wait for consumer to catch up
    - Assert total growth < 100MB
    - Log memory checkpoints
- [ ] Run locally with shortened duration (5 min) to verify structure

#### 3.4 Soak Test - Outbox Sweeper
- [ ] Implement `OutboxSweeperLongRunTests.cs`
  - Inherit from MemoryLeakTestBase
  - `[Trait("Category", "MemoryLeak")]` and `[Trait("Speed", "Soak")]`
  - Test: `When_outbox_sweeper_runs_for_extended_period_memory_remains_stable`
    - `[Fact(Timeout = 3600000)]` - 60 min timeout
    - Configure API with short sweeper interval
    - Publish messages to create outbox activity
    - Monitor memory over 30 minutes
    - Assert stable memory (no linear growth)
- [ ] Run locally with shortened duration (5 min) to verify structure

#### 3.5 Create Soak Workflow
- [ ] Create `.github/workflows/memory-leak-soak.yml`
  - Name: "Memory Leak Soak Tests"
  - Triggers:
    - Schedule: cron '0 2 * * *' (2 AM UTC daily)
    - workflow_dispatch (manual trigger)
    - Push to master on paths: src/**, samples/WebAPI/**, tests/Paramore.Brighter.MemoryLeak.Tests/**
  - Environment variables: DOTNET_SKIP_FIRST_TIME_EXPERIENCE, DOTNET_CLI_TELEMETRY_OPTOUT, DOTNET_NOLOGO
  - Job: soak-tests
    - `timeout-minutes: 90`
    - `runs-on: ubuntu-latest`
    - Services: RabbitMQ, MySQL (mariadb:latest)
    - Steps:
      - Checkout with fetch-depth: 0
      - Setup dotnet (8.0.x, 9.0.x, 10.0.x)
      - Cache NuGet packages
      - Install dependencies
      - Build Release
      - Run soak tests: `dotnet test ./tests/Paramore.Brighter.MemoryLeak.Tests/*.csproj --filter "Category=MemoryLeak&Speed=Soak" --configuration Release --no-build --logger "console;verbosity=detailed" --logger GitHubActions --blame-hang-timeout 70m -v n`
      - Upload memory snapshots (always): `**/*.dmw`, `**/*.dmp`, `**/TestResults/**`, 30 day retention
      - Upload test results (always): `**/TestResults/*.trx`, `**/TestResults/*.xml`, 30 day retention
      - Comment on failure: Create GitHub issue with title "Memory Leak Soak Test Failure"

#### 3.6 Test Soak Workflow
- [ ] Push soak workflow to GitHub
- [ ] Trigger manually via workflow_dispatch
- [ ] Monitor execution (will take 90 minutes)
- [ ] Verify all 3 soak tests complete
- [ ] Verify artifacts are uploaded
- [ ] Download and inspect artifacts

#### 3.7 Soak Test Tuning
- [ ] Review actual soak test results
- [ ] Adjust thresholds if needed
- [ ] Document memory growth patterns observed
- [ ] Verify checkpoints show stability (no linear growth)

**Phase 3 Deliverables:**
- 3 soak test scenarios implemented
- Nightly workflow created and tested
- Artifacts properly collected and retained
- Memory patterns documented

---

## Phase 4: Refinement (Week 4)

**Goal**: Production-ready with documentation and monitoring

### Tasks

#### 4.1 Final Threshold Tuning
- [ ] Review 1 week of soak test results
- [ ] Identify any false positives
- [ ] Adjust thresholds based on statistical data
- [ ] Document threshold rationale in test comments
- [ ] Verify no false positives for 3 consecutive runs

#### 4.2 Diagnostic Output Enhancement
- [ ] Add detailed memory checkpoint output to all soak tests
- [ ] Add object count diagnostics to quick tests
- [ ] Include GC collection counts in output
- [ ] Add timing information to identify slow scenarios

#### 4.3 JetBrains OSS License
- [ ] Document JetBrains dotMemory Unit OSS license requirement
- [ ] Add note to README about free OSS license
- [ ] Document link: https://www.jetbrains.com/community/opensource/
- [ ] Note that tests gracefully skip assertions without license

#### 4.4 Contributing Documentation
- [ ] Create section in CONTRIBUTING.md about memory testing
  - How to run memory leak tests locally
  - How to interpret test failures
  - How to adjust thresholds
  - How to analyze .dmw files with dotMemory
  - When to use Quick vs Soak tests
- [ ] Add examples of common memory leak patterns

#### 4.5 Troubleshooting Runbook
- [ ] Create `specs/002-performance-testing/troubleshooting.md`
  - How to investigate failed quick tests
  - How to analyze soak test artifacts
  - Common causes of memory leaks in Brighter
  - How to use dotMemory standalone tool
  - How to create reproduction test cases
  - When to adjust thresholds vs fix code

#### 4.6 Test Coverage Documentation
- [ ] Document what is covered by memory tests
- [ ] Document what is NOT covered (gaps)
- [ ] Create list of future test scenarios (Phase 5+)
- [ ] Document known limitations

#### 4.7 Monitoring Setup
- [ ] Verify soak tests run nightly without manual intervention
- [ ] Set up alerts for repeated failures (if desired)
- [ ] Document procedure for handling soak test failures
- [ ] Test GitHub issue creation on failure

#### 4.8 Final Verification
- [ ] Run complete test suite: `dotnet test --filter "Category=MemoryLeak"`
- [ ] Verify quick tests complete in < 10 minutes
- [ ] Verify soak tests complete in < 90 minutes
- [ ] Verify all artifacts are collected properly
- [ ] Review 1 week of CI results for stability

#### 4.9 Documentation Review
- [ ] Review all documentation for accuracy
- [ ] Ensure ADR is up to date
- [ ] Ensure README accurately describes current state
- [ ] Update specs/002-performance-testing/.currenttask to "Complete"

**Phase 4 Deliverables:**
- Production-ready memory leak testing infrastructure
- Comprehensive documentation for contributors
- Troubleshooting runbook for investigating failures
- 1 week of stable CI runs

---

## Success Criteria

The implementation will be considered complete when:

1. ✅ All 5 quick test scenarios pass in < 10 minutes
2. ✅ All 3 soak test scenarios pass in < 90 minutes
3. ✅ Quick tests run on every PR automatically
4. ✅ Soak tests run nightly without failures
5. ✅ Artifacts are properly collected and retained
6. ✅ Documentation enables contributors to understand and extend tests
7. ✅ At least 1 week of stable CI runs (no false positives)

---

## Progress Tracking

- **Current Phase**: Phase 1 - Foundation
- **Current Task**: See `.currenttask` file
- **Overall Progress**: 0/4 phases complete

---

## Notes

- Each phase builds on the previous phase
- Verification steps are critical - don't skip them
- Adjust thresholds based on real data, not assumptions
- Document all decisions and rationale
- Keep the team informed of progress and blockers
