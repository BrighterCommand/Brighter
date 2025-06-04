# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Brighter is a .NET messaging framework that implements Command Query Responsibility Segregation (CQRS), Event-Driven Architecture, and the Task Queue pattern. It provides both in-memory command/event processing and inter-service messaging capabilities.

## Build and Development Commands

### Building the Solution
```bash
# Build entire solution
dotnet build Brighter.sln

# Build specific project
dotnet build src/Paramore.Brighter/Paramore.Brighter.csproj

# Build in Release mode
dotnet build Brighter.sln -c Release
```

### Running Tests
```bash
# Run all tests
dotnet test

# Run tests for specific project
dotnet test tests/Paramore.Brighter.Core.Tests/

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run tests matching pattern
dotnet test --filter "When_Handling_A_Command"
```

### Docker Development Environment
```bash
# Start all infrastructure services
docker-compose up -d --build --scale redis-slave=2 --scale redis-sentinel=3

# Start specific services for testing
docker-compose -f docker-compose-rabbitmq.yaml up -d
docker-compose -f docker-compose-mysql.yaml up -d
docker-compose -f docker-compose-postgres.yaml up -d
```

## Architecture Overview

### Core Components
- **CommandProcessor**: Central orchestrator that dispatches commands/events to handlers
- **IHandleRequests<T>**: Interface for command/event handlers with pipeline support
- **Message**: Transport envelope containing headers and body for inter-service communication
- **MessagePump**: Service activator that consumes messages from external queues and dispatches to handlers

### Key Patterns
- **Command Pattern**: Point-to-point commands with single handler (`Send<T>()`)
- **Event Pattern**: Pub-sub events with multiple handlers (`Publish<T>()`) 
- **Request-Reply Pattern**: Synchronous request-response (`Call<T>()`)
- **Task Queue Pattern**: Async messaging via external transports (`Post<T>()`)
- **Outbox Pattern**: Transactional consistency for message publishing

### Pipeline Architecture
Handlers can be decorated with attributes for cross-cutting concerns:
- `[Retry]` - Retry policies with exponential backoff
- `[CircuitBreaker]` - Circuit breaker pattern
- `[RequestLogging]` - Structured logging
- `[Timeout]` - Request timeout handling
- `[FeatureSwitch]` - Feature toggle support

### Project Structure
- `src/Paramore.Brighter/` - Core framework
- `src/Paramore.Brighter.ServiceActivator/` - Message pump infrastructure  
- `src/Paramore.Brighter.MessagingGateway.*/` - Transport implementations (RMQ, AWS SQS, Kafka, etc.)
- `src/Paramore.Brighter.Outbox.*/` - Persistence implementations for outbox pattern
- `src/Paramore.Brighter.Extensions.*/` - DI container integrations
- `tests/` - Test suites organized by component

### Transport Support
The framework supports multiple messaging platforms:
- RabbitMQ (sync/async)
- AWS SQS/SNS
- Apache Kafka
- Azure Service Bus
- Redis
- PostgreSQL (as message broker)
- In-memory (testing)

### Testing Framework
- **Test Framework**: xUnit with FakeItEasy for mocking
- **Test Patterns**: BDD-style naming (`When_X_Then_Y`)
- **Infrastructure**: Docker Compose for integration tests
- **Coverage**: Coverlet for code coverage

### Package Management
- Uses Central Package Management via `Directory.Packages.props`
- Multi-targeting: .NET 8.0 and .NET 9.0
- Global tools: MinVer for versioning, SourceLink for debugging

## Development Guidelines

### Handler Implementation
```csharp
public class MyCommandHandler : RequestHandler<MyCommand>
{
    [Retry(1, 100, Timeout = 1000)]
    [RequestLogging]
    public override MyCommand Handle(MyCommand command)
    {
        // Business logic here
        return base.Handle(command);
    }
}
```

### Message Mapping
All commands/events sent via external transports require a mapper:
```csharp
public class MyCommandMessageMapper : IAmAMessageMapper<MyCommand>
{
    public Message MapToMessage(MyCommand request)
    {
        // Map request to transport message
    }
    
    public MyCommand MapToRequest(Message message)
    {
        // Map transport message to request
    }
}
```

### Configuration Patterns
- Use dependency injection for all components
- Configure subscriptions declaratively via `Subscription` objects
- Configure publications via `Publication` objects for outbound messaging
- Use builder patterns for complex setup (e.g., `CommandProcessorBuilder`)

### Message Handling Best Practices
- Keep handlers focused on single responsibility
- Use async handlers (`RequestHandlerAsync<T>`) for I/O operations
- Leverage pipeline decorators for cross-cutting concerns
- Implement idempotency for message handlers
- Use structured logging with request context

### Integration Testing
- Use Docker Compose to spin up required infrastructure
- Test against real message brokers when possible
- Use `Catch` helper class for exception testing
- Follow BDD naming conventions for test clarity