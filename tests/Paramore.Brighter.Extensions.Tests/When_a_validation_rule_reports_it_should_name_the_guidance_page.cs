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
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

/// <summary>
/// AC-43 (FR-17, FR-22, FR-24.3, FR-25, NFR-10) — a cross-cutting guard over the seven ADR 0074
/// validation messages: each one, on its own single-finding host, names the guidance page rather than
/// only stating that the configuration is wrong.
/// </summary>
public class ValidationMessageGuidancePageTests
{
    private const string GuidancePage = "docs/guides/lifetimes-and-scoping.md";

    [Fact]
    public void When_the_opt_in_is_inert_the_error_should_name_the_guidance_page()
    {
        // Arrange — JoinAmbient with all three lifetimes left at the Transient default: the opt-in has no effect
        var services = new ServiceCollection();
        var builder = services.AddBrighter(options => options.DefaultScopeAffinity = ScopeAffinity.JoinAmbient);
        builder.ValidatePipelines(throwOnError: true);
        var provider = services.BuildServiceProvider();

        // Act
        var result = Validate(provider);

        // Assert — exactly one Error, naming the guidance page
        Assert.Empty(result.Warnings);
        var error = Assert.Single(result.Errors);
        Assert.Contains(GuidancePage, error.Message);
    }

    [Fact]
    public void When_transient_and_scoped_are_mixed_the_error_should_name_the_guidance_page()
    {
        // Arrange — Handler and Transformer Scoped, Mapper Transient: a mixed pair cannot share a
        // pipeline-scoped dependency
        var services = new ServiceCollection();
        var builder = services.AddBrighter(options =>
        {
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Transient;
            options.TransformerLifetime = ServiceLifetime.Scoped;
        });
        builder.ValidatePipelines(throwOnError: true);
        var provider = services.BuildServiceProvider();

        // Act
        var result = Validate(provider);

        // Assert — exactly one Error, naming the guidance page
        Assert.Empty(result.Warnings);
        var error = Assert.Single(result.Errors);
        Assert.Contains(GuidancePage, error.Message);
    }

    [Fact]
    public void When_a_singleton_artefact_has_a_captive_dependency_the_warning_should_name_the_guidance_page()
    {
        // Arrange — a Singleton mapper whose only constructor parameter is registered Scoped
        var services = new ServiceCollection();
        var subscriberRegistry = new ServiceCollectionSubscriberRegistry(services);
        services.AddSingleton(subscriberRegistry);
        var mapperRegistryBuilder = new ServiceCollectionMessageMapperRegistryBuilder(services);
        mapperRegistryBuilder.Register<CaptiveScopedCommand, CaptiveScopedMapper>();
        services.AddSingleton(mapperRegistryBuilder);
        services.AddScoped<IOrderDbContext, FakeOrderDbContext>();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Transient,
            MapperLifetime = ServiceLifetime.Singleton,
            TransformerLifetime = ServiceLifetime.Transient
        });
        var builder = new ServiceCollectionBrighterBuilder(services, subscriberRegistry, mapperRegistryBuilder);
        builder.ValidatePipelines(throwOnError: true);
        var provider = services.BuildServiceProvider();

        // Act
        var result = Validate(provider);

        // Assert — exactly one Warning, naming the guidance page
        Assert.Empty(result.Errors);
        var warning = Assert.Single(result.Warnings);
        Assert.Contains(GuidancePage, warning.Message);
    }

    [Fact]
    public void When_the_application_registers_its_own_options_the_defeated_opt_in_error_should_name_the_guidance_page()
    {
        // Arrange — the application registers IBrighterOptions itself, before AddBrighter, so
        // AddBrighterRequestScope's write-through never runs (D18)
        var services = new ServiceCollection();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped,
            MapperLifetime = ServiceLifetime.Scoped,
            TransformerLifetime = ServiceLifetime.Scoped
        });
        services.AddSingleton(new ScopeAffinityOverride(ScopeAffinity.JoinAmbient));
        var builder = services.AddBrighter(options =>
        {
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Scoped;
            options.TransformerLifetime = ServiceLifetime.Scoped;
        });
        builder.ValidatePipelines(throwOnError: true);
        var provider = services.BuildServiceProvider();

        // Act
        var result = Validate(provider);

        // Assert — exactly one Error, naming the guidance page
        Assert.Empty(result.Warnings);
        var error = Assert.Single(result.Errors);
        Assert.Contains(GuidancePage, error.Message);
    }

    [Fact]
    public void When_two_distinct_scope_providers_are_registered_the_warning_should_name_the_guidance_page()
    {
        // Arrange — two distinct IAmAScopeProvider implementations registered unkeyed
        var services = new ServiceCollection();
        services.AddSingleton<IAmAScopeProvider, AsyncLocalScopeProvider>();
        services.AddSingleton<IAmAScopeProvider, ThrowingScopeProvider>();
        var builder = services.AddBrighter(options =>
        {
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Scoped;
            options.TransformerLifetime = ServiceLifetime.Scoped;
        });
        builder.ValidatePipelines(throwOnError: true);
        var provider = services.BuildServiceProvider();

        // Act
        var result = Validate(provider);

        // Assert — exactly one Warning, naming the guidance page
        Assert.Empty(result.Errors);
        var warning = Assert.Single(result.Warnings);
        Assert.Contains(GuidancePage, warning.Message);
    }

    [Fact]
    public void When_the_opt_in_is_repeated_with_different_affinities_the_warning_should_name_the_guidance_page()
    {
        // Arrange — two constructed ScopeAffinityOverride instances carrying different affinities
        var services = new ServiceCollection();
        services.AddSingleton(new ScopeAffinityOverride(ScopeAffinity.AlwaysNew));
        services.AddSingleton(new ScopeAffinityOverride(ScopeAffinity.JoinAmbient));
        var builder = services.AddBrighter(options =>
        {
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Scoped;
            options.TransformerLifetime = ServiceLifetime.Scoped;
        });
        builder.ValidatePipelines(throwOnError: true);
        var provider = services.BuildServiceProvider();

        // Act
        var result = Validate(provider);

        // Assert — exactly one Warning, naming the guidance page
        Assert.Empty(result.Errors);
        var warning = Assert.Single(result.Warnings);
        Assert.Contains(GuidancePage, warning.Message);
    }

    [Fact]
    public void When_an_override_is_registered_by_factory_delegate_the_warning_should_name_the_guidance_page()
    {
        // Arrange — an affinity override registered by factory delegate, so RepeatedOptIn cannot read a
        // value off it
        var services = new ServiceCollection();
        services.AddSingleton(_ => new ScopeAffinityOverride(ScopeAffinity.JoinAmbient));
        var builder = services.AddBrighter(options =>
        {
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Scoped;
            options.TransformerLifetime = ServiceLifetime.Scoped;
        });
        builder.ValidatePipelines(throwOnError: true);
        var provider = services.BuildServiceProvider();

        // Act
        var result = Validate(provider);

        // Assert — exactly one Warning, naming the guidance page
        Assert.Empty(result.Errors);
        var warning = Assert.Single(result.Warnings);
        Assert.Contains(GuidancePage, warning.Message);
    }

    private static PipelineValidationResult Validate(IServiceProvider provider)
    {
        var validators = provider.GetServices<IAmAPipelineValidator>();
        return PipelineValidationResult.Combine(validators.Select(v => v.Validate()).ToArray());
    }
}
