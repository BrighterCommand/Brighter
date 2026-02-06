# Testing Handlers with Paramore.Brighter.Testing

## Overview

When handlers depend on `IAmACommandProcessor` to publish events or send commands, you need a way to verify those interactions in tests. The `Paramore.Brighter.Testing` package provides `SpyCommandProcessor` - a test double that records all calls for later verification.

## Installation

Add a reference to the `Paramore.Brighter.Testing` package in your test project:

```xml
<PackageReference Include="Paramore.Brighter.Testing" />
```

Or reference the project directly:

```xml
<ProjectReference Include="..\..\src\Paramore.Brighter.Testing\Paramore.Brighter.Testing.csproj" />
```

## Using SpyCommandProcessor

### Basic Usage

Inject `SpyCommandProcessor` as your `IAmACommandProcessor` dependency and verify interactions after exercising the handler:

```csharp
// Arrange
var spy = new SpyCommandProcessor();
var handler = new PlaceOrderHandler(spy);
var command = new PlaceOrder { ProductId = "WIDGET-1", Quantity = 3 };

// Act
handler.Handle(command);

// Assert - verify the handler published an OrderPlaced event
spy.WasCalled(CommandType.Publish).ShouldBeTrue();
var published = spy.Observe<OrderPlaced>();
published.ProductId.ShouldBe("WIDGET-1");
```

### API Layers

SpyCommandProcessor provides a layered API, from simple checks to detailed inspection:

#### Layer 1: Quick Checks

For the most common verification needs:

```csharp
// Was a specific method type called?
spy.WasCalled(CommandType.Send)       // true/false
spy.WasCalled(CommandType.Publish)    // true/false
spy.WasCalled(CommandType.Post)       // true/false

// How many times?
spy.CallCount(CommandType.Send)       // int

// Dequeue requests in FIFO order (consuming)
var command = spy.Observe<MyCommand>();
var @event = spy.Observe<MyEvent>();
```

`Observe<T>()` dequeues the next request of type `T` from the queue. This is useful for verifying multiple calls in sequence. It throws `InvalidOperationException` if no matching request is found.

#### Layer 2: Request Inspection

For examining all calls without consuming them:

```csharp
// Get all requests of a specific type (non-destructive, can call repeatedly)
IEnumerable<MyCommand> commands = spy.GetRequests<MyCommand>();

// Get all recorded calls for a command type (includes timestamp and context)
IEnumerable<RecordedCall> sendCalls = spy.GetCalls(CommandType.Send);

// Get the sequence of command types in call order
IReadOnlyList<CommandType> types = spy.Commands;
// e.g. [Send, Publish, Send] after three calls
```

#### Layer 3: Full Details

For advanced scenarios requiring complete call information:

```csharp
// All recorded calls with full details
IReadOnlyList<RecordedCall> allCalls = spy.RecordedCalls;

foreach (var call in allCalls)
{
    Console.WriteLine($"{call.Type} at {call.Timestamp}: {call.Request.GetType().Name}");
    // call.Context provides the RequestContext if one was passed
}
```

### CommandType Values

Each `IAmACommandProcessor` method maps to a `CommandType`:

| Method | CommandType |
|--------|-------------|
| `Send` | `Send` |
| `SendAsync` | `SendAsync` |
| `Publish` | `Publish` |
| `PublishAsync` | `PublishAsync` |
| `Post` | `Post` |
| `PostAsync` | `PostAsync` |
| `DepositPost` | `Deposit` |
| `DepositPostAsync` | `DepositAsync` |
| `ClearOutbox` | `Clear` |
| `ClearOutboxAsync` | `ClearAsync` |
| `Call` | `Call` |
| Scheduled (sync) | `Scheduler` |
| Scheduled (async) | `SchedulerAsync` |

## Verifying Send/Publish/Post Calls

### Verifying Send

```csharp
var spy = new SpyCommandProcessor();
var handler = new MyHandler(spy);

handler.Handle(new TriggerCommand());

// Quick check
spy.WasCalled(CommandType.Send).ShouldBeTrue();

// Inspect the sent command
var sent = spy.Observe<DownstreamCommand>();
sent.SomeProperty.ShouldBe("expected");
```

### Verifying Publish

```csharp
var spy = new SpyCommandProcessor();
var handler = new OrderHandler(spy);

handler.Handle(new PlaceOrder { OrderId = "ORD-1" });

// Check event was published
spy.CallCount(CommandType.Publish).ShouldBe(1);

// Inspect the event
var events = spy.GetRequests<OrderPlaced>();
events.First().OrderId.ShouldBe("ORD-1");
```

### Verifying Post (External Bus)

```csharp
var spy = new SpyCommandProcessor();
var handler = new NotificationHandler(spy);

handler.Handle(new SendNotification { UserId = "user-1" });

spy.WasCalled(CommandType.Post).ShouldBeTrue();
var posted = spy.Observe<NotificationSent>();
posted.UserId.ShouldBe("user-1");
```

## Verifying the Outbox Pattern (DepositPost + ClearOutbox)

When handlers use the outbox pattern, `SpyCommandProcessor` tracks deposits separately:

```csharp
var spy = new SpyCommandProcessor();
var handler = new TransactionalHandler(spy);

handler.Handle(new ProcessPayment { Amount = 99.99m });

// Verify the deposit
spy.WasCalled(CommandType.Deposit).ShouldBeTrue();
spy.DepositedRequests.Count.ShouldBe(1);

// Get the deposited request
var (id, request) = spy.DepositedRequests.First();
var deposited = request.ShouldBeOfType<PaymentProcessed>();
deposited.Amount.ShouldBe(99.99m);

// Simulate ClearOutbox (moves deposits to observation queue)
spy.ClearOutbox(new[] { id });

// Now it's available via Observe
var cleared = spy.Observe<PaymentProcessed>();
cleared.Amount.ShouldBe(99.99m);
```

## State Management

Use `Reset()` between test scenarios when reusing a spy:

```csharp
var spy = new SpyCommandProcessor();

// First scenario
spy.Send(new CommandA());
spy.WasCalled(CommandType.Send).ShouldBeTrue();

// Reset for next scenario
spy.Reset();

spy.WasCalled(CommandType.Send).ShouldBeFalse();
spy.RecordedCalls.Count.ShouldBe(0);
spy.DepositedRequests.Count.ShouldBe(0);
```

## Extending SpyCommandProcessor

All methods on `SpyCommandProcessor` are `virtual`, allowing you to create specialized subclasses:

```csharp
public class ThrowingSpyCommandProcessor : SpyCommandProcessor
{
    public override void Send<TRequest>(TRequest command, RequestContext? requestContext = null)
    {
        base.Send(command, requestContext); // Still records the call
        throw new InvalidOperationException("Send should not be called in this test");
    }
}
```

This is useful for testing error handling paths in your handlers.

## Alternative: Using Mocking Frameworks

If you prefer mocking frameworks, you can mock `IAmACommandProcessor` directly. `SpyCommandProcessor` is a convenience for when you want a lightweight, framework-independent test double.

### Moq

```csharp
var mock = new Mock<IAmACommandProcessor>();
var handler = new MyHandler(mock.Object);

handler.Handle(new MyCommand());

mock.Verify(p => p.Publish(It.IsAny<MyEvent>(), It.IsAny<RequestContext>()), Times.Once);
```

### NSubstitute

```csharp
var substitute = Substitute.For<IAmACommandProcessor>();
var handler = new MyHandler(substitute);

handler.Handle(new MyCommand());

substitute.Received(1).Publish(Arg.Any<MyEvent>(), Arg.Any<RequestContext>());
```

## Best Practices

1. **Prefer `SpyCommandProcessor` over mocking frameworks** for simple verification - it requires no additional dependencies and provides a clearer API.

2. **Use `Observe<T>()`** for sequential verification when order matters. Use `GetRequests<T>()` when you just need all requests of a type.

3. **Use `Reset()`** if sharing a spy across multiple test methods in a fixture rather than creating new instances. Creating a new instance per test class constructor is preferred.

4. **Test behaviors, not interactions** - verify that the right events/commands were produced with the right data, rather than asserting on exact call sequences.

5. **Extend with subclasses** when you need the spy to throw exceptions or return specific values from `Call<T, TResponse>()`.
