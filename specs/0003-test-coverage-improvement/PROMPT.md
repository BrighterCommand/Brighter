# Test Coverage Improvement - Session Resume Prompt

## Context

This is the test coverage improvement specification for the Brighter framework. The goal is to systematically improve test coverage across the core assemblies using a TDD workflow with user approval for each test.

## Current State

**Spec ID**: 0003
**Status**: Tasks expanded, ready for implementation
**Phase**: All phases defined, awaiting approval to begin implementation
**Last Updated**: Session 2 - Added Phases 8 & 9 based on coverage analysis

### What Was Done This Session

1. **Reviewed existing tasks.md** - 102 tests across 7 phases
2. **Analyzed HTML coverage report** - Identified additional 0% coverage classes not in original plan
3. **Added Phase 8** - "Additional 0% Coverage Classes" with 29 new tests covering:
   - Newtonsoft JSON converters (NChannelNameConverter, NPartitionKeyConverter, etc.)
   - Policy handlers (TimeoutPolicyHandler, TimeoutPolicyAttribute)
   - Logging handlers (RequestLoggingHandlerAsync, RequestLoggingAsyncAttribute)
   - Null implementations (NullLuggageStore, NullOutboxArchiveProvider)
   - Transform attributes (CloudEventsAttribute, CompressAttribute, etc.)
   - Empty message transforms (EmptyMessageTransform, EmptyMessageTransformAsync)
   - Handler collections (RequestHandlers<T>, AsyncRequestHandlers<T>)
   - Other 0% classes (ChannelNameConverter, RoutingKeys, MessageTelemetry, etc.)
4. **Added Phase 9** - "Low Coverage Improvements" with 11 new tests covering:
   - ChannelName (27% → 80%)
   - TaskExtensions (14.8% → 70%)
   - BaggageConverter (47.1% → 80%)
   - DbSystemExtensions (8.6% → 60%)
5. **Updated effort estimates** - Total now 142 tests, 23-32 days estimated effort

### Coverage Analysis Complete

We have generated coverage reports using Coverlet and ReportGenerator:

| Assembly | Line Coverage |
|----------|---------------|
| Paramore.Brighter | 64.7% |
| Paramore.Brighter.Extensions.DependencyInjection | 10.6% |
| Paramore.Brighter.ServiceActivator | 70.2% |
| **Overall** | **62.4%** |

Coverage reports are located in: `specs/0003-test-coverage-improvement/coverage-reports/html/`

### Key Documents

1. **requirements.md** - Full analysis of coverage gaps and test requirements
2. **tasks.md** - 142 test classes organized into 9 phases with TDD workflow
3. **coverage-reports/html/index.html** - Detailed HTML coverage report

### Test Phases (from tasks.md)

| Phase | Focus Area | Test Classes | Priority | Status |
|-------|------------|--------------|----------|--------|
| 1 | Core Value Types | 15 | P1 - Critical | Pending |
| 2 | Builders & Configuration | 8 | P2 - High | Pending |
| 3 | Extension Methods | 10 | P3 - Medium | Pending |
| 4 | JSON Converters | 12 | P3 - Medium | Pending |
| 5 | In-Memory Components | 18 | P4 - Medium | Pending |
| 6 | DI Extensions | 17 | P5 - Lower | Pending |
| 7 | Observability & Misc | 22 | P3 - Medium | Pending |
| 8 | Additional 0% Coverage | 29 | P3 - Medium | **NEW** |
| 9 | Low Coverage Improvements | 11 | P4 - Lower | **NEW** |

**Total: 142 test classes | Estimated effort: 23-32 days**

### Target Test Projects (Group 1 - No external dependencies)

- `Paramore.Brighter.Core.Tests`
- `Paramore.Brighter.Extensions.Tests`
- `Paramore.Brighter.InMemory.Tests`

### Target Assemblies

- `Paramore.Brighter`
- `Paramore.Brighter.Extensions.DependencyInjection`
- `Paramore.Brighter.Extensions.Diagnostics`
- `Paramore.Brighter.ServiceActivator`
- `Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection`
- `Paramore.Brighter.ServiceActivator.Extensions.Diagnostics`
- `Paramore.Brighter.ServiceActivator.Extensions.Hosting`

## Critical Coverage Gaps (0% coverage)

### Paramore.Brighter
- `AsyncRequestHandlers<T>`, `RequestHandlers<T>`
- `DateTimeOffsetExtensions`, `ChannelNameConverter`
- `InMemoryTransactionProvider`
- `MessageTelemetry`
- `NJsonConverters.*` (all Newtonsoft converters)
- `Observability.*` (BrighterMetricsFromTracesProcessor, DbMeter, MessagingMeter, etc.)
- `Policies.Handlers.TimeoutPolicyHandler<T>`
- `RelationalDatabase*` classes
- `Transforms.Attributes.*` (CloudEventsAttribute, CompressAttribute, etc.)
- `Transforms.Storage.NullLuggageStore`
- `EmptyMessageTransform`, `EmptyMessageTransformAsync`
- `RequestLoggingHandlerAsync<T>`, `RequestLoggingAsyncAttribute`

### Low Coverage Classes (Now in Phase 9)
- `ChannelName` - 27% coverage
- `TaskExtensions` - 14.8% coverage
- `BaggageConverter` - 47.1% coverage
- `DbSystemExtensions` - 8.6% coverage

### Paramore.Brighter.Extensions.DependencyInjection (10.6% overall)
- `ServiceCollectionBrighterBuilder`
- `ServiceCollectionExtensions`
- `ServiceProviderTransformerFactory*`
- `UseRpc`

## TDD Workflow

Each test follows this workflow:
1. **RED**: Write failing test
2. **APPROVAL**: Stop and wait for user review
3. **GREEN**: Run test (should pass for existing code)
4. **REFACTOR**: Improve if needed

**Important constraints**:
- No `InternalsVisibleTo` - only test public APIs
- No promoting internal classes to public for testing
- May need to expose object state for verification

## Next Steps

1. **Approve updated tasks.md** - Review the 142 proposed tests (40 new since last session)
2. **Select starting phase** - Recommend Phase 1 (Core Value Types) as foundation
3. **Begin TDD workflow** - Write first test, await approval, proceed

## Commands

```bash
# View coverage report
open specs/0003-test-coverage-improvement/coverage-reports/html/index.html

# Re-run coverage analysis
cd /Users/ian.cooper/CSharpProjects/github/BrighterCommand/test_coverage
dotnet test tests/Paramore.Brighter.Core.Tests/ tests/Paramore.Brighter.Extensions.Tests/ tests/Paramore.Brighter.InMemory.Tests/ --collect:"XPlat Code Coverage" --results-directory:specs/0003-test-coverage-improvement/coverage-reports

# Build solution
dotnet build Brighter.slnx

# Run tests
dotnet test tests/Paramore.Brighter.Core.Tests/
```

## Session Continuation Prompt

To continue this work, say:

> "Let's continue with the test coverage improvement spec (0003). We need to [approve tasks / start Phase X / review coverage gaps]."

Or to start implementation:

> "Let's start implementing Phase 1 tests. Begin with the first task in tasks.md."

## Session History

| Session | Date | Actions |
|---------|------|---------|
| 1 | - | Created requirements.md, tasks.md with 102 tests across 7 phases |
| 2 | - | Analyzed coverage report, added Phase 8 (29 tests) and Phase 9 (11 tests), total now 142 tests |
