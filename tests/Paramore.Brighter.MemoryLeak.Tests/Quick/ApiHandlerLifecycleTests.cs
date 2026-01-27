using System;
using System.Threading.Tasks;
using GreetingsApp.Handlers;
using JetBrains.dotMemoryUnit;
using Paramore.Brighter.MemoryLeak.Tests.Infrastructure;
using Xunit;

namespace Paramore.Brighter.MemoryLeak.Tests.Quick;

/// <summary>
/// Tests to verify that API handlers are properly disposed after request processing.
/// Handler leaks are a common source of memory leaks in long-running services.
/// </summary>
[Trait("Category", "MemoryLeak")]
[Trait("Speed", "Quick")]
public class ApiHandlerLifecycleTests : MemoryLeakTestBase
{
    [Fact]
    [DotMemoryUnit(CollectAllocations = true)]
    public async Task When_processing_commands_handlers_should_be_disposed()
    {
        // Arrange
        using var server = new WebApiTestServer();
        var loadGen = new LoadGenerator(server);

        // Act - Process 1000 commands that trigger handler instantiation
        Console.WriteLine("Sending 1000 requests to exercise command handlers...");
        var result = await loadGen.RunLoadAsync(totalRequests: 1000, concurrentRequests: 10, cancellationToken: TestContext.Current.CancellationToken);

        Console.WriteLine($"Load test result: {result}");

        // Force GC to collect any unreferenced handlers
        ForceGarbageCollection();

        // Assert - No handler instances should remain in memory
        Console.WriteLine("Checking for leaked AddGreetingHandlerAsync instances...");
        AssertNoLeakedHandlers<AddGreetingHandlerAsync>();

        Console.WriteLine("Checking for leaked AddPersonHandlerAsync instances...");
        AssertNoLeakedHandlers<AddPersonHandlerAsync>();

        // Verify we actually processed requests successfully
        Assert.True(result.SuccessCount > 900,
            $"Expected > 900 successful requests but got {result.SuccessCount}. " +
            $"Test may not be exercising handlers properly.");
    }

    [Fact]
    [DotMemoryUnit(CollectAllocations = true)]
    public async Task When_processing_commands_memory_should_not_grow_unbounded()
    {
        // Arrange
        using var server = new WebApiTestServer();
        var loadGen = new LoadGenerator(server);

        // Warmup to stabilize memory baseline
        Console.WriteLine("Warming up with 100 requests...");
        await loadGen.RunLoadAsync(totalRequests: 100, concurrentRequests: 10, cancellationToken: TestContext.Current.CancellationToken);

        ForceGarbageCollection();

        // Take baseline measurement
        var initialBytes = GC.GetTotalMemory(false);
        Console.WriteLine($"Baseline memory: {initialBytes:N0} bytes");

        // Act - Process 500 more commands
        Console.WriteLine("Processing 500 requests to measure memory growth...");
        var result = await loadGen.RunLoadAsync(totalRequests: 500, concurrentRequests: 10, cancellationToken: TestContext.Current.CancellationToken);

        Console.WriteLine($"Load test result: {result}");

        // Force GC to collect temporary objects
        ForceGarbageCollection();

        // Measure final memory
        var finalBytes = GC.GetTotalMemory(false);
        var growth = finalBytes - initialBytes;
        var growthPerRequest = result.TotalRequests > 0 ? growth / result.TotalRequests : 0;

        Console.WriteLine($"Final memory: {finalBytes:N0} bytes");
        Console.WriteLine($"Total growth: {growth:N0} bytes");
        Console.WriteLine($"Growth per request: {growthPerRequest:N0} bytes");

        // Assert - Memory growth should be < 10MB for 500 requests
        // This allows for some legitimate growth (caches, etc.) but catches major leaks
        Assert.True(growth < 10 * 1024 * 1024,
            $"Memory grew by {growth:N0} bytes ({growth / 1024 / 1024}MB) over {result.TotalRequests} requests, " +
            $"exceeds 10MB threshold. Average growth per request: {growthPerRequest:N0} bytes.");

        // Verify we actually processed requests successfully
        Assert.True(result.SuccessCount > 450,
            $"Expected > 450 successful requests but got {result.SuccessCount}");
    }
}
