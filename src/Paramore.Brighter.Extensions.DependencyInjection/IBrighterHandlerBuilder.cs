using System;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    public interface IBrighterHandlerBuilder
    {
        IBrighterHandlerBuilder AutoFromAssemblies(params Assembly[] assemblies);
        IBrighterHandlerBuilder Handlers(Action<IAmASubscriberRegistry> registerHandlers);
        IBrighterHandlerBuilder HandlersFromAssemblies(params Assembly[] assemblies);
        IBrighterHandlerBuilder AsyncHandlers(Action<IAmAnAsyncSubcriberRegistry> registerHandlers);
        IBrighterHandlerBuilder AsyncHandlersFromAssemblies(params Assembly[] assemblies);
        IBrighterHandlerBuilder MapperRegistry(Action<ServiceCollectionMessageMapperRegistry> registerMappers);
        IBrighterHandlerBuilder MapperRegistryFromAssemblies(params Assembly[] assemblies);

        IServiceCollection Services { get; }
    }
}
