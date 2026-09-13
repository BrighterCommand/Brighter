#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

// A Scoped artefact cache keys its resolutions by type behind a Lazy<object?>. A Lazy's default mode
// publishes and remembers a thrown exception, so - left unguarded - a transient failure (a database
// blip, a momentarily-unavailable dependency) would be remembered for the rest of the pipeline's scope,
// turning one bad resolution into a permanent one. This is one cache used by two different acquisition
// routes - a pipeline's own scope (owned) and an ambient scope adopted from the caller (borrowed) - and
// the fix belongs to the cache itself, so both routes must be proven to benefit from it, not just one.
public class ScopedArtefactCacheFaultEvictionTests
{
    [Fact]
    public void When_an_owned_scopes_artefact_resolution_throws_a_later_resolution_in_the_same_pipeline_resolves_again()
    {
        // Arrange - a Scoped mapper whose first construction attempt always throws. No ambient is
        // established, so CreatePipelineScope() returns Brighter's own owned scope
        var state = new FlakyResolutionState();
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddScoped<FlakyOnFirstResolutionMapper>();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped,
            MapperLifetime = ServiceLifetime.Scoped,
            TransformerLifetime = ServiceLifetime.Scoped
        });
        var rootProvider = services.BuildServiceProvider();
        var factory = new ServiceProviderMapperFactory(rootProvider);
        var scope = factory.CreatePipelineScope()!;

        // Act - the first resolution faults; a second resolution of the same type, through the same
        // pipeline scope, follows it
        var firstAttempt = Record.Exception(() => factory.Create(typeof(FlakyOnFirstResolutionMapper), scope));
        var lease = factory.Create(typeof(FlakyOnFirstResolutionMapper), scope);

        // Assert - the first attempt threw the mapper's own exception, and the fault was not
        // remembered: the second attempt resolved a fresh instance instead of rethrowing it
        Assert.IsType<InvalidOperationException>(firstAttempt);
        Assert.NotNull(lease);
        Assert.IsType<FlakyOnFirstResolutionMapper>(lease!.Instance);
    }

    [Fact]
    public void When_a_borrowed_scopes_artefact_resolution_throws_a_later_resolution_in_the_same_pipeline_resolves_again()
    {
        // Arrange - the same flaky mapper, this time resolved through an ambient scope a non-ASP.NET
        // host offers, so CreatePipelineScope() adopts it (borrowed) instead of owning one
        var state = new FlakyResolutionState();
        var scopeProvider = new AsyncLocalScopeProvider();
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddScoped<FlakyOnFirstResolutionMapper>();
        services.AddSingleton<IAmAScopeProvider>(scopeProvider);
        services.AddBrighter(options =>
        {
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Scoped;
            options.TransformerLifetime = ServiceLifetime.Scoped;
            options.DefaultScopeAffinity = ScopeAffinity.JoinAmbient;
        });
        var rootProvider = services.BuildServiceProvider();
        var factory = new ServiceProviderMapperFactory(rootProvider);

        using var ambientScope = rootProvider.CreateScope();
        scopeProvider.Establish(new AsyncLocalAmbientScope(ambientScope.ServiceProvider));
        var scope = factory.CreatePipelineScope()!;

        // Act - the first resolution faults; a second resolution of the same type, through the same
        // borrowed pipeline scope, follows it
        var firstAttempt = Record.Exception(() => factory.Create(typeof(FlakyOnFirstResolutionMapper), scope));
        var lease = factory.Create(typeof(FlakyOnFirstResolutionMapper), scope);

        scopeProvider.Clear();

        // Assert - same outcome as the owned path: the fault propagated once and was not remembered
        Assert.IsType<InvalidOperationException>(firstAttempt);
        Assert.NotNull(lease);
        Assert.IsType<FlakyOnFirstResolutionMapper>(lease!.Instance);
    }

    [Fact]
    public async Task When_concurrent_resolvers_race_a_fault_a_healthy_resolution_published_in_between_is_never_lost()
    {
        // Arrange - a bare cache, exercised directly: several "losing" resolvers share one faulted
        // resolution, while several "retrying" resolvers race both each other and the losers' own
        // fault-eviction to publish and observe the first healthy result. An implementation that evicts
        // unconditionally (rather than only its own now-stale entry) can wipe out a healthy resolution a
        // concurrent resolver already published, so this needs a different arrangement from the owned
        // and borrowed facts above: real concurrency, not a second sequential call
        var cache = new ScopedArtefactCache();
        var artefactType = typeof(object);
        const int loserCount = 4;
        var startBarrier = new Barrier(loserCount + 1);
        var factoryEntered = new ManualResetEventSlim(false);
        var releaseGate = new ManualResetEventSlim(false);
        var successfulInstances = new ConcurrentBag<object>();

        object FaultingFactory()
        {
            factoryEntered.Set();
            releaseGate.Wait();
            throw new InvalidOperationException("first generation always faults");
        }

        object SucceedingFactory()
        {
            var instance = new object();
            successfulInstances.Add(instance);
            return instance;
        }

        object ResolveRetrying()
        {
            for (var attempt = 0; attempt < 100_000; attempt++)
            {
                try
                {
                    return cache.GetOrAdd(artefactType, SucceedingFactory)!;
                }
                catch (InvalidOperationException)
                {
                }
            }

            throw new TimeoutException("ResolveRetrying did not observe a healthy resolution within its attempt budget");
        }

        // Act - four losers all racing the one faulted resolution, synchronised past their own
        // start so none arrives late enough to open a second, unrelated fault generation; once the
        // fault is genuinely established and mid-flight, four retrying resolvers join, so their own
        // republish races the losers' cleanup of the stale entry
        var losers = Enumerable.Range(0, loserCount)
            .Select(_ => Task.Run(() =>
            {
                startBarrier.SignalAndWait();
                return Record.Exception(() => cache.GetOrAdd(artefactType, FaultingFactory));
            }))
            .ToArray();
        startBarrier.SignalAndWait();

        factoryEntered.Wait();
        var retryingResolvers = Enumerable.Range(0, 4).Select(_ => Task.Run(ResolveRetrying)).ToArray();
        releaseGate.Set();

        var faults = await Task.WhenAll(losers);
        var resolved = await Task.WhenAll(retryingResolvers);

        // Assert - every loser saw the fault
        Assert.All(faults, fault => Assert.IsType<InvalidOperationException>(fault));

        // Assert - exactly one healthy instance was ever created: every retrying resolver, and a
        // further resolution afterward, all observe that same instance - no loser's cleanup deleted it
        var healthyInstance = Assert.Single(successfulInstances);
        Assert.All(resolved, instance => Assert.Same(healthyInstance, instance));
        Assert.Same(healthyInstance, cache.GetOrAdd(artefactType, SucceedingFactory));
    }
}
