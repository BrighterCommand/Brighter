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
/// <see cref="BrighterBuilderBoxProvisioningExtensions.UseBoxProvisioning"/> is single-call: a
/// second call throws <see cref="ConfigurationException"/> rather than register every
/// provisioner twice, but a first call that failed can be retried.
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
    /// A <c>configure</c> delegate that throws must not leave the call marked as made.
    /// </summary>
    [Fact]
    public void When_first_call_configure_throws_then_retry_with_succeeding_configure_it_should_not_throw()
    {
        //Arrange
        var services = new ServiceCollection();
        var builder = new StubBrighterBuilder(services);

        var firstThrown = Record.Exception(() => builder.UseBoxProvisioning(_ => throw new InvalidOperationException("configure-side failure")));
        Assert.IsType<InvalidOperationException>(firstThrown);

        //Act
        var retryThrown = Record.Exception(() => builder.UseBoxProvisioning(_ => { }));

        //Assert
        Assert.Null(retryThrown);
    }

    /// <summary>
    /// A registration queued through <see cref="BoxProvisioningOptions.Add"/> that throws must
    /// not leave the call marked as made.
    /// </summary>
    [Fact]
    public void When_first_call_registration_throws_then_retry_with_succeeding_configure_it_should_not_throw()
    {
        //Arrange
        var services = new ServiceCollection();
        var builder = new StubBrighterBuilder(services);

        var firstThrown = Record.Exception(() => builder.UseBoxProvisioning(o =>
            o.Add(_ => throw new InvalidOperationException("registration-side failure"))));
        Assert.IsType<InvalidOperationException>(firstThrown);

        //Act
        var retryThrown = Record.Exception(() => builder.UseBoxProvisioning(_ => { }));

        //Assert
        Assert.Null(retryThrown);
    }

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
