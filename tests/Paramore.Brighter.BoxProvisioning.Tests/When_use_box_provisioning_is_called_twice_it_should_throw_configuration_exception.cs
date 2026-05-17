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
using Xunit;

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
    [Fact]
    public void When_called_twice_on_the_same_builder_it_should_throw_configuration_exception()
    {
        //Arrange
        var services = new ServiceCollection();
        var builder = new StubBrighterBuilder(services);
        builder.UseBoxProvisioning(_ => { });

        //Act
        var thrown = Record.Exception(() => builder.UseBoxProvisioning(_ => { }));

        //Assert
        Assert.IsType<ConfigurationException>(thrown);
    }

    [Fact]
    public void When_called_once_on_a_fresh_builder_it_should_not_throw()
    {
        //Arrange
        var services = new ServiceCollection();
        var builder = new StubBrighterBuilder(services);

        //Act
        var thrown = Record.Exception(() => builder.UseBoxProvisioning(_ => { }));

        //Assert
        Assert.Null(thrown);
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
