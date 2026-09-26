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

using System.Diagnostics;
using System.Text.Json;
using Paramore.Brighter.Azure.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessageScheduler.Azure;
using Paramore.Brighter.Scheduler;

namespace Paramore.Brighter.Azure.Tests.Scheduler;

public class AzureScheduledRequestContextTests
{
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task When_scheduling_a_request_should_preserve_its_context_data(bool isAsync, bool useDateTime)
    {
        //Arrange
        var sender = new FakeServiceBusSender();
        var scheduler = new AzureServiceBusScheduler(sender, new RoutingKey("scheduler-topic"), TimeProvider.System);
        var request = new SuperAwesomeCommand("scheduled context");
        using var parent = new Activity("scheduled request").Start();
        var context = new RequestContext { Span = parent };
        var headers = new Dictionary<string, object> { ["x-attempt"] = 3 };
        context.Bag[RequestContextBagNames.Headers] = headers;
        context.Bag[RequestContextBagNames.PartitionKey] = new PartitionKey("partition-1");
        var delay = TimeSpan.FromMinutes(1);

        //Act
        if (isAsync)
        {
            if (useDateTime)
                await ((IAmARequestSchedulerAsyncWithContext)scheduler).ScheduleAsync(request, RequestSchedulerType.Post, DateTimeOffset.UtcNow.Add(delay), context, CancellationToken.None);
            else
                await ((IAmARequestSchedulerAsyncWithContext)scheduler).ScheduleAsync(request, RequestSchedulerType.Post, delay, context, CancellationToken.None);
        }
        else
        {
            if (useDateTime)
                ((IAmARequestSchedulerSyncWithContext)scheduler).Schedule(request, RequestSchedulerType.Post, DateTimeOffset.UtcNow.Add(delay), context);
            else
                ((IAmARequestSchedulerSyncWithContext)scheduler).Schedule(request, RequestSchedulerType.Post, delay, context);
        }
        headers["x-attempt"] = 4;
        var scheduled = sender.ScheduledMessages.Single();
        var envelope = JsonSerializer.Deserialize<FireAzureScheduler>(scheduled.Body.ToString(), JsonSerialisationOptions.Options)!;
        var snapshot = JsonSerializer.Deserialize<ScheduledRequestContext>(envelope.RequestContextData!, JsonSerialisationOptions.Options)!;

        //Assert
        Assert.That(envelope.SchedulerType, Is.EqualTo(RequestSchedulerType.Post));
        Assert.That(snapshot.TraceParent, Is.EqualTo(parent.Id));
        Assert.That(snapshot.PartitionKey!.Value, Is.EqualTo("partition-1"));
        Assert.That(snapshot.Headers!["x-attempt"], Is.EqualTo(3));
    }
}
