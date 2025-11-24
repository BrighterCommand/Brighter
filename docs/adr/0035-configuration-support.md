# 35. IConfiguration Integration for Brighter Components

Date: 2025-01-24

## Status

Proposed

## Context

Brighter currently supports configuration-as-code (see [ADR 0016](0016-use-configuration-as-code-not-by-convention.md)), which provides type safety and clarity about framework operation. However, modern .NET applications often need to manage configuration across multiple environments (development, staging, production) and sources (appsettings.json, environment variables, Azure Key Vault, etc.). While configuration-as-code is excellent for compile-time safety and explicitness, it can make environment-specific configuration management challenging, requiring code changes or recompilation to adapt to different deployment scenarios.

The .NET platform provides `IConfiguration` as a standard abstraction for configuration, which supports:
- Multiple configuration sources with fallback behavior
- Environment-specific overrides
- Secrets management integration
- Dynamic configuration updates
- Configuration validation

Several Brighter components need configuration that varies by environment:
- **Messaging Gateways** - connection strings, endpoints, credentials
- **Outbox** - database connections, table names, retention policies  
- **Inbox** - database connections, table names, deduplication settings
- **Storage Providers (Luggage Store)** - S3 buckets, blob containers, access credentials
- **Distributed Locks** - connection strings, timeout values, lock prefixes

Currently, Brighter does not provide a standardized way to load component configuration from `IConfiguration`.

## Decision

We will introduce `IConfiguration` integration for Brighter to provide a consistent pattern across all components while maintaining our configuration-as-code principles. This is not a replacement for code-based configuration but an **additive capability** that gives developers flexibility to choose the appropriate approach for their scenario.

### Core Abstraction

Introduce `IAmAConfiguration` as a core abstraction in `Paramore.Brighter` that wraps configuration access. This abstraction allows Brighter to work with configuration independently of the specific configuration system being used (Microsoft.Extensions.Configuration, .NET Aspire, or potentially others in the future).

```csharp
/// <summary>
/// Abstraction for configuration access in Brighter
/// </summary>
public interface IAmAConfiguration
{
    /// <summary>
    /// Gets a configuration sub-section with the specified key
    /// </summary>
    /// <param name="sectionName">The key of the configuration section</param>
    /// <returns>The configuration section</returns>
    IAmAConfiguration GetSection(string sectionName);
    
    /// <summary>
    /// Attempts to bind the configuration to a new instance of type T
    /// </summary>
    /// <typeparam name="T">The type to bind to</typeparam>
    /// <returns>The new instance if successful, null otherwise</returns>
    T? Get<T>();
    
    /// <summary>
    /// Gets a connection string by name
    /// </summary>
    /// <param name="name">The connection string name</param>
    /// <returns>The connection string</returns>
    string? GetConnectionString(string name);

    /// <summary>
    /// Binds the configuration to an existing instance
    /// </summary>
    /// <typeparam name="T">The type of the instance</typeparam>
    /// <param name="obj">The instance to bind to</param>
    void Bind<T>(T obj);
}
```

The implementation in `Paramore.Brighter.Extensions.Configuration` will wrap `IConfiguration`:

```csharp
public class MicrosoftConfiguration : IAmAConfiguration
{
    private readonly IConfiguration _configuration;
    
    public MicrosoftConfiguration(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    
    public IAmAConfiguration GetSection(string sectionName)
    {
        return new MicrosoftConfiguration(_configuration.GetSection(sectionName));
    }

    public T? Get<T>()
    {
        return _configuration.Get<T>();
    }

    public string? GetConnectionString(string name)
    {
        return _configuration.GetConnectionString(name);
    }

    public void Bind<T>(T obj)
    {
        _configuration.Bind(obj);
    }
}
```

### Core Principles

1. **Additive, Not Replacement** - Existing code-based configuration continues to work; configuration support is optional
2. **Consistent Patterns** - All components follow the same configuration structure and factory interface patterns
3. **Type Safety** - Configuration is strongly-typed and validated at startup
4. **Convention with Flexibility** - Default section names follow conventions but can be overridden
5. **Explicit Over Implicit** - Factories are explicit about what they create and what configuration they need

### Configuration Section Convention

All Brighter configuration follows the pattern: `Brighter:<ProviderName>`.

For components that support multiple named instances, the pattern is `Brighter:<ProviderName>:<InstanceName>`. This allows defining multiple configurations for the same provider.

Examples:
```json
{
  "Brighter": {
    "RabbitMQ": {
      "Connection": {
        "Connection": { "AmqpUri": { "Uri": "amqp://guest:guest@localhost:5672" } }
      },
      "AnalyticsConnection": {
        "Connection": { "AmqpUri": { "Uri": "amqp://guest:guest@analytics-bus:5672" } }
      }
    },
    "DynamoDb": {
      "Outbox": {
        "TableName": "brighter-outbox",
        "Region": "us-east-1"
      }
    },
    "SqlServer": {
      "Connection": "Server=.;Database=Brighter;Integrated Security=True;",
      "Inbox": {
        "Table": "inbox_table"
      }
    },
    "PostgreSql": {
      "Connection": "Host=localhost;Database=brighter;Username=postgres;Password=password"
    }
  }
}
```

### Factory Interfaces

Introduce the following factory interfaces in `Paramore.Brighter`:

```csharp
/// <summary>
/// Factory for creating messaging gateway components from configuration
/// </summary>
public interface IAmMessagingGatewayFactoryFromConfiguration
{
    /// <summary>
    /// Creates a channel factory from configuration
    /// </summary>
    /// <param name="configuration">The configuration section</param>
    /// <param name="name">Optional name for named configurations</param>
    /// <param name="sectionName">Optional override for the configuration section name</param>
    /// <returns>A channel factory instance</returns>
    IAmAChannelFactory CreateChannelFactory(IAmAConfiguration configuration, string name, string? sectionName = null);
    
    /// <summary>
    /// Creates a message consumer factory from configuration
    /// </summary>
    /// <param name="configuration">The configuration section</param>
    /// <param name="name">Optional name for named configurations</param>
    /// <param name="sectionName">Optional override for the configuration section name</param>
    /// <returns>A message consumer factory instance</returns>
    IAmAMessageConsumerFactory CreateMessageConsumerFactory(IAmAConfiguration configuration, string name, string? sectionName = null);
    
    /// <summary>
    /// Creates a producer registry from configuration
    /// </summary>
    /// <param name="configuration">The configuration section</param>
    /// <param name="name">Optional name for named configurations</param>
    /// <param name="sectionName">Optional override for the configuration section name</param>
    /// <returns>A producer registry instance</returns>
    IAmAProducerRegistry CreateProducerRegistry(IAmAConfiguration configuration, string name, string? sectionName = null);
}

/// <summary>
/// Factory for creating an outbox from configuration
/// </summary>
public interface IAmAnOutboxFactoryFromConfiguration
{
    /// <summary>
    /// Creates an outbox from configuration
    /// </summary>
    /// <param name="configuration">The configuration section</param>
    /// <param name="name">Optional name for named configurations</param>
    /// <param name="sectionName">Optional override for the configuration section name</param>
    /// <returns>An outbox instance</returns>
    IAmAnOutbox Create(IAmAConfiguration configuration, string? name = null, string? sectionName = null);
}

/// <summary>
/// Factory for creating an inbox from configuration
/// </summary>
public interface IAmAnInboxFactoryFromConfiguration
{
    /// <summary>
    /// Creates an inbox from configuration
    /// </summary>
    /// <param name="configuration">The configuration section</param>
    /// <param name="name">Optional name for named configurations</param>
    /// <param name="sectionName">Optional override for the configuration section name</param>
    /// <returns>An inbox instance</returns>
    IAmAnInbox Create(IAmAConfiguration configuration, string? name = null, string? sectionName = null);
}

/// <summary>
/// Factory for creating a storage provider (luggage store) from configuration
/// </summary>
public interface IAmAStorageProviderFactoryFromConfiguration
{
    /// <summary>
    /// Creates a storage provider from configuration
    /// </summary>
    /// <param name="configuration">The configuration section</param>
    /// <param name="name">Optional name for named configurations</param>
    /// <param name="sectionName">Optional override for the configuration section name</param>
    /// <returns>A storage provider instance</returns>
    IAmAStorageProvider Create(IAmAConfiguration configuration, string? name = null, string? sectionName = null);
}

/// <summary>
/// Factory for creating a distributed lock from configuration
/// </summary>
public interface IAmADistributedLockFactoryFromConfiguration
{
    /// <summary>
    /// Creates a distributed lock from configuration
    /// </summary>
    /// <param name="configuration">The configuration section</param>
    /// <param name="name">Optional name for named configurations</param>
    /// <param name="sectionName">Optional override for the configuration section name</param>
    /// <returns>A distributed lock instance</returns>
    IDistributedLock Create(IAmAConfiguration configuration, string? name = null, string? sectionName = null);
}
```


### Extension Methods

Add extension methods in `Paramore.Brighter.Extensions.Configuration` to simplify usage:

```csharp
public static class ConfigurationExtensions
{
    // Existing method for messaging gateways
    public static T CreateMessageGatewayConnection<T>(
        this IConfiguration configuration, 
        string? name = null, 
        string? sectionName = null) { ... }
    
    // New methods for other components
    public static IAmAnOutbox CreateOutbox<T>(
        this IConfiguration configuration,
        string? name = null,
        string? sectionName = null) where T : IAmAnOutbox
    {
        var factory = Get<IAmAnOutboxFactoryFromConfiguration>(typeof(T).Assembly);
        return factory.Create(new MicrosoftConfiguration(configuration), name, sectionName);
    }
    
    public static IAmAnInbox CreateInbox<T>(
        this IConfiguration configuration,
        string? name = null,
        string? sectionName = null) where T : IAmAnInbox
    {
        var factory = Get<IAmAnInboxFactoryFromConfiguration>(typeof(T).Assembly);
        return factory.Create(new MicrosoftConfiguration(configuration), name, sectionName);
    }
    
    public static IAmAStorageProvider CreateStorageProvider<T>(
        this IConfiguration configuration,
        string? name = null,
        string? sectionName = null) where T : IAmLuggageStorage
    {
        var factory = Get<IAmAStorageProviderFactoryFromConfiguration>(typeof(T).Assembly);
        return factory.Create(new MicrosoftConfiguration(configuration), name, sectionName);
    }
    
    public static IDistributedLock CreateDistributedLock<T>(
        this IConfiguration configuration,
        string? name = null,
        string? sectionName = null) where T : IDistributedLock
    {
        var factory = Get<IAmADistributedLockFactoryFromConfiguration>(typeof(T).Assembly);
        return factory.Create(new MicrosoftConfiguration(configuration), name, sectionName);
    }
}
```

### Implementation Examples

#### DynamoDB Outbox Configuration

```json
{
  "Brighter": {
    "DynamoDb": {
      "Outbox": {
        "TableName": "brighter-outbox",
        "Region": "us-east-1",
        "ServiceUrl": null,
        "NumberOfShards": 5,
        "ScanConcurrency": 4,
        "CreateTableOnMissing": false
      }
    }
  }
}
```

```csharp
// In Paramore.Brighter.DynamoDb
public class DynamoDbOutboxFactoryFromConfiguration : IAmAnOutboxFactoryFromConfiguration
{
    private const string DefaultSectionName = "DynamoDb";
    
    public IAmAnOutbox Create(IAmAConfiguration configuration, string? name, string? sectionName)
    {
        sectionName ??= DefaultSectionName;
        
        var configSection = configuration.GetSection($"Brighter:{sectionName}");
        if (!string.IsNullOrEmpty(name))
        {
            var namedSection = configSection.GetSection(name);
            if (namedSection != null)
                configSection = namedSection;
        }
        
        var config = configSection.Get<DynamoDbConfiguration>();
        if (config == null)
            throw new ConfigurationException($"DynamoDB configuration section 'Brighter:{sectionName}' is missing or invalid");
            
        return new DynamoDbOutbox(config);
    }
}
```

#### PostgreSQL Distributed Lock Configuration

```json
{
  "Brighter": {
    "PostgreSql": {
      "Connection": "Host=localhost;Database=brighter;Username=postgres;Password=password"
    }
  }
}
```

```csharp
// In Paramore.Brighter.Locking.PostgresSql
public class PostgreSqlDistributedLockFactoryFromConfiguration : IAmADistributedLockFactoryFromConfiguration
{
    private const string DefaultSectionName = "PostgreSql";
    
    public IDistributedLock Create(IAmAConfiguration configuration, string? name, string? sectionName)
    {
        sectionName ??= DefaultSectionName;
        
        var configSection = configuration.GetSection($"Brighter:{sectionName}");
        if (!string.IsNullOrEmpty(name))
        {
            var namedSection = configSection.GetSection(name);
            if (namedSection != null)
                configSection = namedSection;
        }
        
        var config = configSection.Get<PostgreSqlLockConfiguration>();
        if (config == null)
            throw new ConfigurationException($"PostgreSQL configuration section 'Brighter:{sectionName}' is missing or invalid");
            
        return new PostgresLockingProvider(new PostgresLockingProviderOptions(config.ConnectionString)
        {
            SchemaName = config.SchemaName,
            LockTimeout = config.LockTimeout
        });
    }
}
```

#### .NET Aspire Configuration Support

Brighter's `IAmAConfiguration` abstraction enables seamless integration with .NET Aspire. Aspire provides connection strings and configuration through the standard `IConfiguration` interface, which Brighter can consume directly.

**Aspire ConnectionString Convention:**

.NET Aspire uses a standardized connection string format: `Aspire:<ComponentName>:Client` with a `ConnectionString` property. Brighter's configuration factories will check for Aspire connection strings as a fallback when provider-specific configuration is not found.

Example Aspire configuration for RabbitMQ:

```json
{
  "Aspire": {
    "RabbitMQ": {
      "Connection": {
        "Ampq": {
          "Uri": "amqp://guest:guest@rabbitmq:5672"
        }
      }
    }
  }
}
```

The `RmqMessagingGatewayFactoryFromConfiguration` already implements Aspire support by checking multiple configuration sources in order:

1. `Brighter:RabbitMQ` - Brighter-specific configuration (highest priority)
2. `Aspire:RabbitMQ:Client` - Aspire configuration
3. `ConnectionStrings:<name>` - Standard .NET connection strings

This pattern should be followed by all factory implementations to ensure consistent Aspire integration:

```csharp
// Example pattern for Aspire support in factories
public IDistributedLock Create(IAmAConfiguration configuration, string? name, string? sectionName)
{
    sectionName ??= DefaultSectionName;
    
    // 1. Try Brighter-specific configuration
    var configSection = configuration.GetSection($"Brighter:{sectionName}");
    var config = new PostgreSqlLockConfiguration();
    configSection.Bind(config);
    
    // 2. Try Aspire configuration
    var aspireSection = configuration.GetSection($"Aspire:{sectionName}:Client");
    var aspireConnectionString = aspireSection.GetSection("ConnectionString").Get<string>();
    if (!string.IsNullOrEmpty(aspireConnectionString))
    {
        config.ConnectionString = aspireConnectionString;
    }
    
    // 3. Try standard connection strings
    var connectionString = configuration.GetConnectionString(name ?? sectionName);
    if (!string.IsNullOrEmpty(connectionString))
    {
        config.ConnectionString = connectionString;
    }
    
    if (string.IsNullOrEmpty(config.ConnectionString))
        throw new ConfigurationException($"No configuration found for {sectionName}");
        
    return new PostgresLockingProvider(new PostgresLockingProviderOptions(config.ConnectionString));
}
```

This approach ensures that Brighter components can be configured through:
- Direct Brighter configuration for full control
- .NET Aspire for cloud-native application orchestration
- Standard connection strings for simple scenarios


### Service Collection Integration

```csharp
// appsettings.json
{
  "Brighter": {
    "RabbitMQ": {
        "Connection": { "AmqpUri": { "Uri": "amqp://guest:guest@localhost:5672" } }
    }
  }
}

// Program.cs
builder.Services.AddConsumers(opt => 
    {
        opt.Subscriptions = builder.Configuration.CreateSubscription<RmqSubscription>();
        opt.DefaultChannelFactory = builder.Configuration.CreateChannelFactory<ChannelFactory>();
    })
    .AddProducers(opt => 
    {
        opt.ProducerRegistry = builder.Configuration.CreateProducerRegistry<RmqProducerRegistry>();
    });
```

## Consequences

### Positive

* **Environment Flexibility** - Developers can manage configuration through standard .NET configuration sources (appsettings.json, environment variables, Azure Key Vault, AWS Systems Manager, etc.)
* **No Code Changes** - Configuration changes don't require code modifications or recompilation for different environments
* **Consistency** - Unified configuration pattern across all Brighter components
* **Testability** - Easier to test with different configurations by swapping configuration sources
* **Secrets Management** - Integration with standard .NET secrets management tools
* **Aspire Integration** - Better support for .NET Aspire and other orchestration tools that provide configuration

### Negative

* **Two Ways to Configure** - Having both code-based and configuration-based approaches may cause confusion for new users
* **Runtime Validation** - Configuration errors are caught at runtime rather than compile-time
* **Documentation Burden** - Need to document both configuration approaches and when to use each
* **Migration Effort** - Existing provider implementations need updates to support the new factory interfaces
* **Testing Complexity** - More test scenarios to cover both configuration approaches

### Mitigation Strategies

1. **Clear Documentation** - Provide guidance on when to use each approach:
   - Use code-based configuration for simple, single-environment scenarios
   - Use IConfiguration integration for multi-environment deployments, when using secrets management, or with orchestration tools
   
2. **Validation at Startup** - Implement configuration validation that runs at application startup to catch errors early

3. **Gradual Rollout** - Implement factory interfaces incrementally across providers, starting with the most commonly used ones

4. **Examples and Samples** - Provide comprehensive samples showing both approaches

5. **Type Safety Preservation** - Use strongly-typed configuration classes that bind to IConfiguration, maintaining type safety benefits

### Migration Path

This is a non-breaking, additive change:

1. Add `IAmAConfiguration` interface to `Paramore.Brighter` core
2. Add factory interfaces to `Paramore.Brighter` core
3. Add `MicrosoftConfiguration` implementation and extension methods in `Paramore.Brighter.Extensions.Configuration`
4. Implement factory interfaces in provider packages incrementally, with Aspire configuration fallback support:
   - Phase 1: Messaging gateways (RabbitMQ, Kafka, AWS SNS/SQS, Azure Service Bus, etc.)
   - Phase 2: Outbox implementations (SQL Server, DynamoDB, PostgreSQL, MySQL, etc.)
   - Phase 3: Inbox implementations
   - Phase 4: Distributed locks (PostgreSQL, DynamoDB, MongoDb, etc.)
   - Phase 5: Storage providers (S3, Azure Blob Storage, etc.)
5. Update DI extension methods to support `*FromConfiguration` variants
6. Update documentation with configuration examples, including .NET Aspire integration scenarios
7. Existing code-based configuration continues to work unchanged

### Relationship to Other ADRs

* Complements [ADR 0016: Use Configuration As Code](0016-use-configuration-as-code-not-by-convention.md) by adding environment flexibility while maintaining explicitness
* Aligns with [ADR 0015: Push Button APIs For DSLs](0015-push-button-api-for-dsl.md) for ASP.NET Core integration
* Supports [ADR 0014: DI Friendly Framework](0014-di-friendly-framework.md) by integrating with standard .NET configuration patterns

