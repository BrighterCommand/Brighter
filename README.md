# Brighter

![canon](https://raw.githubusercontent.com/BrighterCommand/Brighter/master/images/brightercanon-nuget.png)

[![NuGet Version](http://img.shields.io/nuget/v/paramore.brighter.svg)](https://www.nuget.org/packages/paramore.brighter/)
[![NuGet Downloads](http://img.shields.io/nuget/dt/paramore.brighter.svg)](https://www.nuget.org/packages/Paramore.Brighter/)
![CI](https://github.com/BrighterCommand/Brighter/workflows/CI/badge.svg)
[![Coverity Scan Build Status](https://scan.coverity.com/projects/2900/badge.svg)](https://scan.coverity.com/projects/2900)
[![CodeScene Code Health](https://codescene.io/projects/32198/status-badges/code-health)](https://codescene.io/projects/32198)
![CodeScene System Mastery](https://codescene.io/projects/32198/status-badges/system-mastery)

**Brighter is a Command Dispatcher and Command Processor framework for .NET**. It enables you to build loosely coupled, maintainable applications using the Command pattern and supports both in-process and out-of-process messaging for microservices architectures. Its companion project, **[Darker](https://github.com/BrighterCommand/Darker)**, provides the query side for CQRS architectures.

## Why Brighter?

- **Command Dispatcher & Processor**: Implements the Command pattern with a powerful middleware pipeline
- **Clean Architecture Support**: Perfect for implementing ports & adapters (hexagonal architecture)
- **Messaging Made Simple**: Abstract away messaging complexity - just write handlers
- **Middleware Pipeline**: Add cross-cutting concerns like logging, retry, and circuit breakers via attributes
- **Multiple Transports**: Support for RabbitMQ, AWS SNS+SQS, Kafka, Redis, and more
- **Service Activator**: Built-in message consumer for processing messages from queues
- **Observability**: Built-in support for distributed tracing and monitoring

## Prerequisites

- **.NET 8.0 or later** - Brighter targets .NET 8, 9, and 10
- **IDE** - Visual Studio 2022+, VS Code, or JetBrains Rider
- **Docker** (optional) - For running message brokers and databases locally during development

## Quick Start

### Installation

```bash
dotnet add package Paramore.Brighter.Extensions.DependencyInjection
dotnet add package Microsoft.Extensions.Hosting
```

### Basic Usage - Command Dispatcher

**1. Define a Command**

```csharp
public class GreetingCommand(string name) : Command(Id.Random())
{
    public string Name { get; } = name;
}
```

**2. Create a Handler**

```csharp
public class GreetingCommandHandler : RequestHandler<GreetingCommand>
{
    public override GreetingCommand Handle(GreetingCommand command)
    {
        Console.WriteLine($"Hello {command.Name}");
        return base.Handle(command);
    }
}
```

**3. Configure and Send**

```csharp
// Setup
var builder = Host.CreateApplicationBuilder();
builder.Services.AddBrighter().AutoFromAssemblies();
var host = builder.Build();

var commandProcessor = host.Services.GetRequiredService<IAmACommandProcessor>();

// Send command (in-process)
commandProcessor.Send(new GreetingCommand("World"));
```

> **Note:** For async operations, use `RequestHandlerAsync<T>` and override `HandleAsync()` instead, then call `SendAsync()`.

## Out-of-Process Messaging

For microservices communication, Brighter can send and receive events via external message brokers. This typically involves two separate applications: a **sender** that posts events and a **consumer** that processes them.

First, install a transport package (pick one for your broker):

```bash
dotnet add package Paramore.Brighter.MessagingGateway.RMQ.Async  # RabbitMQ
dotnet add package Paramore.Brighter.MessagingGateway.AWSSQS.V4  # AWS SQS (uses AWS SDK v4; use AWSSQS for v3)
dotnet add package Paramore.Brighter.MessagingGateway.Kafka  # Kafka
```

For the consumer application, also install:

```bash
dotnet add package Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection
dotnet add package Paramore.Brighter.ServiceActivator.Extensions.Hosting
```

### 1. Define an Event

```csharp
public class GreetingEvent(string name) : Event(Id.Random())
{
    public string Name { get; } = name;
}
```

### 2. Configure Producers and Post (Sender App)

```csharp
var builder = Host.CreateApplicationBuilder();

var rmqConnection = new RmqMessagingGatewayConnection
{
    AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672")),
    Exchange = new Exchange("paramore.brighter.exchange"),
};

var producerRegistry = new RmqProducerRegistryFactory(
    rmqConnection,
    [
        new() { Topic = new RoutingKey("greeting.event"), RequestType = typeof(GreetingEvent) }
    ]).Create();

builder.Services.AddBrighter()
    .AutoFromAssemblies()
    .AddProducers(configure =>
    {
        configure.ProducerRegistry = producerRegistry;
    });

var host = builder.Build();
var commandProcessor = host.Services.GetRequiredService<IAmACommandProcessor>();

// Post to external message broker
commandProcessor.Post(new GreetingEvent("World"));
```

### 3. Create an Event Handler (Consumer App)

```csharp
public class GreetingEventHandler : RequestHandler<GreetingEvent>
{
    public override GreetingEvent Handle(GreetingEvent @event)
    {
        Console.WriteLine($"Received greeting for {@event.Name}");
        return base.Handle(@event);
    }
}
```

### 4. Configure Consumer Subscriptions (Consumer App)

```csharp
var builder = Host.CreateApplicationBuilder();

var rmqConnection = new RmqMessagingGatewayConnection
{
    AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672")),
    Exchange = new Exchange("paramore.brighter.exchange"),
};

var rmqMessageConsumerFactory = new RmqMessageConsumerFactory(rmqConnection);

builder.Services.AddConsumers(options =>
{
    options.Subscriptions =
    [
        new RmqSubscription<GreetingEvent>(
            new SubscriptionName("greeting-subscription"),
            new ChannelName("greeting.event"),
            new RoutingKey("greeting.event"),
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Create)
    ];
    options.DefaultChannelFactory = new ChannelFactory(rmqMessageConsumerFactory);
}).AutoFromAssemblies();

builder.Services.AddHostedService<ServiceActivatorHostedService>();

var host = builder.Build();
await host.RunAsync();
```

## Key Features

### Middleware Pipeline
Add cross-cutting concerns via attributes on your handlers:

```csharp
public class GreetingCommandHandler : RequestHandler<GreetingCommand>
{
    [RequestLogging(step: 1, timing: HandlerTiming.Before)]
    [UseResiliencePipeline("MyRetryPolicy", step: 2)]
    public override GreetingCommand Handle(GreetingCommand command)
    {
        Console.WriteLine($"Hello {command.Name}");
        return base.Handle(command);
    }
}
```

Register resilience pipelines using Polly's `ResiliencePipelineRegistry` and pass them to `AddBrighter()`:

```csharp
var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>();

// Add Brighter's required internal pipelines
resiliencePipelineRegistry.AddBrighterDefault();

// Add your own pipelines
resiliencePipelineRegistry.TryAddBuilder("MyRetryPolicy",
    (builder, _) => builder.AddRetry(new RetryStrategyOptions
    {
        BackoffType = DelayBackoffType.Linear,
        Delay = TimeSpan.FromSeconds(1),
        MaxRetryAttempts = 3
    }));

builder.Services.AddBrighter(options =>
{
    options.ResiliencePipelineRegistry = resiliencePipelineRegistry;
}).AutoFromAssemblies();
```

**Available Middleware:**
- **Logging**: `[RequestLogging]` / `[RequestLoggingAsync]` - Log commands/events automatically
- **Retry & Circuit Breaker**: `[UseResiliencePipeline]` / `[UseResiliencePipelineAsync]` - Integrates with Polly 8.x for resilience
- **Validation**: `[Validation]` - Validate requests before handling
- **Custom middleware**: Create your own attributes

### Outbox Pattern for Reliable Messaging

Brighter implements the [Outbox pattern](https://brightercommand.gitbook.io/paramore-brighter-documentation/brighter-outbox-support) to guarantee message delivery in distributed systems. Instead of calling `Post` directly, use `DepositPost` to write messages to the Outbox within your database transaction, then `ClearOutbox` to dispatch them after the transaction commits. This ensures consistency between your database state and published messages.

**Supported Outbox Stores:** PostgreSQL, MySQL, MSSQL, SQLite, DynamoDB, MongoDB. See the [full documentation](https://brightercommand.gitbook.io/paramore-brighter-documentation/brighter-outbox-support) for configuration details.

### Multiple Messaging Patterns
- **Send/SendAsync**: Commands - one sender → one handler (in-process only)
- **Publish/PublishAsync**: Events - one publisher → multiple handlers (in-process only)
- **Post/PostAsync**: Send a command or event to an external message broker for out-of-process handling
- **Request-Reply**: Synchronous RPC-style calls over async messaging

### Supported Transports
- RabbitMQ (AMQP)
- AWS SQS + SNS
- Azure Service Bus
- Apache Kafka
- Redis Streams
- PostgreSQL & MSSQL (as message transports)
- In-Memory (for testing)

## Use Cases

### Clean Architecture / Hexagonal Architecture
Use Brighter as your application's port layer. Commands and Events represent your use cases, and handlers implement the application logic.

### Microservices Integration
Abstract away messaging infrastructure. Developers write domain code (commands/events and handlers) while Brighter handles message routing, serialization, and transport.

### Task Queue / Job Processing
Use Brighter's Service Activator to consume messages from queues and process them with retry, circuit breaker, and monitoring built-in.

## Learn More

📚 **[Full Documentation](https://brightercommand.gitbook.io/paramore-brighter-documentation/)** - Comprehensive guides, tutorials, and API reference

**Topics covered in the documentation:**
- Advanced middleware and pipelines
- Outbox and Inbox patterns
- Service Activator for message consumers
- Distributed tracing and observability
- Health checks and monitoring
- Multiple database and transport configurations
- CQRS patterns
- Microservices architecture patterns

## Related Projects

- **[Darker](https://github.com/BrighterCommand/Darker)** - The query-side companion to Brighter. Implements the Query Processor pattern for CQRS architectures.

## Community & Support

- **GitHub**: [BrighterCommand/Brighter](https://github.com/BrighterCommand/Brighter)
- **Issues**: [Report bugs or request features](https://github.com/BrighterCommand/Brighter/issues)
- **Discussions**: [Ask questions and share ideas](https://github.com/BrighterCommand/Brighter/discussions)
- **Stack Overflow**: Tag your questions with `brighter`

## NuGet Packages

Latest stable releases are available on [NuGet](https://www.nuget.org/profiles/BrighterCommand/).

**Core Packages:**
- `Paramore.Brighter` - Core command processor and dispatcher
- `Paramore.Brighter.Extensions.DependencyInjection` - `AddBrighter()` for .NET dependency injection
- `Paramore.Brighter.ServiceActivator` - Message pump for consuming messages
- `Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection` - `AddConsumers()` for .NET dependency injection
- `Paramore.Brighter.ServiceActivator.Extensions.Hosting` - Run the message pump as a hosted service

**Transport Packages:**
- `Paramore.Brighter.MessagingGateway.RMQ.Async` - RabbitMQ
- `Paramore.Brighter.MessagingGateway.AWSSQS.V4` - AWS SQS+SNS (AWS SDK v4; use `AWSSQS` for v3)
- `Paramore.Brighter.MessagingGateway.Kafka` - Apache Kafka
- `Paramore.Brighter.MessagingGateway.AzureServiceBus` - Azure Service Bus
- `Paramore.Brighter.MessagingGateway.Redis` - Redis Streams

**Outbox/Inbox Packages:**
- `Paramore.Brighter.Outbox.PostgreSql` - PostgreSQL outbox
- `Paramore.Brighter.Outbox.MsSql` - MSSQL outbox
- `Paramore.Brighter.Outbox.MySql` - MySQL outbox
- `Paramore.Brighter.Outbox.DynamoDB` - DynamoDB outbox
- And more...

For bleeding-edge builds from the `master` branch, see [GitHub Packages](https://github.com/orgs/BrighterCommand/packages/).

## Contributing

We welcome contributions! See our [Contributing Guide](CONTRIBUTING.md) for details on:
- Setting up your development environment
- Building and testing the code
- Submitting pull requests
- Running tests with Docker Compose

## License

Brighter is licensed under the MIT licence. See [LICENCE](LICENCE.txt) for details.

---

## Acknowledgements

Portions of this code are based on Stephen Cleary's [AsyncEx's AsyncContext](https://github.com/StephenCleary/AsyncEx/blob/master/doc/AsyncContext.md).

