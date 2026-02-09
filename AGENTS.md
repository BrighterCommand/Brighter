# AGENTS.md

This file provides guidance to coding agents when working with code in this repository.

## How to Use This File
This file contains instructions for coding agents to help them understand the context and requirements of the project. It is not intended to be modified by contributors. Human contributors should follow the guidelines in the [CONTRIBUTING.md](CONTRIBUTING.md) file. These guidelines derive from that document.

## Detailed Instructions
For comprehensive guidance on working with this codebase, agents should read the following files in `.agent_instructions/` as needed:

- [Build and Development Commands](.agent_instructions/build_and_development.md) - Build scripts, test commands, and Docker setup
- [Project Structure](.agent_instructions/project_structure.md) - Organization of the codebase and testing framework
- [Code Style](.agent_instructions/code_style.md) - C# conventions and architectural patterns
- [Design Principles](.agent_instructions/design_principles.md) - Responsibility-Driven Design and architectural guidance
- [Testing](.agent_instructions/testing.md) - TDD practices, test structure, and testing guidelines
- [Documentation](.agent_instructions/documentation.md) - XML documentation standards and licensing requirements
- [Dependency Management](.agent_instructions/dependency_management.md) - Package management with Directory.Packages.props

---

## Build and Development Commands

### Building the Solution

```bash
# Build entire solution
dotnet build Brighter.slnx

# Build specific project
dotnet build src/Paramore.Brighter/Paramore.Brighter.csproj

# Build in Release mode
dotnet build Brighter.slnx -c Release
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

---

## Code Style

- Follow .NET C# conventions
  - Use Microsoft's C# naming conventions for identifiers
    - Use PascalCase for public and protected members, including properties, methods, and classes.
    - Use camelCase for private and internal members, including fields and parameters.
    - Use PascalCase for namespaces.
    - Use PascalCase for enum values.
    - Use camelCase for local variables.
  - For a const, use an All Caps naming convention, with underscores between words i.e. `public const int MAX_RETRY_COUNT = 5;`
- Prefer expression-bodied members for simple properties and methods.
- Use readonly for fields that do not change after construction.
- Enable nullable on projects: `<Nullable>enable</Nullable>`
- Use assemblies to provide modularity. Separate into assemblies based on responsibilities.
- We support both sync and async I/O
  - Suffix async methods with Async.
  - For I/O, provide both sync and async implementations.
- Default to a class per source file approach.

---

## Design Principles

- Use Responsibility-Driven Design
  - Focus on the responsibilities that a class has.
  - Maximize Abstraction - elide the distinction between data and behavior.
  - Distribute Behavior - promote a delegated control architecture.
  - Preserve Flexibility - design objects so interior details can be readily changed.
- Support optionality through interfaces.
  - If an interface describes a role that an implementor provides, use the naming convention IAmA* e.g. `public interface IAmAProducerRegistry { }`
- Avoid primitive obsession - use expressive types instead of primitives where appropriate.
- Principles:
  - Tidy is better than cluttered.
  - Reveal intention; be explicit to support future readers.
  - Prefer simplicity.
  - Do not duplicate knowledge.
  - Keep methods small and focused on a single responsibility.

---

## Testing Guidelines

### TDD Workflow

We follow Test-Driven Development with an approval workflow:

1. **RED**: Write a failing test that specifies the behavior
2. **APPROVAL**: Present the test for user review before proceeding
3. **GREEN**: Implement minimum code to make the test pass
4. **REFACTOR**: Improve the design while keeping tests green

**Important**: When working on implementation tasks:
- Write the test first and **STOP for user approval**
- The user will review the test before implementation proceeds
- Only write code necessary for the test to pass; do not write speculative code

### Test Naming and Structure

- Use xUnit as the testing framework
- Name test methods: `When_[condition]_should_[expected_behavior]`
- Prefer one test case per file
- Name test files to match the test method: `When_[condition]_should_[expected_behavior].cs`
- If multiple test cases per file, name the class `[Behavior]Tests`

### Test Structure (AAA Pattern)

```csharp
public class When_sending_a_command
{
    public When_sending_a_command()
    {
        // Arrange - set up pre-conditions
    }
    
    [Fact]
    public void It_should_invoke_the_handler()
    {
        // Act - perform the action
        
        // Assert - verify the outcome
    }
}
```

### Test Scope and Isolation

- Only test exports from an assembly (public methods on public classes)
- Do not expose internal classes for testing
- **NEVER use `InternalsVisibleTo`** to expose internal classes for testing
- Tests should be coupled to behavior, not implementation details
- Refactoring internals should never break tests

### Test Doubles

- Use fakes or mocks for I/O only (not for class isolation)
- Prefer in-memory implementations over mocks (e.g., `InMemoryOutbox`, `InMemoryMessageProducer`)
- Do NOT inject dependencies just for test isolation - we use developer tests where isolation is to the most recent edit

### Test Categories

**Group 1 - Unit Tests (no external dependencies)**:
- `Paramore.Brighter.Core.Tests`
- `Paramore.Brighter.Extensions.Tests`
- `Paramore.Brighter.InMemory.Tests`

**Group 2 - Integration Tests (require Docker)**:
- All other test assemblies (Kafka, RabbitMQ, databases, etc.)
- Use docker-compose files in solution root for infrastructure

---

## Documentation

- All public types and members must have XML documentation
- Include `<summary>`, `<param>`, `<returns>`, and `<exception>` tags as appropriate
- All source files must include MIT license header:

```csharp
/*
The MIT License (MIT)
...
*/
```

---

## Dependency Management

- Use `Directory.Packages.props` for centralized package version management
- Do not specify versions in individual `.csproj` files
- When adding new packages, add the version to `Directory.Packages.props` first
