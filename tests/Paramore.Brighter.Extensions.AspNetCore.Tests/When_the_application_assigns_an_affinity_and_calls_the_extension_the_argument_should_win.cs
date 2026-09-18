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
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests;

// AC-48 (FR-17, C-10, D18) - when the application itself sets DefaultScopeAffinity in its own
// AddBrighter delegate, and the AddBrighterRequestScope extension is also called, the extension's own
// affinity argument is what the resolved IBrighterOptions ends up carrying, and that is what decides
// adoption - not the application's own setting, no matter which of the two calls runs first. Nothing
// raises a validation finding over the two disagreeing: the design deliberately treats a plain
// application-set default as indistinguishable from never having set one, so this is documented
// behaviour, not something a startup check reports on.
public class ApplicationAssignedAffinityOverriddenByExtensionTests
{
    [Fact]
    public async Task When_the_extensions_default_join_ambient_runs_after_the_applications_own_always_new_it_should_win()
    {
        // Arrange - the application's own delegate sets AlwaysNew; the extension is then called with no
        // argument, taking its JoinAmbient default
        await using var factory = new RegistrationOrderWebApplicationFactory(
            hostDefaultAffinity: ScopeAffinity.AlwaysNew,
            extensionAffinityArgument: null,
            extensionCallBeforeAddBrighter: false);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/orders", content: null);

        // Assert - no exception at startup or on the request (no validation finding is raised over the
        // disagreement), the resolved options carry the extension's JoinAmbient, and the handler shares
        // the controller's own Scoped instance because adoption actually happened
        response.EnsureSuccessStatusCode();
        var options = factory.Services.GetRequiredService<IBrighterOptions>();
        Assert.Equal(ScopeAffinity.JoinAmbient, options.DefaultScopeAffinity);
        var recorder = factory.Services.GetRequiredService<OrderDbContextRecorder>();
        Assert.Same(recorder.ControllerInstance, recorder.HandlerInstance);
    }

    [Fact]
    public async Task When_the_extensions_default_join_ambient_runs_before_the_applications_own_always_new_it_should_still_win()
    {
        // Arrange - same disagreement as the first fact, but the extension call is registered before
        // AddBrighter rather than after
        await using var factory = new RegistrationOrderWebApplicationFactory(
            hostDefaultAffinity: ScopeAffinity.AlwaysNew,
            extensionAffinityArgument: null,
            extensionCallBeforeAddBrighter: true);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/orders", content: null);

        // Assert - unchanged from the first fact: registration order does not matter
        response.EnsureSuccessStatusCode();
        var options = factory.Services.GetRequiredService<IBrighterOptions>();
        Assert.Equal(ScopeAffinity.JoinAmbient, options.DefaultScopeAffinity);
        var recorder = factory.Services.GetRequiredService<OrderDbContextRecorder>();
        Assert.Same(recorder.ControllerInstance, recorder.HandlerInstance);
    }

    [Fact]
    public async Task When_the_extension_carries_always_new_it_should_win_over_the_applications_own_join_ambient()
    {
        // Arrange - the mirror image of the disagreement: the application's own delegate sets
        // JoinAmbient, and the extension is passed AlwaysNew explicitly. The rule is symmetric, not
        // "the more permissive value wins", so this direction is checked once, not in both orderings
        await using var factory = new RegistrationOrderWebApplicationFactory(
            hostDefaultAffinity: ScopeAffinity.JoinAmbient,
            extensionAffinityArgument: ScopeAffinity.AlwaysNew,
            extensionCallBeforeAddBrighter: false);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/orders", content: null);

        // Assert - no exception at startup or on the request, the resolved options carry the
        // extension's AlwaysNew, and nothing adopts: the handler resolves a fresh, already-disposed
        // instance rather than sharing the controller's own
        response.EnsureSuccessStatusCode();
        var options = factory.Services.GetRequiredService<IBrighterOptions>();
        Assert.Equal(ScopeAffinity.AlwaysNew, options.DefaultScopeAffinity);
        var recorder = factory.Services.GetRequiredService<OrderDbContextRecorder>();
        Assert.NotSame(recorder.ControllerInstance, recorder.HandlerInstance);
        Assert.Equal(1, recorder.HandlerInstance!.DisposeCount);
    }
}
