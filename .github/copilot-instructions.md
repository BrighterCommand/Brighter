# ---
applyTo: '**'
---

## How to Use This File
This file contains instructions for GitHub Copilot to help it understand the context and requirements of the project. It is not intended to be modified by contributors. Human contributors should follow the guidelines in the [CONTRIBUTING.md](CONTRIBUTING.md) file. These guidelines derive from that document.

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

## Project Structure
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

## Code Style

- Follow .NET C# conventions
  - Use Microsoft's C# naming conventions for identifiers
    - Use PascalCase for public and protected members, including properties, methods, and classes.
    - Use camelCase for private and internal members, including fields and parameters.
    - Use PascalCase for namespaces.
    - Use PascalCase for enum values.
    - Use camelCase for local variables.
  - For a const, use an All Caps naming convention, with underscores between words i.e. `public const int MAX_RETRY_COUNT = 5;` This replaces rules in the Microsoft C# naming convention.
  - Follow Microsoft's C# coding conventions
    - Use braces for all control statements, unless they are single-line.
    - Use spaces around binary operators and after commas.
    - Use a single blank line to separate methods and properties.
    - Use a single blank line to separate logical sections of code within a method.
    - Use a single blank line to separate using directives.
  - DO NOT use Microsoft's Framework Design Guidelines. They are not idiomatic and outdated.
- Prefer expression-bodied members for simple properties and methods.
- Use readonly for fields that do not change after construction.
- Enable nullable on projects:
  - `<Nullable>enable</Nullable>` should be set in the project file, and that new code should use nullable reference types.
  - Make types nullable to indicate optionality.
- You may use marker interfaces. We find marker interfaces useful for a base type for async and sync interfaces.
- Use assemblies to provide modularity. Separate into assemblies based on responsibilities.
- We support both sync and async I/O
  - Suffix async methods with async.
  - For I/O, you should provide both sync and async implementations.
  - Prefer explicit threads to using the thread pool.
- Divide into responsibilities based on optionality. Required behaviors should exist in Paramore.Brighter and Paramore.Brighter.ServiceActivator.
  - Other assemblies should add optional behaviors, allowing users to only take dependencies on the resulting NuGet packages if they require that functionality.
  - There is a balance here. We want you to load as few dependencies as possible, without bringing in too many behaviors you do not need.
  - As we support multiple message brokers (a.k.a. transports), these should always use their own assembly.
  - As we support multiple outbox providers, these should always use their own assembly.
  - As we support multiple inbox providers, these should always use their own assembly.
  - As we support multiple schedulers, these should use their own assembly.
  - As we support multiple locking providers, these should use their own assembly.  
- Default to a class per source file approach, unless one class clearly exists as the details of another.
- Use Responsibility Driven Design
  - Focus on the responsibilities that a class has.
  - "Responsibility-driven design specifies object behavior before object structure and other implementation considerations are determined. We have found that it minimizes the rework required for major design changes."
  - Maximize Abstraction
    - Elide the distinction between data and behavior.
    - Think of responsibilities for “knowing”, “doing”, and “deciding”
  - Distribute Behavior
    - Promote a delegated control architecture
    - Make objects smart— give them behaviors, not just data
  - Preserve Flexibility
    - Design objects so interior details can be readily changed
  - Objects have roles.
    - Common roles are stereotypes: information holder, structurer, service provider, coordinator, controller, interfacer
- Support optionality through interfaces.
  - If an interface describes a role that an implementor provides, use the naming convention IAmA* e.g. `public interface IAmAProducerRegistry { }`
  - Consider if a user might wish to override our implementation of a public class with theirs, for TDD, or extension.
  - If so, provide an interface for them to override.
  - It is acceptable in that case to use an interface, even if we have one implementation.
  - For internal classes, only provide an interface if there is optionality.
- Avoid primitive obsession.
  - Where a primitive (string, int, bool, double, float etc.) could be replaced with a more expressive type, use a class, struct or record.
  - Only use int for numeric values that have no domain meaning; only use string for string values that have no domain meaning.
  - Where we need to serialize, or for interoperability, you may use primitive types as part of that serialization, instead of writing convertors, for simplicity.
- Principles
  - Tidy is better than cluttered.
  - Reveal intention; be explicit to support future readers.
  - Prefer simplicity.
  - Do not duplicate knowledge.
  - Avoid having more than one level of indentation in a method.
  - Do not add new types without necessity.
  - There should be one-- and preferably only one --obvious way to do it.
  - If the implementation is hard to explain, it's a bad idea.
  - Keep methods small and focused on a single responsibility
- Follow Beck's "Tidy First" approach by separating structural changes from behavioral changes
  - Separate all changes into two distinct types:
    - STRUCTURAL CHANGES: Rearranging code without changing behavior (renaming, extracting methods, moving code)
    - BEHAVIORAL CHANGES: Adding or modifying actual functionality
  - Never mix structural and behavioral changes in the same commit
  - Always make structural changes first when both are needed
  - Validate structural changes do not alter behavior by running tests before and after
  - Not all of our code follows these conventions.
    - Some of our older code uses older conventions.
    - Follow the boy scout rule, and fix these, as part of your work.

## Testing

- Use TDD where possible.
- Write developer tests using xUnit.
- Name test methods in the format: When_[condition]_should_[expected_behavior]. Name test classes [behavior]Tests for the behavior being tested across all tests in the file, for example CommandProcessorPostBoxBulkClearAsyncTests.
- Ensure all new features and bug fixes include appropriate test coverage.

### TDD Style

- We write developer tests
  - Failure of a test case implicates the most recent edit.
  - Do not use mocks to isolate the System Under Test (SUT).
    - We prefer developer tests that implicate the most recent edit, not isolation of classes.
- Where possible, we are test first
  - Red: Write a failing test
  - Green: Make the test pass, commit any sins necessary to move fast
  - Refactor: Improve the design of the code.
- Where possible, avoid writing tests after.
  - This will not give you scope control - only writing the code required by tests.
    - You should only write the code necessary for a test to pass; do not write speculative code.
  - It will not push you to focus on design of your classes for behavior.
    - Pay attention to the usability of your class and method; it should be self-describing.
  - We accept test after when working with I/O implementations, where test-first is impractical.
- Tests should confirm the behavior of the SUT.
  - A test is a specification-first exploration of the behavior of the system.
    - A test provides an executable specification, of a given behavior.
  - Tests should be coupled to the behavior of the system and not to the implementation details.
    - It should be possible to refactor implementation details, without breaking tests.
  - Tests should use the Arrange/Act/Assert structure; make it explicit with comments i.e. //Arrange //Act //Assert
    - The Arrange should set up any pre-conditions for the test.
    - The Arrange code should be within the constructor of the test class, if shared by multiple tests
    - The Arrange code should use the Evident Data pattern.
      - In Evident Data we highlight the state that impacts the test outcome.
      - We may use the Test Data Builder pattern to hide noise, so as to focus on Evident Data.
- The trigger for a new test is a new behavior.
  - The trigger for a new test is NOT a new method.
  - The next test should always be the most obvious step you can make towards implementing the requirement
- Only test exports from an assembly
  - To be clear, this means an access modifier of public on methods on public classes.
  - Do not test details, such as methods on internal classes, or private methods.
- Do not expose more than is necessary from an assembly
  - An assembly is a module, it's surface area should be as narrow as possible.
  - Do not make export classes or methods from a module to test them; we only test exports from modules, not implementation details.
  - By following the rules for only testing behaviors, you only need to write tests for the behaviors exposed from the module not its details.
  - Private or Internal classes used in the implementation do not need tests - they are covered by the behavior that led to their creation.
- Use fakes or mocks for I/O for testing core libraries such as Paramore.Brighter or Paramore.Brighter.ServiceActivator
  - Consider writing in-memory replacements for I/O, that could be used in a production system, over a fake or mock.
  - Look for existing classes that use the naming convention InMemory*
  - Use the naming convention InMemory for your own in-memory implementations.
  - See [ADR 0023](docs/adr/0023-reactor-and-nonblocking-io.md) for advice on how to replace I/O.
- Do NOT use fakes or mocks for isolating a class.
  - We use developer tests: isolation is to the most recent edit, not a class.
  - Do not inject dependencies into a constructor or property for test isolation 
- You MAY use fakes or mocks (test doubles) for I/O or the strategy pattern. Prefer in-memory alternatives to fakes to mocks. 
  - You may use a test double to replace I/O as it is slow and has shared fixture making tests brittle.
  - You should look at using an in-memory substitute if testing core functionality that can use a range of alternative I/O. For example you can use an in-memory substitute such as InMemoryMessageProducer or InMemoryOutbox.
  - If you are testing the implementation of a messaging gateway (transport), outbox, inbox or other I/O adapter, you should create a suite of tests that use those directly to prove the implementation works. We separate these into test assemblies that require the dependency on a broker or database to run. This allows the core tests, that substitute I/O to run without additional dependencies, and indicates what dependencies you need to run a particular test suite.
  or use of the strategy pattern to satisfy the open-closed principle.
- Only add code needed to satisfy a behavioral requirement expressed in a test.
  - Do not add speculative code, the need for which is not indicated by test.

## Documentation

- Update or add Documentation comments for all exports from assemblies.
  - To be clear exports: means all public and protected methods of public classes/structs/records/enums.
    - We do not add Documentation comments internal or private classes, or internal methods
  - Documentation are indicated by `///`
  - Documentation comments use XML
  - Documentation comments show up in Intellisense for developers. Bear this in mind when writing comments, as they should be helpful to a developer using the API but not so verbose that a developer would not choose to read it when using intellisense. Use `<remarks>` for notes on implementation or more detailed instructions.
  - They should also be helpful to a developer or LLM reading the code.
  - We provide some guidance on specific elements:
  - Use `<summary>` element to provide an overview of the purpose of the class or method. What behavior or state does it encapsulate? What would you use it for. Use `<paramref>` if you refer to parameters in the summary.
  - Use the `<param>` tag to describe parameters to a constructor or method.
    - Use `<see cref="">` to document the type of the parameter
    - Indicate what the parameter is for, what effect setting it has and if it is optional. If it is optional describe any default value and its impact.
    - The developer should be clear what values they need to provide for the parameter to control desired behavior.
  - Use `<returns>` to indicate the `<see cref="">` of the return type, optionality, and what the value represents.
  - Use `<typeparam>` to indicate the intent of a generic type parameter; document any constraints on the type.
  - Use `<exception>` to document any exceptions that the method call can throw.
  - Use `<value>` to document a property. Like a `<summary>` it should indicate purpose. Like a `<param>` or `<return>` it should use `<see cref="">` to indicate type.

```csharp
/// <summary>
/// Gets or sets the current status.
/// </summary>
/// <value>The current status as a <see cref="string"/>.</value>
public string Status { get; set; }
```

  - Use `<remarks>` for advice to developers or LLMs working with the code directly. Include information on how the method is implemented where it is not obvious from the code or significant design decisions have been made. Consider what you would want to know if maintaining this method. Use `<see href="">` if you need to link to external documentation.  This can also be used for more detailed information than could be included in the `<summary>`.
  - Prefer to use good variable and method names to express intent, over inline comments.
    - Use the refactoring "Extract Method To Express Intent" to encapsulate code in a named method that explains intent, over using a comment.
    - Do not add comments for what may be easily inferred from the code.
    - In tests you may use //Arrange, //Act, //Assert.
    - If code has a complex algorithm or non-obvious implementation, prefer to use `/// <remarks>`
  - Example:

  ```csharp
  /// <summary>
  /// Sends a message to the specified recipient.
  /// </summary>
  /// <param name="recipient">The recipient's address.</param>
  /// <returns>The message ID.</returns>
  public string SendMessage(string recipient) { ... }
  ```  

- Documentation comments should be changed when APIs change.  
- Document new features and changes in the Docs repository of the BrighterCommand organization.

### Licensing

- We add a license comment to every src file
- The license should be at the very top of each source file, before any using statements or code.
- We use the MIT license.
- You should add your name and the year, if it is a new file.
- You should put the license comment in a `# region License` block
- An LLM should use the name and year of the contributor instructing the LLM
- As an example

```csharp
#region License

/* The MIT License (MIT)
Copyright © [Year] [Your Name] [Your Contact Email]

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

# endregion
```

## Dependency Management

- Use Directory.Packages.props for central package management.
- All projects should reference the central Directory.Packages.props file
- Inside, you then define each of the respective package versions required of your projects using <PackageVersion /> elements that define the package ID and version.

``` xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Newtonsoft.Json" Version="13.0.1" />
  </ItemGroup>
</Project>
```

- Within the project files, you then reference the packages without specifying a version, as the version is managed centrally.

``` xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Newtonsoft.Json" />
  </ItemGroup>
</Project>
```

- Align all Microsoft.Extensions.* and System.* package versions.
- Avoid mixing preview and stable package versions.
- Enable CentralPackageTransitivePinningEnabled where possible.
