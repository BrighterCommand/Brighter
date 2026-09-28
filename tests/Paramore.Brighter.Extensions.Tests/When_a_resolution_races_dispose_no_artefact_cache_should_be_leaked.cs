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

// PR #4282 review finding #2 (final re-review pass) - dbe70e0b fixed a resolver-vs-resolver race on
// ServiceProviderLifetimeScope.ResolveOwnedArtefactCache's fallback path, but a second, different race
// remained: resolver-vs-Dispose. All four teardown paths read _ownedFallbackCache directly rather than
// claiming it atomically the way _scope is claimed, so a resolver thread that reads _ownedFallbackCache as
// null (not yet published), is preempted, and a concurrent Dispose() runs to completion in that window
// (sees _ownedFallbackCache still null, so its own cleanup is a no-op) - then when the resolver resumes and
// publishes its own newly-created cache, nothing will ever dispose it, since Dispose() already ran its
// one-shot teardown. Drives the race entirely through public surface: a hand-built ServiceCollection that
// never calls AddBrighter, so the fallback path is forced, with N resolver threads and one Dispose thread
// released simultaneously via a Barrier to maximise contention between the very first Scoped resolution and
// a concurrent Dispose.
[Collection(ScopedArtefactCacheLiveCountCollection.Name)]
public class DisposeRacesFirstScopedResolutionTests
{
    [Fact]
    public async Task When_a_resolution_races_dispose_no_artefact_cache_should_be_leaked()
    {
        const int trials = 200;
        const int resolversPerTrial = 16;

        var baseline = ScopedArtefactCache.LiveCount;

        for (var trial = 0; trial < trials; trial++)
        {
            var services = new ServiceCollection();
            services.AddTransient<DisposeRaceHandler>();
            services.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Scoped });

            var provider = services.BuildServiceProvider();
            var factory = new ServiceProviderHandlerFactory(provider);
            var pipelineScope = factory.CreatePipelineScope();
            var lifetime = new DisposeRaceTestLifetimeScope(pipelineScope);

            using var barrier = new Barrier(resolversPerTrial + 1);
            var tasks = new Task[resolversPerTrial + 1];
            for (var i = 0; i < resolversPerTrial; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    try
                    {
                        ((IAmAHandlerFactorySync)factory).Create(typeof(DisposeRaceHandler), lifetime);
                    }
                    catch (ObjectDisposedException)
                    {
                        // expected: this resolution lost the race against the concurrent Dispose below
                    }
                });
            }
            tasks[resolversPerTrial] = Task.Run(() =>
            {
                barrier.SignalAndWait();
                pipelineScope!.Dispose();
            });

            await Task.WhenAll(tasks);
        }

        // Assert - every ScopedArtefactCache this scenario constructed, across every trial, was disposed;
        // none survived as an undisposed loser of a resolution racing Dispose
        Assert.Equal(baseline, ScopedArtefactCache.LiveCount);
    }

    private class DisposeRaceHandler : RequestHandler<DisposeRaceCommand>
    {
        public override DisposeRaceCommand Handle(DisposeRaceCommand command) => command;
    }

    private class DisposeRaceCommand : Command
    {
        public DisposeRaceCommand() : base(Guid.NewGuid()) { }
    }

    private class DisposeRaceTestLifetimeScope : IAmALifetime
    {
        public DisposeRaceTestLifetimeScope(IAmAScope? pipelineScope = null) => PipelineScope = pipelineScope;
        public IAmAScope? PipelineScope { get; }
        public void Add(IHandleRequests instance) { }
        public void Add(IHandleRequestsAsync instance) { }
        public void Dispose() => PipelineScope?.Dispose();
        public ValueTask DisposeAsync() => PipelineScope?.DisposeAsync() ?? default;
    }
}
