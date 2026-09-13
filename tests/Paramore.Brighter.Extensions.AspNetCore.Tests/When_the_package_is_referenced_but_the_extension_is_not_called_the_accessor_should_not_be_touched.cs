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

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests;

// AC-14 half 2 (FR-11(a), FR-15) - a host that references this package but never calls
// AddBrighterRequestScope must behave exactly as if the package were absent: no web host, a spy
// IHttpContextAccessor registered directly (not via AddHttpContextAccessor, which only
// AddBrighterRequestScope itself calls), and a Send, a Publish and a Post all run to completion. The
// spy's own zero-accesses count is the assertion - nothing in the package may consult it unless the
// extension that wires it into Brighter's ambient-scope machinery is actually called.
public class PackageReferencedButExtensionNotCalledTests
{
    [Fact]
    public async Task When_the_package_is_referenced_but_the_extension_is_not_called_the_accessor_should_not_be_touched()
    {
        // Arrange - no web host, no AddBrighterRequestScope call anywhere in this collection
        var accessor = new RecordingHttpContextAccessor(context: null);

        var routingKey = new RoutingKey("package-not-called");
        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
        {
            { routingKey, new InMemoryMessageProducer(new InternalBus(), new Publication { Topic = routingKey, RequestType = typeof(PackageNotCalledPostCommand) }) }
        });

        var services = new ServiceCollection();
        services.AddSingleton<IHttpContextAccessor>(accessor);
        services.AddBrighter(options =>
        {
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Scoped;
            options.TransformerLifetime = ServiceLifetime.Scoped;
        })
        .AddProducers(cfg => cfg.ProducerRegistry = producerRegistry);

        var provider = services.BuildServiceProvider();
        var commandProcessor = provider.GetRequiredService<IAmACommandProcessor>();

        // Act
        commandProcessor.Send(new PackageNotCalledSendCommand());
        await commandProcessor.PublishAsync(new PackageNotCalledPublishedEvent());
        commandProcessor.Post(new PackageNotCalledPostCommand());

        // Assert - the accessor was never read: nothing in the package runs unless
        // AddBrighterRequestScope is called
        Assert.Equal(0, accessor.HttpContextReadCount);
    }
}
