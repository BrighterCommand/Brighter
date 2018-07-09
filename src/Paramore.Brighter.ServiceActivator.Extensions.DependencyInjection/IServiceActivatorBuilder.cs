using System;
using System.Reflection;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection
{
    public interface IServiceActivatorBuilder : IBrighterHandlerBuilder
    {
        IServiceActivatorBuilder MapperRegistry(Action<ServiceCollectionMessageMapperRegistry> registerMappers);
        IServiceActivatorBuilder MapperRegistryFromAssemblies(params Assembly[] assemblies);
    }
}