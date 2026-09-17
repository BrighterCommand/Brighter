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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

/// <summary>
/// Groups the <see cref="ScopedArtefactCache.LiveCount"/>-asserting test into a collection that does not
/// run in parallel with the rest of the suite. <c>LiveCount</c> is a process-wide static counter, so any
/// other test concurrently constructing or disposing a <see cref="ScopedArtefactCache"/> would pollute the
/// delta this test measures around its own scenario. Serialising it removes that contention without
/// weakening what it asserts, mirroring <c>PumpContextDeadlockCollection</c>'s use of the same pattern for
/// a different process-wide contention hazard.
/// </summary>
[CollectionDefinition(ScopedArtefactCacheLiveCountCollection.Name, DisableParallelization = true)]
public sealed class ScopedArtefactCacheLiveCountCollection
{
    public const string Name = "ScopedArtefactCacheLiveCount";
}

// PR #4282 review finding #3 - ServiceProviderLifetimeScope.ResolveOwnedArtefactCache's fallback path
// (reached only on a hand-built host that never registered ScopedArtefactCache itself, i.e. never ran
// AddBrighter/BrighterHandlerBuilder) used LazyInitializer.EnsureInitialized's no-syncLock overload, whose
// documented contract allows the factory to run more than once under concurrent first callers - only one
// result publishes; the rest were silently discarded, never disposed, permanently inflating
// ScopedArtefactCache's own live-instance counter (a leak-detection instrument). Drives the race entirely
// through public surface: a hand-built ServiceCollection that never calls AddBrighter, so the fallback
// path is forced, with many threads released simultaneously via a Barrier to maximise contention on the
// very first Scoped resolution.
[Collection(ScopedArtefactCacheLiveCountCollection.Name)]
public class ManyThreadsRaceFirstScopedResolutionTests
{
    [Fact]
    public async Task When_many_threads_race_the_first_scoped_resolution_no_artefact_cache_should_be_leaked()
    {
        const int trials = 8;
        const int threadsPerTrial = 64;

        var baseline = ScopedArtefactCache.LiveCount;

        for (var trial = 0; trial < trials; trial++)
        {
            var services = new ServiceCollection();
            services.AddTransient<RaceHandler>();
            services.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Scoped });

            var provider = services.BuildServiceProvider();
            var factory = new ServiceProviderHandlerFactory(provider);
            var pipelineScope = factory.CreatePipelineScope();
            var lifetime = new TestLifetimeScope(pipelineScope);

            using var barrier = new Barrier(threadsPerTrial);
            var tasks = new Task[threadsPerTrial];
            for (var i = 0; i < threadsPerTrial; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    ((IAmAHandlerFactorySync)factory).Create(typeof(RaceHandler), lifetime);
                });
            }

            await Task.WhenAll(tasks);

            pipelineScope!.Dispose();
        }

        // Assert - every ScopedArtefactCache this scenario constructed, across every trial, was disposed;
        // none survived as an undisposed loser of the first-resolution race
        Assert.Equal(baseline, ScopedArtefactCache.LiveCount);
    }

    private class RaceHandler : RequestHandler<RaceCommand>
    {
        public override RaceCommand Handle(RaceCommand command) => command;
    }

    private class RaceCommand : Command
    {
        public RaceCommand() : base(Guid.NewGuid()) { }
    }

    private class TestLifetimeScope : IAmALifetime
    {
        public TestLifetimeScope(IAmAScope? pipelineScope = null) => PipelineScope = pipelineScope;
        public IAmAScope? PipelineScope { get; }
        public void Add(IHandleRequests instance) { }
        public void Add(IHandleRequestsAsync instance) { }
        public void Dispose() => PipelineScope?.Dispose();
    }
}
