# Migrating to instance-scoped logging

This is a breaking change for the next major release (V11). Brighter no longer exposes `ApplicationLogging` or reads a process-wide logger factory. Each application supplies and owns its factory. Disposing one application's factory cannot redirect another application's logs.

## Dependency injection

Previously, a bare service collection could rely on a factory assigned through `ApplicationLogging.LoggerFactory`. Remove that static assignment and register logging before resolving Brighter services:

```csharp
services.AddLogging(logging => logging.AddConsole());
services.AddBrighter();
```

The generic host already registers logging. A bare `ServiceCollection` does not. Missing registration is a configuration error; Brighter does not silently substitute a no-op factory.

Objects constructed before registration still need their own logging arguments. `AddBrighter()` cannot inject a factory into an outbox or producer registry that the application has already constructed. Prefer the service-provider overload of `AddProducers`:

```csharp
services.AddBrighter().AddProducers(provider =>
{
    var factory = provider.GetRequiredService<ILoggerFactory>();
    return new ProducersConfiguration
    {
        Outbox = new SqliteOutbox(database, factory.CreateLogger<SqliteOutbox>()),
        ProducerRegistry = new RmqProducerRegistryFactory(connection, publications, factory).Create()
    };
});
```

For consumers, replace `new RmqMessageConsumerFactory(connection)` with an explicitly supplied factory, preferably resolved by the deferred registration:

```csharp
services.AddConsumers(provider => new ConsumersOptions
{
    Subscriptions = subscriptions,
    DefaultChannelFactory = new ChannelFactory(new RmqMessageConsumerFactory(
        connection, loggerFactory: provider.GetRequiredService<ILoggerFactory>()))
});
```

The same rule applies to inboxes, channel factories, schedulers, and built-in handlers constructed by a custom handler factory. Resolve their logging dependencies from the same application container.

## Fluent builders

The final configuration stage requires `ConfigureLogging(factory)` before it exposes `Build()`:

```csharp
var processor = CommandProcessorBuilder.StartNew()
    .Handlers(handlers)
    .DefaultResilience()
    .NoExternalBus()
    .NoInstrumentation()
    .RequestContextFactory(new InMemoryRequestContextFactory())
    .RequestSchedulerFactory(new InMemorySchedulerFactory(factory))
    .ConfigureLogging(factory)
    .Build();
```

`DispatchBuilder` requires the same logging stage after instrumentation. Direct calls on the concrete builder also validate configuration at runtime.

## Constructors and manual handler chains

Public constructors now require `ILoggerFactory` or `ILogger<T>`. Several signatures reorder arguments to place required dependencies before optional arguments. Recompile consumers and use named arguments when migrating positional calls. Implementers of the builder interfaces must also adopt the new logging stage.

A factory is used when an object creates other components or runtime categories. A typed logger is used for a leaf built directly by dependency injection. Internal leaf helpers may receive a pre-created logger; callers must use that helper's category.

For a manually composed handler chain, call `ConfigureLogging(factory)` on every `RequestHandler<T>` or `RequestHandlerAsync<T>` before execution. PipelineBuilder configures handlers and decorators automatically. An unconfigured manual chain fails with configuration guidance rather than silently dropping relay logs.

To disable logging deliberately, supply `NullLoggerFactory.Instance` or `NullLogger<T>.Instance`. This opt-out is appropriate for isolated tests; application samples should supply a real factory. The application owns disposal of an explicitly constructed factory. Brighter does not dispose that factory.

## Categories and upgrades

Existing logger categories remain stable, including historical category names that differ from the emitting class. This preserves `Logging:LogLevel` filters. Transformer initialization cleanup uses the transformer's factory category, matching the original implementation. Category corrections belong in a separate documented change.

Do not replace `ApplicationLogging.LoggerFactory = factory` with another static assignment. Pass the factory into the object graph or register it with the application's service collection.
