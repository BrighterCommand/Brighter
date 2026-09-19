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

using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests;

// AC-26 (FR-21, D5); ADR 0072 step 3 - opting a host into the ambient-scope extension only changes
// anything for a Scoped pipeline. A Transient pipeline keeps its own per-resolution isolation and never
// shares an instance with the controller, whichever affinity is selected; a Singleton pipeline keeps
// resolving the same handler and the same dependency it was built with the first time, whichever
// affinity is selected. Only a Scoped pipeline under the join-ambient affinity actually shares an
// instance with the controller - everything else is exactly as if the extension had never been called.
public class LifetimeAffinityInertOutsideScopedTests
{
    [Fact]
    public async Task When_all_transient_under_join_ambient_the_handler_should_resolve_a_different_instance()
    {
        // Arrange - every Brighter lifetime is Transient, and the extension is told to join the ambient
        await using var factory = new LifetimeAffinityWebApplicationFactory(ServiceLifetime.Transient, ScopeAffinity.JoinAmbient);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/orders", content: null);

        // Assert - a Transient pipeline never asks for an ambient, so it resolves and disposes its own
        // fresh instance regardless of the affinity in force
        response.EnsureSuccessStatusCode();
        var recorder = factory.Services.GetRequiredService<OrderDbContextRecorder>();
        Assert.NotSame(recorder.ControllerInstance, recorder.HandlerInstance);
        Assert.Equal(1, recorder.HandlerInstance!.DisposeCount);
    }

    [Fact]
    public async Task When_all_transient_under_always_new_the_handler_should_resolve_a_different_instance()
    {
        // Arrange - same all-Transient host, but the extension is told never to join the ambient
        await using var factory = new LifetimeAffinityWebApplicationFactory(ServiceLifetime.Transient, ScopeAffinity.AlwaysNew);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/orders", content: null);

        // Assert - unchanged from the join-ambient run: the affinity setting made no difference
        response.EnsureSuccessStatusCode();
        var recorder = factory.Services.GetRequiredService<OrderDbContextRecorder>();
        Assert.NotSame(recorder.ControllerInstance, recorder.HandlerInstance);
        Assert.Equal(1, recorder.HandlerInstance!.DisposeCount);
    }

    [Fact]
    public async Task When_all_scoped_under_join_ambient_the_handler_should_share_the_controllers_instance()
    {
        // Arrange - every Brighter lifetime is Scoped, and the extension is told to join the ambient -
        // the one combination where adoption actually happens
        await using var factory = new LifetimeAffinityWebApplicationFactory(ServiceLifetime.Scoped, ScopeAffinity.JoinAmbient);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/orders", content: null);

        // Assert - the handler resolves the controller's own instance from the shared request scope
        response.EnsureSuccessStatusCode();
        var recorder = factory.Services.GetRequiredService<OrderDbContextRecorder>();
        Assert.Same(recorder.ControllerInstance, recorder.HandlerInstance);
    }

    [Fact]
    public async Task When_all_scoped_under_always_new_the_handler_should_resolve_a_different_instance()
    {
        // Arrange - same all-Scoped host, but the extension is told never to join the ambient
        await using var factory = new LifetimeAffinityWebApplicationFactory(ServiceLifetime.Scoped, ScopeAffinity.AlwaysNew);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/orders", content: null);

        // Assert - without the join-ambient affinity, a Scoped pipeline still gets its own fresh scope
        response.EnsureSuccessStatusCode();
        var recorder = factory.Services.GetRequiredService<OrderDbContextRecorder>();
        Assert.NotSame(recorder.ControllerInstance, recorder.HandlerInstance);
        Assert.Equal(1, recorder.HandlerInstance!.DisposeCount);
    }

    [Fact]
    public async Task When_all_singleton_under_join_ambient_two_sends_should_resolve_the_same_handler_and_dependency()
    {
        // Arrange - every Brighter lifetime is Singleton, and the extension is told to join the ambient.
        // The handler here takes a container-Singleton dependency, not a Scoped one, since a Singleton
        // handler taking a Scoped dependency would be a captive-dependency graph
        await using var factory = new LifetimeAffinityWebApplicationFactory(ServiceLifetime.Singleton, ScopeAffinity.JoinAmbient);
        using var client = factory.CreateClient();

        // Act - two Send calls from two separate HTTP requests
        var first = await client.PostAsync("/api/singleton-orders", content: null);
        var second = await client.PostAsync("/api/singleton-orders", content: null);

        // Assert - both requests resolved the same handler instance and the same dependency instance
        first.EnsureSuccessStatusCode();
        second.EnsureSuccessStatusCode();
        var recorder = factory.Services.GetRequiredService<SingletonOrderRecorder>();
        Assert.Equal(2, recorder.Entries.Count);
        Assert.Same(recorder.Entries[0].Handler, recorder.Entries[1].Handler);
        Assert.Same(recorder.Entries[0].Dependency, recorder.Entries[1].Dependency);
    }

    [Fact]
    public async Task When_all_singleton_under_always_new_two_sends_should_resolve_the_same_handler_and_dependency()
    {
        // Arrange - same all-Singleton host, but the extension is told never to join the ambient
        await using var factory = new LifetimeAffinityWebApplicationFactory(ServiceLifetime.Singleton, ScopeAffinity.AlwaysNew);
        using var client = factory.CreateClient();

        // Act - two Send calls from two separate HTTP requests
        var first = await client.PostAsync("/api/singleton-orders", content: null);
        var second = await client.PostAsync("/api/singleton-orders", content: null);

        // Assert - unchanged from the join-ambient run: the affinity setting made no difference
        first.EnsureSuccessStatusCode();
        second.EnsureSuccessStatusCode();
        var recorder = factory.Services.GetRequiredService<SingletonOrderRecorder>();
        Assert.Equal(2, recorder.Entries.Count);
        Assert.Same(recorder.Entries[0].Handler, recorder.Entries[1].Handler);
        Assert.Same(recorder.Entries[0].Dependency, recorder.Entries[1].Dependency);
    }
}
