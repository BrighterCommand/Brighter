using System;
using System.Reflection;
using Paramore.Brighter.AspNetCore;

namespace Paramore.Brighter.HostedService
{
    public interface IServiceActivatorBuilder : IBrighterHandlerBuilder
    {
        IServiceActivatorBuilder MapperRegistry(Action<HostMessageMapperRegistry> registerMappers);
        IServiceActivatorBuilder MapperRegistryFromAssemblies(params Assembly[] assemblies);
    }
}