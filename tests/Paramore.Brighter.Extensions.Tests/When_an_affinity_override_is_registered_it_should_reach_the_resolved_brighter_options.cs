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

using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

// FR-17, FR-14, C-12, C-12a, D18; ADR 0076 step 3 — a registered ScopeAffinityOverride must reach the
// IBrighterOptions each of the four Brighter registration entry points resolves, regardless of
// registration order. Each host below uses exactly one entry point, with lifetime triple
// {Scoped, Scoped, Scoped}, and registers the override via a plain AddSingleton (never TryAdd*, since
// RegisterBrighterOptions's own factory reads it with GetService and a first-wins TryAdd would silently
// drop a second registration). Each pair's second fact is the falsifiable direction: the host sets
// DefaultScopeAffinity = JoinAmbient itself, then the override carries AlwaysNew - the property's own
// default - so a silently-dropped override would still read AlwaysNew and pass for the wrong reason.
public class ScopeAffinityOverrideRegistrationTests
{
    [Fact]
    public void When_an_affinity_override_is_registered_it_should_reach_the_resolved_brighter_options()
    {
        // Arrange - AddBrighter(Action<BrighterOptions>), override carries JoinAmbient
        var services = new ServiceCollection();
        services.AddSingleton(new ScopeAffinityOverride(ScopeAffinity.JoinAmbient));
        services.AddBrighter(options =>
        {
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Scoped;
            options.TransformerLifetime = ServiceLifetime.Scoped;
        });

        // Act
        var resolved = services.BuildServiceProvider().GetRequiredService<IBrighterOptions>();

        // Assert - the override reaches the object the factories actually read
        Assert.Equal(ScopeAffinity.JoinAmbient, resolved.DefaultScopeAffinity);
    }

    [Fact]
    public void When_add_brighter_action_already_sets_join_ambient_the_registered_override_should_still_win_with_always_new()
    {
        // Arrange - falsifiable direction
        var services = new ServiceCollection();
        services.AddBrighter(options =>
        {
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Scoped;
            options.TransformerLifetime = ServiceLifetime.Scoped;
            options.DefaultScopeAffinity = ScopeAffinity.JoinAmbient;
        });
        services.AddSingleton(new ScopeAffinityOverride(ScopeAffinity.AlwaysNew));

        // Act
        var resolved = services.BuildServiceProvider().GetRequiredService<IBrighterOptions>();

        // Assert
        Assert.Equal(ScopeAffinity.AlwaysNew, resolved.DefaultScopeAffinity);
    }

    [Fact]
    public void When_an_affinity_override_is_registered_with_add_brighter_func_it_should_reach_the_resolved_brighter_options()
    {
        // Arrange - AddBrighter(Func<IServiceProvider, BrighterOptions>), override carries JoinAmbient
        var services = new ServiceCollection();
        services.AddSingleton(new ScopeAffinityOverride(ScopeAffinity.JoinAmbient));
        services.AddBrighter(_ => new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped,
            MapperLifetime = ServiceLifetime.Scoped,
            TransformerLifetime = ServiceLifetime.Scoped
        });

        // Act
        var resolved = services.BuildServiceProvider().GetRequiredService<IBrighterOptions>();

        // Assert
        Assert.Equal(ScopeAffinity.JoinAmbient, resolved.DefaultScopeAffinity);
    }

    [Fact]
    public void When_add_brighter_func_already_sets_join_ambient_the_registered_override_should_still_win_with_always_new()
    {
        // Arrange - falsifiable direction
        var services = new ServiceCollection();
        services.AddBrighter(_ => new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped,
            MapperLifetime = ServiceLifetime.Scoped,
            TransformerLifetime = ServiceLifetime.Scoped,
            DefaultScopeAffinity = ScopeAffinity.JoinAmbient
        });
        services.AddSingleton(new ScopeAffinityOverride(ScopeAffinity.AlwaysNew));

        // Act
        var resolved = services.BuildServiceProvider().GetRequiredService<IBrighterOptions>();

        // Assert
        Assert.Equal(ScopeAffinity.AlwaysNew, resolved.DefaultScopeAffinity);
    }

    [Fact]
    public void When_an_affinity_override_is_registered_with_add_consumers_action_it_should_reach_the_resolved_brighter_options()
    {
        // Arrange - AddConsumers(Action<ConsumersOptions>) alone, override carries JoinAmbient
        var services = new ServiceCollection();
        services.AddSingleton(new ScopeAffinityOverride(ScopeAffinity.JoinAmbient));
        services.AddConsumers(options =>
        {
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Scoped;
            options.TransformerLifetime = ServiceLifetime.Scoped;
        });

        // Act
        var resolved = services.BuildServiceProvider().GetRequiredService<IBrighterOptions>();

        // Assert
        Assert.Equal(ScopeAffinity.JoinAmbient, resolved.DefaultScopeAffinity);
    }

    [Fact]
    public void When_add_consumers_action_already_sets_join_ambient_the_registered_override_should_still_win_with_always_new()
    {
        // Arrange - falsifiable direction
        var services = new ServiceCollection();
        services.AddConsumers(options =>
        {
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Scoped;
            options.TransformerLifetime = ServiceLifetime.Scoped;
            options.DefaultScopeAffinity = ScopeAffinity.JoinAmbient;
        });
        services.AddSingleton(new ScopeAffinityOverride(ScopeAffinity.AlwaysNew));

        // Act
        var resolved = services.BuildServiceProvider().GetRequiredService<IBrighterOptions>();

        // Assert
        Assert.Equal(ScopeAffinity.AlwaysNew, resolved.DefaultScopeAffinity);
    }

    [Fact]
    public void When_an_affinity_override_is_registered_with_add_consumers_func_it_should_reach_the_resolved_brighter_options()
    {
        // Arrange - AddConsumers(Func<IServiceProvider, ConsumersOptions>) alone, override carries JoinAmbient
        var services = new ServiceCollection();
        services.AddSingleton(new ScopeAffinityOverride(ScopeAffinity.JoinAmbient));
        services.AddConsumers(_ => new ConsumersOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped,
            MapperLifetime = ServiceLifetime.Scoped,
            TransformerLifetime = ServiceLifetime.Scoped
        });

        // Act
        var resolved = services.BuildServiceProvider().GetRequiredService<IBrighterOptions>();

        // Assert
        Assert.Equal(ScopeAffinity.JoinAmbient, resolved.DefaultScopeAffinity);
    }

    [Fact]
    public void When_add_consumers_func_already_sets_join_ambient_the_registered_override_should_still_win_with_always_new()
    {
        // Arrange - falsifiable direction
        var services = new ServiceCollection();
        services.AddConsumers(_ => new ConsumersOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped,
            MapperLifetime = ServiceLifetime.Scoped,
            TransformerLifetime = ServiceLifetime.Scoped,
            DefaultScopeAffinity = ScopeAffinity.JoinAmbient
        });
        services.AddSingleton(new ScopeAffinityOverride(ScopeAffinity.AlwaysNew));

        // Act
        var resolved = services.BuildServiceProvider().GetRequiredService<IBrighterOptions>();

        // Assert
        Assert.Equal(ScopeAffinity.AlwaysNew, resolved.DefaultScopeAffinity);
    }
}
