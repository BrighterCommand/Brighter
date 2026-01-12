# Brighter

![canon](https://raw.githubusercontent.com/BrighterCommand/Brighter/master/images/brightercanon-nuget.png)

[![NuGet Version](http://img.shields.io/nuget/v/paramore.brighter.svg)](https://www.nuget.org/packages/paramore.brighter/)
[![NuGet Downloads](http://img.shields.io/nuget/dt/paramore.brighter.svg)](https://www.nuget.org/packages/Paramore.Brighter/)
![CI](https://github.com/BrighterCommand/Brighter/workflows/CI/badge.svg)
[![Coverity Scan Build Status](https://scan.coverity.com/projects/2900/badge.svg)](https://scan.coverity.com/projects/2900)
[![CodeScene Code Health](https://codescene.io/projects/32198/status-badges/code-health)](https://codescene.io/projects/32198)
![CodeScene System Mastery](https://codescene.io/projects/32198/status-badges/system-mastery)

**Brighter is a Command Dispatcher and Command Processor framework for .NET**. It enables you to build loosely coupled, maintainable applications using the Command pattern and supports both in-process and out-of-process messaging for microservices architectures.

## Why Brighter?

- **Command Dispatcher & Processor**: Implements the Command pattern with a powerful middleware pipeline
- **Clean Architecture Support**: Perfect for implementing ports & adapters (hexagonal architecture)
- **Messaging Made Simple**: Abstract away messaging complexity - just write handlers
- **Middleware Pipeline**: Add cross-cutting concerns like logging, retry, and circuit breakers via attributes
- **Multiple Transports**: Support for RabbitMQ, AWS SNS+SQS, Kafka, Redis, and more
- **Service Activator**: Built-in message consumer for processing messages from queues
- **Observability**: Built-in support for distributed tracing and monitoring

## Quick Start

### Installation

```bash
dotnet add package Paramore.Brighter
```

For messaging support, also install a transport package:

```bash
dotnet add package Paramore.Brighter.MessagingGateway.RMQ  # RabbitMQ
dotnet add package Paramore.Brighter.MessagingGateway.AWSSQS  # AWS SQS
dotnet add package Paramore.Brighter.MessagingGateway.Kafka  # Kafka
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
    [RequestLogging(step: 1, timing: HandlerTiming.Before)]
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

### Out-of-Process Messaging

For microservices communication, Brighter can publish events to external message brokers.

**1. Define an Event**

```csharp
public class GreetingEvent : Event
{
    public GreetingEvent(string name) : base(Id.Random())
    {
        Name = name;
    }

    public string Name { get; }
}
```

**2. Configure External Bus and Publish**

> **Note:** Make sure you've installed a transport package like `Paramore.Brighter.MessagingGateway.RMQ`

```csharp
var builder = Host.CreateApplicationBuilder();
builder.Services.AddBrighter()
    .AutoFromAssemblies()
    .UseExternalBus(bus =>
    {
        bus.UseRabbitMQ(new RmqMessagingGatewayConfiguration
        {
            AmqpUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672"))
        });
    });

var host = builder.Build();
var commandProcessor = host.Services.GetRequiredService<IAmACommandProcessor>();

// Publish to external message broker
await commandProcessor.PublishAsync(new GreetingEvent("World"));
```

**3. Subscribe to Events**

```csharp
public class GreetingEventHandler : RequestHandler<GreetingEvent>
{
    public override GreetingEvent Handle(GreetingEvent @event)
    {
        Console.WriteLine($"Received greeting for {@event.Name}");
        return base.Handle(@event);
    }
}

// Configure subscription (add to your builder configuration)
builder.Services.AddServiceActivator()
    .Subscriptions(s =>
        s.Add<GreetingEvent>(
            new Subscription<GreetingEvent>(
                new SubscriptionName("greeting-subscription"),
                new ChannelName("greeting.event"),
                new RoutingKey("greeting.event")
            )
        )
    );
```

## Key Features

### Middleware Pipeline
Add cross-cutting concerns via attributes on your handlers:

```csharp
public class GreetingCommandHandler : RequestHandler<GreetingCommand>
{
    [RequestLogging(step: 1, timing: HandlerTiming.Before)]
    [UsePolicy(policy: "MyRetryPolicy", step: 2)]
    public override GreetingCommand Handle(GreetingCommand command)
    {
        Console.WriteLine($"Hello {command.Name}");
        return base.Handle(command);
    }
}

// Configure policies
builder.Services.AddBrighter()
    .AutoFromAssemblies()
    .Policies(p => p.Add("MyRetryPolicy", Policy.Handle<Exception>().Retry(3)));
```

**Available Middleware:**
- **Logging**: `[RequestLogging]` - Log commands/events automatically
- **Retry & Circuit Breaker**: `[UsePolicy]` - Integrates with Polly for resilience
- **Validation**: `[Validation]` - Validate requests before handling
- **Custom middleware**: Create your own attributes

### Outbox Pattern for Reliable Messaging

Brighter implements the Outbox pattern to guarantee message delivery in distributed systems:

```csharp
builder.Services.AddBrighter()
    .AutoFromAssemblies()
    .UseExternalBus(/* configure transport */)
    .UseOutbox(new PostgreSqlOutbox(/* configuration */));
```

The Outbox ensures that messages are written to the database atomically with your business logic, then reliably delivered to the message broker. This prevents message loss and ensures consistency.

**Supported Outbox Stores:** PostgreSQL, MySQL, MSSQL, SQLite, DynamoDB, MongoDB

### Multiple Messaging Patterns
- **Send/SendAsync**: Commands - one sender → one handler (in-process)
- **Publish/PublishAsync**: Events - one publisher → multiple subscribers (in-process or via broker)
- **Post/PostAsync**: Commands/Events - out-of-process via message broker
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

## Community & Support

- **GitHub**: [BrighterCommand/Brighter](https://github.com/BrighterCommand/Brighter)
- **Issues**: [Report bugs or request features](https://github.com/BrighterCommand/Brighter/issues)
- **Discussions**: [Ask questions and share ideas](https://github.com/BrighterCommand/Brighter/discussions)
- **Stack Overflow**: Tag your questions with `brighter`

## NuGet Packages

Latest stable releases are available on [NuGet](https://www.nuget.org/profiles/BrighterCommand/).

**Core Packages:**
- `Paramore.Brighter` - Core command processor and dispatcher
- `Paramore.Brighter.ServiceActivator` - Message pump for consuming messages

**Transport Packages:**
- `Paramore.Brighter.MessagingGateway.RMQ` - RabbitMQ
- `Paramore.Brighter.MessagingGateway.AWSSQS` - AWS SQS+SNS
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

