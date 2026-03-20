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

using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Defer.Handlers;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Validation;

public class AssemblyScanningOpenGenericExclusionTests
{
    [Fact]
    public void When_scanning_assemblies_should_not_register_open_generic_type_parameters()
    {
        // Arrange — scan the Brighter core assembly, which contains DeferMessageOnErrorHandler<TRequest>
        var services = new ServiceCollection();
        var subscriberRegistry = new ServiceCollectionSubscriberRegistry(services);
        var mapperRegistry = new ServiceCollectionMessageMapperRegistryBuilder(services);
        var builder = new ServiceCollectionBrighterBuilder(services, subscriberRegistry, mapperRegistry);

        var brighterAssembly = typeof(DeferMessageOnErrorHandler<>).Assembly;
        builder.HandlersFromAssemblies(new[] { brighterAssembly }, null);
        builder.AsyncHandlersFromAssemblies(new[] { brighterAssembly }, null);

        // Act
        var requestTypes = subscriberRegistry.GetRegisteredRequestTypes();

        // Assert — no registered request type should be a generic type parameter (e.g. TRequest)
        var openGenericParameters = requestTypes.Where(t => t.IsGenericParameter).ToList();
        Assert.Empty(openGenericParameters);
    }
}
