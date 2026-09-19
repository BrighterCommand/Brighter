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
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Policies.Handlers;
using Paramore.Brighter.Transforms.Storage;
using Paramore.Brighter.Transforms.Transformers;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

/// <summary>
/// AC-42 (FR-22.3) — a captive dependency on a <c>Singleton</c> artefact is reported as one Warning, and
/// the four bounds of the detection contract (D15's constructor selection, direct-parameter-only reads, the
/// Brighter-exclusion conjunction, and its mapper-side asymmetry) are pinned.
/// </summary>
public class SingletonArtefactCaptiveDependencyTests
{
    [Fact]
    public void When_a_singleton_artefact_requires_a_scoped_service_validation_should_warn()
    {
        // Arrange — a producer-only host with {Transient, Singleton, Transient}, FR-22.2-conformant because
        // Singleton is discarded and the remainder is uniform, and a mapper whose single constructor
        // requires the AddScoped IOrderDbContext
        var services = new ServiceCollection();
        var subscriberRegistry = new ServiceCollectionSubscriberRegistry(services);
        services.AddSingleton(subscriberRegistry);
        var mapperRegistryBuilder = new ServiceCollectionMessageMapperRegistryBuilder(services);
        mapperRegistryBuilder.Register<CaptiveScopedCommand, CaptiveScopedMapper>();
        services.AddSingleton(mapperRegistryBuilder);
        services.AddScoped<IOrderDbContext, FakeOrderDbContext>();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            DefaultScopeAffinity = ScopeAffinity.AlwaysNew,
            HandlerLifetime = ServiceLifetime.Transient,
            MapperLifetime = ServiceLifetime.Singleton,
            TransformerLifetime = ServiceLifetime.Transient
        });

        var builder = new ServiceCollectionBrighterBuilder(services, subscriberRegistry, mapperRegistryBuilder);
        builder.ValidatePipelines(throwOnError: true);

        var provider = services.BuildServiceProvider();

        // Act — startup succeeds (a warning, not an error) because ValidatePipelines() called last with
        // throwOnError: true never throws on a Warning-severity finding
        var validators = provider.GetServices<IAmAPipelineValidator>();
        var result = PipelineValidationResult.Combine(validators.Select(v => v.Validate()).ToArray());

        // Assert — exactly one warning names both the mapper type and IOrderDbContext, and points at the
        // guidance page
        Assert.True(result.IsValid);
        var warning = Assert.Single(result.Warnings);
        Assert.Contains(nameof(CaptiveScopedMapper), warning.Message);
        Assert.Contains(nameof(IOrderDbContext), warning.Message);
        Assert.Contains("docs/guides/lifetimes-and-scoping.md", warning.Message);
    }

    [Fact]
    public void When_a_singleton_mapper_requires_only_singleton_and_transient_services_validation_should_not_warn()
    {
        // Arrange — a mapper whose single constructor requires only AddSingleton- and AddTransient-registered
        // services
        var services = new ServiceCollection();
        var subscriberRegistry = new ServiceCollectionSubscriberRegistry(services);
        services.AddSingleton(subscriberRegistry);
        var mapperRegistryBuilder = new ServiceCollectionMessageMapperRegistryBuilder(services);
        mapperRegistryBuilder.Register<NonCaptiveCommand, NonCaptiveMapper>();
        services.AddSingleton(mapperRegistryBuilder);
        services.AddSingleton<ISomeSingletonService, FakeSomeSingletonService>();
        services.AddTransient<ISomeTransientService, FakeSomeTransientService>();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            DefaultScopeAffinity = ScopeAffinity.AlwaysNew,
            HandlerLifetime = ServiceLifetime.Transient,
            MapperLifetime = ServiceLifetime.Singleton,
            TransformerLifetime = ServiceLifetime.Transient
        });

        var builder = new ServiceCollectionBrighterBuilder(services, subscriberRegistry, mapperRegistryBuilder);
        builder.ValidatePipelines(throwOnError: true);

        var provider = services.BuildServiceProvider();

        // Act
        var validators = provider.GetServices<IAmAPipelineValidator>();
        var result = PipelineValidationResult.Combine(validators.Select(v => v.Validate()).ToArray());

        // Assert — no Scoped dependency, so no warning
        Assert.True(result.IsValid);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void When_a_singleton_mapper_requires_a_transient_that_itself_requires_a_scoped_service_validation_should_not_warn()
    {
        // Arrange — a mapper requiring an AddTransient gateway that itself requires the AddScoped
        // IOrderDbContext — pins C-20(ii)'s direct-parameter-only limit
        var services = new ServiceCollection();
        var subscriberRegistry = new ServiceCollectionSubscriberRegistry(services);
        services.AddSingleton(subscriberRegistry);
        var mapperRegistryBuilder = new ServiceCollectionMessageMapperRegistryBuilder(services);
        mapperRegistryBuilder.Register<TransitivelyCaptiveCommand, TransitivelyCaptiveMapper>();
        services.AddSingleton(mapperRegistryBuilder);
        services.AddScoped<IOrderDbContext, FakeOrderDbContext>();
        services.AddTransient<ITransientGateway, TransientGatewayUsingScopedContext>();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            DefaultScopeAffinity = ScopeAffinity.AlwaysNew,
            HandlerLifetime = ServiceLifetime.Transient,
            MapperLifetime = ServiceLifetime.Singleton,
            TransformerLifetime = ServiceLifetime.Transient
        });

        var builder = new ServiceCollectionBrighterBuilder(services, subscriberRegistry, mapperRegistryBuilder);
        builder.ValidatePipelines(throwOnError: true);

        var provider = services.BuildServiceProvider();

        // Act
        var validators = provider.GetServices<IAmAPipelineValidator>();
        var result = PipelineValidationResult.Combine(validators.Select(v => v.Validate()).ToArray());

        // Assert — the captive dependency is transitive (through ITransientGateway), not direct, so no warning
        Assert.True(result.IsValid);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void When_a_singleton_mapper_has_two_constructors_the_widest_should_be_inspected()
    {
        // Arrange — two public constructors, a wider (ISomeSingletonService, ISomeTransientService,
        // IOrderDbContext) and a narrower (ISomeSingletonService)
        var services = new ServiceCollection();
        var subscriberRegistry = new ServiceCollectionSubscriberRegistry(services);
        services.AddSingleton(subscriberRegistry);
        var mapperRegistryBuilder = new ServiceCollectionMessageMapperRegistryBuilder(services);
        mapperRegistryBuilder.Register<WidestConstructorCommand, WidestConstructorMapper>();
        services.AddSingleton(mapperRegistryBuilder);
        services.AddSingleton<ISomeSingletonService, FakeSomeSingletonService>();
        services.AddTransient<ISomeTransientService, FakeSomeTransientService>();
        services.AddScoped<IOrderDbContext, FakeOrderDbContext>();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            DefaultScopeAffinity = ScopeAffinity.AlwaysNew,
            HandlerLifetime = ServiceLifetime.Transient,
            MapperLifetime = ServiceLifetime.Singleton,
            TransformerLifetime = ServiceLifetime.Transient
        });

        var builder = new ServiceCollectionBrighterBuilder(services, subscriberRegistry, mapperRegistryBuilder);
        builder.ValidatePipelines(throwOnError: true);

        var provider = services.BuildServiceProvider();

        // Act
        var validators = provider.GetServices<IAmAPipelineValidator>();
        var result = PipelineValidationResult.Combine(validators.Select(v => v.Validate()).ToArray());

        // Assert — D15 selects the widest constructor (most parameters), naming IOrderDbContext
        Assert.True(result.IsValid);
        var warning = Assert.Single(result.Warnings);
        Assert.Contains(nameof(WidestConstructorMapper), warning.Message);
        Assert.Contains(nameof(IOrderDbContext), warning.Message);
    }

    [Fact]
    public void When_a_singleton_handler_is_decorated_with_use_policy_async_validation_should_not_warn_against_the_decorator()
    {
        // Arrange — a Singleton handler decorated with [UsePolicyAsync], so ExceptionPolicyHandlerAsync<>
        // joins its pipeline, registered exactly as assembly scanning would register it (an open generic
        // service-to-self registration)
        var services = new ServiceCollection();
        var subscriberRegistry = new ServiceCollectionSubscriberRegistry(services);
        services.AddSingleton(subscriberRegistry);
        subscriberRegistry.RegisterAsync<PolicyDecoratedCommand, PolicyDecoratedCommandHandlerAsync>();
        subscriberRegistry.EnsureHandlerIsRegistered(typeof(ExceptionPolicyHandlerAsync<>));
        var mapperRegistryBuilder = new ServiceCollectionMessageMapperRegistryBuilder(services);
        services.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            DefaultScopeAffinity = ScopeAffinity.AlwaysNew,
            HandlerLifetime = ServiceLifetime.Singleton,
            MapperLifetime = ServiceLifetime.Transient,
            TransformerLifetime = ServiceLifetime.Transient
        });

        var builder = new ServiceCollectionBrighterBuilder(services, subscriberRegistry, mapperRegistryBuilder);
        builder.ValidatePipelines(throwOnError: true);

        var provider = services.BuildServiceProvider();

        // Act
        var validators = provider.GetServices<IAmAPipelineValidator>();
        var result = PipelineValidationResult.Combine(validators.Select(v => v.Validate()).ToArray());

        // Assert — no warning against the decorator: the handler half of the exclusion mechanism
        Assert.True(result.IsValid);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void When_a_singleton_transform_is_brighters_own_claim_check_transformer_validation_should_not_warn()
    {
        // Arrange — {_, _, TransformerLifetime = Singleton} using Brighter's own ClaimCheckTransformer, with
        // IAmAStorageProvider and IAmAStorageProviderAsync registered AddScoped. Registering the transformer
        // explicitly is what makes it a candidate at all
        var services = new ServiceCollection();
        var subscriberRegistry = new ServiceCollectionSubscriberRegistry(services);
        services.AddSingleton(subscriberRegistry);
        subscriberRegistry.Register<ClaimCheckCommand, ClaimCheckCommandHandler>();
        var mapperRegistryBuilder = new ServiceCollectionMessageMapperRegistryBuilder(services);
        mapperRegistryBuilder.Register<ClaimCheckCommand, ClaimCheckMapper>();
        services.AddSingleton(mapperRegistryBuilder);
        services.AddSingleton<ClaimCheckTransformer>();
        services.AddScoped<IAmAStorageProvider, InMemoryStorageProvider>();
        services.AddScoped<IAmAStorageProviderAsync, InMemoryStorageProvider>();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            DefaultScopeAffinity = ScopeAffinity.AlwaysNew,
            HandlerLifetime = ServiceLifetime.Transient,
            MapperLifetime = ServiceLifetime.Transient,
            TransformerLifetime = ServiceLifetime.Singleton
        });

        var builder = new ServiceCollectionBrighterBuilder(services, subscriberRegistry, mapperRegistryBuilder);
        builder.ValidatePipelines(throwOnError: true);

        var provider = services.BuildServiceProvider();

        // Act
        var validators = provider.GetServices<IAmAPipelineValidator>();
        var result = PipelineValidationResult.Combine(validators.Select(v => v.Validate()).ToArray());

        // Assert — no warning: the transform half of the exclusion mechanism, via
        // TransformAttribute.GetHandlerType(), which RequestHandlerAttribute never reaches
        Assert.True(result.IsValid);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void When_a_singleton_transform_defined_in_the_test_assembly_is_excluded_by_the_assembly_prefix_rule()
    {
        // Arrange — {Transient, Transient, Singleton} with a mapper decorated by a [WrapWith] transform
        // defined in this very Paramore.Brighter.Extensions.Tests assembly, requiring the AddScoped
        // IOrderDbContext — the only case that fails under `== "Paramore.Brighter"` and passes under the
        // prefix rule
        var services = new ServiceCollection();
        var subscriberRegistry = new ServiceCollectionSubscriberRegistry(services);
        services.AddSingleton(subscriberRegistry);
        subscriberRegistry.Register<PrefixExcludedCommand, PrefixExcludedCommandHandler>();
        var mapperRegistryBuilder = new ServiceCollectionMessageMapperRegistryBuilder(services);
        mapperRegistryBuilder.Register<PrefixExcludedCommand, PrefixExcludedMapper>();
        services.AddSingleton(mapperRegistryBuilder);
        services.AddSingleton<PrefixExcludedTransform>();
        services.AddScoped<IOrderDbContext, FakeOrderDbContext>();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            DefaultScopeAffinity = ScopeAffinity.AlwaysNew,
            HandlerLifetime = ServiceLifetime.Transient,
            MapperLifetime = ServiceLifetime.Transient,
            TransformerLifetime = ServiceLifetime.Singleton
        });

        var builder = new ServiceCollectionBrighterBuilder(services, subscriberRegistry, mapperRegistryBuilder);
        builder.ValidatePipelines(throwOnError: true);

        var provider = services.BuildServiceProvider();

        // Act
        var validators = provider.GetServices<IAmAPipelineValidator>();
        var result = PipelineValidationResult.Combine(validators.Select(v => v.Validate()).ToArray());

        // Assert — no warning: the assembly-name prefix match excludes it
        Assert.True(result.IsValid);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void When_a_singleton_mapper_defined_in_the_same_assembly_as_an_excluded_transform_validation_should_still_warn()
    {
        // Arrange — the same Paramore.Brighter.Extensions.Tests assembly, but a Singleton MAPPER (not a
        // transform) requiring the AddScoped IOrderDbContext — pinning C-20(iv)'s gap as a deliberate
        // asymmetry: no mapper is ever returned by an attribute, so the exclusion cannot reach one
        var services = new ServiceCollection();
        var subscriberRegistry = new ServiceCollectionSubscriberRegistry(services);
        services.AddSingleton(subscriberRegistry);
        var mapperRegistryBuilder = new ServiceCollectionMessageMapperRegistryBuilder(services);
        mapperRegistryBuilder.Register<AsymmetricCaptiveCommand, AsymmetricCaptiveMapper>();
        services.AddSingleton(mapperRegistryBuilder);
        services.AddScoped<IOrderDbContext, FakeOrderDbContext>();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            DefaultScopeAffinity = ScopeAffinity.AlwaysNew,
            HandlerLifetime = ServiceLifetime.Transient,
            MapperLifetime = ServiceLifetime.Singleton,
            TransformerLifetime = ServiceLifetime.Transient
        });

        var builder = new ServiceCollectionBrighterBuilder(services, subscriberRegistry, mapperRegistryBuilder);
        builder.ValidatePipelines(throwOnError: true);

        var provider = services.BuildServiceProvider();

        // Act
        var validators = provider.GetServices<IAmAPipelineValidator>();
        var result = PipelineValidationResult.Combine(validators.Select(v => v.Validate()).ToArray());

        // Assert — a warning IS reported. Same assembly as the excluded transform, opposite outcome
        Assert.True(result.IsValid);
        var warning = Assert.Single(result.Warnings);
        Assert.Contains(nameof(AsymmetricCaptiveMapper), warning.Message);
        Assert.Contains(nameof(IOrderDbContext), warning.Message);
    }

    [Fact]
    public void When_a_singleton_mapper_has_two_equally_wide_constructors_validation_should_not_warn_and_should_not_resolve()
    {
        // Arrange — two public constructors of the same parameter count, one taking IOrderDbContext and one
        // not. Not activatable by Microsoft's own container at all
        var services = new ServiceCollection();
        var subscriberRegistry = new ServiceCollectionSubscriberRegistry(services);
        services.AddSingleton(subscriberRegistry);
        var mapperRegistryBuilder = new ServiceCollectionMessageMapperRegistryBuilder(services);
        mapperRegistryBuilder.Register<TiedConstructorCommand, TiedConstructorMapper>();
        services.AddSingleton(mapperRegistryBuilder);
        services.AddScoped<IOrderDbContext, FakeOrderDbContext>();
        services.AddSingleton<ISomeSingletonService, FakeSomeSingletonService>();
        services.AddTransient<ISomeTransientService, FakeSomeTransientService>();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            DefaultScopeAffinity = ScopeAffinity.AlwaysNew,
            HandlerLifetime = ServiceLifetime.Transient,
            MapperLifetime = ServiceLifetime.Singleton,
            TransformerLifetime = ServiceLifetime.Transient
        });

        var builder = new ServiceCollectionBrighterBuilder(services, subscriberRegistry, mapperRegistryBuilder);
        builder.ValidatePipelines(throwOnError: true);

        var provider = services.BuildServiceProvider();

        // Act — validation only; TiedConstructorMapper is never resolved
        var validators = provider.GetServices<IAmAPipelineValidator>();
        var result = PipelineValidationResult.Combine(validators.Select(v => v.Validate()).ToArray());

        // Assert — a tie on the widest constructor count means nothing is inspected, so no warning
        Assert.True(result.IsValid);
        Assert.Empty(result.Warnings);
    }
}
