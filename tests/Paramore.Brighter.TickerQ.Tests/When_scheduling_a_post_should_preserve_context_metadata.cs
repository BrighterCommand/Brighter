#region Licence

/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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

#nullable enable

using System.Text.Json;
using Paramore.Brighter.TickerQ.Tests.TestDoubles;

namespace Paramore.Brighter.TickerQ.Tests;

[ClassDataSource<TickerQTestHost>(Shared = SharedType.PerAssembly)]
public class TickerQScheduledPostContextTests(TickerQTestHost host)
{
    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task When_scheduling_a_post_should_preserve_context_metadata(bool isAsync, bool useDateTime)
    {
        //Arrange
        var processor = host.Processor;
        var context = new RequestContext();
        var headers = new Dictionary<string, object> { ["x-attempt"] = 3 };
        var properties = new Dictionary<string, object> { ["tenant"] = "tenant-1" };
        context.Bag[RequestContextBagNames.Headers] = headers;
        context.Bag[RequestContextBagNames.CloudEventsAdditionalProperties] = properties;
        context.Bag[RequestContextBagNames.PartitionKey] = new PartitionKey("partition-1");
        var request = new ScheduledPostContextEvent();
        var delay = TimeSpan.FromSeconds(1);

        //Act
        if (isAsync)
        {
            if (useDateTime) await processor.PostAsync(DateTimeOffset.UtcNow.Add(delay), request, context);
            else await processor.PostAsync(delay, request, context);
        }
        else
        {
            if (useDateTime) processor.Post(DateTimeOffset.UtcNow.Add(delay), request, context);
            else processor.Post(delay, request, context);
        }
        headers["x-attempt"] = 4;
        properties["tenant"] = "changed";
        await host.EventuallyOnBus<ScheduledPostContextEvent>(request.Id);

        //Assert
        var message = await Assert.That(host.InternalBus.Stream(host.RoutingKeyFor<ScheduledPostContextEvent>())
            .Where(message => message.Id == request.Id)).HasSingleItem();
        await Assert.That(message.Id).IsEqualTo(request.Id);
        await Assert.That(message.Header.Bag["x-attempt"]).IsEqualTo(3);
        await Assert.That(message.Header.PartitionKey.Value).IsEqualTo("partition-1");
        using var json = JsonDocument.Parse(message.Body.Value);
        await Assert.That(json.RootElement.GetProperty("tenant").GetString()).IsEqualTo("tenant-1");
    }
}
