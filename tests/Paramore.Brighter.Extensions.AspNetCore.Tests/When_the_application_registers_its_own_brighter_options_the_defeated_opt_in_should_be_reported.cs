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
using Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests;

// AC-50 (FR-22.4, FR-17, FR-14, C-12, C-15, D18) - an application that registers its own IBrighterOptions
// defeats AddBrighterRequestScope's write-through on every one of Brighter's four registration entry
// points and in either ordering between the extension and AddBrighter/AddConsumers, because
// RegisterBrighterOptions (ADR 0076) leaves an already-registered unkeyed IBrighterOptions alone rather
// than overwriting it. Until this rule, the only symptom was an opt-in that adopted nothing and said
// nothing. The rule reads registrations, not values - it must not compare the override's affinity with
// the resolved object's, since an override carrying AlwaysNew (the option's own default) is by value
// indistinguishable from an override that was never applied; the identical-values fact below is the
// falsifier for that.
public class DefeatedOptInValidationTests
{
    [Fact]
    public async Task When_the_application_registers_its_own_options_before_add_brighter_and_throw_on_error_is_false_the_defeated_opt_in_should_report_one_error_and_resolve_always_new()
    {
        // Arrange - the base host: the application registers its own IBrighterOptions (a conformant
        // {Scoped, Scoped, Scoped} triple, no lifetime rule fires) before AddBrighter, whose own delegate
        // sets no lifetime since it is never invoked; AddBrighterRequestScope() with no argument
        var capturingProvider = new CapturingLoggerProvider();
        await using var factory = new DefeatedOptInWebApplicationFactory(
            applicationRegistersOwnOptions: true, capturingProvider);
        using var client = factory.CreateClient();

        // Act - accessing Services starts the host and runs BrighterValidationHostedService.StartAsync;
        // the controller action Sends PlaceOrder
        var response = await client.PostAsync("/api/orders", content: null);

        // Assert - startup proceeded (throwOnError: false), the resolved options are the application's
        // own (AlwaysNew, its own default, since the override never reached it), and the handler resolved
        // a fresh IOrderDbContext rather than the controller's own
        response.EnsureSuccessStatusCode();
        var resolvedOptions = factory.Services.GetRequiredService<IBrighterOptions>();
        Assert.Equal(ScopeAffinity.AlwaysNew, resolvedOptions.DefaultScopeAffinity);
        var recorder = factory.Services.GetRequiredService<OrderDbContextRecorder>();
        Assert.NotSame(recorder.ControllerInstance, recorder.HandlerInstance);

        // Assert - exactly one Error, naming the affinity the extension registered (JoinAmbient, its own
        // default), that the resolved options were supplied by the application, the remedy, and the guide
        var errorEntry = Assert.Single(capturingProvider.Entries, e => e.Level == LogLevel.Error);
        Assert.Contains("JoinAmbient", errorEntry.Message);
        Assert.Contains("supplied by the application", errorEntry.Message);
        Assert.Contains("docs/guides/lifetimes-and-scoping.md", errorEntry.Message);
    }

    [Fact]
    public async Task When_throw_on_error_is_true_the_defeated_opt_in_should_fail_startup()
    {
        // Arrange - the same base shape, but throwOnError: true
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped,
            MapperLifetime = ServiceLifetime.Scoped,
            TransformerLifetime = ServiceLifetime.Scoped
        });
        var builder = services.AddBrighter(options => { });
        services.AddBrighterRequestScope();
        builder.ValidatePipelines(throwOnError: true);

        var provider = services.BuildServiceProvider();
        var hostedService = provider.GetServices<IHostedService>().OfType<BrighterValidationHostedService>().Single();

        // Act & Assert - startup fails with the identical message, and no controller action could run
        var exception = await Assert.ThrowsAsync<PipelineValidationException>(
            () => hostedService.StartAsync(CancellationToken.None));

        Assert.Contains("JoinAmbient", exception.Message);
        Assert.Contains("supplied by the application", exception.Message);
        Assert.Contains("docs/guides/lifetimes-and-scoping.md", exception.Message);
    }

    [Fact]
    public async Task When_the_application_registers_its_own_options_after_add_brighter_the_defeated_opt_in_should_still_be_reported()
    {
        // Arrange - the application's registration placed AFTER AddBrighter: a plain AddSingleton that does
        // not contest RegisterBrighterOptions's TryAdd-shaped guard but wins resolution as the last unkeyed
        // descriptor. This branch fails under a mechanism that only records whether Brighter's own
        // TryAddSingleton found the service already present, rather than asking which descriptor is last.
        var capturingProvider = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddProvider(capturingProvider));
        var builder = services.AddBrighter(options => { });
        services.AddBrighterRequestScope();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped,
            MapperLifetime = ServiceLifetime.Scoped,
            TransformerLifetime = ServiceLifetime.Scoped
        });
        builder.ValidatePipelines(throwOnError: false);

        var provider = services.BuildServiceProvider();
        var hostedService = provider.GetServices<IHostedService>().OfType<BrighterValidationHostedService>().Single();

        // Act
        await hostedService.StartAsync(CancellationToken.None);

        // Assert - the same single Error as the before-ordering
        var errorEntry = Assert.Single(capturingProvider.Entries, e => e.Level == LogLevel.Error);
        Assert.Contains("JoinAmbient", errorEntry.Message);
        Assert.Contains("supplied by the application", errorEntry.Message);
        Assert.Contains("docs/guides/lifetimes-and-scoping.md", errorEntry.Message);
    }

    [Fact]
    public async Task When_the_extensions_affinity_matches_the_applications_own_value_the_defeated_opt_in_should_still_be_reported()
    {
        // Arrange - the falsifier: the extension passes AlwaysNew, and the application's own pre-registered
        // options object also carries AlwaysNew - override and resolved object hold the identical value.
        // This is the only branch that fails under an implementation comparing the override's affinity
        // with the resolved object's rather than asking which descriptor is registered.
        var capturingProvider = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddProvider(capturingProvider));
        services.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped,
            MapperLifetime = ServiceLifetime.Scoped,
            TransformerLifetime = ServiceLifetime.Scoped,
            DefaultScopeAffinity = ScopeAffinity.AlwaysNew
        });
        var builder = services.AddBrighter(options => { });
        services.AddBrighterRequestScope(ScopeAffinity.AlwaysNew);
        builder.ValidatePipelines(throwOnError: false);

        var provider = services.BuildServiceProvider();
        var hostedService = provider.GetServices<IHostedService>().OfType<BrighterValidationHostedService>().Single();

        // Act
        await hostedService.StartAsync(CancellationToken.None);

        // Assert - still exactly one Error, naming the override's own AlwaysNew
        var errorEntry = Assert.Single(capturingProvider.Entries, e => e.Level == LogLevel.Error);
        Assert.Contains("AlwaysNew", errorEntry.Message);
        Assert.Contains("supplied by the application", errorEntry.Message);
        Assert.Contains("docs/guides/lifetimes-and-scoping.md", errorEntry.Message);
    }

    [Fact]
    public async Task When_the_extension_is_called_before_add_brighter_the_defeated_opt_in_should_still_be_reported()
    {
        // Arrange - the application still registers first, but the extension call is placed before
        // AddBrighter this time (the opposite order from the base fact) - the defeat is not an ordering
        // failure of the extension
        var capturingProvider = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddProvider(capturingProvider));
        services.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped,
            MapperLifetime = ServiceLifetime.Scoped,
            TransformerLifetime = ServiceLifetime.Scoped
        });
        services.AddBrighterRequestScope();
        var builder = services.AddBrighter(options => { });
        builder.ValidatePipelines(throwOnError: false);

        var provider = services.BuildServiceProvider();
        var hostedService = provider.GetServices<IHostedService>().OfType<BrighterValidationHostedService>().Single();

        // Act
        await hostedService.StartAsync(CancellationToken.None);

        // Assert - the same single Error regardless of this ordering
        var errorEntry = Assert.Single(capturingProvider.Entries, e => e.Level == LogLevel.Error);
        Assert.Contains("JoinAmbient", errorEntry.Message);
        Assert.Contains("supplied by the application", errorEntry.Message);
        Assert.Contains("docs/guides/lifetimes-and-scoping.md", errorEntry.Message);
    }

    [Fact]
    public async Task When_add_brighter_func_is_used_the_defeated_opt_in_should_still_be_reported()
    {
        // Arrange - the AddBrighter(Func<IServiceProvider, BrighterOptions>) entry point
        var capturingProvider = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddProvider(capturingProvider));
        services.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped,
            MapperLifetime = ServiceLifetime.Scoped,
            TransformerLifetime = ServiceLifetime.Scoped
        });
        services.AddBrighterRequestScope();
        var builder = services.AddBrighter(_ => new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped,
            MapperLifetime = ServiceLifetime.Scoped,
            TransformerLifetime = ServiceLifetime.Scoped
        });
        builder.ValidatePipelines(throwOnError: false);

        var provider = services.BuildServiceProvider();
        var hostedService = provider.GetServices<IHostedService>().OfType<BrighterValidationHostedService>().Single();

        // Act
        await hostedService.StartAsync(CancellationToken.None);

        // Assert
        var errorEntry = Assert.Single(capturingProvider.Entries, e => e.Level == LogLevel.Error);
        Assert.Contains("JoinAmbient", errorEntry.Message);
        Assert.Contains("supplied by the application", errorEntry.Message);
        Assert.Contains("docs/guides/lifetimes-and-scoping.md", errorEntry.Message);
    }

    [Fact]
    public async Task When_add_consumers_action_alone_is_used_the_defeated_opt_in_should_still_be_reported()
    {
        // Arrange - the AddConsumers(Action<ConsumersOptions>) entry point, alone (no AddBrighter). The
        // application pre-registers a ConsumersOptions, not a bare BrighterOptions, matching this
        // project's own convention for a consumer host (AC-45's own fixtures do the same)
        var capturingProvider = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddProvider(capturingProvider));
        services.AddSingleton<IBrighterOptions>(new ConsumersOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped,
            MapperLifetime = ServiceLifetime.Scoped,
            TransformerLifetime = ServiceLifetime.Scoped
        });
        services.AddBrighterRequestScope();
        var builder = services.AddConsumers(options => { });
        builder.ValidatePipelines(throwOnError: false);
        services.AddHostedService<ServiceActivatorHostedService>();

        var provider = services.BuildServiceProvider();
        var hostedService = provider.GetServices<IHostedService>().OfType<ServiceActivatorHostedService>().Single();

        // Act
        await hostedService.StartAsync(CancellationToken.None);

        // Assert
        var errorEntry = Assert.Single(capturingProvider.Entries, e => e.Level == LogLevel.Error);
        Assert.Contains("JoinAmbient", errorEntry.Message);
        Assert.Contains("supplied by the application", errorEntry.Message);
        Assert.Contains("docs/guides/lifetimes-and-scoping.md", errorEntry.Message);
    }

    [Fact]
    public async Task When_add_consumers_func_alone_is_used_the_defeated_opt_in_should_still_be_reported()
    {
        // Arrange - the AddConsumers(Func<IServiceProvider, ConsumersOptions>) entry point, alone. This
        // overload binds IAmConsumerOptions by casting whatever IBrighterOptions resolves to, so the
        // application's pre-registered instance has to be a ConsumersOptions - a bare BrighterOptions
        // would throw InvalidCastException while the dispatcher is constructed, before validation ever runs
        var capturingProvider = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddProvider(capturingProvider));
        services.AddSingleton<IBrighterOptions>(new ConsumersOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped,
            MapperLifetime = ServiceLifetime.Scoped,
            TransformerLifetime = ServiceLifetime.Scoped
        });
        services.AddBrighterRequestScope();
        var builder = services.AddConsumers(_ => new ConsumersOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped,
            MapperLifetime = ServiceLifetime.Scoped,
            TransformerLifetime = ServiceLifetime.Scoped
        });
        builder.ValidatePipelines(throwOnError: false);
        services.AddHostedService<ServiceActivatorHostedService>();

        var provider = services.BuildServiceProvider();
        var hostedService = provider.GetServices<IHostedService>().OfType<ServiceActivatorHostedService>().Single();

        // Act
        await hostedService.StartAsync(CancellationToken.None);

        // Assert
        var errorEntry = Assert.Single(capturingProvider.Entries, e => e.Level == LogLevel.Error);
        Assert.Contains("JoinAmbient", errorEntry.Message);
        Assert.Contains("supplied by the application", errorEntry.Message);
        Assert.Contains("docs/guides/lifetimes-and-scoping.md", errorEntry.Message);
    }

    [Fact]
    public async Task When_the_application_does_not_register_its_own_options_the_control_host_should_report_no_finding()
    {
        // Arrange - the control host: identical to the base fact except the application's registration is
        // removed, so nothing defeats the opt-in. This is the fact that makes the other eight mean
        // anything - it must produce no finding at all
        var capturingProvider = new CapturingLoggerProvider();
        await using var factory = new DefeatedOptInWebApplicationFactory(
            applicationRegistersOwnOptions: false, capturingProvider);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/orders", content: null);

        // Assert - startup proceeded, the resolved options carry JoinAmbient (the opt-in actually applied),
        // and the handler shares the controller's own instance
        response.EnsureSuccessStatusCode();
        var resolvedOptions = factory.Services.GetRequiredService<IBrighterOptions>();
        Assert.Equal(ScopeAffinity.JoinAmbient, resolvedOptions.DefaultScopeAffinity);
        var recorder = factory.Services.GetRequiredService<OrderDbContextRecorder>();
        Assert.Same(recorder.ControllerInstance, recorder.HandlerInstance);

        // Assert - no finding at all, attributing the Error in every other fact to the defeated
        // registration rather than to the host shape
        Assert.DoesNotContain(capturingProvider.Entries, e => e.Level >= LogLevel.Warning);
    }
}
