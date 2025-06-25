# Contributing

## Project Structure

- Organize code by feature and responsibility (e.g., Core, Transforms, Tests).
- Place tests in the tests directory, mirroring the structure of the main codebase.

Our code is organized as follows:

- We add code for for the Brighter framework under the src directory
  - Within src, we use projects for modularity
    - We divide our projects by responsibility
      - Paramore.Brighter contains core functionality for our Command Processor and Command Dispatcher. It also contains core code allowing the framework to act as a message producer in an event driven architecture.
      - Paramore.Brighter.ServiceActivator contains core functionality for our Message Pump which allows us to consume messages in an event driven architecture.

## Architecture Decision Records

- If adding a new capability to our framework, write an Architecture Decision Record (ADR)
  - The ADR should focus on the why of your decision, over implementation details, which can be better found in the code.
  - Follow the format defined in [ADR 0001](/Users/ian.cooper/CSharpProjects/github/BrighterCommand/Brighter/docs/adr/0001-record-architecture-decisions.md)
  - Use the ADR to agree what you want to do, before you do it.
  - Use the ADR to signal to others why Brighter is built the way it is, to others, including future maintainers.

## Testing

- Use TDD where possible.
- Write developer tests using xUnit.
- Name test methods in the format: When_[condition]_should_[expected_behavior].
- Ensure all new features and bug fixes include appropriate test coverage.

### TDD Style

- We write developer tests
  - Failure of a test case implicates the most recent edit.
  - Do not use mocks to isolate the System Under Test (SUT).
  - You might want to watch [this video](http://vimeo.com/68375232) to understand our preferred testing approach
  - Or review [TDD Revisited](https://github.com/iancooper/Presentations/blob/master/Kent%20Beck%20Style%20TDD%20-%20Seven%20Years%20After.pdf)
- Where possible, we are test first
  - Red: Write a failing test
  - Green: Make the test pass, commit any sins necessary to move fast
  - Refactor: Improve the design of the code.
- Where possible, avoid writing tests after.
  - This will not give you scope control - only writing the code required by tests.
  - It will not push you to focus on design of your classes for behavior.
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
  - The *next_ test should always be the most obvious step you can make towards implementing the requirement
- Only test exports from an assembly
  - To be clear, this means an access modifier of public on methods on public classes.
  - Do not test details, such as methods on internal classes, or private methods.
- Do not expose more than is necessary from an assembly
  - An assembly is a module, it's surface area should be as narrow as possible.
  - Do not make export classes or methods from a module to test them; we only test exports from modules, not implementation details.
- Use fakes or mocks for I/O for testing core libraries such as Paramore.Brighter or Paramore.Brighter.ServiceActivator
  - Consider writing in-memory replacements for I/O, that could be used in a production system, over a fake or mock.
  - Look for existing classes that use the naming convention InMemory*
  - Use the naming convention InMemory for your own in-memory implementations.
  - See [ADR 0023](docs/adr/0023-reactor-and-nonblocking-io.md) for advice on how to replace I/O.
- Do NOT use fakes or mocks for isolating a class.
  - We use developer tests: isolation is to the most recent edit, not a class.
  - Do not inject dependencies into a constructor or property for test isolation, unless it is for I/O, or use of the strategy pattern to satisfy the open-closed principle.
- Only add code needed to satisfy a behavioral requirement expressed in a test.
  - Do not add speculative code, the need for which is not indicated by test.

## Code Style

- Follow .NET C# conventions
  - Use [Microsoft's C# naming conventions for identifiers](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/identifier-names)
  - For a const, use an All Caps naming convention, with underscores between words i.e. I_AM_A_CONSTANT. This replaces rules in the Microsoft C# naming convention.
  - Follow [Microsoft's C# coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
  - DO NOT use Microsoft's Framework Design Guidelines. They are not idiomatic and outdated.
- Prefer expression-bodied members for simple properties and methods.
- Use readonly for fields that do not change after construction.
- Enable nullable on projects; make types nullable to indicate optionality.
- You may use marker interfaces. We find marker interfaces useful for a base type for async and sync interfaces.
- Use assembles to provide modularity. Separate into assemblies based on responsibilities.
- We support both sync and async I/O
  - Suffix async methods with async.
  - For I/O, you should provide both sync and async implementations.
  - Prefer explicit threads to using the thread pool.
- Divide into responsibilities based on optionality. Required behaviors should exist in Paramore.Brighter and Paramore.Brighter.ServiceActivator.
  - Other assembles should add optional behaviors, allowing users to only take dependencies on the resulting NuGet packages if they require that functionality.
  - There is a balance here. We want you to load as few dependencies as possible, without bringing in too many behaviors you do not need.
  - As we support multiple message brokers (a.k.a. transports), these should always use their own assembly.
  - As we support multiple outbox providers, these should always use their own assembly.
  - As we support multiple inbox providers, these should always use their own assembly.
  - As we support multiple schedulers, these should use their own assembly.
  - As we support multiple locking providers, these should use their own assembly.  
- Default to a class per source file approach, unless one class is clearly only exists as the details of another.
- Support optionality through interfaces.
  - If an interface describes a role that an implementor provides, use the naming convention IAmA* e.g. IAmAProducerRegistry.
  - Consider if a user might wish to override our implementation of a public class with theirs, for TDD, or extension.
  - If so, provide an interface for them to override.
  - It is acceptable in that case to use an interface, even if we have one implementation.
  - For internal classes, only provide an interface if there is optionality.
- Avoid primitive obsession.
  - Where a primitive (string, int, bool, double, float etc.) could be replaced with a more expressive type, use a class, struct or record.
  - Only use int for numeric values that have no domain meaning; only use string for string values that have no domain meaning.
  - Where we need to serialize, or for interoperability, you may use primitive types as part of that serialization, instead of writing convertors, for simplicity.
- Not all of our code follows these conventions.
  - Some of our older code uses older conventions.
  - Follow the boy scout rule, and fix these, as part of your work.

Dependency Management

- Use Directory.Packages.props for central package management.
- Align all Microsoft.Extensions._and System._ package versions.
- Avoid mixing preview and stable package versions.
- Enable CentralPackageTransitivePinningEnabled where possible.

Commit Messages

- Use imperative mood (e.g., "Add support for X", "Fix bug in Y").
- Reference issues or PRs where relevant.

Build & Test

- Use dotnet build and dotnet test to verify changes locally.
- Ensure all tests pass before submitting a PR.

Documentation

- Update or add XML comments for public APIs.
- Document new features and changes in the README.md or relevant docs.

CloudEvents & Message Transformation

- When wrapping messages as CloudEvents, ensure the data property is JSON if the original content type is JSON; otherwise, treat as plain text.
- When unwrapping, restore the original content type and body.

### Making Changes ###

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

### Submitting Changes ###

- Fork the project
- Clone your fork
- Branch your fork. This is your active development branch
- We separate the core library from add-ons.

   1. Consider if your change is really core, or could be shipped as an add-on. Let the user 'buy-in' to your feature over making them take it.
   2. Minimize your dependency on other libraries - try to limit yourself to the ones you **need** so you don't force consumers to buy a chain of dependencies
   3. As an example, for a transport, prefer ADO.NET over using an ORM to avoid additional dependencies on other OSS frameworks
   4. There are obvious exceptions, AWS SDK for AWS components for example. If in doubt ask.

- If this is a Core project (Brighter or ServiceActivator)

        1. Use Test Driven-Development. 
         1. New behaviors should have a test or 
-         3. This project uses [FakeItEasy](https://github.com/FakeItEasy/FakeItEasy). So should you to contribute.

- If this is a non-core project, such as a Transport or a Store

        1. You may use a Test-After approach here if you prefer, as you are implementing a defined interface for a plug-in
        2. Your testing strategy here may focus on protecting against regression of these components once working
        3. Consider providing a sample to run the code
        4. Consider chaos engineering approaches, i.e. use blockade to simulate network partitions, restart the broker etc.  
              
- Try to follow the [Microsoft .NET Framework Design Guidelines] (<https://github.com/dotnet/corefx/tree/master/Documentation#coding-guidelines>)

1. Providing [BDD] (<http://dannorth.net/introducing-bdd/>) style tests should provide for the need to use scenarios to test the design of your API
2. Use the coding style from [dotnet/corefx] (<https://github.com/dotnet/corefx/blob/master/Documentation/coding-guidelines/coding-style.md>)
3. You can use [codeformatter] (<https://github.com/dotnet/codeformatter>) if you can run VS2015 to automatically update your format
        4. Ensure you update the template for your copyright if using codeformatter
  
0. Make your tests pass
0. Commit

1. Try to write a [good commit message](http://tbaggery.com/2008/04/19/a-note-about-git-commit-messages.html).

0. Merge back into your fork
0. Push to your fork
0. Submit a pull request
0. Sit back, and wait.

1. Try pinging @BrighterCommmand on Twitter if you hear nothing

### Architecture Decision Record ###

If your pull request makes a change to the design of Brighter, please include an [Architectural Decision Record](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions)(ADR) for the change. This should describe the agreed design for the change. The ADR helps us review the change, so that we can understand that we have built what was agreed. (In addition, an ADR captures design decisions so that we can understand why implementation choices were made later).

You can create the ADR as the first step on a new branch/fork. Then your first commit includes the ADR describing the change. This then allows you to create a draft PR which includes the ADR. This allows others to review their understanding of the change, and give feedback early on if those expectations are mismatched. If you update the design as you learn, add another ADR that supercedes the old one.

### Conventional Commits ###

In order to keep a clean commit history, please use [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/#specification) when contributing to Brighter.

### Contributor Licence Agreement ###

To safeguard the project we ask you to sign a Contributor Licence Agreement. The goal is to let you keep your copyright, but to assign it to the project so that it can use it in perpetuity. It is still yours, but the project is not at risk from having multiple contributors holding the copyright, with anyone able to hold it to ransom by removing their grant of licence.

The process of signing works through GitHub.

To get started, <a href="https://www.clahub.com/agreements/iancooper/Paramore">sign the Contributor License Agreement</a>.

### Contributor Code of Conduct ###

Please note that this project is released with a Contributor Code of Conduct. By participating in this project you agree to abide by its terms.

The code of conduct is from [Contributor Covenant](http://contributor-covenant.org/)
