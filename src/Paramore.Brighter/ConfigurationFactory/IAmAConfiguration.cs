namespace Paramore.Brighter.ConfigurationFactory;

/// <summary>
/// Abstraction for configuration access in Brighter components.
/// Provides a configuration system-independent interface that wraps underlying configuration providers
/// such as Microsoft.Extensions.Configuration, .NET Aspire, or other configuration systems.
/// </summary>
/// <remarks>
/// This interface enables Brighter to support configuration-based initialization of components
/// (messaging gateways, outboxes, inboxes, distributed locks, storage providers) while maintaining
/// independence from specific configuration implementations.
/// See ADR 0035 for details on Brighter's configuration support strategy.
/// </remarks>
public interface IAmAConfiguration
{
    /// <summary>
    /// Gets a configuration sub-section with the specified key.
    /// </summary>
    /// <param name="sectionName">The key of the configuration section to retrieve.</param>
    /// <returns>An <see cref="IAmAConfiguration"/> representing the configuration section, or an empty section if not found.</returns>
    /// <remarks>
    /// This method is used to navigate hierarchical configuration structures. For example, to access
    /// configuration at "Brighter:RabbitMQ:Connection", you would call GetSection("Brighter"), then
    /// GetSection("RabbitMQ"), then GetSection("Connection").
    /// </remarks>
    IAmAConfiguration GetSection(string sectionName);
    
    /// <summary>
    /// Attempts to bind the configuration to a new instance of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type to bind the configuration to. Should be a class or struct with properties matching the configuration structure.</typeparam>
    /// <returns>A new instance of <typeparamref name="T"/> with properties populated from configuration, or null if binding fails.</returns>
    /// <remarks>
    /// This method creates a new instance of the target type and populates its properties based on the
    /// configuration values. Property names should match configuration keys (case-insensitive).
    /// </remarks>
    T? Get<T>();
    
    /// <summary>
    /// Gets a connection string by name from the configuration provider.
    /// </summary>
    /// <param name="name">The name of the connection string to retrieve.</param>
    /// <returns>The connection string as a <see cref="string"/>, or null if not found.</returns>
    /// <remarks>
    /// Connection strings are typically stored in a dedicated "ConnectionStrings" section in configuration
    /// files (e.g., appsettings.json). This method provides access to those connection strings.
    /// </remarks>
    string? GetConnectionString(string name);

    /// <summary>
    /// Binds the configuration values to an existing instance.
    /// </summary>
    /// <typeparam name="T">The type of the instance to bind to.</typeparam>
    /// <param name="obj">The existing instance whose properties should be populated from configuration.</param>
    /// <remarks>
    /// This method updates the properties of an existing object based on configuration values.
    /// Unlike <see cref="Get{T}"/>, this method does not create a new instance but modifies the provided one.
    /// Property names should match configuration keys (case-insensitive).
    /// </remarks>
    void Bind<T>(T obj);
}
