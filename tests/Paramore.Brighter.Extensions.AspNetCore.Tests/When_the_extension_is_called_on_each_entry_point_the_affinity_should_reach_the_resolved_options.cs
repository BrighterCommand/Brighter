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
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;
using Xunit;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests;

// AC-45 (FR-17, FR-14, C-12a, D13, D18) - AddBrighterRequestScope's opt-in must reach the same
// IBrighterOptions object each of Brighter's four registration entry points actually resolves, on every
// one of them, and adoption must genuinely work through all four - not just that the resolved property
// carries the right value. Each host below uses exactly one entry point, lifetime triple
// {Scoped, Scoped, Scoped}, and calls AddBrighterRequestScope() with no affinity argument so its
// JoinAmbient default applies. Adoption is proven by putting a live ambient scope directly onto
// IHttpContextAccessor.HttpContext - the same thing ASP.NET Core's own middleware does per request,
// without needing a real HTTP round-trip - then sending a command whose handler resolves a Scoped
// IMarker: under JoinAmbient the handler's marker is the ambient scope's own instance; under the
// falsifiable direction - the host itself sets JoinAmbient, then the extension is called passing
// AlwaysNew, the non-default starting value that makes the clause falsifiable - the resolved options
// carry AlwaysNew and the handler's marker is a fresh instance, not the ambient's.
public class RequestScopeRegistrationEntryPointTests
{
    [Fact]
    public void When_add_brighter_action_calls_the_extension_the_affinity_should_reach_the_options_and_adoption_should_work()
    {
        // Arrange - AddBrighter(Action<BrighterOptions>), extension called with the JoinAmbient default
        var services = new ServiceCollection();
        services.AddScoped<IMarker, Marker>();
        services.AddSingleton<RegistrationAffinityRecorder>();
        services.AddBrighterRequestScope();
        services.AddBrighter(options =>
        {
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Scoped;
            options.TransformerLifetime = ServiceLifetime.Scoped;
        });
        var provider = services.BuildServiceProvider();

        // Act
        var resolvedOptions = provider.GetRequiredService<IBrighterOptions>();
        var ambientMarker = SendUnderAmbientScope(provider);

        // Assert - the extension's affinity reached the object the factories read, and adoption worked
        Assert.Equal(ScopeAffinity.JoinAmbient, resolvedOptions.DefaultScopeAffinity);
        Assert.Same(ambientMarker, provider.GetRequiredService<RegistrationAffinityRecorder>().ResolvedMarker);
    }

    [Fact]
    public void When_add_brighter_action_already_sets_join_ambient_the_extensions_always_new_should_still_win()
    {
        // Arrange - falsifiable direction: the host itself sets JoinAmbient, the extension overrides with AlwaysNew
        var services = new ServiceCollection();
        services.AddScoped<IMarker, Marker>();
        services.AddSingleton<RegistrationAffinityRecorder>();
        services.AddBrighter(options =>
        {
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Scoped;
            options.TransformerLifetime = ServiceLifetime.Scoped;
            options.DefaultScopeAffinity = ScopeAffinity.JoinAmbient;
        });
        services.AddBrighterRequestScope(ScopeAffinity.AlwaysNew);
        var provider = services.BuildServiceProvider();

        // Act
        var resolvedOptions = provider.GetRequiredService<IBrighterOptions>();
        var ambientMarker = SendUnderAmbientScope(provider);

        // Assert - the extension wins regardless of order, and nothing adopted the live ambient
        Assert.Equal(ScopeAffinity.AlwaysNew, resolvedOptions.DefaultScopeAffinity);
        Assert.NotSame(ambientMarker, provider.GetRequiredService<RegistrationAffinityRecorder>().ResolvedMarker);
    }

    [Fact]
    public void When_add_brighter_func_calls_the_extension_the_affinity_should_reach_the_options_and_adoption_should_work()
    {
        // Arrange - AddBrighter(Func<IServiceProvider, BrighterOptions>), extension called with the JoinAmbient default
        var services = new ServiceCollection();
        services.AddScoped<IMarker, Marker>();
        services.AddSingleton<RegistrationAffinityRecorder>();
        services.AddBrighterRequestScope();
        services.AddBrighter(_ => new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped,
            MapperLifetime = ServiceLifetime.Scoped,
            TransformerLifetime = ServiceLifetime.Scoped
        });
        var provider = services.BuildServiceProvider();

        // Act
        var resolvedOptions = provider.GetRequiredService<IBrighterOptions>();
        var ambientMarker = SendUnderAmbientScope(provider);

        // Assert
        Assert.Equal(ScopeAffinity.JoinAmbient, resolvedOptions.DefaultScopeAffinity);
        Assert.Same(ambientMarker, provider.GetRequiredService<RegistrationAffinityRecorder>().ResolvedMarker);
    }

    [Fact]
    public void When_add_brighter_func_already_sets_join_ambient_the_extensions_always_new_should_still_win()
    {
        // Arrange - falsifiable direction
        var services = new ServiceCollection();
        services.AddScoped<IMarker, Marker>();
        services.AddSingleton<RegistrationAffinityRecorder>();
        services.AddBrighter(_ => new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped,
            MapperLifetime = ServiceLifetime.Scoped,
            TransformerLifetime = ServiceLifetime.Scoped,
            DefaultScopeAffinity = ScopeAffinity.JoinAmbient
        });
        services.AddBrighterRequestScope(ScopeAffinity.AlwaysNew);
        var provider = services.BuildServiceProvider();

        // Act
        var resolvedOptions = provider.GetRequiredService<IBrighterOptions>();
        var ambientMarker = SendUnderAmbientScope(provider);

        // Assert
        Assert.Equal(ScopeAffinity.AlwaysNew, resolvedOptions.DefaultScopeAffinity);
        Assert.NotSame(ambientMarker, provider.GetRequiredService<RegistrationAffinityRecorder>().ResolvedMarker);
    }

    [Fact]
    public void When_add_consumers_action_alone_calls_the_extension_the_affinity_should_reach_the_options_and_adoption_should_work()
    {
        // Arrange - AddConsumers(Action<ConsumersOptions>) alone, extension called with the JoinAmbient default
        var services = new ServiceCollection();
        services.AddScoped<IMarker, Marker>();
        services.AddSingleton<RegistrationAffinityRecorder>();
        services.AddBrighterRequestScope();
        services.AddConsumers(options =>
        {
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Scoped;
            options.TransformerLifetime = ServiceLifetime.Scoped;
        });
        services.AddHostedService<ServiceActivatorHostedService>();
        var provider = services.BuildServiceProvider();

        // Act
        var resolvedOptions = provider.GetRequiredService<IBrighterOptions>();
        var ambientMarker = SendUnderAmbientScope(provider);

        // Assert
        Assert.Equal(ScopeAffinity.JoinAmbient, resolvedOptions.DefaultScopeAffinity);
        Assert.Same(ambientMarker, provider.GetRequiredService<RegistrationAffinityRecorder>().ResolvedMarker);
    }

    [Fact]
    public void When_add_consumers_action_already_sets_join_ambient_the_extensions_always_new_should_still_win()
    {
        // Arrange - falsifiable direction
        var services = new ServiceCollection();
        services.AddScoped<IMarker, Marker>();
        services.AddSingleton<RegistrationAffinityRecorder>();
        services.AddConsumers(options =>
        {
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Scoped;
            options.TransformerLifetime = ServiceLifetime.Scoped;
            options.DefaultScopeAffinity = ScopeAffinity.JoinAmbient;
        });
        services.AddBrighterRequestScope(ScopeAffinity.AlwaysNew);
        services.AddHostedService<ServiceActivatorHostedService>();
        var provider = services.BuildServiceProvider();

        // Act
        var resolvedOptions = provider.GetRequiredService<IBrighterOptions>();
        var ambientMarker = SendUnderAmbientScope(provider);

        // Assert
        Assert.Equal(ScopeAffinity.AlwaysNew, resolvedOptions.DefaultScopeAffinity);
        Assert.NotSame(ambientMarker, provider.GetRequiredService<RegistrationAffinityRecorder>().ResolvedMarker);
    }

    [Fact]
    public void When_add_consumers_func_alone_calls_the_extension_the_affinity_should_reach_the_options_and_adoption_should_work()
    {
        // Arrange - AddConsumers(Func<IServiceProvider, ConsumersOptions>) alone, extension called with the JoinAmbient default
        var services = new ServiceCollection();
        services.AddScoped<IMarker, Marker>();
        services.AddSingleton<RegistrationAffinityRecorder>();
        services.AddBrighterRequestScope();
        services.AddConsumers(_ => new ConsumersOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped,
            MapperLifetime = ServiceLifetime.Scoped,
            TransformerLifetime = ServiceLifetime.Scoped
        });
        services.AddHostedService<ServiceActivatorHostedService>();
        var provider = services.BuildServiceProvider();

        // Act
        var resolvedOptions = provider.GetRequiredService<IBrighterOptions>();
        var ambientMarker = SendUnderAmbientScope(provider);

        // Assert
        Assert.Equal(ScopeAffinity.JoinAmbient, resolvedOptions.DefaultScopeAffinity);
        Assert.Same(ambientMarker, provider.GetRequiredService<RegistrationAffinityRecorder>().ResolvedMarker);
    }

    [Fact]
    public void When_add_consumers_func_already_sets_join_ambient_the_extensions_always_new_should_still_win()
    {
        // Arrange - falsifiable direction
        var services = new ServiceCollection();
        services.AddScoped<IMarker, Marker>();
        services.AddSingleton<RegistrationAffinityRecorder>();
        services.AddConsumers(_ => new ConsumersOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped,
            MapperLifetime = ServiceLifetime.Scoped,
            TransformerLifetime = ServiceLifetime.Scoped,
            DefaultScopeAffinity = ScopeAffinity.JoinAmbient
        });
        services.AddBrighterRequestScope(ScopeAffinity.AlwaysNew);
        services.AddHostedService<ServiceActivatorHostedService>();
        var provider = services.BuildServiceProvider();

        // Act
        var resolvedOptions = provider.GetRequiredService<IBrighterOptions>();
        var ambientMarker = SendUnderAmbientScope(provider);

        // Assert
        Assert.Equal(ScopeAffinity.AlwaysNew, resolvedOptions.DefaultScopeAffinity);
        Assert.NotSame(ambientMarker, provider.GetRequiredService<RegistrationAffinityRecorder>().ResolvedMarker);
    }

    // Puts a request-scope ambient directly onto IHttpContextAccessor.HttpContext - the same thing
    // ASP.NET Core's own middleware does per request - sends RegistrationAffinityCommand while it is
    // live, then clears it, and returns the ambient scope's own IMarker for the caller to compare
    // against what RegistrationAffinityHandler actually resolved.
    private static IMarker SendUnderAmbientScope(IServiceProvider provider)
    {
        var accessor = provider.GetRequiredService<IHttpContextAccessor>();
        using var requestScope = provider.CreateScope();
        var ambientMarker = requestScope.ServiceProvider.GetRequiredService<IMarker>();
        accessor.HttpContext = new DefaultHttpContext { RequestServices = requestScope.ServiceProvider };
        try
        {
            provider.GetRequiredService<IAmACommandProcessor>().Send(new RegistrationAffinityCommand());
        }
        finally
        {
            accessor.HttpContext = null;
        }
        return ambientMarker;
    }
}
