using Microsoft.Extensions.Configuration;
using Paramore.Brighter.ConfigurationFactory;

namespace Paramore.Brighter.Extensions.Configuration;

/// <summary>
/// Adapter that bridges Microsoft.Extensions.Configuration to Brighter's configuration abstraction.
/// Wraps <see cref="IConfiguration"/> to provide configuration access through <see cref="IAmAConfiguration"/>,
/// enabling Brighter components to work with standard .NET configuration sources.
/// </summary>
/// <remarks>
/// This adapter enables Brighter to consume configuration from any source supported by Microsoft.Extensions.Configuration,
/// including:
/// <list type="bullet">
/// <item><description>appsettings.json files</description></item>
/// <item><description>Environment variables</description></item>
/// <item><description>Command-line arguments</description></item>
/// <item><description>Azure Key Vault</description></item>
/// <item><description>AWS Systems Manager Parameter Store</description></item>
/// <item><description>.NET Aspire configuration</description></item>
/// <item><description>User secrets (development)</description></item>
/// </list>
/// <para>
/// The adapter maintains the hierarchical structure and fallback behavior of the underlying <see cref="IConfiguration"/>,
/// allowing Brighter's factory implementations to resolve configuration from multiple sources with proper precedence.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// var configuration = new ConfigurationBuilder()
///     .AddJsonFile("appsettings.json")
///     .AddEnvironmentVariables()
///     .Build();
/// 
/// var brighterConfig = new MicrosoftConfiguration(configuration);
/// var rabbitMqSection = brighterConfig.GetSection("Brighter:RabbitMQ");
/// </code>
/// </para>
/// See ADR 0035 for details on Brighter's configuration support strategy.
/// </remarks>
/// <param name="configuration">The <see cref="IConfiguration"/> instance to wrap. Must not be null.</param>
public class MicrosoftConfiguration(IConfiguration configuration) : IAmAConfiguration
{
    /// <summary>
    /// Gets a configuration sub-section with the specified key.
    /// </summary>
    /// <param name="sectionName">The key of the configuration section to retrieve.</param>
    /// <returns>A <see cref="MicrosoftConfiguration"/> wrapping the requested section.</returns>
    /// <remarks>
    /// This method delegates to <see cref="IConfiguration.GetSection"/> and wraps the result in a new
    /// <see cref="MicrosoftConfiguration"/> instance, preserving the adapter pattern through the configuration hierarchy.
    /// If the section does not exist, an empty configuration section is returned (following IConfiguration semantics).
    /// </remarks>
    public IAmAConfiguration GetSection(string sectionName)
    {
        return new MicrosoftConfiguration(configuration.GetSection(sectionName));
    }

    /// <summary>
    /// Attempts to bind the configuration to a new instance of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type to bind the configuration to. Should be a class or struct with properties matching the configuration structure.</typeparam>
    /// <returns>A new instance of <typeparamref name="T"/> with properties populated from configuration, or null if binding fails.</returns>
    /// <remarks>
    /// This method delegates to <see cref="ConfigurationBinder.Get{T}(IConfiguration)"/> which uses reflection to
    /// create an instance and populate properties. Property names are matched case-insensitively to configuration keys.
    /// Complex objects, collections, and nested types are supported.
    /// </remarks>
    public T? Get<T>()
    {
        return configuration.Get<T>();
    }

    /// <summary>
    /// Gets a connection string by name from the configuration provider.
    /// </summary>
    /// <param name="name">The name of the connection string to retrieve.</param>
    /// <returns>The connection string as a <see cref="string"/>, or null if not found.</returns>
    /// <remarks>
    /// This method delegates to <see cref="IConfiguration.GetConnectionString"/> which looks for connection strings
    /// in the "ConnectionStrings" configuration section. This is the standard .NET convention for storing database
    /// and service connection strings.
    /// <para>
    /// Example configuration:
    /// <code>
    /// {
    ///   "ConnectionStrings": {
    ///     "RabbitMQ": "amqp://guest:guest@localhost:5672",
    ///     "Database": "Server=.;Database=Brighter;Integrated Security=True;"
    ///   }
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public string? GetConnectionString(string name)
    {
        return configuration.GetConnectionString(name);
    }

    /// <summary>
    /// Binds the configuration values to an existing instance.
    /// </summary>
    /// <typeparam name="T">The type of the instance to bind to.</typeparam>
    /// <param name="obj">The existing instance whose properties should be populated from configuration.</param>
    /// <remarks>
    /// This method delegates to <see cref="ConfigurationBinder.Bind(IConfiguration, object)"/> which uses reflection
    /// to update properties of the existing object. Unlike <see cref="Get{T}"/>, this method does not create a new
    /// instance but modifies the provided one. This is useful for applying configuration overrides to objects with
    /// default values already set.
    /// Property names are matched case-insensitively to configuration keys.
    /// </remarks>
    public void Bind<T>(T obj)
    {
        configuration.Bind(obj);
    }
}
