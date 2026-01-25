# 37. Provide Roslyn Analyzers for Brighter

Date: 2026-01-25

## Status

Proposed

## Context

Using Brighter correctly often involves adhering to specific patterns and conventions that are not strictly enforced by the C# compiler alone. For example, developers must ensure that:
- Attributes like `[Wrap]` are used correctly to configure the pipeline.
- `Publication` objects are initialized with a valid `RequestType` that implements `IRequest` for proper mapping.
- Subscriptions are configured with valid parameters.

Currently, violations of these rules often result in runtime errors (e.g., `RuntimeBinderException`, `ArgumentException`) or silent failures. identifying these issues requires running the application, which delays the feedback loop and increases the risk of bugs reaching production.

We want to "shift left" on these checks, providing feedback to developers as they write code.

## Decision

We will introduce a new project, `Paramore.Brighter.Analyzer`, containing custom Roslyn analyzers.
This project will be distributed as a NuGet package that developers can include in their projects.

The analyzers will inspect code at compile-time and report diagnostics (warnings or errors) for known invalid usage patterns of Brighter components.

The initial implementation will include the following analyzers:

1. **PublicationRequestTypeAssignmentAnalyzer**: 
   - Ensures that when a `Publication` object is created, the `RequestType` property is explicitly assigned.
   - Verifies that the assigned type implements `IRequest`.
   
2. **SubscriptionConstructorAnalyzer**:
   - Validates that `Subscription` objects are instantiated with correct arguments and configuration.

3. **WrapAttributeAnalyzer**:

## Technical Implementation Strategy

The logic for the analyzers will be implemented using the **Roslyn API** and the **Visitor Pattern**.

1.  **Roslyn Syntax & Operation Trees**: We leverage Roslyn to inspect the code structure. We specifically focus on the **Operation Tree (IOperation)**, which provides a semantic view of the code (e.g., understanding that a line is an object creation regardless of syntax).
2.  **Visitor Pattern**: To efficiently traverse the code, we implement a `Visitor` (e.g., `RequestTypeAssignmentVisitor`).
    - The **Visitor** "walks" through specific nodes of the code tree (like `ObjectCreation`).
    - We utilize **Double Dispatch** (via the `Accept` method) to ensure the correct `Visit` method is called for each node type. This avoids the need for explicit type casting and massive `if-else` or `switch` statements to determine node types, making the code cleaner and more maintainable.
    - It maintains state as it visits nodes (e.g., "I am currently inside a Publication object creation").

## Consequences

### Positive
- **Immediate Feedback**: Developers receive feedback on incorrect usage within the IDE and at build time.
- **Reduced Runtime Errors**: Prevents a class of configuration and usage errors from occurring at runtime.
- **Better Discovery**: Helps new users learn Brighter's constraints and requirements through compiler messages rather than documentation lookup or trial-and-error.

### Negative
- **Maintenance**: The analyzer project must be maintained and updated as Brighter's API and patterns evolve.

