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

// AC-18 (FR-15, FR-17, FR-24.3, C-10, D13, D16, D18) - the affinity the registration extension itself
// carries is what decides adoption, no matter what the host's own AddBrighter delegate set the default
// to, and no matter which of the extension call or AddBrighter is registered first. Each host below has
// lifetime triple {Scoped, Scoped, Scoped}, one controller sending a command to a handler that resolves
// the same kind of Scoped dependency the controller itself holds, and a recording ambient-source
// decorator registered last so it is the one every pipeline actually asks - proving the extension's own
// affinity, not merely the host's default, is what reaches the ask.
public class ExtensionAffinityArgumentTests
{
    [Fact]
    public async Task When_the_extension_overrides_the_hosts_own_join_ambient_the_extensions_always_new_should_win()
    {
        // Arrange - the host itself sets JoinAmbient, then the extension is called passing AlwaysNew
        await using var factory = new RegistrationOrderWebApplicationFactory(
            hostDefaultAffinity: ScopeAffinity.JoinAmbient,
            extensionAffinityArgument: ScopeAffinity.AlwaysNew,
            extensionCallBeforeAddBrighter: false);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/orders", content: null);

        // Assert - the handler resolved a fresh instance, already disposed, and the ask carried AlwaysNew
        response.EnsureSuccessStatusCode();
        var recorder = factory.Services.GetRequiredService<OrderDbContextRecorder>();
        Assert.NotSame(recorder.ControllerInstance, recorder.HandlerInstance);
        Assert.Equal(1, recorder.HandlerInstance!.DisposeCount);
        Assert.Equal(new[] { ScopeAffinity.AlwaysNew }, ScopeProviderRecorderFor(factory).Decisions);
    }

    [Fact]
    public async Task When_the_extension_defaults_to_join_ambient_over_the_hosts_own_always_new_it_should_adopt()
    {
        // Arrange - the host itself sets AlwaysNew, then the extension is called with no argument (its JoinAmbient default)
        await using var factory = new RegistrationOrderWebApplicationFactory(
            hostDefaultAffinity: ScopeAffinity.AlwaysNew,
            extensionAffinityArgument: null,
            extensionCallBeforeAddBrighter: false);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/orders", content: null);

        // Assert - the handler shares the controller's own instance, and the ask carried JoinAmbient
        response.EnsureSuccessStatusCode();
        var recorder = factory.Services.GetRequiredService<OrderDbContextRecorder>();
        Assert.Same(recorder.ControllerInstance, recorder.HandlerInstance);
        Assert.Equal(new[] { ScopeAffinity.JoinAmbient }, ScopeProviderRecorderFor(factory).Decisions);
    }

    [Fact]
    public async Task When_the_always_new_override_is_registered_before_add_brighter_the_outcome_is_unchanged()
    {
        // Arrange - same as the first fact, but the extension call now runs before AddBrighter, not after
        await using var factory = new RegistrationOrderWebApplicationFactory(
            hostDefaultAffinity: ScopeAffinity.JoinAmbient,
            extensionAffinityArgument: ScopeAffinity.AlwaysNew,
            extensionCallBeforeAddBrighter: true);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/orders", content: null);

        // Assert - unchanged from the first fact
        response.EnsureSuccessStatusCode();
        var recorder = factory.Services.GetRequiredService<OrderDbContextRecorder>();
        Assert.NotSame(recorder.ControllerInstance, recorder.HandlerInstance);
        Assert.Equal(1, recorder.HandlerInstance!.DisposeCount);
        Assert.Equal(new[] { ScopeAffinity.AlwaysNew }, ScopeProviderRecorderFor(factory).Decisions);
    }

    [Fact]
    public async Task When_the_join_ambient_default_is_registered_before_add_brighter_the_outcome_is_unchanged()
    {
        // Arrange - same as the second fact, but the extension call now runs before AddBrighter, not after
        await using var factory = new RegistrationOrderWebApplicationFactory(
            hostDefaultAffinity: ScopeAffinity.AlwaysNew,
            extensionAffinityArgument: null,
            extensionCallBeforeAddBrighter: true);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/orders", content: null);

        // Assert - unchanged from the second fact
        response.EnsureSuccessStatusCode();
        var recorder = factory.Services.GetRequiredService<OrderDbContextRecorder>();
        Assert.Same(recorder.ControllerInstance, recorder.HandlerInstance);
        Assert.Equal(new[] { ScopeAffinity.JoinAmbient }, ScopeProviderRecorderFor(factory).Decisions);
    }

    private static DelegatingScopeProviderRecorder ScopeProviderRecorderFor(RegistrationOrderWebApplicationFactory factory) =>
        (DelegatingScopeProviderRecorder)factory.Services.GetRequiredService<IAmAScopeProvider>();
}
