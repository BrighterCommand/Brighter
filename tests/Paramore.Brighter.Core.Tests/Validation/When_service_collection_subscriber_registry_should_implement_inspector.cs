#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.Validation.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Validation;

public class ServiceCollectionSubscriberRegistryInspectorTests
{
    private readonly ServiceCollectionSubscriberRegistry _registry;

    public ServiceCollectionSubscriberRegistryInspectorTests()
    {
        // Arrange
        var services = new ServiceCollection();
        _registry = new ServiceCollectionSubscriberRegistry(services);
        _registry.Register<MyDescribableCommand, MyPublicSyncHandler>();
    }

    [Fact]
    public void When_cast_to_inspector_should_return_registered_request_types()
    {
        // Act
        var inspector = (IAmASubscriberRegistryInspector)_registry;
        var requestTypes = inspector.GetRegisteredRequestTypes();

        // Assert
        Assert.Contains(typeof(MyDescribableCommand), requestTypes);
    }

    [Fact]
    public void When_cast_to_inspector_should_return_handler_types_for_request()
    {
        // Act
        var inspector = (IAmASubscriberRegistryInspector)_registry;
        var handlerTypes = inspector.GetHandlerTypes(typeof(MyDescribableCommand));

        // Assert
        Assert.Contains(typeof(MyPublicSyncHandler), handlerTypes);
    }

    [Fact]
    public void When_cast_to_inspector_should_return_empty_for_unregistered_request()
    {
        // Act
        var inspector = (IAmASubscriberRegistryInspector)_registry;
        var handlerTypes = inspector.GetHandlerTypes(typeof(MyBareRequest));

        // Assert
        Assert.Empty(handlerTypes);
    }
}
