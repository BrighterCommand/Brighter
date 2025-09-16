# Project Structure

- `src/Paramore.Brighter/` - Core framework
- `src/Paramore.Brighter.ServiceActivator/` - Message pump infrastructure  
- `src/Paramore.Brighter.MessagingGateway.*/` - Transport implementations (RMQ, AWS SQS, Kafka, etc.)
- `src/Paramore.Brighter.Outbox.*/` - Persistence implementations for outbox pattern
- `src/Paramore.Brighter.Extensions.*/` - DI container integrations
- `tests/` - Test suites organized by component

## Testing Framework
- **Test Framework**: xUnit with FakeItEasy for mocking
- **Test Patterns**: BDD-style naming (`When_X_Then_Y`)
- **Infrastructure**: Docker Compose for integration tests

## Package Management
- Uses Central Package Management via `Directory.Packages.props`
- Multi-targeting: .NET 8.0 and .NET 9.0
- Global tools: MinVer for versioning, SourceLink for debugging