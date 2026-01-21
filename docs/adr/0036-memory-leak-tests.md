# 36. Memory Leak Testing Infrastructure

Date: 2026-01-12

## Status

Proposed

## Context

Brighter has received reports of memory leaks in production environments. These leaks can manifest as:
- Handler instances not being disposed after command/query processing
- DbContext instances accumulating over time
- Message producer connections (RabbitMQ, Kafka) not being properly released
- Outbox/Inbox message processing leaving objects in memory
- ServiceActivator consumer threads holding references to disposed objects
- Issues with scoped, transient and singleton lifetimes

Without systematic memory leak testing, these issues are discovered late in production, making them:
- Difficult to reproduce and diagnose
- Expensive to fix due to emergency response requirements
- Damaging to user trust and system stability
- Time-consuming to track down through manual profiling

Currently, Brighter has no automated memory leak detection in its CI/CD pipeline. The existing test suite covers functional correctness but doesn't verify memory behavior under sustained load or detect gradual memory accumulation.

Manual profiling with tools like dotMemory or PerfView is performed ad-hoc when issues are reported, but this reactive approach means:
- Issues reach production before detection
- Regression testing for memory leaks is manual and inconsistent
- Contributors don't get feedback on memory behavior during development
- No baseline exists for acceptable memory growth patterns

The WebAPI samples in `samples/WebAPI` provide realistic scenarios for testing:
- **WebAPI_EFCore/GreetingsWeb**: REST API using CommandProcessor, Darker queries, EF Core, and message publishing
- **WebAPI_EFCore/SalutationAnalytics**: Message consumer using ServiceActivator with Inbox pattern

These samples exercise the core Brighter patterns that are most susceptible to memory leaks:
- Scoped handler lifetime management
- Database connection pooling
- Message producer/consumer lifecycles
- Transactional outbox/inbox operations
- Background processing (sweeper, consumers)

## Decision

We will create a comprehensive, repeatable memory leak testing infrastructure integrated into GitHub Actions workflows using a two-tier testing strategy:

### Two-Tier Testing Strategy

**1. Quick Tests (5-10 minutes) - Run on every PR**
- Fast memory checks that catch obvious leaks
- Test handler disposal, DbContext lifecycle, connection management
- Run 500-1000 operations to detect immediate leaks
- Provide fast feedback to contributors before merge
- Use strict thresholds: 0 leaked instances, <10MB growth

**2. Soak Tests (30-60 minutes) - Run nightly on main branch**
- Long-running tests under sustained load (10k+ operations)
- Detect gradual memory accumulation over time
- Monitor memory checkpoints every 5 minutes
- Test realistic production scenarios
- Catch subtle leaks that quick tests might miss

### Tooling Decisions

**JetBrains dotMemory Unit** will be used for memory profiling and assertions because:
- Provides explicit memory assertions in unit tests
- Can check for specific leaked object types
- Tracks memory growth over test execution
- Works in CI/CD environments
- Free for open-source projects via JetBrains OSS program
- Generates `.dmw` workspace files for offline analysis
- Gracefully degrades when not available (`FailIfRunWithoutSupport = false`)

**WebApplicationFactory** (ASP.NET Core TestServer) will be used for API testing because:
- In-process testing without network overhead
- Full control over configuration and environment
- Faster test execution than external process testing
- Deterministic behavior for reproducible results
- Allows dependency injection customization

**xUnit** will continue as the test framework (existing infrastructure) with trait-based filtering:
- `[Trait("Category", "MemoryLeak")]` - Identifies all memory tests
- `[Trait("Speed", "Quick")]` - Fast tests for PR runs
- `[Trait("Speed", "Soak")]` - Long-running tests for nightly runs

### Test Project Structure

Create new test project: `tests/Paramore.Brighter.MemoryLeak.Tests`

**Infrastructure Layer:**
- `MemoryLeakTestBase.cs` - Base class with dotMemory helper methods
  - `AssertNoLeakedHandlers<T>()` - Verify handlers are disposed
  - `AssertDbContextsDisposed<T>()` - Verify DbContexts are released
  - `AssertMemoryGrowthWithinBounds()` - Check bounded memory growth
- `WebApiTestServer.cs` - WebApplicationFactory wrapper for GreetingsWeb
- `ConsumerTestHost.cs` - IHost wrapper for SalutationAnalytics consumer
- `LoadGenerator.cs` - HTTP load generation with configurable concurrency

**Quick Tests:**
- `ApiHandlerLifecycleTests.cs` - Handler disposal verification (1000 requests)
- `DbContextLifecycleTests.cs` - DbContext lifecycle checks (500 operations)
- `CommandProcessorMemoryTests.cs` - CommandProcessor memory behavior
- `MessageProducerMemoryTests.cs` - Producer connection management
- `ConsumerBasicMemoryTests.cs` - Consumer handler disposal

**Soak Tests:**
- `ApiUnderLoadTests.cs` - 30 min, 10k+ requests, memory checkpoints
- `ContinuousConsumerTests.cs` - 30 min, 10k+ messages
- `OutboxSweeperLongRunTests.cs` - Background sweeper stability

### Memory Thresholds

**Quick Tests (strict, immediate feedback):**
- Handler instances after GC: 0 (strict - no leaked handlers)
- DbContext instances after GC: 0 (strict - must be disposed)
- Memory growth per 1000 commands: < 5MB
- Memory growth per 500 API requests: < 10MB
- RabbitMQ connection objects: < 50 (pooled connections)

**Soak Tests (realistic sustained load):**
- Total memory growth over 30 minutes: < 50MB
- Memory growth per request: < 1KB (average)
- Consumer memory after 10k messages: < 100MB growth
- No unbounded linear growth pattern (stable checkpoints)

### CI/CD Integration

**Quick Tests - New job in `.github/workflows/ci.yml`:**
```yaml
memory-leak-quick:
  runs-on: ubuntu-latest
  timeout-minutes: 15
  needs: [build]
  services:
    rabbitmq:
      image: brightercommand/rabbitmq:3.13-management-delay
```
- Runs in parallel with other test jobs (postgres, mysql, etc.)
- Fails fast if memory leaks detected
- Uploads `.dmw` snapshots on failure (7 day retention)

**Soak Tests - New workflow `.github/workflows/memory-leak-soak.yml`:**
- Scheduled: Daily at 2 AM UTC
- Manual trigger via workflow_dispatch
- Push to master (on src/, samples/ changes)
- 90 minute timeout for all soak tests
- Always upload artifacts (30 day retention)
- Create GitHub issue on failure

### Package Dependencies

Add to `Directory.Packages.props`:
- `JetBrains.dotMemoryUnit` version 3.2.20220510
- `Microsoft.AspNetCore.Mvc.Testing` version per target framework (8.0/9.0/10.0)

Project references:
- `samples/WebAPI/WebAPI_EFCore/GreetingsWeb/GreetingsWeb.csproj`
- `samples/WebAPI/WebAPI_EFCore/SalutationAnalytics/SalutationAnalytics.csproj`

### Test Execution Pattern

**GC Collection Pattern:**
```csharp
// Thorough cleanup before assertions
GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();
```

**Memory Checkpoint Pattern:**
```csharp
// Soak tests: Track memory over time
var checkpoint = dotMemory.Check();
// ... operations ...
var final = dotMemory.Check();
var growth = final.TotalMemory - checkpoint.TotalMemory;
```

**Warmup Phase:**
All soak tests include warmup to stabilize baseline:
```csharp
// Warmup: 100 operations, then establish baseline
await loadGen.RunLoadAsync(100, 10);
GC.Collect();
var baseline = dotMemory.Check();
```

## Consequences

### Positive Consequences

**1. Early Detection**
- Memory leaks caught during PR review, not in production
- Contributors get immediate feedback on memory behavior
- Regression testing prevents reintroduction of fixed leaks

**2. Confidence in Releases**
- Systematic verification that handlers/contexts are properly disposed
- Proof that memory remains stable under sustained load
- Baseline metrics for acceptable memory behavior

**3. Diagnostic Capabilities**
- `.dmw` workspace files enable offline analysis with dotMemory
- Memory checkpoints show exactly when/where leaks occur
- Clear thresholds make failures actionable

**4. Documentation Through Tests**
- Tests demonstrate correct disposal patterns
- Examples of proper handler/context lifecycle management
- Load patterns document expected production behavior

**5. Contributor Experience**
- Fast feedback loop (5-10 min quick tests)
- Clear pass/fail criteria
- No manual profiling required during development

### Negative Consequences

**1. CI Time Increase**
- Quick tests add 10-15 minutes to PR workflow (run in parallel)
- Soak tests consume 90 minutes of runner time nightly
- Mitigation: Quick tests run in parallel; soak tests only on schedule

**2. Maintenance Overhead**
- Thresholds may need tuning as code evolves
- False positives require investigation and threshold adjustment
- Mitigation: Phase 4 includes threshold tuning and documentation

**3. External Dependency**
- Requires JetBrains dotMemory Unit package
- Tests gracefully skip memory assertions if unavailable
- Free OSS license from JetBrains (need to apply)

**4. Test Flakiness Risk**
- Memory measurements can vary between runs
- GC timing is non-deterministic
- Mitigation: Generous thresholds, multiple GC passes, warmup phases

**5. Learning Curve**
- Contributors need to understand memory testing concepts
- More complex than functional testing
- Mitigation: Good documentation, clear examples, helpful base classes

### Implementation Phases

**Phase 1 (Week 1): Foundation**
- Create test project structure
- Implement base classes with dotMemory helpers
- 2-3 basic quick tests working locally
- Verification: `dotnet test --filter "Speed=Quick"` passes

**Phase 2 (Week 2): Quick Tests + CI**
- Complete all 5 quick test scenarios
- Add memory-leak-quick job to ci.yml
- Tune thresholds based on real CI data
- Verification: Green check on sample PR

**Phase 3 (Week 3): Soak Tests**
- Implement 3 soak test scenarios
- Create memory-leak-soak.yml workflow
- Test with workflow_dispatch trigger
- Verification: Successful nightly run with artifacts

**Phase 4 (Week 4): Production Ready**
- Tune thresholds from real data
- Document troubleshooting procedures
- Add to CONTRIBUTING.md
- Verification: Week of clean runs

### Migration Notes for Contributors

**Running Tests Locally:**
```bash
# Quick tests (5-10 min)
dotnet test --filter "Category=MemoryLeak&Speed=Quick"

# Requires RabbitMQ (via Docker)
docker run -d -p 5672:5672 brightercommand/rabbitmq:3.13-management-delay
```

**Without dotMemory Unit:**
- Tests will still run but skip memory assertions
- Useful for quick iteration without profiling
- CI always has dotMemory Unit available

**Investigating Failures:**
- Download `.dmw` artifacts from failed CI runs
- Open in JetBrains dotMemory standalone tool
- Analyze retained objects and allocation paths

### Future Enhancements (Not in Scope)

1. **Additional Samples**: WebAPI_Dapper, Greetings_Sweeper
2. **More Transports**: Kafka, Azure Service Bus memory profiles
3. **Database Variations**: MySQL, PostgreSQL, SQL Server leaks
4. **BenchmarkDotNet Integration**: Allocation profiling per operation
5. **Historical Tracking**: Trend analysis over time
6. **Memory Reports**: HTML reports from .dmw files

### Success Criteria

The memory leak testing infrastructure will be considered successful when:
1. Quick tests provide feedback within 15 minutes on every PR
2. Soak tests run reliably every night without false positives
3. At least one real memory leak is caught before reaching production
4. Contributors understand how to write and debug memory tests
5. Memory-related issues decrease in production environments
