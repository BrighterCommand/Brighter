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

#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Polly.Registry;
using System.Threading.Tasks;

namespace Paramore.Brighter.BoxProvisioning.Tests;

/// <summary>
/// <see cref="BrighterBuilderBoxProvisioningExtensions.UseBoxProvisioning"/> is single-call
/// by contract. The hosted-service registration is guarded by
/// <c>TryAddEnumerable</c>, but per-backend <c>Add{Backend}{Box}</c> registrations are not —
/// each invocation appends another <c>AddSingleton&lt;IAmABoxProvisioner&gt;</c>, so a
/// second <c>UseBoxProvisioning</c> call would double-register every provisioner and the
/// hosted service would run each migration twice. The reviewer's "guard the second call or
/// document the single-call contract loudly" prompt — we pick the louder guard: throw
/// <see cref="ConfigurationException"/> on the second call with a message that names the
/// supported pattern (all provisioners inside one delegate).
/// </summary>
public class UseBoxProvisioningIdempotencyTests
{
    [Test]
    public async Task When_called_twice_on_the_same_builder_it_should_throw_configuration_exception()
    {
        //Arrange
        var services = new ServiceCollection();
        var builder = new StubBrighterBuilder(services);
        builder.UseBoxProvisioning(_ => { });

        //Act
        Exception? thrown = null;
        try
        {
            builder.UseBoxProvisioning(_ => { });
        }
        catch (Exception e)
        {
            thrown = e;
        }

        //Assert
        await Assert.That(thrown).IsTypeOf<ConfigurationException>();
    }

    [Test]
    public async Task When_called_once_on_a_fresh_builder_it_should_not_throw()
    {
        //Arrange
        var services = new ServiceCollection();
        var builder = new StubBrighterBuilder(services);

        //Act
        Exception? thrown = null;
        try
        {
            builder.UseBoxProvisioning(_ => { });
        }
        catch (Exception e)
        {
            thrown = e;
        }

        //Assert
        await Assert.That(thrown).IsNull();
    }

    /// <summary>
    /// Companion to the double-call guard: when <c>configure(options)</c> throws on the FIRST
    /// invocation, the marker MUST NOT be registered — otherwise the operator's natural
    /// retry hits the "already invoked" guard with a misleading message. The marker
    /// registration must therefore land AFTER <c>configure(options)</c> returns
    /// successfully, so a throwing configure leaves the service collection in the same
    /// state as the never-invoked case.
    /// </summary>
    [Test]
    public async Task When_first_call_configure_throws_then_retry_with_succeeding_configure_it_should_not_throw()
    {
        //Arrange
        var services = new ServiceCollection();
        var builder = new StubBrighterBuilder(services);

        //First call — configure throws. The "already invoked" guard must not be tripped by
        //this attempt because the configure delegate failed before any registrations ran.
        Exception? firstThrown = null;
        try
        {
            builder.UseBoxProvisioning(_ => throw new InvalidOperationException("configure-side failure"));
        }
        catch (Exception e)
        {
            firstThrown = e;
        }
        await Assert.That(firstThrown).IsTypeOf<InvalidOperationException>();

        //Act — retry with a successful configure delegate.
        Exception? retryThrown = null;
        try
        {
            builder.UseBoxProvisioning(_ => { });
        }
        catch (Exception e)
        {
            retryThrown = e;
        }

        //Assert — retry succeeds. RED with marker-registered-before-configure: retry
        //would see the leaked marker and throw the ConfigurationException "already invoked"
        //message even though the first invocation never completed.
        await Assert.That(retryThrown).IsNull();
    }

    /// <summary>
    /// Sibling to the configure-throws case: when a per-backend registration callback
    /// (the <c>Add{Backend}{Box}</c> body queued into <see cref="BoxProvisioningOptions.Add"/>)
    /// throws DURING the foreach that applies them — e.g. an NRE inside an extension method or
    /// a misconfigured option — the marker MUST NOT be registered either, for the same reason.
    /// The marker registration must therefore land AFTER the registrations foreach completes,
    /// not between <c>configure(options)</c> and the foreach (which is where it lived before
    /// this fix). Pre-fix: marker registered before the foreach; a throwing registration left
    /// the marker in the collection and the operator's natural retry hit the "already invoked"
    /// guard with a misleading message.
    /// </summary>
    [Test]
    public async Task When_first_call_registration_throws_then_retry_with_succeeding_configure_it_should_not_throw()
    {
        //Arrange
        var services = new ServiceCollection();
        var builder = new StubBrighterBuilder(services);

        //First call — configure succeeds but a queued registration callback throws while the
        //foreach is applying them. Represents an NRE-throwing AddXOutbox extension.
        Exception? firstThrown = null;
        try
        {
            builder.UseBoxProvisioning(o =>
            o.Add(_ => throw new InvalidOperationException("registration-side failure")));
        }
        catch (Exception e)
        {
            firstThrown = e;
        }
        await Assert.That(firstThrown).IsTypeOf<InvalidOperationException>();

        //Act — retry with a successful configure delegate (no throwing registrations).
        Exception? retryThrown = null;
        try
        {
            builder.UseBoxProvisioning(_ => { });
        }
        catch (Exception e)
        {
            retryThrown = e;
        }

        //Assert — retry succeeds. RED with marker-registered-between-configure-and-foreach:
        //retry would see the leaked marker and throw the ConfigurationException "already
        //invoked" message even though the first invocation never completed its registrations.
        await Assert.That(retryThrown).IsNull();
    }

    /// <summary>
    /// Inline stub for <see cref="IBrighterBuilder"/>; mirrors the per-backend test stubs
    /// (e.g. <c>tests/Paramore.Brighter.PostgresSQL.Tests/BoxProvisioning/StubBrighterBuilder.cs</c>)
    /// but lives inside the core BoxProvisioning test project so the framework-level
    /// idempotency contract is testable without a per-backend test home.
    /// </summary>
    private sealed class StubBrighterBuilder : IBrighterBuilder
    {
        public IServiceCollection Services { get; }

        public StubBrighterBuilder(IServiceCollection services)
        {
            Services = services;
        }

        public IBrighterBuilder AutoFromAssemblies(IEnumerable<Assembly>? extraAssemblies = null, IEnumerable<Type>? excludeDynamicHandlerTypes = null, Type? defaultMessageMapper = null, Type? asyncDefaultMessageMapper = null) => this;
        public IBrighterBuilder AsyncHandlers(Action<IAmAnAsyncSubcriberRegistry> registerHandlers) => this;
        public IBrighterBuilder AsyncHandlersFromAssemblies(IEnumerable<Assembly> assemblies, IEnumerable<Type>? excludeDynamicHandlerTypes = null) => this;
        public IBrighterBuilder Handlers(Action<IAmASubscriberRegistry> registerHandlers) => this;
        public IBrighterBuilder HandlersFromAssemblies(IEnumerable<Assembly> assemblies, IEnumerable<Type>? excludeDynamicHandlerTypes = null) => this;
        public IBrighterBuilder MapperRegistry(Action<ServiceCollectionMessageMapperRegistryBuilder> registerMappers, Type? defaultMessageMapper = null, Type? asyncDefaultMessageMapper = null) => this;
        public IBrighterBuilder MapperRegistryFromAssemblies(IEnumerable<Assembly> assemblies, Type? defaultMessageMapper = null, Type? asyncDefaultMessageMapper = null) => this;
        public IBrighterBuilder TransformsFromAssemblies(IEnumerable<Assembly> assemblies) => this;

#pragma warning disable CS0618
        public IPolicyRegistry<string>? PolicyRegistry { get; set; }
#pragma warning restore CS0618
        public ResiliencePipelineRegistry<string>? ResiliencePolicyRegistry { get; set; }
    }
}