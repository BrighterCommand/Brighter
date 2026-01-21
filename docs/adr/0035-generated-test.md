# 35. Test Generation Tool

Date: 2025-11-18

## Status

Proposed

## Context

Brighter doesn't have shared tests between integration test projects, making inconsistent behaviour between them and causing some providers to have more test coverage than others. Different implementations (e.g., outbox providers for PostgreSQL, MySQL, MSSQL, DynamoDB, etc.) need the same core test suite to ensure consistency across the codebase, but manually duplicating tests across projects is error-prone and difficult to maintain.

Additionally, not all providers work in the same way - for example, not all messaging gateways support features like partition keys or message delays, so a flexible approach is needed to accommodate provider-specific capabilities.

## Options

### Option 1: Using a Shared Test Project

The most simple approach is to create a shared project where we can create the basic test suite that is referenced by all provider-specific test projects.

**Pros**:
- Simple to implement
- Tests run directly without code generation

**Cons**:
- Would need many properties/configuration flags to control test behavior, making tests complex and hard to maintain
  - Not all providers work the same way (e.g., different feature support for partition keys, message delays)
  - Difficult to conditionally enable/disable tests based on provider capabilities
  - Can lead to complex conditional logic within shared tests
- Harder to customize tests for provider-specific edge cases

### Option 2: Creating a Tool for Generating Shared Tests (Chosen)

Create a command-line tool (`Paramore.Brighter.Test.Generator`) that generates test code from **Liquid templates** based on a **`test-configuration.json`** configuration file.

**Pros**:
- Generated tests are readable and can be customized per provider after generation
- Template-based approach provides flexibility for different provider capabilities
- Clean separation between template logic and generated tests
- Each provider gets a complete, standalone test suite
- Easy to add new test patterns by creating new templates
- Configuration-driven approach makes it easy to specify provider-specific details
- Generated code can be reviewed and modified if needed

**Cons**:
- Requires maintaining Liquid templates
- Developers need to regenerate tests when templates change
- Adds an extra build step to the development workflow
- Generated code must be committed to source control

## Decision

We have created a test generation tool at `tools/Paramore.Brighter.Test.Generator` that uses the **Liquid templating engine** (via the Fluid library) to generate consistent test suites across different provider implementations.

### Tool Architecture

The tool is a command-line application that:
- Reads a **`test-configuration.json`** file from the test project directory
- Uses Liquid templates stored in `Templates/` directory
- Generates test classes based on the configuration
- Supports multiple test categories (Outbox sync/async tests, etc.)

### Configuration File Format

Each test project requiring generated tests must have a `test-configuration.json` file:

```json
{
  "Namespace": "Paramore.Brighter.PostgresSQL.Tests",
  "DestinyFolder": "./", 
  "MessageFactory": "DefaultMessageFactory",
  "Outboxes": {
    "Text": {
      "Transaction": "System.Data.Common.DbTransaction",
      "OutboxProvider": "PostgresTextOutboxProvider"
    }
  }
}
```

### Template Structure

Templates are organized in the `Templates/` directory:
- `MessageFactory.cs.liquid` - Interface and base implementations for test message creation
- `Outbox/Sync/*.liquid` - Synchronous outbox tests
- `Outbox/Async/*.liquid` - Asynchronous outbox tests

Templates use Liquid syntax with variables like `{{ Namespace }}`, `{{ OutboxProvider }}`, and `{{ MessageFactory }}` that are replaced during generation.

### Usage

```bash
dotnet run --project tools/Paramore.Brighter.Test.Generator -- --file tests/Paramore.Brighter.PostgresSQL.Tests/test-configuration.json
```

## Consequences

### Positive

- **Consistency**: All provider implementations get the same core test suite
- **Maintainability**: Changes to test patterns can be made in templates and propagated to all providers
- **Flexibility**: Templates can accommodate provider-specific features through configuration
- **Discoverability**: Generated tests are readable and can be reviewed in source control
- **Customization**: Generated tests can be modified for provider-specific edge cases
- **Test Coverage**: Ensures all providers have comprehensive test coverage

### Negative

- **Learning Curve**: Developers need to learn Liquid templating syntax to modify templates
- **Build Complexity**: Adds an extra step to regenerate tests when templates change
- **Code Duplication**: Generated test code is duplicated across provider test projects
- **Maintenance**: Both templates and `test-configuration.json` files need to be maintained
- **Regeneration Required**: When templates change, all affected test projects need regeneration

### Migration Path

1. Create `test-configuration.json` for each provider test project
2. Run the generator tool to create initial test suite
3. Review and commit generated tests
4. When adding new test patterns, update templates and regenerate
5. Gradually migrate existing manually-written tests to use the generated pattern
