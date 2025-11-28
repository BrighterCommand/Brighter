using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Paramore.Brighter.ConfigurationFactory;

namespace Paramore.Brighter.Extensions.Configuration;

/// <summary>
/// Extension methods for <see cref="IConfiguration"/> to enable configuration-based initialization of Brighter components.
/// Provides convenient methods to create messaging gateways, outboxes, inboxes, storage providers, and distributed locks
/// from configuration sources such as appsettings.json, environment variables, Azure Key Vault, or .NET Aspire.
/// </summary>
/// <remarks>
/// These extension methods provide a bridge between Microsoft.Extensions.Configuration and Brighter's configuration
/// abstraction layer. They automatically discover and instantiate the appropriate factory implementations from the
/// provider assemblies using reflection.
/// Configuration follows the convention "Brighter:{ProviderName}" with support for named instances via
/// "Brighter:{ProviderName}:{InstanceName}".
/// See ADR 0035 for details on Brighter's configuration support strategy.
/// </remarks>
public static class ConfigurationExtensions
{
    /// <summary>
    /// Creates a messaging gateway configuration from the <see cref="IConfiguration"/> with full control over section name and instance name.
    /// </summary>
    /// <typeparam name="T">The type of gateway configuration to create. Must implement <see cref="IAmGatewayConfiguration"/>.</typeparam>
    /// <param name="configuration">The <see cref="IConfiguration"/> containing the messaging gateway settings.</param>
    /// <param name="name">The optional name for the configuration instance, allowing multiple configurations for the same provider.</param>
    /// <param name="sectionName">The optional override for the configuration section name. If null, uses the provider's default section name.</param>
    /// <returns>An instance of <typeparamref name="T"/> configured from the configuration source.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no factory implementation for <see cref="IAmMessagingGatewayFromConfigurationFactory"/> is found in the assembly.</exception>
    /// <remarks>
    /// This overload provides full flexibility for configuration location. The <paramref name="sectionName"/> parameter
    /// allows overriding the default provider section name, useful for non-standard configuration structures.
    /// The method automatically discovers the factory implementation from the assembly containing <typeparamref name="T"/>
    /// and delegates configuration loading to the factory.
    /// </remarks>
    public static T CreateMessageGatewayConnection<T>(this IConfiguration configuration, 
        string? name = null,
        string? sectionName = null)
        where T : class, IAmGatewayConfiguration
    {
        var factory = Get<IAmMessagingGatewayFromConfigurationFactory>(typeof(T).Assembly);
        return (T)factory.CreateMessageConsumerFactory(new MicrosoftConfiguration(configuration), name, sectionName);
    }

    public static Subscription[] CreateSubscriptions(this IConfiguration configuration, 
        string? name = null, 
        string? sectionName = null)
    {
        var cfg = new MicrosoftConfiguration(configuration);
        var factories = GetAll<IAmMessagingGatewayFromConfigurationFactory>();
        var subscriptions = new List<Subscription>();

        foreach (var factory in factories)
        {
            subscriptions.AddRange(factory.CreateSubscriptions(cfg, name, sectionName));
        }

        return subscriptions.ToArray();
    }

    public static IAmAProducerRegistry CreateProducerRegistry(this IConfiguration configuration,
        string? name = null,
        string? sectionName = null)
    {
        var cfg = new MicrosoftConfiguration(configuration);
        var factories = GetAll<IAmMessagingGatewayFromConfigurationFactory>()
            .ToList();
        var messageProducerFactories = new List<IAmAMessageProducerFactory>();
        
        foreach (var factory in factories)
        {
            messageProducerFactories.Add(factory.CreateMessageProducerFactory(cfg, name, sectionName));
        }

        return new CombinedProducerRegistryFactory(messageProducerFactories.ToArray())
            .Create();
    }

    /// <summary>
    /// Discovers and instantiates an implementation of the specified interface from the given assembly.
    /// </summary>
    /// <typeparam name="T">The interface type to find an implementation for.</typeparam>
    /// <param name="assembly">The <see cref="Assembly"/> to search for implementations.</param>
    /// <returns>An instance of the first concrete, non-abstract class that implements <typeparamref name="T"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no implementation of <typeparamref name="T"/> is found in the assembly.</exception>
    /// <remarks>
    /// This method uses reflection to scan the assembly for concrete classes implementing the specified interface.
    /// It instantiates the first matching type using the parameterless constructor via <see cref="Activator.CreateInstance(Type)"/>.
    /// This is used internally to automatically discover factory implementations in provider assemblies.
    /// </remarks>
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
    
    private static IEnumerable<T> GetAll<T>()
    {
        var @interface = typeof(T);
        foreach (var type in AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes()))
        {
            if (!type.IsClass || type.IsAbstract)
            {
                continue;
            }

            if (@interface.IsAssignableFrom(type))
            {
                yield return (T)Activator.CreateInstance(type)!;
            }
        }
    }
}
