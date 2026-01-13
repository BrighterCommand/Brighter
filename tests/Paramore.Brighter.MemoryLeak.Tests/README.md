# Memory Leak Tests for Brighter

This project contains memory leak detection tests for the Brighter command processor and messaging framework.

## Overview

These tests verify that Brighter components properly manage memory under load:
- **Handlers** are disposed after processing
- **DbContext** instances don't accumulate
- **Message connections** are properly pooled
- **Memory growth** stays within acceptable bounds

For architectural details, see [ADR-0036: Memory Leak Testing Infrastructure](../../docs/adr/0036-memory-leak-tests.md).

## Quick Start

### Prerequisites

- Docker and Docker Compose
- .NET 9.0 SDK
- 8GB+ RAM recommended

### Option 1: Using Existing Docker Compose (Recommended)

```bash
# From repository root
docker compose -f docker-compose-rmq.yaml up -d

# Verify RabbitMQ is running
docker compose -f docker-compose-rmq.yaml ps

# Run quick tests (from repository root)
dotnet test tests/Paramore.Brighter.MemoryLeak.Tests/ --filter "Speed=Quick"

# Cleanup
docker compose -f docker-compose-rmq.yaml down
```

### Option 2: Using Test-Specific Docker Compose

```bash
# From this directory
cd tests/Paramore.Brighter.MemoryLeak.Tests

# Start dependencies
docker compose up -d

# Run tests
dotnet test --filter "Category=MemoryLeak"

# Cleanup (including volumes)
docker compose down -v
```

## Test Categories

### Quick Tests (`Speed=Quick`)
Fast tests (5-10 minutes total) that run on every PR:

- **ApiHandlerLifecycleTests** - Verifies handlers are disposed (1000 requests)
- **DbContextLifecycleTests** - Checks for DbContext leaks (500 operations)

Run only quick tests:
```bash
dotnet test --filter "Speed=Quick"
```

### Soak Tests (`Speed=Soak`)
Long-running tests (30-60 minutes) for nightly runs:

- **ApiUnderLoadTests** - API stability over 30 minutes (10k+ requests)
- **ContinuousConsumerTests** - Message consumer stability
- **OutboxSweeperLongRunTests** - Background processing stability

Run only soak tests:
```bash
dotnet test --filter "Speed=Soak"
```

## Understanding Test Results

### Successful Test Output
```
Memory Leak Test: 1000 total, 1000 success, 0 failure (100.0% success rate)
Checking for leaked AddGreetingHandlerAsync instances...
Checking for leaked AddPersonHandlerAsync instances...
Final memory: 45,234,567 bytes
Total growth: 2,345,678 bytes
Growth per request: 2,345 bytes
✓ Passed When_processing_commands_handlers_should_be_disposed
```

### Test Failure Indicators
- **Leaked handlers**: Handler instances remain after GC
- **Undisposed DbContext**: DbContext objects not released
- **Excessive memory growth**: Growth exceeds thresholds (>10MB for quick tests)
- **Low success rate**: < 90% of requests succeeded (indicates infrastructure issues)

## Troubleshooting

### RabbitMQ Connection Failures

**Symptom**: Tests fail with connection errors

**Solution**:
```bash
# Check if RabbitMQ is running
docker ps | grep rabbitmq

# Check RabbitMQ logs
docker logs brighter-memory-test-rmq

# Verify RabbitMQ is accessible
curl http://localhost:15672  # Management UI
```

**Common Issues**:
- Port 5672 or 15672 already in use
- RabbitMQ container failed to start
- Firewall blocking connections

### High Memory Growth

**Symptom**: Tests fail with "Memory grew by X bytes, exceeds threshold"

**Solutions**:
1. **First run warmup**: Run tests twice - first run includes JIT compilation
   ```bash
   dotnet test --filter "Speed=Quick"  # Warmup
   dotnet test --filter "Speed=Quick"  # Actual measurement
   ```

2. **Check for actual leaks**: Look for "leaked X instances" messages
3. **Review threshold**: Growth thresholds are in test source code

### Tests Timing Out

**Symptom**: Tests hang or timeout

**Solutions**:
- Verify RabbitMQ is responsive: `docker logs brighter-memory-test-rmq`
- Check system resources (CPU, memory, disk)
- Reduce concurrent requests in tests if system is constrained

### dotMemory Unit Not Available

**Expected behavior**: Tests will still run but skip memory-specific assertions

**Output**:
```
dotMemory Unit not available - skipping handler leak check
```

**To enable dotMemory assertions**:
- Install JetBrains dotMemory Unit NuGet package (already in project)
- For CI: Package is included automatically
- For local: Package should restore with `dotnet restore`

## Test Architecture

### Infrastructure Components

- **MemoryLeakTestBase** - Base class with memory assertion helpers
- **WebApiTestServer** - WebApplicationFactory wrapper for in-process API testing
- **LoadGenerator** - Generates concurrent HTTP requests
- **ConsumerTestHost** - Wrapper for message consumer testing (future)

### How Tests Work

1. **Setup**: Start WebApplicationFactory with test configuration
2. **Warmup**: Run initial requests to stabilize JIT/caches
3. **Baseline**: Take memory snapshot after GC
4. **Load**: Execute test operations (100-1000+ requests)
5. **Cleanup**: Force garbage collection
6. **Assert**: Check for leaked objects and memory growth

### Memory Thresholds

**Quick Tests**:
- Handler instances: 0 after GC (strict)
- DbContext instances: 0 after GC (strict)
- Memory growth: < 10MB per 500-1000 requests

**Soak Tests**:
- Total growth: < 50MB over 30 minutes
- Per-request growth: < 1KB average
- No unbounded linear growth pattern

## CI/CD Integration

### GitHub Actions

Tests run automatically in CI:

**On Every PR**:
- Quick tests run in parallel with other test jobs
- Timeout: 15 minutes
- RabbitMQ provided via GitHub Actions services

**Nightly (Scheduled)**:
- Soak tests run at 2 AM UTC
- Timeout: 90 minutes
- Artifacts uploaded for analysis

### Artifacts

When tests fail, memory snapshots are uploaded:
- `.dmw` files - dotMemory workspace files
- `.dmp` files - Process dumps (if applicable)
- Test results (`.trx`, `.xml`)

Analyze with JetBrains dotMemory standalone tool.

## Development Workflow

### Adding New Tests

1. Create test class in `Quick/` or `Soak/` directory
2. Inherit from `MemoryLeakTestBase`
3. Add traits: `[Trait("Category", "MemoryLeak")]` and `[Trait("Speed", "Quick|Soak")]`
4. Use base class helpers: `AssertNoLeakedHandlers<T>()`, `AssertDbContextsDisposed<T>()`
5. Follow naming convention: `When_[scenario]_should_[assertion]`

Example:
```csharp
[Trait("Category", "MemoryLeak")]
[Trait("Speed", "Quick")]
public class MyMemoryTests : MemoryLeakTestBase
{
    [Fact]
    public async Task When_doing_something_memory_should_not_leak()
    {
        using var server = new WebApiTestServer();
        var loadGen = new LoadGenerator(server);

        await loadGen.RunLoadAsync(totalRequests: 500, concurrentRequests: 10);

        ForceGarbageCollection();

        AssertNoLeakedHandlers<MyHandler>();
    }
}
```

### Tuning Thresholds

Thresholds are defined in test source code. To adjust:

1. Run tests multiple times to establish baseline
2. Account for legitimate growth (caches, connection pools)
3. Update threshold values in test assertions
4. Document rationale in test comments

## Related Documentation

- [ADR-0036: Memory Leak Testing Infrastructure](../../docs/adr/0036-memory-leak-tests.md) - Architecture decision
- [Implementation Plan](../../specs/002-performance-testing/plan.md) - Detailed task breakdown
- [Project Specifications](../../specs/002-performance-testing/README.md) - Project overview

## Support

For issues or questions:
1. Check troubleshooting section above
2. Review test output for specific error messages
3. Check GitHub Actions logs for CI failures
4. Review ADR-0036 for design decisions
