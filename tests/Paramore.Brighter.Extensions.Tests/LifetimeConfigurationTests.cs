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

namespace Paramore.Brighter.Extensions.Tests;

/// <summary>
/// Tests DI container registration lifetimes for handlers, mappers, and transformers.
/// Note: As of the factory-managed lifetime refactoring, all components are registered as Transient in the DI container.
/// This Transient registration is only for DI resolution; actual runtime handler lifetime behavior (Singleton/Scoped/Transient)
/// is determined by ServiceProviderHandlerFactory based on IBrighterOptions.HandlerLifetime.
/// See FactoryLifetimeTests for runtime handler lifetime behavior tests.
/// </summary>
public class LifetimeConfigurationTests
{
    [Test]
    public async Task AddBrighter_WithDefaultLifetimes_RegistersAllAsTransient()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act - Don't configure any lifetimes
        services.AddBrighter().AutoFromAssemblies();

        // Assert - All components are registered as Transient in DI
        // (Actual handler lifetime is managed by ServiceProviderHandlerFactory at runtime)
        var handlerDescriptor = services.FirstOrDefault(x => x.ServiceType == typeof(TestEventHandler));
        await Assert.That(handlerDescriptor).IsNotNull();
        await Assert.That(handlerDescriptor.Lifetime).IsEqualTo(ServiceLifetime.Transient);

        var mapperDescriptor = services.FirstOrDefault(x => x.ServiceType == typeof(TestEventMessageMapper));
        await Assert.That(mapperDescriptor).IsNotNull();
        await Assert.That(mapperDescriptor.Lifetime).IsEqualTo(ServiceLifetime.Transient);

        var transformerDescriptor = services.FirstOrDefault(x => x.ServiceType == typeof(TestTransform));
        await Assert.That(transformerDescriptor).IsNotNull();
        await Assert.That(transformerDescriptor.Lifetime).IsEqualTo(ServiceLifetime.Transient);
    }
}
