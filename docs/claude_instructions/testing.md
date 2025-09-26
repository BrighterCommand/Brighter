# Testing

- Use TDD where possible.
- Write developer tests using xUnit.
- Name test methods in the format: When_[condition]_should_[expected_behavior]. Name test classes [behavior]Tests for the behavior being tested across all tests in the file, for example CommandProcessorPostBoxBulkClearAsyncTests.
- Ensure all new features and bug fixes include appropriate test coverage.

## TDD Style

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

## Test Scope and Isolation

- Only test exports from an assembly
  - To be clear, this means an access modifier of public on methods on public classes.
  - Do not test details, such as methods on internal classes, or private methods.
- Do not expose more than is necessary from an assembly
  - An assembly is a module, it's surface area should be as narrow as possible.
  - Do not make export classes or methods from a module to test them; we only test exports from modules, not implementation details.
  - By following the rules for only testing behaviors, you only need to write tests for the behaviors exposed from the module not its details.
  - Private or Internal classes used in the implementation do not need tests - they are covered by the behavior that led to their creation.

## Test Doubles

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