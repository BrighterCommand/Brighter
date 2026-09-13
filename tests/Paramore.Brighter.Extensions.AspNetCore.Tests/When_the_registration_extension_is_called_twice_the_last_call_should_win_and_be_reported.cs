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
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests;

// AC-49 (FR-17, FR-25, NFR-10) - calling AddBrighterRequestScope more than once is determined (the last
// call's affinity wins, matching FR-24.3's last-registration-wins rule for the provider) and reported: a
// repeat carrying more than one distinct affinity is a Warning naming every value and the effective one,
// while a repeat carrying the same affinity twice is idempotent in effect and is not a finding - the
// exclusion FR-24.3 already makes for a repeated identical provider registration, made here for the
// identical reason.
public class RepeatedRequestScopeOptInValidationTests
{
    [Fact]
    public async Task When_the_extension_is_called_always_new_then_join_ambient_the_last_call_should_win_and_be_reported()
    {
        // Arrange - the base host: AlwaysNew first, then JoinAmbient, around a single AddBrighter call, all
        // three lifetimes Scoped, ValidatePipelines() called last
        var capturingProvider = new CapturingLoggerProvider();
        await using var factory = new RepeatedRequestScopeWebApplicationFactory(
            firstCall: ScopeAffinity.AlwaysNew, secondCall: ScopeAffinity.JoinAmbient, capturingProvider);
        using var client = factory.CreateClient();

        // Act - accessing Services starts the host and runs BrighterValidationHostedService.StartAsync;
        // the controller action Sends PlaceOrder
        var response = await client.PostAsync("/api/orders", content: null);

        // Assert - the last call's affinity, JoinAmbient, is the one the factories read, and the handler
        // resolved the controller's own Scoped instance, so the affinity and the provider agree
        response.EnsureSuccessStatusCode();
        var resolvedOptions = factory.Services.GetRequiredService<IBrighterOptions>();
        Assert.Equal(ScopeAffinity.JoinAmbient, resolvedOptions.DefaultScopeAffinity);
        var recorder = factory.Services.GetRequiredService<OrderDbContextRecorder>();
        Assert.Same(recorder.ControllerInstance, recorder.HandlerInstance);

        // Assert - exactly one Warning naming both values and identifying JoinAmbient as effective, and no
        // duplicate-provider finding alongside it (both calls register the same provider implementation
        // type, which that rule excludes)
        var warning = Assert.Single(capturingProvider.Entries, e => e.Level >= LogLevel.Warning);
        Assert.Contains("AlwaysNew", warning.Message);
        Assert.Contains("JoinAmbient", warning.Message);
        Assert.Contains("docs/guides/lifetimes-and-scoping.md", warning.Message);
    }

    [Fact]
    public async Task When_the_extension_is_called_join_ambient_then_always_new_the_last_call_should_still_win()
    {
        // Arrange - the reversed order: JoinAmbient first, then AlwaysNew - pins "last call wins", not "the
        // more permissive value wins"
        var capturingProvider = new CapturingLoggerProvider();
        await using var factory = new RepeatedRequestScopeWebApplicationFactory(
            firstCall: ScopeAffinity.JoinAmbient, secondCall: ScopeAffinity.AlwaysNew, capturingProvider);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/orders", content: null);

        // Assert - the last call's affinity, AlwaysNew, is effective, and nothing adopts
        response.EnsureSuccessStatusCode();
        var resolvedOptions = factory.Services.GetRequiredService<IBrighterOptions>();
        Assert.Equal(ScopeAffinity.AlwaysNew, resolvedOptions.DefaultScopeAffinity);
        var recorder = factory.Services.GetRequiredService<OrderDbContextRecorder>();
        Assert.NotSame(recorder.ControllerInstance, recorder.HandlerInstance);

        // Assert - the same single Warning, naming both values regardless of which was registered last
        var warning = Assert.Single(capturingProvider.Entries, e => e.Level >= LogLevel.Warning);
        Assert.Contains("AlwaysNew", warning.Message);
        Assert.Contains("JoinAmbient", warning.Message);
        Assert.Contains("docs/guides/lifetimes-and-scoping.md", warning.Message);
    }

    [Fact]
    public async Task When_the_extension_is_called_twice_with_the_same_affinity_no_finding_should_be_reported()
    {
        // Arrange - the control: both calls pass JoinAmbient. A repeated identical opt-in is idempotent in
        // effect and must not be reported, exactly as a repeated identical provider registration isn't
        var capturingProvider = new CapturingLoggerProvider();
        await using var factory = new RepeatedRequestScopeWebApplicationFactory(
            firstCall: ScopeAffinity.JoinAmbient, secondCall: ScopeAffinity.JoinAmbient, capturingProvider);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/orders", content: null);

        // Assert - the repeated affinity is effective and the handler adopted the controller's instance
        response.EnsureSuccessStatusCode();
        var resolvedOptions = factory.Services.GetRequiredService<IBrighterOptions>();
        Assert.Equal(ScopeAffinity.JoinAmbient, resolvedOptions.DefaultScopeAffinity);
        var recorder = factory.Services.GetRequiredService<OrderDbContextRecorder>();
        Assert.Same(recorder.ControllerInstance, recorder.HandlerInstance);

        // Assert - no finding at all, attributing the Warning in the two facts above to the differing
        // affinities rather than to the host shape
        Assert.DoesNotContain(capturingProvider.Entries, e => e.Level >= LogLevel.Warning);
    }
}
