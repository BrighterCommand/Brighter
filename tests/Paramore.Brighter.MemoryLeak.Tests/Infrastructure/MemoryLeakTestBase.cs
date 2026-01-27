using System;
using JetBrains.dotMemoryUnit;
using Xunit;

namespace Paramore.Brighter.MemoryLeak.Tests.Infrastructure;

/// <summary>
/// Base class for memory leak tests providing dotMemory assertion helpers.
/// Tests gracefully skip memory assertions if dotMemory Unit is not available.
/// </summary>
[DotMemoryUnit(FailIfRunWithoutSupport = false)]
public abstract class MemoryLeakTestBase : IDisposable
{
    protected MemoryLeakTestBase()
    {
        // dotMemory Unit will handle initialization automatically
        Console.WriteLine("MemoryLeakTestBase initialized");
    }

    /// <summary>
    /// Asserts that no instances of the specified handler type remain in memory after GC.
    /// </summary>
    /// <typeparam name="THandler">The handler type to check for leaks</typeparam>
    protected void AssertNoLeakedHandlers<THandler>()
    {
        dotMemory.Check(memory =>
        {
            var survivors = memory.GetObjects(where => where.Type.Is<THandler>());
            Assert.True(
                survivors.ObjectsCount == 0,
                $"Found {survivors.ObjectsCount} leaked {typeof(THandler).Name} instances. " +
                $"Handlers should be disposed after request processing."
            );
        });
    }

    /// <summary>
    /// Asserts that no DbContext instances of the specified type remain in memory after GC.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type to check for leaks</typeparam>
    protected void AssertDbContextsDisposed<TContext>()
    {
        // Force thorough garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        dotMemory.Check(memory =>
        {
            var contexts = memory.GetObjects(where => where.Type.Is<TContext>());
            Assert.True(
                contexts.ObjectsCount == 0,
                $"Found {contexts.ObjectsCount} undisposed {typeof(TContext).Name} instances. " +
                $"DbContexts should be properly disposed after use."
            );
        });
    }

    /// <summary>
    /// Asserts that memory growth stays within acceptable bounds over repeated operations.
    /// Uses GC.GetTotalMemory for measurements as it's more reliable than dotMemory snapshots.
    /// </summary>
    /// <param name="operation">The operation to execute repeatedly</param>
    /// <param name="iterations">Number of times to execute the operation</param>
    /// <param name="maxGrowthBytes">Maximum acceptable memory growth in bytes</param>
    /// <param name="operationName">Name of the operation for error messages</param>
    protected void AssertMemoryGrowthWithinBounds(
        Action operation,
        int iterations,
        long maxGrowthBytes,
        string operationName)
    {
        // Force GC and get baseline
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var initialBytes = GC.GetTotalMemory(false);
        Console.WriteLine($"{operationName}: Initial memory = {initialBytes:N0} bytes");

        // Execute operations with periodic GC
        for (int i = 0; i < iterations; i++)
        {
            operation();

            // Periodic GC to prevent false positives from gen0/gen1 accumulation
            if (i % 100 == 0 && i > 0)
            {
                GC.Collect(0, GCCollectionMode.Optimized);
            }
        }

        // Final thorough GC before measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalBytes = GC.GetTotalMemory(false);
        var growth = finalBytes - initialBytes;
        var growthPerIteration = iterations > 0 ? growth / iterations : 0;

        Console.WriteLine($"{operationName}: Final memory = {finalBytes:N0} bytes");
        Console.WriteLine($"{operationName}: Total growth = {growth:N0} bytes");
        Console.WriteLine($"{operationName}: Growth per iteration = {growthPerIteration:N0} bytes");

        Assert.True(
            growth < maxGrowthBytes,
            $"{operationName}: Memory grew by {growth:N0} bytes over {iterations} iterations, " +
            $"exceeds threshold of {maxGrowthBytes:N0} bytes. " +
            $"Average growth per iteration: {growthPerIteration:N0} bytes."
        );
    }

    /// <summary>
    /// Performs thorough garbage collection to ensure accurate memory measurements.
    /// Call this before taking memory snapshots.
    /// </summary>
    protected void ForceGarbageCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    /// <summary>
    /// Disposes resources and performs final garbage collection.
    /// </summary>
    public virtual void Dispose()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
