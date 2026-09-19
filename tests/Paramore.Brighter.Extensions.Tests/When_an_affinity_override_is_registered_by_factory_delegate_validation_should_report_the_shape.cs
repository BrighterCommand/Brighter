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

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

// AC-53 (FR-17) - an affinity override registered by factory delegate still takes effect (D18's
// write-through resolves it), but supplies no ImplementationInstance for RepeatedOptIn (T7.8) to read a
// value off, so a conflicting repeat carrying it would go unreported. This rule reports the registration
// shape itself - readable - rather than the value, which isn't, and it is a Warning because nothing about
// such a host is broken: what's lost is a diagnostic, not the opt-in.
public class UnreadableOverrideValidationTests
{
    [Fact]
    public async Task When_an_override_is_registered_by_factory_delegate_validation_should_report_the_shape_and_still_apply_it()
    {
        // Arrange - a factory-delegate registration, the shape a third-party opt-in package can write
        // since ScopeAffinityOverride is public in the DI package; all three lifetimes Scoped
        var capturingProvider = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddProvider(capturingProvider));
        services.AddSingleton(_ => new ScopeAffinityOverride(ScopeAffinity.JoinAmbient));
        var builder = services.AddBrighter(options =>
        {
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Scoped;
            options.TransformerLifetime = ServiceLifetime.Scoped;
        });
        builder.ValidatePipelines(throwOnError: true);

        var provider = services.BuildServiceProvider();
        var hostedService = provider.GetServices<IHostedService>().OfType<BrighterValidationHostedService>().Single();

        // Act - a Warning must not trip throwOnError: true
        await hostedService.StartAsync(CancellationToken.None);

        // Assert - the override's own affinity is still the effective one; the finding is about
        // reportability, not a lost opt-in
        var resolvedOptions = provider.GetRequiredService<IBrighterOptions>();
        Assert.Equal(ScopeAffinity.JoinAmbient, resolvedOptions.DefaultScopeAffinity);

        // Assert - exactly one Warning, naming the registration shape, the unreadability, the remedy and
        // the guidance page
        var warning = Assert.Single(capturingProvider.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains("factory delegate", warning.Message);
        Assert.Contains("cannot be read", warning.Message);
        Assert.Contains("constructed instance", warning.Message);
        Assert.Contains("docs/guides/lifetimes-and-scoping.md", warning.Message);
    }

    [Fact]
    public async Task When_two_overrides_are_registered_one_by_instance_and_one_by_factory_delegate_only_the_unreadable_override_warning_should_be_reported()
    {
        // Arrange - two overrides carrying different affinities, one a constructed instance, one a
        // factory delegate. The delegate descriptor contributes nothing to RepeatedOptIn's distinctness
        // set, so only one distinct value is visible to that rule - the silence this rule exists to break
        var capturingProvider = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddProvider(capturingProvider));
        services.AddSingleton(new ScopeAffinityOverride(ScopeAffinity.AlwaysNew));
        services.AddSingleton(_ => new ScopeAffinityOverride(ScopeAffinity.JoinAmbient));
        var builder = services.AddBrighter(options =>
        {
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Scoped;
            options.TransformerLifetime = ServiceLifetime.Scoped;
        });
        builder.ValidatePipelines(throwOnError: true);

        var provider = services.BuildServiceProvider();
        var hostedService = provider.GetServices<IHostedService>().OfType<BrighterValidationHostedService>().Single();

        // Act
        await hostedService.StartAsync(CancellationToken.None);

        // Assert - exactly one Warning overall: the unreadable-override one, not FR-17's repeated-opt-in
        // one
        var warning = Assert.Single(capturingProvider.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains("factory delegate", warning.Message);
    }

    [Fact]
    public async Task When_the_override_is_registered_as_a_constructed_instance_no_finding_should_be_reported()
    {
        // Arrange - the control: registered as a constructed instance, as AddBrighterRequestScope itself
        // does, so the Warning above is attributable to the registration shape, not to the mere presence
        // of an override
        var capturingProvider = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddProvider(capturingProvider));
        services.AddSingleton(new ScopeAffinityOverride(ScopeAffinity.JoinAmbient));
        var builder = services.AddBrighter(options =>
        {
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Scoped;
            options.TransformerLifetime = ServiceLifetime.Scoped;
        });
        builder.ValidatePipelines(throwOnError: true);

        var provider = services.BuildServiceProvider();
        var hostedService = provider.GetServices<IHostedService>().OfType<BrighterValidationHostedService>().Single();

        // Act
        await hostedService.StartAsync(CancellationToken.None);

        // Assert - no finding at all
        Assert.DoesNotContain(capturingProvider.Entries, e => e.Level >= LogLevel.Warning);
    }
}
