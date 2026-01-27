# Performance Testing - Memory Leak Detection

This specification implements comprehensive memory leak testing infrastructure for Brighter as documented in [ADR-0036: Memory Leak Testing Infrastructure](../../docs/adr/0036-memory-leak-tests.md).

## Overview

We're building a two-tier memory leak testing strategy:
- **Quick Tests** (5-10 min): Run on every PR to catch obvious leaks early
- **Soak Tests** (30-60 min): Run nightly to detect gradual memory accumulation

## Goals

1. **Early Detection**: Catch memory leaks during PR review, not in production
2. **Systematic Verification**: Ensure handlers, DbContexts, and connections are properly disposed
3. **Confidence in Releases**: Prove memory stability under sustained load
4. **Diagnostic Capabilities**: Provide actionable insights when leaks occur

## Architecture

- **Tooling**: JetBrains dotMemory Unit for memory assertions
- **Test Infrastructure**: WebApplicationFactory for in-process API testing
- **Target Samples**: WebAPI_EFCore (GreetingsWeb API + SalutationAnalytics consumer)
- **CI Integration**: New jobs in GitHub Actions workflows

## Test Coverage

### Quick Tests (PR runs)
- Handler lifecycle verification
- DbContext disposal checks
- CommandProcessor memory behavior
- Message producer connection management
- Consumer handler disposal

### Soak Tests (Nightly runs)
- API under sustained load (30 min, 10k+ requests)
- Continuous message consumption (30 min, 10k+ messages)
- Outbox sweeper stability over time

## Memory Thresholds

**Quick Tests:**
- Handler instances: 0 after GC (strict)
- DbContext instances: 0 after GC (strict)
- Memory growth per 1000 commands: < 5MB
- Memory growth per 500 requests: < 10MB
- Connection objects: < 50

**Soak Tests:**
- Total growth over 30 minutes: < 50MB
- Growth per request: < 1KB average
- Consumer growth after 10k messages: < 100MB
- No unbounded linear growth

## Implementation Status

Current phase: **Phase 1 - Foundation**

See [plan.md](./plan.md) for detailed task breakdown and progress tracking.

## Related Documents

- [ADR-0036: Memory Leak Testing Infrastructure](../../docs/adr/0036-memory-leak-tests.md)
- [Implementation Plan](./plan.md)
- [Current Task](./.currenttask)

## Quick Start

```bash
# Run quick tests locally (requires RabbitMQ)
docker run -d -p 5672:5672 brightercommand/rabbitmq:3.13-management-delay
dotnet test --filter "Category=MemoryLeak&Speed=Quick"

# Run soak tests (long-running)
dotnet test --filter "Category=MemoryLeak&Speed=Soak"
```
