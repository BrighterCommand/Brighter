# Migration: Instance-scoped logging (deprecating `ApplicationLogging`)

## What changed

Brighter previously routed all logging through a process-wide mutable static,
`Paramore.Brighter.Logging.ApplicationLogging.LoggerFactory`. The dependency-injection
extensions copied the container's `ILoggerFactory` into this static when building the
`CommandProcessor` and the `Dispatcher`.

This caused two problems:

1. **Use-after-dispose.** When the `ServiceProvider` was disposed it disposed that
   `ILoggerFactory`, but the static kept a reference to the now-disposed instance, so later
   logging could throw `ObjectDisposedException`.
2. **Cross-talk between hosts.** Two Brighter instances in one process shared the single
   static; the second to start overwrote the first, so one could log through the other's
   factory. Running two Brighters side by side never worked cleanly.

Brighter now flows an `ILoggerFactory` **as an instance** through the object graph it
constructs (command processor, outbox/producer mediator, dispatcher, message pumps,
pipelines, schedulers, …). The DI extensions resolve the container's `ILoggerFactory` and
pass it through `CommandProcessorBuilder.ConfigureLogging(...)` /
`DispatchBuilder.ConfigureLogging(...)`. Nothing writes to the static any more.

`ApplicationLogging` is now `[Obsolete]`. It remains only as a crash-free, no-op fallback
(default `NullLoggerFactory`, and `CreateLogger<T>()` tolerates a disposed factory) and will
be removed in a future release.

## Do I need to do anything?

- **Using the DI extensions (`AddBrighter` / `AddConsumers`) with `AddLogging(...)`?**
  No change required. The container's `ILoggerFactory` is picked up automatically and flowed
  to the objects Brighter builds.

- **Constructing transports, outboxes/inboxes, or other Brighter objects yourself?**
  Those types now take an optional trailing `ILoggerFactory? loggerFactory = null`
  (stores take `ILogger? logger = null`). If you do not pass one, that object logs to a
  no-op logger (it no longer falls back to a process-wide static). To keep log output, pass
  your `ILoggerFactory` through the relevant factory/constructor, e.g.:

  ```csharp
  var producerRegistry = new RmqProducerRegistryFactory(
      connection,
      publications,
      loggerFactory)          // <-- pass your ILoggerFactory
      .Create();

  var outbox = new SqliteOutbox(
      configuration,
      logger: loggerFactory.CreateLogger<SqliteOutbox>());
  ```

- **Referencing `ApplicationLogging` directly?** Replace it with an injected
  `ILoggerFactory`. Setting `ApplicationLogging.LoggerFactory` no longer affects Brighter's
  logging.

## Why the behaviour change (silent unless a factory is supplied)?

Keeping the static populated is what made two Brighters unable to coexist and what caused the
dispose-time crash. Removing it is the only way to make logging correctly scoped per instance.
Where Brighter owns construction (via DI/hosting), it supplies the factory for you; where you
own construction, you now supply it explicitly.
