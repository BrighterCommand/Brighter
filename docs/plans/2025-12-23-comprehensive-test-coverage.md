# Comprehensive Test Coverage Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add 25 tests covering factory-managed lifetimes, CommandProcessor isolation, thread safety, resource management, and error handling.

**Architecture:** Tests organized into focused test classes: FactoryLifetimeTests, FactoryThreadSafetyTests, FactoryResourceManagementTests, FactoryErrorHandlingTests, CommandProcessorIsolationTests.

**Tech Stack:** .NET 9, C#, xUnit

---

## Task 1: Add Singleton Lifetime Tests

**Files:**
- Modify: `tests/Paramore.Brighter.Extensions.Tests/FactoryLifetimeTests.cs`

**Step 1: Add Singleton sync and async tests**

```csharp
[Fact]
public void Factory_WithSingletonLifetime_ReturnsSameInstanceAcrossScopes()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddTransient<TestHandler>();
    services.AddSingleton<IBrighterOptions>(new BrighterOptions
    {
        HandlerLifetime = ServiceLifetime.Singleton
    });

    var provider = services.BuildServiceProvider();
    var factory = new ServiceProviderHandlerFactory(provider);
    var lifetime1 = new TestLifetimeScope();
    var lifetime2 = new TestLifetimeScope();

    // Act
    var handler1 = ((IAmAHandlerFactorySync)factory).Create(typeof(TestHandler), lifetime1);
    var handler2 = ((IAmAHandlerFactorySync)factory).Create(typeof(TestHandler), lifetime2);

    // Assert
    Assert.Same(handler1, handler2);
}

[Fact]
public void AsyncFactory_WithSingletonLifetime_ReturnsSameInstanceAcrossScopes()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddTransient<TestAsyncHandler>();
    services.AddSingleton<IBrighterOptions>(new BrighterOptions
    {
        HandlerLifetime = ServiceLifetime.Singleton
    });

    var provider = services.BuildServiceProvider();
    var factory = new ServiceProviderHandlerFactory(provider);
    var lifetime1 = new TestLifetimeScope();
    var lifetime2 = new TestLifetimeScope();

    // Act
    var handler1 = ((IAmAHandlerFactoryAsync)factory).Create(typeof(TestAsyncHandler), lifetime1);
    var handler2 = ((IAmAHandlerFactoryAsync)factory).Create(typeof(TestAsyncHandler), lifetime2);

    // Assert
    Assert.Same(handler1, handler2);
}
```

**Step 2: Run tests**

Run: `dotnet test tests/Paramore.Brighter.Extensions.Tests/ --filter "Singleton"`
Expected: PASS

**Step 3: Commit**

```bash
git add tests/Paramore.Brighter.Extensions.Tests/FactoryLifetimeTests.cs
git commit -m "test: add Singleton lifetime tests for sync and async factories"
```

---

## Task 2: Add Async Factory Tests (Scoped + Transient)

**Files:**
- Modify: `tests/Paramore.Brighter.Extensions.Tests/FactoryLifetimeTests.cs`

**Step 1: Add TestAsyncHandler and async lifetime tests**

```csharp
[Fact]
public void AsyncFactory_WithScopedLifetime_ReturnsSameInstanceWithinScope()
{
    var services = new ServiceCollection();
    services.AddTransient<TestAsyncHandler>();
    services.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Scoped });

    var provider = services.BuildServiceProvider();
    var factory = new ServiceProviderHandlerFactory(provider);
    var lifetime = new TestLifetimeScope();

    var handler1 = ((IAmAHandlerFactoryAsync)factory).Create(typeof(TestAsyncHandler), lifetime);
    var handler2 = ((IAmAHandlerFactoryAsync)factory).Create(typeof(TestAsyncHandler), lifetime);

    Assert.Same(handler1, handler2);
}

[Fact]
public void AsyncFactory_WithScopedLifetime_ReturnsDifferentInstancesAcrossScopes()
{
    var services = new ServiceCollection();
    services.AddTransient<TestAsyncHandler>();
    services.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Scoped });

    var provider = services.BuildServiceProvider();
    var factory = new ServiceProviderHandlerFactory(provider);

    var handler1 = ((IAmAHandlerFactoryAsync)factory).Create(typeof(TestAsyncHandler), new TestLifetimeScope());
    var handler2 = ((IAmAHandlerFactoryAsync)factory).Create(typeof(TestAsyncHandler), new TestLifetimeScope());

    Assert.NotSame(handler1, handler2);
}

[Fact]
public void AsyncFactory_WithTransientLifetime_ReturnsDifferentInstances()
{
    var services = new ServiceCollection();
    services.AddTransient<TestAsyncHandler>();
    services.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });

    var provider = services.BuildServiceProvider();
    var factory = new ServiceProviderHandlerFactory(provider);
    var lifetime = new TestLifetimeScope();

    var handler1 = ((IAmAHandlerFactoryAsync)factory).Create(typeof(TestAsyncHandler), lifetime);
    var handler2 = ((IAmAHandlerFactoryAsync)factory).Create(typeof(TestAsyncHandler), lifetime);

    Assert.NotSame(handler1, handler2);
}

private class TestAsyncHandler : RequestHandlerAsync<TestCommand>
{
    public override Task<TestCommand> HandleAsync(TestCommand command, CancellationToken cancellationToken = default)
        => Task.FromResult(command);
}
```

**Step 2: Run tests**

Run: `dotnet test tests/Paramore.Brighter.Extensions.Tests/ --filter "AsyncFactory"`
Expected: PASS

**Step 3: Commit**

```bash
git add tests/Paramore.Brighter.Extensions.Tests/FactoryLifetimeTests.cs
git commit -m "test: add async handler factory tests for Scoped and Transient"
```

---

## Task 3: Add Handler with Dependencies Test

**Files:**
- Modify: `tests/Paramore.Brighter.Extensions.Tests/FactoryLifetimeTests.cs`

**Step 1: Add dependency injection test**

```csharp
[Fact]
public void Factory_HandlerWithDependency_ResolvesBothCorrectly()
{
    var services = new ServiceCollection();
    services.AddSingleton<IDependencyService, DependencyService>();
    services.AddTransient<HandlerWithDependency>();
    services.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Scoped });

    var provider = services.BuildServiceProvider();
    var factory = new ServiceProviderHandlerFactory(provider);

    var handler = (HandlerWithDependency)((IAmAHandlerFactorySync)factory).Create(typeof(HandlerWithDependency), new TestLifetimeScope())!;

    Assert.NotNull(handler);
    Assert.NotNull(handler.Dependency);
}

private interface IDependencyService { }
private class DependencyService : IDependencyService { }
private class HandlerWithDependency : RequestHandler<TestCommand>
{
    public IDependencyService Dependency { get; }
    public HandlerWithDependency(IDependencyService dependency) => Dependency = dependency;
    public override TestCommand Handle(TestCommand command) => command;
}
```

**Step 2: Run test**

Run: `dotnet test tests/Paramore.Brighter.Extensions.Tests/ --filter "HandlerWithDependency"`
Expected: PASS

**Step 3: Commit**

```bash
git add tests/Paramore.Brighter.Extensions.Tests/FactoryLifetimeTests.cs
git commit -m "test: add handler with dependency test"
```

---

## Task 4: Add Disposal Verification Tests

**Files:**
- Modify: `tests/Paramore.Brighter.Extensions.Tests/FactoryLifetimeTests.cs`

**Step 1: Add disposal tests**

```csharp
[Fact]
public void Factory_Release_AllowsSubsequentCreation()
{
    var services = new ServiceCollection();
    services.AddTransient<TestHandler>();
    services.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Scoped });

    var provider = services.BuildServiceProvider();
    var factory = new ServiceProviderHandlerFactory(provider);
    var lifetime = new TestLifetimeScope();

    var handler1 = ((IAmAHandlerFactorySync)factory).Create(typeof(TestHandler), lifetime);
    ((IAmAHandlerFactorySync)factory).Release(handler1);
    var handler2 = ((IAmAHandlerFactorySync)factory).Create(typeof(TestHandler), lifetime);

    Assert.NotNull(handler2);
}

[Fact]
public void Factory_UnregisteredHandler_ReturnsNull()
{
    var services = new ServiceCollection();
    services.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });

    var provider = services.BuildServiceProvider();
    var factory = new ServiceProviderHandlerFactory(provider);

    var handler = ((IAmAHandlerFactorySync)factory).Create(typeof(TestHandler), new TestLifetimeScope());

    Assert.Null(handler);
}
```

**Step 2: Run tests**

Run: `dotnet test tests/Paramore.Brighter.Extensions.Tests/ --filter "Release OR Unregistered"`
Expected: PASS

**Step 3: Commit**

```bash
git add tests/Paramore.Brighter.Extensions.Tests/FactoryLifetimeTests.cs
git commit -m "test: add disposal and unregistered handler tests"
```

---

## Task 5: Add CommandProcessor Isolation Tests

**Files:**
- Create: `tests/Paramore.Brighter.Extensions.Tests/CommandProcessorIsolationTests.cs`

**Step 1: Create test file**

```csharp
#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class CommandProcessorIsolationTests
{
    [Fact]
    public void TwoCommandProcessors_HaveIsolatedState()
    {
        var services1 = new ServiceCollection();
        services1.AddBrighter();
        var provider1 = services1.BuildServiceProvider();

        var services2 = new ServiceCollection();
        services2.AddBrighter();
        var provider2 = services2.BuildServiceProvider();

        var cp1 = provider1.GetRequiredService<IAmACommandProcessor>();
        var cp2 = provider2.GetRequiredService<IAmACommandProcessor>();

        Assert.NotSame(cp1, cp2);
    }

    [Fact]
    public async Task ParallelCommandProcessors_AreAllUnique()
    {
        var tasks = new Task[10];
        var processors = new IAmACommandProcessor[10];

        for (int i = 0; i < 10; i++)
        {
            var index = i;
            tasks[i] = Task.Run(() =>
            {
                var services = new ServiceCollection();
                services.AddBrighter();
                var provider = services.BuildServiceProvider();
                processors[index] = provider.GetRequiredService<IAmACommandProcessor>();
            });
        }

        await Task.WhenAll(tasks);

        for (int i = 0; i < 10; i++)
            for (int j = i + 1; j < 10; j++)
                Assert.NotSame(processors[i], processors[j]);
    }
}
```

**Step 2: Run tests**

Run: `dotnet test tests/Paramore.Brighter.Extensions.Tests/ --filter "CommandProcessorIsolation"`
Expected: PASS

**Step 3: Commit**

```bash
git add tests/Paramore.Brighter.Extensions.Tests/CommandProcessorIsolationTests.cs
git commit -m "test: add CommandProcessor isolation tests"
```

---

## Task 6: Add Thread Safety Tests

**Files:**
- Create: `tests/Paramore.Brighter.Extensions.Tests/FactoryThreadSafetyTests.cs`

**Step 1: Create thread safety test file**

```csharp
#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk> */
#endregion

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class FactoryThreadSafetyTests
{
    [Fact]
    public async Task ConcurrentSingletonResolution_ReturnsSameInstance()
    {
        var services = new ServiceCollection();
        services.AddTransient<ThreadTestHandler>();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Singleton });

        var provider = services.BuildServiceProvider();
        var factory = new ServiceProviderHandlerFactory(provider);
        var handlers = new ConcurrentBag<IHandleRequests>();

        var tasks = Enumerable.Range(0, 100).Select(_ => Task.Run(() =>
        {
            var handler = ((IAmAHandlerFactorySync)factory).Create(typeof(ThreadTestHandler), new TestLifetimeScope());
            handlers.Add(handler!);
        }));

        await Task.WhenAll(tasks);

        Assert.Single(handlers.Distinct());
    }

    [Fact]
    public async Task ConcurrentScopedResolution_SameScopeReturnsSameInstance()
    {
        var services = new ServiceCollection();
        services.AddTransient<ThreadTestHandler>();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Scoped });

        var provider = services.BuildServiceProvider();
        var factory = new ServiceProviderHandlerFactory(provider);
        var sharedLifetime = new TestLifetimeScope();
        var handlers = new ConcurrentBag<IHandleRequests>();

        var tasks = Enumerable.Range(0, 50).Select(_ => Task.Run(() =>
        {
            var handler = ((IAmAHandlerFactorySync)factory).Create(typeof(ThreadTestHandler), sharedLifetime);
            handlers.Add(handler!);
        }));

        await Task.WhenAll(tasks);

        Assert.Single(handlers.Distinct());
    }

    [Fact]
    public async Task ConcurrentTransientResolution_ReturnsDifferentInstances()
    {
        var services = new ServiceCollection();
        services.AddTransient<ThreadTestHandler>();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });

        var provider = services.BuildServiceProvider();
        var factory = new ServiceProviderHandlerFactory(provider);
        var handlers = new ConcurrentBag<IHandleRequests>();

        var tasks = Enumerable.Range(0, 50).Select(_ => Task.Run(() =>
        {
            var handler = ((IAmAHandlerFactorySync)factory).Create(typeof(ThreadTestHandler), new TestLifetimeScope());
            handlers.Add(handler!);
        }));

        await Task.WhenAll(tasks);

        Assert.Equal(50, handlers.Distinct().Count());
    }

    [Fact]
    public async Task ConcurrentSingletonResolution_OnlyCreatesOneInstance()
    {
        CountingHandler.ResetCount();

        var services = new ServiceCollection();
        services.AddTransient<CountingHandler>();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Singleton });

        var provider = services.BuildServiceProvider();
        var factory = new ServiceProviderHandlerFactory(provider);
        var handlers = new ConcurrentBag<IHandleRequests>();

        var barrier = new Barrier(100);
        var tasks = Enumerable.Range(0, 100).Select(_ => Task.Run(() =>
        {
            barrier.SignalAndWait();
            var handler = ((IAmAHandlerFactorySync)factory).Create(typeof(CountingHandler), new TestLifetimeScope());
            handlers.Add(handler!);
        }));

        await Task.WhenAll(tasks);

        Assert.Equal(1, CountingHandler.InstanceCount);
    }

    private class ThreadTestHandler : RequestHandler<ThreadTestCommand>
    {
        public override ThreadTestCommand Handle(ThreadTestCommand command) => command;
    }

    private class CountingHandler : RequestHandler<ThreadTestCommand>
    {
        private static int _count;
        public static int InstanceCount => _count;
        public static void ResetCount() => _count = 0;
        public CountingHandler() => Interlocked.Increment(ref _count);
        public override ThreadTestCommand Handle(ThreadTestCommand command) => command;
    }

    private class ThreadTestCommand : Command
    {
        public ThreadTestCommand() : base(Guid.NewGuid()) { }
    }

    private class TestLifetimeScope : IAmALifetime
    {
        public void Add(IHandleRequests instance) { }
        public void Add(IHandleRequestsAsync instance) { }
        public void Dispose() { }
    }
}
```

**Step 2: Run tests**

Run: `dotnet test tests/Paramore.Brighter.Extensions.Tests/ --filter "ThreadSafety"`
Expected: PASS (if Singleton test fails, implementation needs Lazy<T>)

**Step 3: Commit**

```bash
git add tests/Paramore.Brighter.Extensions.Tests/FactoryThreadSafetyTests.cs
git commit -m "test: add thread safety tests for concurrent handler resolution"
```

---

## Task 7: Add Error Handling Tests

**Files:**
- Create: `tests/Paramore.Brighter.Extensions.Tests/FactoryErrorHandlingTests.cs`

**Step 1: Create error handling test file**

```csharp
#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk> */
#endregion

using System;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class FactoryErrorHandlingTests
{
    [Fact]
    public void Factory_HandlerConstructorThrows_PropagatesException()
    {
        var services = new ServiceCollection();
        services.AddTransient<ThrowingHandler>();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });

        var provider = services.BuildServiceProvider();
        var factory = new ServiceProviderHandlerFactory(provider);

        Assert.ThrowsAny<Exception>(() =>
            ((IAmAHandlerFactorySync)factory).Create(typeof(ThrowingHandler), new TestLifetimeScope()));
    }

    [Fact]
    public void Factory_InvalidHandlerType_ReturnsNull()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });

        var provider = services.BuildServiceProvider();
        var factory = new ServiceProviderHandlerFactory(provider);

        var result = ((IAmAHandlerFactorySync)factory).Create(typeof(string), new TestLifetimeScope());

        Assert.Null(result);
    }

    [Fact]
    public void Factory_NullLifetime_WithTransient_StillWorks()
    {
        var services = new ServiceCollection();
        services.AddTransient<SimpleHandler>();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });

        var provider = services.BuildServiceProvider();
        var factory = new ServiceProviderHandlerFactory(provider);

        var handler = ((IAmAHandlerFactorySync)factory).Create(typeof(SimpleHandler), null!);

        Assert.NotNull(handler);
    }

    private class ThrowingHandler : RequestHandler<ErrorCommand>
    {
        public ThrowingHandler() => throw new InvalidOperationException("Constructor failed");
        public override ErrorCommand Handle(ErrorCommand command) => command;
    }

    private class SimpleHandler : RequestHandler<ErrorCommand>
    {
        public override ErrorCommand Handle(ErrorCommand command) => command;
    }

    private class ErrorCommand : Command
    {
        public ErrorCommand() : base(Guid.NewGuid()) { }
    }

    private class TestLifetimeScope : IAmALifetime
    {
        public void Add(IHandleRequests instance) { }
        public void Add(IHandleRequestsAsync instance) { }
        public void Dispose() { }
    }
}
```

**Step 2: Run tests**

Run: `dotnet test tests/Paramore.Brighter.Extensions.Tests/ --filter "ErrorHandling"`
Expected: PASS

**Step 3: Commit**

```bash
git add tests/Paramore.Brighter.Extensions.Tests/FactoryErrorHandlingTests.cs
git commit -m "test: add error handling tests"
```

---

## Task 8: Final Verification

**Step 1: Run all Extensions tests**

Run: `dotnet test tests/Paramore.Brighter.Extensions.Tests/`
Expected: All tests pass (50+ tests)

**Step 2: Run Core and InMemory tests**

Run: `dotnet test tests/Paramore.Brighter.Core.Tests/ && dotnet test tests/Paramore.Brighter.InMemory.Tests/`
Expected: All tests pass

**Step 3: Commit and push**

```bash
git push origin feature/deferred-instantiation
```

---

## Summary

| Task | Tests | Coverage |
|------|-------|----------|
| 1. Singleton lifetime | 2 | Sync + Async |
| 2. Async factory | 3 | Scoped (2) + Transient |
| 3. Handler with dependencies | 1 | DI integration |
| 4. Disposal/unregistered | 2 | Release + null |
| 5. CommandProcessor isolation | 2 | Instance + parallel |
| 6. Thread safety | 4 | Singleton, Scoped, Transient, strict |
| 7. Error handling | 3 | Throws, invalid, null lifetime |
| **Total** | **17** | **Comprehensive** |

Note: Original 3 tests in FactoryLifetimeTests remain (Scoped same/different, Transient sync), bringing total to ~20 new tests.
