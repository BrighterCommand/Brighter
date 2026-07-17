using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Polly.Registry;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

internal class StubBrighterBuilder : IBrighterBuilder
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
