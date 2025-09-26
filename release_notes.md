# Release Notes #
NuGet packages for the last good build on master are available via [AppVeyor](https://ci.appveyor.com/project/BrighterCommand/paramore-brighter) (see Artifacts) or the nuget feed is [https://ci.appveyor.com/nuget/paramore-brighter-m289d49fraww](https://ci.appveyor.com/nuget/paramore-brighter-m289d49fraww). The simplest way to work with this is to use the Nuget Feed, which is every successful build, So 

When we push a collection of functionality it is available via [nuget.org](http://www.nuget.org) and symbol files are published to [symbolsource.org](http://www.symbolsource.org)

This section lists features in master, available by [AppVeyor](https://ci.appveyor.com/project/BrighterCommand/paramore-brighter), but not yet deployed to [nuget.org](http://www.nuget.org).

## Master ##


## Release 10.0.0 ##

### Proactor and Reactor

We have made significant changes to Brighter's concurrency models. We now use terminology that derives from the Reactor and Proactor patterns, replacing the previous "blocking" and "non-blocking" terminology with clearer semantic meaning.

- **Reactor Model**: Uses blocking I/O for optimal performance in single-threaded scenarios
- **Proactor Model**: Uses non-blocking I/O for improved throughput when sharing resources across multiple threads

We now have a complete async pipeline for the Proactor and a complete sync pipeline for the Reactor, whereas previously only dispatch was async in the Proactor pipeline. Our synchronization context has been updated to use Stephen Cleary's AsyncEx approach instead of Stephen Toub's original article, providing better error handling and more reliable continuation management.

**Breaking Change**: The `runAsync` flag on Subscription has been renamed to `MessagePumpType` for clarity. Update your subscriptions:

```csharp
// V9
var subscription = new Subscription(typeof(MyHandler), isAsync: true);

// V10
var subscription = new Subscription(typeof(MyHandler), messagePumpType: MessagePumpType.Proactor);
```

### Dynamic Request Types and Cloud Events

Brighter now supports multiple message types on the same channel through dynamic request type resolution. This enables content-based routing where the message type is determined at runtime from Cloud Events metadata rather than compile-time generic parameters.

**Breaking Change**: Message pump generic parameters have been removed. The message type is now determined at runtime using a `Func<Message, Type>` strategy:

```csharp
// V9
var pump = new Proactor<MyRequest>(subscription, channel);

// V10
var pump = new Proactor(subscription, channel);
// Type determined via Cloud Events or subscription strategy
```

### Cloud Events Support

Full Cloud Events specification support has been added across all transports:

- **JSON Format**: Support for Cloud Events in JSON format with configurable additional properties
- **Transport Integration**: Native Cloud Events support in Kafka, AWS SNS/SQS, RabbitMQ, and MQTT
- **Message Routing**: Use Cloud Events type for message routing and handler dispatch

```csharp
// Configure Cloud Events
var publication = new Publication 
{
    CloudEventsFormat = CloudEventsFormat.Json,
    CloudEventsAdditionalProperties = new Dictionary<string, object>
    {
        ["extension"] = "value"
    }
};
```

### OpenTelemetry Integration

Comprehensive OpenTelemetry support has been added throughout Brighter:

- **Transport Tracing**: Automatic trace propagation across message boundaries with W3C TraceContext ([PR #3605](https://github.com/BrighterCommand/Brighter/pull/3605))
- **Outbox Tracing**: Distributed tracing for all outbox implementations ([PR #3557](https://github.com/BrighterCommand/Brighter/pull/3557))
- **Inbox Tracing**: OpenTelemetry support for all inbox implementations ([PR #3576](https://github.com/BrighterCommand/Brighter/pull/3576))
- **Claim Check Tracing**: Tracing support for claim check pattern and luggage stores ([PR #3614](https://github.com/BrighterCommand/Brighter/pull/3614))
- **Span Attributes**: Rich semantic attributes following OpenTelemetry conventions
- **Instrumentation Control**: Configurable instrumentation options across all tracer operations ([PR #3620](https://github.com/BrighterCommand/Brighter/pull/3620))

OpenTelemetry integration enables end-to-end distributed tracing across message boundaries, making it easier to diagnose performance issues and understand message flow in distributed systems.

### Nullable Reference Types

**Breaking Change**: Nullable reference types are now enabled across all projects. You may need to update your code to handle nullable warnings:

```csharp
// Ensure proper null handling
public class MyHandler : RequestHandler<MyCommand>
{
    public override MyCommand Handle(MyCommand command)
    {
        // compiler will now warn about potential null references
        return command ?? throw new ArgumentNullException(nameof(command));
    }
}
```

### Builder API Changes

**Breaking Change**: Builder methods have been renamed for clarity:

```csharp
// V9
services.AddBrighter()
    .UseMessaging()
    .UseRmqTransport();

// V10  
services.AddBrighter()
    .AddProducers()
    .AddConsumers();
```

### Polly Resilience Pipeline

**Breaking Change**: New resilience pipeline attributes replace legacy timeout policies ([PR #3677](https://github.com/BrighterCommand/Brighter/pull/3677)):

```csharp
// V9 - Deprecated
[TimeoutPolicy(milliseconds: 5000)]
public override MyResult Handle(MyCommand command) { }

// V10 - New approach
[UseResiliencePipeline("MyPipeline")]
public override MyResult Handle(MyCommand command) { }
```

The `TimeoutPolicyAttribute` is now marked as obsolete. The new approach provides:
- **Full Polly v8 Support**: Access to all Polly resilience strategies
- **CancellationToken Integration**: Proper cancellation token flow from resilience pipelines ([PR #3698](https://github.com/BrighterCommand/Brighter/pull/3698))
- **Enhanced Context**: Request context integration with Polly's resilience context

Configure resilience pipelines in your DI container:

```csharp
services.AddResilienceEnrichment();
services.Configure<ResilienceOptions>(options =>
{
    options.Pipelines.Add("MyPipeline", builder => builder
        .AddTimeout(TimeSpan.FromSeconds(5))
        .AddRetry(new RetryStrategyOptions())
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions()));
});
```

### Default Message Mappers

**Important Clarification**: An explicit message mapper isn't always required for every `IRequest`. Brighter provides default mappers that can handle most common scenarios:

- **JsonMapper**: Automatically handles JSON serialization/deserialization for requests and events
- **CloudEventsMapper**: Handles Cloud Events format messages 
- **Built-in Fallbacks**: Brighter will attempt to use appropriate default mappers when no explicit mapper is registered

You only need to create custom message mappers when you require explicit transforms or have specific serialization requirements. The default mappers can also serve as templates for custom implementations.

```csharp
// No explicit mapper needed - will use JsonMapper by default
public class MyCommand : Command<MyCommand> 
{
    public string Name { get; set; }
    public int Value { get; set; }
}

// Only create custom mappers when you need specific transforms
public class MyCustomMapper : IAmAMessageMapper<MyCommand>
{
    // Custom serialization logic here
}
```

### Request Context Enhancements

**Breaking Change**: The `IRequestContext` interface has been enhanced to support:

- **Partition Key**: Set message partition keys dynamically (see [PR #3678](https://github.com/BrighterCommand/Brighter/pull/3678))
- **Custom Headers**: Add headers via request context
- **Resilience Context**: Integration with Polly Resilience Pipeline

```csharp
// Set partition key and custom headers via request context
public class MyHandler : RequestHandler<MyCommand>
{
    public override MyCommand Handle(MyCommand command)
    {
        Context.Span.SetAttribute("custom.header", "value");
        Context.PartitionKey = command.TenantId;
        
        return base.Handle(command);
    }
}
```

### AWS SDK v4 Support

Complete AWS SDK v4 support has been added:

- **SNS/SQS**: Standard and FIFO queue support
- **DynamoDB**: Inbox, Outbox, and Distributed Lock implementations  
- **S3**: Luggage store for claim check pattern

You can now use the latest AWS SDK v4 while maintaining backwards compatibility with v3.

### Transport Improvements

**PostgreSQL Message Broker**: Added support for using PostgreSQL as a message broker ([PR #3612](https://github.com/BrighterCommand/Brighter/pull/3612)), enabling pub/sub messaging patterns directly with PostgreSQL's LISTEN/NOTIFY functionality.

**RabbitMQ Enhancements**:
- **Quorum Queues**: Support for RabbitMQ quorum queues for improved consistency and availability ([PR #3638](https://github.com/BrighterCommand/Brighter/pull/3638))
- **RabbitMQ 7.x**: Updated to support RabbitMQ client library v7
- **Connection Stability**: Improved connection handling and error recovery ([PR #3685](https://github.com/BrighterCommand/Brighter/pull/3685))

```csharp
// Configure Quorum queues
var subscription = new RmqSubscription<MyMessage>(
    queueType: QueueType.Quorum,
    isDurable: true,         // Required for quorum queues
    highAvailability: false  // Must be false for quorum queues
);
```

**Kafka Improvements**:
- **Configuration Callback**: Enhanced configuration support through KafkaSubscription callback ([PR #3640](https://github.com/BrighterCommand/Brighter/pull/3640))
- **Updated Defaults**: Improved default configuration values for better out-of-the-box experience ([PR #3618](https://github.com/BrighterCommand/Brighter/pull/3618))

**AWS Improvements**:
- **SQS Publication Enhancement**: Allow setting queue attributes and channel type in SqsPublication ([PR #3666](https://github.com/BrighterCommand/Brighter/pull/3666))
- **S3 Claim-Check**: Fixed AWS S3 claim-check implementation ([PR #3632](https://github.com/BrighterCommand/Brighter/pull/3632))

**General Transport Features**:
- **Cloud Events Unwrapping**: Fixed Cloud Events unwrapping for JSON format ([PR #3686](https://github.com/BrighterCommand/Brighter/pull/3686))
- **Outbox Improvements**: Enhanced outbox pattern with better URI handling and code consolidation ([PR #3659](https://github.com/BrighterCommand/Brighter/pull/3659))
- **RocketMQ Fixes**: Various improvements to RocketMQ transport ([PR #3696](https://github.com/BrighterCommand/Brighter/pull/3696))

### Circuit Breaking

Topic-level circuit breaking has been added to prevent cascade failures:

- **Failure Tracking**: Automatic tracking of dispatch failures per topic
- **Configurable Thresholds**: Set failure thresholds and cooldown periods  
- **Automatic Recovery**: Topics automatically recover after cooldown period
- **Bulk Dispatch Support**: Circuit breaking now properly supports bulk dispatch operations ([PR #3688](https://github.com/BrighterCommand/Brighter/pull/3688))
- **Per-Transport Integration**: Circuit breaking is integrated with MongoDB ([PR #3692](https://github.com/BrighterCommand/Brighter/pull/3692)) and other outbox implementations

The bulk dispatch implementation brings circuit breaking inline with single dispatch, allowing individual batches to be retried and providing better control over transport-specific batching behavior.

See [PR #3651](https://github.com/BrighterCommand/Brighter/pull/3651) for the core implementation and [PR #3688](https://github.com/BrighterCommand/Brighter/pull/3688) for bulk dispatch support.

### Scheduled Messaging

Brighter now supports scheduling messages to be sent at specific times or after delays. This feature allows you to defer message processing until a future time, enabling scenarios like delayed notifications, scheduled tasks, and time-based workflows.

- **Time-based Scheduling**: Schedule messages for specific dates and times
- **Delay-based Scheduling**: Schedule messages to be sent after a specified delay
- **Multiple Schedulers**: Support for Quartz, AWS EventBridge Scheduler, and Azure Service Bus scheduled messages
- **Command Processor Integration**: New `SendAt` and `PublishAt` methods on the Command Processor

```csharp
// Schedule a command to be sent at a specific time
await commandProcessor.SendAt(new MyCommand(), DateTime.UtcNow.AddHours(1));

// Schedule an event to be published after a delay  
await commandProcessor.PublishAt(new MyEvent(), TimeSpan.FromMinutes(30));

// Configure with Quartz scheduler
services.AddBrighter()
    .UseQuartzScheduler(options => {
        options.ConnectionString = connectionString;
    });
```

See [PR #3487](https://github.com/BrighterCommand/Brighter/pull/3487) for more details.

### Multi-Transport Support

A new `CombinedChannelFactory` enables using multiple transports simultaneously:

```csharp
var factory = new CombinedChannelFactory();
factory.AddTransport("kafka", kafkaFactory);
factory.AddTransport("rabbitmq", rmqFactory);
```

### Performance Improvements

- **GUID v7**: Support for GUID v7 on .NET 9+ for better database performance ([PR #3687](https://github.com/BrighterCommand/Brighter/pull/3687))
- **Sealed Classes**: Internal classes sealed to reduce virtual dispatch overhead ([PR #3552](https://github.com/BrighterCommand/Brighter/pull/3552))
- **Optimized Collections**: Reduced dictionary lookups and improved collection usage ([PR #3550](https://github.com/BrighterCommand/Brighter/pull/3550))
- **Memory Optimization**: Better memory usage in SQL data readers and stream handling ([PR #3625](https://github.com/BrighterCommand/Brighter/pull/3625))
- **Source-Generated Logging**: Migrated to source-generated logging for superior performance and stronger typing ([PR #3579](https://github.com/BrighterCommand/Brighter/pull/3579))
- **Reduced Allocations**: Optimized string comparisons and reduced unnecessary allocations ([PR #3555](https://github.com/BrighterCommand/Brighter/pull/3555))

GUID v7 provides better database clustering and performance characteristics compared to GUID v4, especially beneficial for high-throughput scenarios with database-backed outboxes and inboxes.

### .NET 9 Support

Brighter now supports .NET 9 with optimizations for the latest runtime features.

### Test Infrastructure and Developer Experience

**Breaking Change**: Fluent Assertions has been removed in favor of xUnit assertions for better maintainability and licensing compliance.

**Enhanced Testing**:
- **Colorful Test Output**: Improved test runner with colorful output and GitHub Actions logger support ([PR #3635](https://github.com/BrighterCommand/Brighter/pull/3635))
- **Better Test Infrastructure**: Enhanced test reliability and coverage across all transport implementations
- **Connection Provider Registration**: Improved registration of connection and transaction provider interfaces ([PR #3668](https://github.com/BrighterCommand/Brighter/pull/3668))

**Code Quality Improvements**:
- **CA1852 Warnings**: Enabled and fixed analyzer warnings for unsealed classes ([PR #3554](https://github.com/BrighterCommand/Brighter/pull/3554))
- **RFC3339 Typo Fix**: Corrected method name typo `ToRcf3339` to `ToRfc3339` ([PR #3622](https://github.com/BrighterCommand/Brighter/pull/3622))

### Command Processor Dispatching Strategy

Enhanced command processor with support for content-based routing using specification patterns ([PR #3652](https://github.com/BrighterCommand/Brighter/pull/3652)). This enables routing requests based on content rather than just type, supporting more sophisticated message routing scenarios.

### Additional Bug Fixes and Improvements

- **Outbox Sweeper**: Fixed NullReference exception in outbox sweeper ([PR #3683](https://github.com/BrighterCommand/Brighter/pull/3683))
- **ASB Defer Exception**: Fixed issue where Azure Service Bus defer exception caused attempted reject then complete ([PR #3619](https://github.com/BrighterCommand/Brighter/pull/3619))
- **Scheduler Tests**: Fixed scheduler tests for long scheduling windows with proper EntryTimeToLive configuration ([PR #3582](https://github.com/BrighterCommand/Brighter/pull/3582))
- **Quorum Queue Tests**: Enhanced quorum queue testing to properly validate queue creation ([PR #3642](https://github.com/BrighterCommand/Brighter/pull/3642))

### Breaking Changes Summary

For users upgrading from V9 to V10:

1. **Update Subscription Configuration**:
   - Replace `isAsync/runAsync` with `MessagePumpType.Proactor` or `MessagePumpType.Reactor`

2. **Handle Nullable Reference Types**:
   - Address nullable warnings in your handlers and commands

3. **Update Builder Calls**:
   - Replace messaging builder methods with `AddProducers()`/`AddConsumers()`

4. **Migrate Timeout Policies**:
   - Replace `[TimeoutPolicy]` with `[UseResiliencePipeline]` and Polly configuration

5. **Message ID Changes**:
   - Message and Correlation IDs are now strings (defaulting to GUID strings)

6. **Generic Message Pumps**:
   - Remove generic type parameters if directly instantiating message pumps

7. **Test Framework Changes**:
   - Replace Fluent Assertions with xUnit assertions in your test projects

8. **Default Message Mappers**:
   - Review your message mappers - many can now be removed in favor of default implementations

### Database Schema Updates

If you use Inbox/Outbox patterns, you may need to update your database schemas. New DDL scripts are available in the repository for each supported database provider.

### Migration Guide

For detailed migration guidance, see the [V10 Migration Guide](https://brightercommand.github.io/Brighter/migration/v10) in our documentation.

## Release 9.X ##

## Binary Serialization Fixes

* MessageBody  nows store the character encoding type (defaults to UTF8) to allow correct conversion back to a string when using Value property
* Use a CharacterEncoding.Raw for binary content (will be a Base64 string for Value)
* Kafka transport payload is now byte[] and not string. This prevents corruption of Kafka 'header' of 5 bytes to store schema registry when used with schema registry support
* DynamoDb now uses a byte[] and not a string for the message body to prevent lossy conversions
* ContentType on Header is set from Body, if not set on the Header

## Kafka Fixes

* Kafka now serliases the ReplyTo Header correctly

## New Transforms

* Compression Transform now available to compress messages using Gzip (or Brotli or Deflate on .NET 6 or 7)

## Release 9.3.6 ##

- Set correct partition key (kafka key) for Kafka messages  
- Add default option for Header bags serialisation 
- Set correct span status for Send and SendAsync @easyfy-fredrik
- Note that this version pulls v7 of System.Text.Json which has breaking changes for users of System.Text.Json, see https://devblogs.microsoft.com/dotnet/system-text-json-in-dotnet-7/#breaking-changes

## Release 9.3.0 ##
- Bug with DynamoDb Outbox and the Outbox Sweeper fixed. The Sweeper required a topic argument supplied by a dictionary of args
  - Required adding a Dictionary<string, object> to various interfaces, which defaults to null, hence the minor version bump as these interfaces have new capabiities
- Internal change to move outstanding message box to a semaphore slim over a mutex as thread-safe. Not strictly neededm, but follows our policy of moving to semaphore slim
- Changes to the DynamoDb Outbox implementation as Outstanding Message check was not behaving as expected
- The interfaces around Outbox configuration will likely change in v10 to avoid current split and need to configure on both publication and outbox

## Release 9.1.20 ##
- Bug with Kafka Consumer failing to commit offsets fixed. Caused by Monitor being used for a lock on one thread and released on another, which does not work. Replaced with SemaphoreSlim.
- Behavior of Kafka Consumer offset sweep changed. It now runs every x seconds, and not every x seconds since a flush. This will cause it to run more frequently, but it is easier to reason about.

## Release 9.1.14 ##
 - Fixed missing negation operator when checking for AWS resources 

## Release 9.1.14 ##
 - Renamed MessageStore to Outbox and CommandStore to Inbox for clarity with well-known pattern names outside this team
     - Impact is wide, namespaces, class names and project names, so this is a ***BREAKING CHANGE***
     - Mostly you can search and replace to fix
 - Added support for a global inbox via a UseInbox configuration parameter to the Command Processor
    - Will insert an Inbox in all pipelines
    - Can be overriden by a NoGlobalInbox attribute for don't add to pipeline, or an alternative UseInbox attribute to vary config
 - The goal here is to be clearer than our own internal names, which don't help folks who were not part of this team
 - The Outbox now fills up if a producer fails to send. You can set an upper limit on your producer, which is the maximum outstanding messages that you want in the Outbox before we throw an exception. This is not the same as Outbox size limits or sweeper, which is seperate and mainly intended if you don't want the Outbox limit to fail-fast on hitting a limit but keep accumulating results  
 - Added caching of attributes on target handlers in the pipeline build step
     - This means we don't do reflection every time we build the pipeline for a request
     - We do still always call the handler factory to instantiate as we don't own handler lifetime, implementer does
     - We added a method to clear the pipeline cache, particularly for testing where you want to test configuration scenarios
 - Added ability to persist RabbitMQ messages
 - Added subscription to blocked/unblocked RMQ channel events. A warning log is created when a channel becomes blocked and an info log is generated when the channel becomes unblocked.
 - Improved the Kafka Client. It now uses the publisher/creator model to ensure that a message is in Brighter format i.e. headers as well as body; updated configuration values; generally improved reliability. This is a breaking change with previous versions of the Kafka client.
 - The class BrighterMessaging now only has a default constructor and now has setters on properties. Use the initializer syntax instead - new BrighterMessage{} to avoid having redundant constructor arguments.
 - Changes to how we configure transports - renaming classes and extending their functionality
     - Connection is renamed to Subscription
     - Added a matching Publication for producers
     - Base class includes the attributes that Brighter Core (Brighter & ServiceActivator) need
     - Derived classes contain transport specific details
     - On SQSConnection, renamed VisibilityTimeout to LockTimeout to more generically describe its purpose seperated from GatewayConfiguration, that now has a marker interface, used to connect to the Gateway and not about how we publish or subscribe
     - We now have the option to declare infastructure separately and Validate or Assume it exists, still have an option to Create which is the default
     - We think it will be most useful for environments like AWS where there is a price to checking (HTTP call, and often looping through results)  
     - Added support for a range of parameters that we did not have before such as dead letter queues, security etc via these platform specific configuration files  
 - Provided a short form of the BrighterMessaging constructor, that queries object provided for async versions of interfaces
 - Changed IsAsync to RunAsync on a Subscription for clarity
 - Supports an async pipeline: callbacks should happen on the same thread as the handler (and the pump), avoiding thread pool threads
 - Fixed issue in SQlite with SQL to mark a message as dispatched
 
## Release 8.1.1399 ##
 - Update nuget libs
 - RabbitMQ 6.*
 - Fix correlationid no been sent correctly when using SqlCommandStore

## Release 8.1.1036 ##
  - Fixes issue when a rabbitmq connection is dropped it sometimes ends up with 2 connections and then does not dispose the ghost connection.
  - Fix for System.InvalidOperationException: You cannot enqueue more items than the buffer length #846
  - fix for Suppress and log BrokerUnreachableException during ResetConnection #502

## Release 8.0.* ##
  - Added SourceLink debugging and are shipping .pdb files in the nuget package.
  - Strong Name in line with Open Source guidance https://docs.microsoft.com/en-us/dotnet/standard/library-guidance/strong-naming. Where libraries we rely on are not strong named we don't strong name our code.
  - Removed `IAmAPolicyRegistry` and replaced it with `IPolicyRegistry<string>` from Polly, it is a drop in replacement but in a the Polly namespace.
  - Removed our `PolicyRegistry` and now use the `PolicyRegistry` from Polly, it is a drop in replacement but in a the Polly namespace.
  - Support for Feature Switches on handlers
  - Switch Command Sourcing Handler to using an Exists method when checking for duplicate messages
  - Rewritten AWS SQS + SNS transport
  - Support for DynamoDB Message and Command Stores (Jonny Olliff-Lee @DevJonny)
  - Added a Call() method to CommandProcessor to support Request-Reply
  - Add a context field to the command store, to allow identification of a context, and share a table across multiple handlers. Note that this is a breaking schema change for users of the command store
  - Command Sourcing handler now writes to store only once the handler has successfully completed
  - Renamed InputChannelFactory to ChannelFactory as we don't have an OutputChannelFactory any more (and not for some time)
  - Channel buffer now only source for message pump, populated via consumer when empty
  - Consumers now return an array of messages, default size of 1 but can be up to 10
  - Switch RMQ Consumers back to basic consume to support batch delivery
  - RMQ now supports batch sizes of up to 10 for consuming messages
  - SNS+SQS now supports batch sizes of up to 10 for consuming messages
  - Added support for the Outbox pattern via DepositPost and ClearPostBox
  - Fixed https://github.com/BrighterCommand/Brighter/issues/156 to allow different exchange types to be set (was broken by support of delayed exchange)
  
   
## Release 7.4.0 ##
  - Updated to signed version of Polly, works with netcore2.1.
  - Fix for Sql CommandStore.
  - Fixes to make flaky tests stable.
  
## Release 7.3.0 ##  
  - Added beta Support for a Redis transport
  - Support for Binding a channel to multiple topics 
  - RMQ Transport: Fixed handling of socket timeout where node we are connected to (not master) partitions from cluster and is paused under the pause minority strategy. Now resets connection successfully.
  - RMQ Transport: Fixed issue with OperationInterrupted exception when master node partitions and we are connected to it
  - Overall improved reliability of Brighter RMQ transport when connecting to a cluster that experiences a partition
  - Fixed an issue where multiple performers did not have distinct names and so could not be tracked
  - RMQ changed from push rabbit consumer to just simple pull based.

## Release 7.2.0 ##
  - Support for PostgreSql Message Store (Tarun Pothulapati @Pothulapati)
  - Support for MySql Message and Command Stores (Derek Comartin @dcomartin)
  - Support for Kafka Messaging Gateway - Beta (Wayne Hunsley @whunsley)
  - Support for MSSql Messaging Gateway - Beta (Fred Hoogduin @Red-F)

## Release 7.1.0 ##
  - Fixes issue with high CPU when failing to connect to RabbitMQ.
  - Fixes missing High Availability setting, had to make changes to IAmAChannelFactory.

## Release 7.0.137 - 7.0.143 ##
  - Support for .NET Core (NETSTANDARD 1.5)

**Breaking Changes**
  - Configuration no longer supports XML based config sections. We use data structures instead, and expect you to configure mostly in code, initializing those data structures from your config system of choice yourself. We recommend following 12-Factor Apps guidelines and preferring enviroment variables for items that vary by environment over XML or JSON based configuration files. (We may consider providing config sections in Contrib again, please feedback if this is a critical issue for you. PRs welcome.)
  - Dropped CommandProcessor from namespaces and folder names, to shorten, and remove semantic issue that it is not just a Command Processor
  - Changed namespaces and folders to be CamelCase
  - As a result, your using statements will need revision with this release
  - Some namespaces i.e Paramore.Brighter.Policy changed to avoid clashes now CamelCase (has become Paramore.Brighter.Policies)


## Release 6.1.0 ##
 - Support for binary message payloads i.e. not just text/plain for JSON or XML. Current support is modelled around use of protobuf over RMQ

## Release 6.0.28 ##

Fix issue with encoding of non-string types and transmission of correlation id https://github.com/BrighterCommand/Brighter/pull/180

## Release 6.0.6 ##
- Increase logging level when we stop reading from a queue that cannot be readhttps://github.com/BrighterCommand/Brighter/pull/179
- Peformance issue caused by creation of a logger per requesthandler instance. The logger is now static, but is initialized lazily and can be overridden for TDD or legacy compatibility 

## Release 6.0.0 ##

**Breaking Changes**
- CommandProcessorBuilder no longer takes .Logger(logger)
- In the abstract RequestHandler `logger` is now `Logger`
- `RequestLogging` has moved namespace to `paramore.brighter.commandprocessor.logging.Attributes`

**Bug fixes**:

- Fixed issue #132: concurrent usages of the RabbitMQ messaging gateway would sometimes throw an exception
- Fixed issue #134: We no longer use async/await in the command processor. This caused issues with ASP.NET synchronization contexts, resulting in a deadlock when waiting on the thread that was also being used to run the completion. See http://blog.stephencleary.com/2012/07/dont-block-on-async-code.html We wil revisit async when we write *Async versions of the CommandProcesor APIs suitable for using in hosts that can run async code without deadlocking their synchronization context.
- Fixed issue 110: Where we want to log we have two constuctors. A constructor that directly takes an iLog that you provide either directly or via your ioC container; a constructor that defaults that to LogProvider.GetCurrentClassLogger
	- In Production code you should set up your log provider and use the constructors that do not take an ILog reference.
	- In Test code you should inject the ILog using a fake logger. We don't recommend testing log output, its an implementation detail, unless its an important part of your acceptance criteria for that behaviour.
	- This means that your production code should not need to take a direct dependency on Paramore's ILog implementation.
	- This is a BREAKING CHANGE because we remove the ability to inject the constructor via the *Builder objects, so as to remove the temptation to do that when you should rely on the LibLog framework to wrap your current logger.

**Features:**
- Huge feature, Async; added support for SendAsync and PublishAsync to an IHandleRequestsAsync pipeline.
- Basic support for publishing to Azure Service Bus with `paramore.brighter.commandprocessor.messaginggateway.azureservicebus`.


## Release 5 ##

**Bug Fixes:**
 - #100 `CommandProcessor.Post` fails with Object reference not set to an instance of an object.
 - Fix RequeueMessage exhaustion to log ERROR.
 - #101 Updated `Requeue` method to send a message to a specific queue as opposed to a topic.
 -  Added a message store write timeout and message gateway timeout on a post; perviously we wait indefinitely (bad Brighter team, no biscuit).
 - Replace `Successor` write-only property with `SetSuccessor` method.
 - Message Viewer, fixed startup issues.
 - Removed a few unused interfaces.
 - Correct exceptions namespace to actions.

**Features:**
 - A connection can now be flagged as isDurable in the configuration. Choosing isDurable when using RMQ as the broker will create a durable channel (i.e. does not die if no one is consuming it, and thus continues to subscribe to messages that match it's topic). We think there are sufficient trade-offs with a message store that allows replay to make this setting false by default, but have configured to allow users to make this choice dependent on the characteristics of their consumers (i.e. sufficiently intermittend that messages would be lost).
 - #92 Added [Event Store](https://geteventstore.com/ "Event Store") Message Store implementation
 - #30 Changed RabbitMQ Messaging Gateway to support multiple performers per connection, fixing the pipeline errors from RabbitMQ Client
 - Added a UseCommandSourcing attribute that stores commands to a command store. This is the Event Sourcing paradigm described by Martin Fowler in http://martinfowler.com/eaaDev/EventSourcing.html The term Command Sourcing refers to the fact that as described the pattern stores commands (instructions to change state) not events (the results of applying those commands).
	- This may result in a breaking change that the Id on IRequest requires a setter to allow it to be deserialized
 - Added MS SQL Command Store implementation
 - Added monitoring attribute, which fires message onto control bus
 - Cleaning up code so working with dnx and Portable will be easier
 - Message Viewer, Add paging
 - Update Code of Conduct to Contributor Covenant 1.1.0
 - Add DDL scripts to help create SQL based schemes

**Remove and Depreciated:**
 - Flag the method `Repost` on `IAmACommandProcessor` as obsolete, We will probably drop this in the next release. We suggest that you use the message store directly to retrieve a message and then call Post.
 - Dropped support for RavenDb as a message store, we feel EventStore covers this scenario better where non-relational stores are an option
 - Removed release branch. We just tag a release on master now, so this only existed to support an older version of the library that was pre the tagging strategy. Removed now as confusing to new users of the library.

## Release 4.0.215 ##
1. Fixed an issue where you could not have multiple UsePolicy or FallbackPolicy attributes on a single handler.#
2. We pool connections now, to prevent clients with large number of channels overwhelming servers.
3. Add concept of delayed (deferred) message sending.
4. Implement delayed requeuing using gateway support (when supported).
5. Delayed message provider support for RabbitMQ using [rabbitmq_delayed_message_exchange plugin (3.5+)](https://github.com/rabbitmq/rabbitmq-delayed-message-exchange/).
6. Renamed RequeueException to DeferMessageAction and moved it into the command processor project.
7. Fixed and issues with unhandled exceptions from handlers when an event is published not been logged correctly
8. The first early version of a Message Store Viewer has been release as a zip file download

## Release 3.0.129 ##
1. We now support a Fallback method on IHandleRequests<TRequest> which is intended to be used for compensating or emergency action when a Handle method cannot be executed. The [FallbackPolicy] attribute supports the pipeline calling the Fallback method for you, in the event of either any exception bubbling into the handler, or a broken circuit exception bubbling into the handler.
2. Fix issue with RabbitMQ consumers running on a High Availability cluster not cancelling properly after cluster failover.
3. Fixed bug with config section duplication https://github.com/BrighterCommand/Brighter/issues/52
4. Added functionality so after a specified number of unacceptable message (unable to read from queue or map message) a connection is shutdown, by default unacceptable message are acked and dropped. https://github.com/BrighterCommand/Brighter/issues/51
5. Move RequeueException to paramore.brighter.commandprocessor.exceptions (breaking change).

## Release 3 ##


1. Refactored **IAmAMessagingGateway** into a **IAmAMessageConsumer** and **IAmAMessageProducer** to support differing approaches to producing and consuming messages for a particular flavour of Message-Oriented-Middleware. *These changes are a breaking binary change for users of earlier versions.*
	1. NOTE: IF YOU USE TASK QUEUES PLEASE SAVE YOUR SERVICEACTIVATORCONNECTIONS IN YOUR APP.CONFIG AS THE V2.0.1 BRIGHTER.SERVICEACTIVATOR UNINSTALL WILL DELETE THEM (FIXED FOR V3)
2. Created an **IAmAChannel** abstraction to allow differing Application Layer dependencies for the Work Queue
2. Upgraded Packages we depend on, including RabbitMQ. *There is still an issue with our having a hard dependency on a RabbitMQ client that might vary from your RabbitMQ client version, but as a NuGet package there are few workarounds. We suggest building from source where this issue is problematic, for now.*
3. Significant stability improvements on the RabbitMQ client
	1. Fixed issues around re-connection of the client leading to lost messages.
	2. Fixed issues when explicitly closing and re-opening connections
	3. Provided support for a **RequeueException** to requeue messages that are 'out-of-time' to help with resequencing.
	1. We now dispose of channels aggressively on closure, instead of waiting for garbage collection
2. Moved from [Common.Logging](https://github.com/net-commons/common-logging) to [LibLog](https://github.com/damianh/LibLog)  *These changes are a breaking binary change for users of earlier versions.*
3. We now call Release for all **RequestHandler<>** derived handlers that we construct from an **IAmAHandlerFactory**, not just those that implement IDisposable.
4. Note that the RestMS server is **NOT** ready for production usage. It's primary value, as of today, is an alternative to RabbitMQ for design purposes. It is hoped to produce a stable version for use as a ControlBus in a future release.
