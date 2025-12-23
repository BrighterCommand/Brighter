# Factory-Managed Handler Lifetimes Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Eliminate the `tempOptions` double-invocation pattern by having `ServiceProviderHandlerFactory` manage handler lifetimes at runtime instead of relying on DI container registration lifetimes.

**Architecture:** Always register handlers as Transient in DI. The factory reads `IBrighterOptions.HandlerLifetime` at resolution time and manages Scoped/Singleton behavior internally via instance caching. This removes the need for registries to know lifetimes at registration time, eliminating the `tempOptions` workaround.

**Tech Stack:** .NET 9, C#, Microsoft.Extensions.DependencyInjection, xUnit

---

## Task 1: Add Scoped Instance Caching to ServiceProviderHandlerFactory

**Files:**
- Modify: `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderHandlerFactory.cs`
- Test: `tests/Paramore.Brighter.Extensions.Tests/FactoryLifetimeTests.cs`

**Step 1: Write the failing test**

Create new test file `tests/Paramore.Brighter.Extensions.Tests/FactoryLifetimeTests.cs`:

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

using System;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class FactoryLifetimeTests
{
    [Fact]
    public void Factory_WithScopedLifetime_ReturnsSameInstanceWithinScope()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<TestHandler>();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped
        });

        var provider = services.BuildServiceProvider();
        var factory = new ServiceProviderHandlerFactory(provider);
        var lifetime = new TestLifetimeScope();

        // Act
        var handler1 = ((IAmAHandlerFactorySync)factory).Create(typeof(TestHandler), lifetime);
        var handler2 = ((IAmAHandlerFactorySync)factory).Create(typeof(TestHandler), lifetime);

        // Assert - Same scope should return same instance
        Assert.Same(handler1, handler2);
    }

    [Fact]
    public void Factory_WithScopedLifetime_ReturnsDifferentInstancesAcrossScopes()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<TestHandler>();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped
        });

        var provider = services.BuildServiceProvider();
        var factory = new ServiceProviderHandlerFactory(provider);
        var lifetime1 = new TestLifetimeScope();
        var lifetime2 = new TestLifetimeScope();

        // Act
        var handler1 = ((IAmAHandlerFactorySync)factory).Create(typeof(TestHandler), lifetime1);
        var handler2 = ((IAmAHandlerFactorySync)factory).Create(typeof(TestHandler), lifetime2);

        // Assert - Different scopes should return different instances
        Assert.NotSame(handler1, handler2);
    }

    [Fact]
    public void Factory_WithTransientLifetime_ReturnsDifferentInstancesEachTime()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<TestHandler>();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Transient
        });

        var provider = services.BuildServiceProvider();
        var factory = new ServiceProviderHandlerFactory(provider);
        var lifetime = new TestLifetimeScope();

        // Act
        var handler1 = ((IAmAHandlerFactorySync)factory).Create(typeof(TestHandler), lifetime);
        var handler2 = ((IAmAHandlerFactorySync)factory).Create(typeof(TestHandler), lifetime);

        // Assert - Transient should return new instance each time
        Assert.NotSame(handler1, handler2);
    }

    private class TestHandler : IHandleRequests<TestCommand>
    {
        public IHandleRequests<TestCommand> FallbackHandler { get; set; } = null!;
        public IRequestContext? Context { get; set; }
        public HandlerName Name => new(nameof(TestHandler));
        public void SetWasCancelled(bool wasCancelled) { }
        public TestCommand Handle(TestCommand command) => command;
        public void InitializeFromAttributeParams(params object[] initializerList) { }
    }

    private class TestCommand : Command
    {
        public TestCommand() : base(Guid.NewGuid()) { }
    }

    private class TestLifetimeScope : IAmALifetime
    {
        public void Add(IHandleRequests instance) { }
        public void Add(IHandleRequestsAsync instance) { }
        public void Dispose() { }
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/Paramore.Brighter.Extensions.Tests/ --filter "FactoryLifetimeTests"`
Expected: FAIL - Factory doesn't cache scoped instances

**Step 3: Modify ServiceProviderHandlerFactory to support scoped caching**

Update `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderHandlerFactory.cs`:

```csharp
#region Licence
/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    /// <summary>
    /// A factory for handlers using the .NET IoC container for implementation details.
    /// Manages handler lifetimes (Singleton/Scoped/Transient) based on IBrighterOptions
    /// rather than relying solely on DI container registration lifetimes.
    /// </summary>
    public class ServiceProviderHandlerFactory : IAmAHandlerFactorySync, IAmAHandlerFactoryAsync
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ServiceLifetime _handlerLifetime;
        private readonly ConcurrentDictionary<IAmALifetime, IServiceScope> _scopes = new();
        private readonly ConcurrentDictionary<(IAmALifetime, Type), object> _scopedInstances = new();

        /// <summary>
        /// Constructs a factory that uses the .NET IoC container as the factory
        /// </summary>
        /// <param name="serviceProvider">The .NET IoC container</param>
        public ServiceProviderHandlerFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            var options = (IBrighterOptions?)serviceProvider.GetService(typeof(IBrighterOptions));
            _handlerLifetime = options?.HandlerLifetime ?? ServiceLifetime.Transient;
        }

        /// <summary>
        /// Creates an instance of the request handler.
        /// Lifetime behavior is determined by IBrighterOptions.HandlerLifetime:
        /// - Singleton: Single instance from root provider
        /// - Scoped: Cached per IAmALifetime scope
        /// - Transient: New instance each time
        /// </summary>
        IHandleRequests? IAmAHandlerFactorySync.Create(Type handlerType, IAmALifetime lifetime)
        {
            return _handlerLifetime switch
            {
                ServiceLifetime.Singleton => CreateSingleton<IHandleRequests>(handlerType),
                ServiceLifetime.Scoped => GetOrCreateScoped<IHandleRequests>(handlerType, lifetime),
                _ => CreateTransient<IHandleRequests>(handlerType, lifetime)
            };
        }

        /// <summary>
        /// Creates an instance of the async request handler.
        /// Lifetime behavior is determined by IBrighterOptions.HandlerLifetime.
        /// </summary>
        IHandleRequestsAsync? IAmAHandlerFactoryAsync.Create(Type handlerType, IAmALifetime lifetime)
        {
            return _handlerLifetime switch
            {
                ServiceLifetime.Singleton => CreateSingleton<IHandleRequestsAsync>(handlerType),
                ServiceLifetime.Scoped => GetOrCreateScoped<IHandleRequestsAsync>(handlerType, lifetime),
                _ => CreateTransient<IHandleRequestsAsync>(handlerType, lifetime)
            };
        }

        private T? CreateSingleton<T>(Type handlerType) where T : class
        {
            return (T?)_serviceProvider.GetService(handlerType);
        }

        private T? GetOrCreateScoped<T>(Type handlerType, IAmALifetime lifetime) where T : class
        {
            var key = (lifetime, handlerType);
            var instance = _scopedInstances.GetOrAdd(key, _ =>
            {
                var scope = _scopes.GetOrAdd(lifetime, _ => _serviceProvider.CreateScope());
                return scope.ServiceProvider.GetService(handlerType)!;
            });
            return (T?)instance;
        }

        private T? CreateTransient<T>(Type handlerType, IAmALifetime lifetime) where T : class
        {
            if (!_scopes.ContainsKey(lifetime))
                _scopes.TryAdd(lifetime, _serviceProvider.CreateScope());

            return (T?)_scopes[lifetime].ServiceProvider.GetService(handlerType);
        }

        /// <summary>
        /// Release the request handler
        /// </summary>
        public void Release(IHandleRequests handler, IAmALifetime lifetime)
        {
            if (_handlerLifetime == ServiceLifetime.Singleton) return;

            var disposal = handler as IDisposable;
            disposal?.Dispose();

            ReleaseScope(lifetime);
        }

        public void Release(IHandleRequestsAsync? handler, IAmALifetime lifetime)
        {
            if (_handlerLifetime == ServiceLifetime.Singleton) return;

            var disposal = handler as IDisposable;
            disposal?.Dispose();

            ReleaseScope(lifetime);
        }

        private void ReleaseScope(IAmALifetime lifetime)
        {
            // Clear scoped instances for this lifetime
            foreach (var key in _scopedInstances.Keys)
            {
                if (key.Item1 == lifetime)
                    _scopedInstances.TryRemove(key, out _);
            }

            if (!_scopes.TryGetValue(lifetime, out IServiceScope? scope))
                return;

            scope.Dispose();
            _scopes.TryRemove(lifetime, out _);
        }
    }
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/Paramore.Brighter.Extensions.Tests/ --filter "FactoryLifetimeTests"`
Expected: PASS (all 3 tests)

**Step 5: Run all tests to verify no regressions**

Run: `dotnet test tests/Paramore.Brighter.Extensions.Tests/`
Expected: All tests pass

**Step 6: Commit**

```bash
git add src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderHandlerFactory.cs
git add tests/Paramore.Brighter.Extensions.Tests/FactoryLifetimeTests.cs
git commit -m "feat: add scoped instance caching to ServiceProviderHandlerFactory

Factory now manages Scoped lifetime internally via instance caching per
IAmALifetime scope, rather than relying solely on DI container lifetimes.
This enables handlers to be registered as Transient while still getting
Scoped behavior when configured."
```

---

## Task 2: Simplify ServiceCollectionSubscriberRegistry (Remove Lifetime Parameter)

**Files:**
- Modify: `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionSubscriberRegistry.cs`

**Step 1: Remove lifetime from constructor and always use Transient**

Update the file to remove the `_lifetime` field and always register handlers as Transient:

```csharp
// Change constructor from:
public ServiceCollectionSubscriberRegistry(IServiceCollection services, ServiceLifetime lifetime = ServiceLifetime.Transient)
{
    _services = services;
    _registry = new SubscriberRegistry();
    _lifetime = lifetime;
}

// To:
public ServiceCollectionSubscriberRegistry(IServiceCollection services)
{
    _services = services;
    _registry = new SubscriberRegistry();
}
```

And update all `Add` and `Register` methods to use `ServiceLifetime.Transient`:

```csharp
// Change from:
_services.TryAdd(new ServiceDescriptor(handlerType, handlerType, _lifetime));

// To:
_services.TryAdd(new ServiceDescriptor(handlerType, handlerType, ServiceLifetime.Transient));
```

**Step 2: Build to verify**

Run: `dotnet build src/Paramore.Brighter.Extensions.DependencyInjection/`
Expected: Build errors - callers still passing lifetime

**Step 3: Note the callers that need updating (for next tasks)**

The build errors will show us what needs to change. Don't fix them yet.

**Step 4: Commit (partial - will cause build break until Task 4)**

```bash
git add src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionSubscriberRegistry.cs
git commit -m "refactor: remove lifetime parameter from ServiceCollectionSubscriberRegistry

BREAKING: Handlers are now always registered as Transient in DI.
Actual lifetime behavior is managed by ServiceProviderHandlerFactory
based on IBrighterOptions.HandlerLifetime.

Part of factory-managed-lifetimes initiative."
```

---

## Task 3: Simplify ServiceCollectionTransformerRegistry and ServiceCollectionMessageMapperRegistryBuilder

**Files:**
- Modify: `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionTransformerRegistry.cs`
- Modify: `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionMessageMapperRegistryBuilder.cs`

**Step 1: Update ServiceCollectionTransformerRegistry**

Remove lifetime parameter, always use Transient:

```csharp
// Change constructor from:
public ServiceCollectionTransformerRegistry(IServiceCollection services, ServiceLifetime serviceLifetime = ServiceLifetime.Singleton)

// To:
public ServiceCollectionTransformerRegistry(IServiceCollection services)
```

**Step 2: Update ServiceCollectionMessageMapperRegistryBuilder**

Remove lifetime parameter, always use Transient:

```csharp
// Change constructor from:
public ServiceCollectionMessageMapperRegistryBuilder(IServiceCollection services, ServiceLifetime lifetime = ServiceLifetime.Singleton)

// To:
public ServiceCollectionMessageMapperRegistryBuilder(IServiceCollection services)
```

**Step 3: Commit**

```bash
git add src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionTransformerRegistry.cs
git add src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionMessageMapperRegistryBuilder.cs
git commit -m "refactor: remove lifetime parameters from transformer and mapper registries

Consistent with ServiceCollectionSubscriberRegistry change.
All registrations now use Transient - actual lifetime managed by factory."
```

---

## Task 4: Simplify BrighterHandlerBuilder (Remove Lifetime Parameters)

**Files:**
- Modify: `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionExtensions.cs`

**Step 1: Update BrighterHandlerBuilder signature**

Remove the lifetime parameters:

```csharp
// Change from:
public static IBrighterBuilder BrighterHandlerBuilder(
    IServiceCollection services,
    Func<IServiceProvider, BrighterOptions> optionsFunc,
    ServiceLifetime handlerLifetime = ServiceLifetime.Transient,
    ServiceLifetime transformerLifetime = ServiceLifetime.Transient,
    ServiceLifetime mapperLifetime = ServiceLifetime.Transient)

// To:
public static IBrighterBuilder BrighterHandlerBuilder(
    IServiceCollection services,
    Func<IServiceProvider, BrighterOptions> optionsFunc)
```

**Step 2: Update registry instantiations**

```csharp
// Change from:
var subscriberRegistry = new ServiceCollectionSubscriberRegistry(services, handlerLifetime);
var transformRegistry = new ServiceCollectionTransformerRegistry(services, transformerLifetime);
var mapperRegistry = new ServiceCollectionMessageMapperRegistryBuilder(services, mapperLifetime);

// To:
var subscriberRegistry = new ServiceCollectionSubscriberRegistry(services);
var transformRegistry = new ServiceCollectionTransformerRegistry(services);
var mapperRegistry = new ServiceCollectionMessageMapperRegistryBuilder(services);
```

**Step 3: Commit**

```bash
git add src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionExtensions.cs
git commit -m "refactor: remove lifetime parameters from BrighterHandlerBuilder

Registries no longer need lifetime - they always use Transient.
Actual lifetime managed by ServiceProviderHandlerFactory at runtime."
```

---

## Task 5: Remove tempOptions Pattern from AddBrighter

**Files:**
- Modify: `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionExtensions.cs`
- Test: `tests/Paramore.Brighter.Extensions.Tests/ServiceProviderLambdaTests.cs`

**Step 1: Write test verifying configure runs once**

Add to `ServiceProviderLambdaTests.cs`:

```csharp
[Fact]
public void AddBrighter_ConfigureActionRunsExactlyOnce()
{
    // Arrange
    var services = new ServiceCollection();
    var invokeCount = 0;

    // Act
    services.AddBrighter(options =>
    {
        invokeCount++;
        options.HandlerLifetime = ServiceLifetime.Scoped;
    });

    var provider = services.BuildServiceProvider();
    var options = provider.GetRequiredService<IBrighterOptions>();

    // Assert - Configure should run exactly once (via Options pattern)
    Assert.Equal(1, invokeCount);
    Assert.Equal(ServiceLifetime.Scoped, options.HandlerLifetime);
}
```

**Step 2: Run test**

Run: `dotnet test tests/Paramore.Brighter.Extensions.Tests/ --filter "ConfigureActionRunsExactlyOnce"`
Expected: FAIL - currently runs twice (tempOptions + Options pattern)

**Step 3: Remove tempOptions pattern**

Update `AddBrighter(Action<BrighterOptions>?)`:

```csharp
public static IBrighterBuilder AddBrighter(
    this IServiceCollection services,
    Action<BrighterOptions>? configure = null)
{
    if (services == null)
        throw new ArgumentNullException(nameof(services));

    // Register with Options pattern for extensibility (PostConfigure support)
    services.AddOptions<BrighterOptions>();
    if (configure != null)
        services.Configure(configure);

    // Register IBrighterOptions resolved from IOptions<BrighterOptions>
    services.TryAddSingleton<IBrighterOptions>(sp =>
        sp.GetRequiredService<IOptions<BrighterOptions>>().Value);

    // No tempOptions needed - factory reads lifetime from resolved options
    return BrighterHandlerBuilder(services, sp =>
        (BrighterOptions)sp.GetRequiredService<IBrighterOptions>());
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/Paramore.Brighter.Extensions.Tests/ --filter "ConfigureActionRunsExactlyOnce"`
Expected: PASS

**Step 5: Run all tests**

Run: `dotnet test tests/Paramore.Brighter.Extensions.Tests/`
Expected: All tests pass

**Step 6: Commit**

```bash
git add src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionExtensions.cs
git add tests/Paramore.Brighter.Extensions.Tests/ServiceProviderLambdaTests.cs
git commit -m "fix: remove tempOptions double-invocation pattern

Configure action now runs exactly once via Options pattern.
Factory reads HandlerLifetime from resolved IBrighterOptions at runtime.

Fixes: inefficiency, inconsistency risk, and complexity concerns."
```

---

## Task 6: Update LifetimeConfigurationTests

**Files:**
- Modify: `tests/Paramore.Brighter.Extensions.Tests/LifetimeConfigurationTests.cs`

**Step 1: Update tests to verify factory-managed lifetimes**

The existing `LifetimeConfigurationTests` verify DI container registration lifetimes. We need to update them to verify actual runtime behavior via factory instead.

Update tests to use the factory directly rather than checking `ServiceDescriptor.Lifetime`.

**Step 2: Run tests**

Run: `dotnet test tests/Paramore.Brighter.Extensions.Tests/ --filter "LifetimeConfiguration"`
Expected: All tests pass

**Step 3: Commit**

```bash
git add tests/Paramore.Brighter.Extensions.Tests/LifetimeConfigurationTests.cs
git commit -m "test: update LifetimeConfigurationTests for factory-managed lifetimes

Tests now verify actual runtime behavior via ServiceProviderHandlerFactory
rather than checking DI container registration lifetimes."
```

---

## Task 7: Final Verification

**Step 1: Build entire solution**

Run: `dotnet build`
Expected: Build succeeded

**Step 2: Run all extension tests**

Run: `dotnet test tests/Paramore.Brighter.Extensions.Tests/`
Expected: All tests pass

**Step 3: Run core tests**

Run: `dotnet test tests/Paramore.Brighter.Core.Tests/`
Expected: All tests pass

**Step 4: Run InMemory tests**

Run: `dotnet test tests/Paramore.Brighter.InMemory.Tests/`
Expected: All tests pass

**Step 5: Commit summary**

```bash
git log --oneline HEAD~7..HEAD
```

---

## Summary of Changes

| File | Change |
|------|--------|
| `ServiceProviderHandlerFactory.cs` | Add scoped instance caching, read full lifetime from options |
| `ServiceCollectionSubscriberRegistry.cs` | Remove lifetime parameter, always Transient |
| `ServiceCollectionTransformerRegistry.cs` | Remove lifetime parameter, always Transient |
| `ServiceCollectionMessageMapperRegistryBuilder.cs` | Remove lifetime parameter, always Transient |
| `ServiceCollectionExtensions.cs` | Remove tempOptions pattern, simplify BrighterHandlerBuilder |
| `FactoryLifetimeTests.cs` | New - tests for factory-managed lifetimes |
| `ServiceProviderLambdaTests.cs` | Add test for single configure invocation |
| `LifetimeConfigurationTests.cs` | Update for factory-managed lifetimes |

---

## Benefits Achieved

1. **Configure runs exactly once** - No more double invocation
2. **No tempOptions workaround** - Cleaner code
3. **Simpler API** - No lifetime parameters threading through
4. **PostConfigure still works** - Options pattern preserved
5. **Consistent behavior** - Factory manages all lifetime semantics
