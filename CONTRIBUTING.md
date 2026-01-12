# Contributing

## How to Use This File

Use this file to follow our coding guidelines when submitting to Brighter.

## Architecture Decision Records

- If adding a new capability to our framework, write an Architecture Decision Record (ADR)
  - The ADR should focus on the why of your decision, over implementation details, which can be better found in the code.
  - Follow the format defined in [ADR 0001](docs/adr/0001-record-architecture-decisions.md)
  - Use the ADR to agree what you want to do, before you do it.
  - Use the ADR to signal to others why Brighter is built the way it is, to others, including future maintainers.

  You can create the ADR as the first step on a new branch/fork. Then your first commit includes the ADR describing the change. This then allows you to create a draft PR which includes the ADR. This allows others to review their understanding of the change, and give feedback early on if those expectations are mismatched. If you update the design as you learn, add another ADR that supersedes the old one.

## Code Style

- Follow .NET C# conventions
  - Use [Microsoft's C# naming conventions for identifiers](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/identifier-names)
  - For a const, use an All Caps naming convention, with underscores between words i.e. I_AM_A_CONSTANT. This replaces rules in the Microsoft C# naming convention.
  - Follow [Microsoft's C# coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
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
  - If an interface describes a role that an implementor provides, use the naming convention IAmA* e.g. `public interface IAmAProducerRegistry { }`.
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
    - You might want to watch [this video](http://vimeo.com/68375232) to understand our preferred testing approach
    - Or review [TDD Revisited](https://github.com/iancooper/Presentations/blob/master/Kent%20Beck%20Style%20TDD%20-%20Seven%20Years%20After.pdf)
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

### Generated Tests

- Brighter uses a test generation tool to ensure consistency across provider implementations (e.g., outbox/inbox implementations for different databases).
- Generated tests provide a baseline test suite that all providers must pass, ensuring consistent behavior across implementations.
- The test generator uses Liquid templates to create test code based on a `test-configuration.json` file in each test project.

#### How to Generate Tests

To generate tests for all test projects, run one of the following scripts from the root directory:

**On Linux/macOS:**
```bash
./generate-test.sh
```

**On Windows:**
```powershell
.\generate-test.ps1
```

**For a specific test project:**
```bash
dotnet run --project tools/Paramore.Brighter.Test.Generator -- --file tests/[YourTestProject]/test-configuration.json
```

#### When to Regenerate Tests

- When templates are updated in `tools/Paramore.Brighter.Test.Generator/Templates/`
- When adding a new provider implementation that needs the standard test suite
- When updating test patterns to ensure all providers follow the new pattern

#### Customizing Generated Tests

- Generated tests can be customized after generation for provider-specific edge cases
- Each test project should have a `test-configuration.json` file specifying provider-specific details
- See [ADR 0035](docs/adr/0035-geneated-test.md) for more details on the test generation architecture

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
- You should put the license comment in a `#region Licence` block (note: British spelling, no space)
- An LLM should use the name and year of the contributor instructing the LLM
- As an example

```csharp
#region Licence

/* The MIT License (MIT)
Copyright © [Year] [Your Name] [Your Contact Email]

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion
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

## Making Changes

- Make sure you have the latest version
- Submit a ticket for the issue, using the GitHub Issue Tracker
- It's worth checking first to see if someone else has raised your issue. We might have responded to them, or someone might be fixing it.
- If you want to add a feature, as opposed to fixing something that is broken, you should still raise an issue.
  - That helps us understand what you want to add, so we can give more specific advice and check it does not conflict with some work in progress on a branch etc.
  - If someone else is already working on the suggestion, then hopefully you can collaborate with them.
  - Add a comment to an issue if you pick it up to work on, so everyone else knows.  
- If you have a defect please include the following:
  - Steps to reproduce the issue
  - A failing test if possible (see below)
  - The stacktrace for any errors you encountered

### Build & Test

#### Building Locally

- Use dotnet build and dotnet test to verify changes locally.
- Ensure all tests pass before submitting a PR.

```bash
# Build entire solution
dotnet build Brighter.sln

# Run all tests
dotnet test
```

#### Using Docker Compose for Testing

We provide Docker Compose files to run infrastructure services needed for testing without installing them locally.

**Option 1: Run All Services (Full Test Suite)**

Use the main docker-compose.yaml to run everything:

```bash
docker-compose up -d
```

This starts: RabbitMQ, MySQL, MSSQL, PostgreSQL, Redis, DynamoDB, Kafka (with Zookeeper), Jaeger, Prometheus, and OpenTelemetry Collector.

**Option 2: Run Specific Services (Targeted Testing)**

If you're only working on a specific area, use the individual compose files:

| Service | File | Use Case |
|---------|------|----------|
| RabbitMQ | `docker-compose-rmq.yaml` | Testing RabbitMQ messaging gateway |
| Kafka | `docker-compose-kafka.yaml` | Testing Kafka messaging gateway |
| MySQL | `docker-compose-mysql.yaml` | Testing MySQL outbox/inbox |
| PostgreSQL | `docker-compose-postgres.yaml` | Testing PostgreSQL outbox/inbox |
| MSSQL | `docker-compose-mssql.yaml` | Testing MSSQL outbox/inbox |
| MongoDB | `docker-compose-mongodb.yaml` | Testing MongoDB outbox/inbox |
| DynamoDB | `docker-compose-dynamodb.yaml` | Testing DynamoDB outbox/inbox |
| Redis | `docker-compose-redis.yaml` | Testing Redis messaging gateway |
| MQTT | `docker-compose-mqtt.yaml` | Testing MQTT messaging gateway |
| RocketMQ | `docker-compose-rocketmq.yaml` | Testing RocketMQ messaging gateway |
| LocalStack | `docker-compose-localstack.yaml` | Testing AWS services locally |
| Jaeger | `docker-compose-jaeger.yaml` | Testing distributed tracing |
| Prometheus | `docker-compose-prometheus.yaml` | Testing metrics collection |

**Example - Testing only PostgreSQL outbox:**
```bash
docker-compose -f docker-compose-postgres.yaml up -d
```

**Example - Testing RabbitMQ with observability:**
```bash
docker-compose -f docker-compose-rmq.yaml -f docker-compose-jaeger.yaml up -d
```

**Note:** If you have locally installed versions of these services, you'll need to either stop them or edit the compose files to avoid port conflicts.

**Cleanup:**
```bash
# Stop and remove containers
docker-compose down

# Stop specific compose file
docker-compose -f docker-compose-postgres.yaml down

# Remove volumes (clean slate)
docker-compose down -v
```

### Commit Messages

- Try to write a [good commit message](http://tbaggery.com/2008/04/19/a-note-about-git-commit-messages.html).
- Use imperative mood (e.g., "Add support for X", "Fix bug in Y").
- Reference issues or PRs where relevant.
- In order to keep a clean commit history, please use [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/#specification) when contributing to Brighter.

### Repository Branching Strategy

Understanding our branching strategy will help you target the right branch for your pull request:

| Branch        | Description   |
| ------------- | ------------- |
| Master | The tip of active development. Anything in master should ship at the next release. Code here should conform to CI basics: compile, pass tests etc.  |
| Release [X] | The code for an actively supported release. Created when master needs breaking changes that are not compatible with the current release. We support one 'historical' release. |
| [Other]  | A branch for any work that is not ready to go into master (for example would break CI) or is experimental i.e. we don't know if we intend to ever ship, we are just trying out ideas. |

**When submitting a pull request:** Target the `master` branch unless you're fixing a bug in a specific release branch.

### Submitting Changes

- Fork the project
- Clone your fork
- Branch your fork. This is your active development branch
- Merge back into your fork
- Push to your fork
- Submit a pull request
- Sit back, and wait.
- Try pinging @BrighterCommand on Twitter if you hear nothing

### Contributor License Agreement

To safeguard the project we ask you to sign a Contributor License Agreement. The goal is to let you keep your copyright, but to assign it to the project so that it can use it in perpetuity. It is still yours, but the project is not at risk from having multiple contributors holding the copyright, with anyone able to hold it to ransom by removing their grant of license.

The process of signing works through GitHub.

To get started, <a href="https://www.clahub.com/agreements/iancooper/Paramore">sign the Contributor License Agreement</a>.

### Contributor Code of Conduct

Please note that this project is released with a Contributor Code of Conduct. By participating in this project you agree to abide by its terms.

The code of conduct is from [Contributor Covenant](http://contributor-covenant.org/)

## Project Structure

Our code is organized as follows:

- We add code for for the Brighter framework under the src directory
  - Within src, we use projects for modularity
    - We divide our projects by responsibility
      - Paramore.Brighter contains core functionality for our Command Processor and Command Dispatcher. It also contains core code allowing the framework to act as a message producer in an event driven architecture.
      - Paramore.Brighter.Archive.Azure contains an archiver implementation for Azure, which archives Outbox messages to Blob storage.
      - Paramore.Brighter.Dapper support for using Dapper with Brighter.
      - Paramore.Brighter.DynamoDb support for using DynamoDb with Brighter.
      - Paramore.Brighter.Extensions.DependencyInjection provides extensions for working with .NET's ServiceCollection.
      - Paramore.Brighter.Extensions.Diagnostics extensions for adding Brighter's OpenTelemetry support into an application.
      - Paramore.Brighter.Inbox.DynamoDB an implementation of Brighter's Inbox, backed by DynamoDb.
      - Paramore.Brighter.Inbox.MongoDb an implementation of Brighter's Inbox, backed by MongoDb.
      - Paramore.Brighter.Inbox.MsSql an implementation of Brighter's Inbox, backed by MSSQL.
      - Paramore.Brighter.Inbox.MySql an implementation of Brighter's Inbox, backed by MySQL.
      - Paramore.Brighter.Inbox.Postgres an implementation of Brighter's Inbox, backed by Postgres.
      - Paramore.Brighter.Inbox.Sqlite an implementation of Brighter's Inbox, backed by Sqlite.
      - Paramore.Brighter.Locking.Azure a locking provider using Azure Blob storage, allows leader election for an Outbox Sweeper or Archiver.
      - Paramore.Brighter.Locking.DynamoDB a locking provider using DynamoDB allows leader election for an Outbox Sweeper or Archiver.
      - Paramore.Brighter.Locking.MongoDb a locking provider using MongoDb, allows leader election for an Outbox Sweeper or Archiver.
      - Paramore.Brighter.Locking.MsSql a locking provider using MsSql, allows leader election for an Outbox Sweeper or Archiver.
      - Paramore.Brighter.Locking.MySql a locking provider using MySql, allows leader election for an Outbox Sweeper or Archiver.
      - Paramore.Brighter.Locking.PostgresSql a locking provider using Postgres, allows leader election for an Outbox Sweeper or Archiver.
      - Paramore.Brighter.MessageScheduler.Aws a scheduler for delayed CommandProcessor operations, using Aws Scheduler to implement delays.
      - Paramore.Brighter.MessageScheduler.Azure a scheduler for delayed CommandProcessor operations, using ASB Scheduler to implement delays.
      - Paramore.Brighter.MessageScheduler.Hangfire a scheduler for delayed CommandProcessor operations, using Hangfire to implement delays.
      - Paramore.Brighter.MessageScheduler.Quartz a scheduler for delayed CommandProcessor operations, using Quartz to implement delays.
      - Paramore.Brighter.MessagingGateway.AWSSQS a messaging gateway that abstracts access to a broker, providing access to SNS & SQS.
      - Paramore.Brighter.MessagingGateway.AzureServiceBus a messaging gateway that abstracts access to a broker, providing access to ASB.
      - Paramore.Brighter.MessagingGateway.Kafka a messaging gateway that abstracts access to a broker, providing access to Kafka.
      - Paramore.Brighter.MessagingGateway.MQTT a messaging gateway that abstracts access to a broker, providing access to MQTT.
      - Paramore.Brighter.MessagingGateway.MsSql a messaging gateway that abstracts access to a broker, providing access to MSSQL used as a broker.
      - Paramore.Brighter.MessagingGateway.Postgres a messaging gateway that abstracts access to a broker, providing access to Postgres used as a broker.
      - Paramore.Brighter.MessagingGateway.Redis a messaging gateway that abstracts access to a broker, providing access to Redis used a broker.
      - Paramore.Brighter.MessagingGateway.RMQ.Async a messaging gateway that abstracts access to a broker, providing access to RMQ used as a broker. This uses RMQ client libraries of V7 or above, which are async-only.
      - Paramore.Brighter.MessagingGateway.RMQ.Sync a messaging gateway that abstracts access to a broker, providing access to RMQ used as a broker. This uses RMQ client libraries of V6, which are sync-only.
      - Paramore.Brighter.MongoDb support for using MongoDb with Brighter.
      - Paramore.Brighter.MsSql support for using MSSQL with Brighter.
      - Paramore.Brighter.MsSql.Azure support for using Azure MSSQL with Brighter.
      - Paramore.Brighter.MsSql.Dapper support for using MSSQL via Dapper with Brighter.
      - Paramore.Brighter.MsSql.EntityFrameworkCore support for using MSSQL via EF Core with Brighter.
      - Paramore.Brighter.MySql support for using MySql with Brighter.
      - Paramore.Brighter.MySql.Dapper support for using MySql via Dapper with Brighter.
      - Paramore.Brighter.MySql.EntityFrameworkCore support for using MySql via EF Core with Brighter.
      - Paramore.Brighter.Outbox.DynamoDB an implementation of Brighter's Outbox, backed by DynamoDb.
      - Paramore.Brighter.Outbox.Hosting support for using HostedService to run an Outbox Sweeper or Archiver.
      - Paramore.Brighter.Outbox.MongoDb an implementation of Brighter's Outbox, backed by MongoDb.
      - Paramore.Brighter.Outbox.MsSql an implementation of Brighter's Outbox, backed by MSSQL.
      - Paramore.Brighter.Outbox.MySql an implementation of Brighter's Outbox, backed by MySql.
      - Paramore.Brighter.Outbox.PostgreSql an implementation of Brighter's Outbox, backed by PostgreSql.
      - Paramore.Brighter.Outbox.Sqlite an implementation of Brighter's Outbox, backed by Sqlite.
      - Paramore.Brighter.PostgreSql support for using Postgres with Brighter.
      - Paramore.Brighter.PostgreSql.EntityFrameworkCore support for using Postgres via EF Core with Brighter.
      - Paramore.Brighter.ServiceActivator.Control base library support for creating a control plane for dynamic configuration or Brighter.
      - Paramore.Brighter.ServiceActivator.Control.Api HTTP API support for creating a control plane for dynamic configuration or Brighter.
      - Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection provides extensions for working with .NET's ServiceCollection.
      - Paramore.Brighter.ServiceActivator.Extensions.Diagnostics extensions for adding Brighter's Health Check support into an application.
      - Paramore.Brighter.ServiceActivator.Extensions.Hosting support for running a Dispatcher as a background service.
      - Paramore.Brighter.Sqlite support for using Sqlite with Brighter.
      - Paramore.Brighter.Sqlite.Dapper support for using Sqlite with Dapper for Brighter.
      - Paramore.Brighter.Sqlite.EntityFrameworkCore support for using Sqlite with EF Core for Brighter.
      - Paramore.Brighter.Tranformers.AWS support for using AWS with a Claim Check.
      - Paramore.Brighter.Transformers.Azure support for using Azure with a Claim Check.
      - Paramore.Brighter.Transformers.MongoGridFS support for using MongoGridFS with a Claim Check.
- We add code for for tests for the Brighter framework under the tests directory
  - Within core, we use projects to partition our tests by their dependencies.
  - Paramore.Brighter.AWS.Tests Messaging Gateway tests that depend on AWS SNS and SQS, and S3 for Claims Checks.
  - Paramore.Brighter.AWSScheduler.Tests Scheduler tests that depend on AWS Scheduler.
  - Paramore.Brighter.Azure.Tests Tests that depend on Azure Blob storage for Claims Checks.
  - Paramore.Brighter.AzureServiceBus.Tests Messaging Gateway tests that depend on Azure Service Bus.
  - Paramore.Brighter.Core.Tests Tests that exercise the core functionality from Paramore.Brighter and Paramore.ServiceActivator. No I/O dependencies, uses in-memory implementations instead.
  - Paramore.Brighter.DynamoDB.Tests Tests of Outbox and Inbox implementations that use DynamoDB.
  - Paramore.Brighter.Extensions.Tests Tests of Brighter's extensions to .NET HostBuilder.
  - Paramore.Brighter.Hangfire.Tests Tests for the implementation of Brighter's scheduled CommandProcessor requests using Hangfire.
  - Paramore.Brighter.InMemory.Tests Tests for the Brighter's in-memory messaging gateways (transports), outboxes, inboxes, schedulers etc.
  - Paramore.Brighter.Kafka.Tests Messaging Gateway tests that depend on Kafka.
  - Paramore.Brighter.MongoDb.Tests Tests of Outbox and Inbox implementations that use MongoDb.
  - Paramore.Brighter.MQTT.Tests Messaging Gateway tests that depend on MQTT.
  - Paramore.Brighter.MSSQL.Tests Tests of Outbox and Inbox implementations that use MSSQL; Messaging Gateway tests that depend on MSSQL.
  - Paramore.Brighter.MySQL.Tests Tests of Outbox and Inbox implementations that use MySql
  - Paramore.Brighter.PostgresSQL.Tests Tests of Outbox and Inbox implementations that use Postgres.
  - Paramore.Brighter.Quartz.Tests Tests for the implementation of Brighter's scheduled CommandProcessor requests using Quartz.
  - Paramore.Brighter.Redis.Tests Messaging Gateway tests that depend on Redis.
  - Paramore.Brighter.RMQ.Async.Tests Messaging Gateway tests that depend on RabbitMQ's .NET Client V7+.
  - Paramore.Brighter.RMQ.Sync.Tests Messaging Gateway tests that depend on RabbitMQ's .NET Client V6.
  - Paramore.Brighter.Sqlite.Tests Tests of Outbox and Inbox implementations that use Sqlite.
  - Paramore.Test.Helpers Tools for helping write developer tests.
