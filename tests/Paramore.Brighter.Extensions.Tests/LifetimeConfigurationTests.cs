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

using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

/// <summary>
/// Tests DI registration lifetimes for handlers, mappers, and transformers.
/// Note: As of the factory-managed lifetime refactoring, all components are registered as Transient in DI.
/// Actual handler lifetime behavior is managed by ServiceProviderHandlerFactory at runtime.
/// See FactoryLifetimeTests for runtime handler lifetime behavior tests.
/// </summary>
public class LifetimeConfigurationTests
{
    [Fact]
    public void AddBrighter_WithDefaultLifetimes_RegistersAllAsTransient()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act - Don't configure any lifetimes
        services.AddBrighter().AutoFromAssemblies();

        // Assert - All components are registered as Transient in DI
        // (Actual handler lifetime is managed by ServiceProviderHandlerFactory at runtime)
        var handlerDescriptor = services.FirstOrDefault(x => x.ServiceType == typeof(TestEventHandler));
        Assert.NotNull(handlerDescriptor);
        Assert.Equal(ServiceLifetime.Transient, handlerDescriptor.Lifetime);

        var mapperDescriptor = services.FirstOrDefault(x => x.ServiceType == typeof(TestEventMessageMapper));
        Assert.NotNull(mapperDescriptor);
        Assert.Equal(ServiceLifetime.Transient, mapperDescriptor.Lifetime);

        var transformerDescriptor = services.FirstOrDefault(x => x.ServiceType == typeof(TestTransform));
        Assert.NotNull(transformerDescriptor);
        Assert.Equal(ServiceLifetime.Transient, transformerDescriptor.Lifetime);
    }
}
