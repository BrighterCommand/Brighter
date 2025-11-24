using System;
using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace Paramore.Brighter.Extensions.Configuration;

public static class ConfigurationExtensions
{
    public static T CreateMessageGatewayConnection<T>(this IConfiguration configuration)
    {
        return CreateMessageGatewayConnection<T>(configuration, null, null);
    }
    
    public static T CreateMessageGatewayConnection<T>(this IConfiguration configuration, string? name)
    {
        return CreateMessageGatewayConnection<T>(configuration, name, null);
    }
    
    public static T CreateMessageGatewayConnection<T>(this IConfiguration configuration, 
        string? name,
        string? sectionName)
    {
        var factory = Get<IAmGatewayConfigurationFactoryFromConfiguration>(typeof(T).Assembly);
        return (T)factory.Create(new MicrosoftConfiguration(configuration), name, sectionName);
    }

    private static T Get<T>(Assembly assembly)
    {
        var @interface = typeof(T);
        foreach (var type in assembly.GetTypes())
        {
            if (!type.IsClass || type.IsAbstract)
            {
                continue;
            }

            if (@interface.IsAssignableFrom(type))
            {
                return (T)Activator.CreateInstance(type)!;
            }
        }

        throw new InvalidOperationException($"Interface {typeof(T).FullName} isn't implement");
    }
}
