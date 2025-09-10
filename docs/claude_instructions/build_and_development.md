# Build and Development Commands

## Building the Solution
```bash
# Build entire solution
dotnet build Brighter.sln

# Build specific project
dotnet build src/Paramore.Brighter/Paramore.Brighter.csproj

# Build in Release mode
dotnet build Brighter.sln -c Release
```

## Running Tests
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

## Docker Development Environment
```bash
# Start all infrastructure services
docker-compose up -d --build --scale redis-slave=2 --scale redis-sentinel=3

# Start specific services for testing
docker-compose -f docker-compose-rabbitmq.yaml up -d
docker-compose -f docker-compose-mysql.yaml up -d
docker-compose -f docker-compose-postgres.yaml up -d
```