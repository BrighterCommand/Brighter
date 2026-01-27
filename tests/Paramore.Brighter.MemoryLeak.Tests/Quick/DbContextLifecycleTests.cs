using System;
using System.Threading.Tasks;
using GreetingsApp.EntityGateway;
using JetBrains.dotMemoryUnit;
using Paramore.Brighter.MemoryLeak.Tests.Infrastructure;
using Xunit;

namespace Paramore.Brighter.MemoryLeak.Tests.Quick;

/// <summary>
/// Tests to verify that DbContext instances are properly disposed after database operations.
/// DbContext leaks are a common source of memory and connection pool exhaustion.
/// </summary>
[Trait("Category", "MemoryLeak")]
[Trait("Speed", "Quick")]
public class DbContextLifecycleTests : MemoryLeakTestBase
{
    [Fact]
    [DotMemoryUnit(CollectAllocations = true)]
    public async Task When_processing_database_operations_dbcontexts_should_be_disposed()
    {
        // Arrange
        using var server = new WebApiTestServer();
        var loadGen = new LoadGenerator(server);

        // Act - Process 500 requests that exercise DbContext
        // Each request creates a person (INSERT) and adds a greeting (INSERT + query)
        Console.WriteLine("Sending 500 requests to exercise DbContext...");
        var result = await loadGen.RunLoadAsync(totalRequests: 500, concurrentRequests: 10, cancellationToken: TestContext.Current.CancellationToken);

        Console.WriteLine($"Load test result: {result}");

        // Force GC to collect any unreferenced DbContext instances
        ForceGarbageCollection();

        // Assert - No DbContext instances should remain in memory
        // The GreetingsEntityGateway is the DbContext used by the application
        Console.WriteLine("Checking for leaked GreetingsEntityGateway (DbContext) instances...");
        AssertDbContextsDisposed<GreetingsEntityGateway>();

        // Verify we actually processed requests successfully
        Assert.True(result.SuccessCount > 450,
            $"Expected > 450 successful requests but got {result.SuccessCount}. " +
            $"Test may not be exercising DbContext properly.");
    }
}
