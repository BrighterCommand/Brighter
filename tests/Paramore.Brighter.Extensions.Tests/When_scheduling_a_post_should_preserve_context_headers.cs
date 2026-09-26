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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.MessageMappers;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

[Collection("CommandProcessor")]
public class ScheduledPostContextTests
{
    public static TheoryData<bool, bool, string, bool, bool> ContextCases
    {
        get
        {
            var cases = new TheoryData<bool, bool, string, bool, bool>();
            foreach (var isAsync in new[] { false, true })
                foreach (var useDateTime in new[] { false, true })
                    foreach (var partitionKey in new[] { "partition-1", "35ac2856-7b11-479c-a2bd-c470365767ec", "2026-09-26T10:00:00Z" })
                        foreach (var typedPartitionKey in new[] { false, true })
                            cases.Add(isAsync, useDateTime, partitionKey, typedPartitionKey, false);
            foreach (var isAsync in new[] { false, true })
                foreach (var useDateTime in new[] { false, true })
                    cases.Add(isAsync, useDateTime, "partition-1", true, true);
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(ContextCases))]
    public async Task When_scheduling_a_post_should_preserve_context_headers(bool isAsync, bool useDateTime, string partitionKey, bool typedPartitionKey, bool cloudEvents)
    {
        //Arrange
        var bus = new InternalBus();
        var topic = new RoutingKey("scheduled-context.event");
        var timeProvider = new FakeTimeProvider();
        var services = new ServiceCollection();
        services.AddBrighter()
            .UseScheduler(new InMemorySchedulerFactory { TimeProvider = timeProvider })
            .AddProducers(options => options.ProducerRegistry = new InMemoryProducerRegistryFactory(bus,
                [new Publication { Topic = topic, RequestType = typeof(DefaultMapperEvent) }],
                InstrumentationOptions.All).Create())
            .MapperRegistry(registry =>
            {
                if (cloudEvents)
                {
                    registry.Register<DefaultMapperEvent, CloudEventJsonMessageMapper<DefaultMapperEvent>>();
                    registry.RegisterAsync<DefaultMapperEvent, CloudEventJsonMessageMapper<DefaultMapperEvent>>();
                }
            });
        await using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<IAmACommandProcessor>();
        var context = new RequestContext();
        var cloudEventProperties = new Dictionary<string, object> { ["tenant"] = "tenant-1" };
        context.Bag[RequestContextBagNames.CloudEventsAdditionalProperties] = cloudEventProperties;
        var headers = new Dictionary<string, object>
        {
            ["x-attempt"] = 3,
            ["correlation-id"] = "3f2504e0-4f89-11d3-9a0c-0305e82c3301",
            ["date"] = "2026-09-26",
            ["sequence"] = 42L,
            ["score"] = 1.5d,
            ["large-score"] = 1e30d,
            ["binary"] = new byte[] { 1, 2, 3 },
            ["timestamp"] = new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.FromHours(5.5))
        };
        context.Bag[RequestContextBagNames.Headers] = headers;
        context.Bag[RequestContextBagNames.PartitionKey] = typedPartitionKey ? new PartitionKey(partitionKey) : partitionKey;
        var circular = new Dictionary<string, object>();
        circular["self"] = circular;
        context.Bag["runtime-callback"] = (Action)(() => { });
        context.Bag["runtime-state"] = circular;
        var immediate = new DefaultMapperEvent { Text = "immediate" };
        var scheduled = new DefaultMapperEvent { Text = "scheduled" };
        var delay = TimeSpan.FromMilliseconds(200);

        //Act
        await processor.PostAsync(immediate, context);
        if (isAsync)
        {
            if (useDateTime)
                await processor.PostAsync(timeProvider.GetUtcNow().Add(delay), scheduled, context);
            else
                await processor.PostAsync(delay, scheduled, context);
        }
        else
        {
            if (useDateTime)
                processor.Post(timeProvider.GetUtcNow().Add(delay), scheduled, context);
            else
                processor.Post(delay, scheduled, context);
        }

        ((Dictionary<string, object>)context.Bag[RequestContextBagNames.Headers])["x-attempt"] = 4;
        context.Bag[RequestContextBagNames.PartitionKey] = "changed-after-scheduling";
        Assert.Single(bus.Stream(topic));
        cloudEventProperties["tenant"] = "changed";
        timeProvider.Advance(delay);

        //Assert
        var messages = bus.Stream(topic).ToArray();
        Assert.Equal(2, messages.Length);
        var immediateMessage = Assert.Single(messages, message => message.Id == immediate.Id);
        var scheduledMessage = Assert.Single(messages, message => message.Id == scheduled.Id);
        Assert.Equal(3, immediateMessage.Header.Bag["x-attempt"]);
        Assert.True(scheduledMessage.Header.Bag.ContainsKey("x-attempt"));
        Assert.Equal(3, scheduledMessage.Header.Bag["x-attempt"]);
        foreach (var key in headers.Keys.Where(key => key != "x-attempt"))
        {
            var expected = immediateMessage.Header.Bag[key];
            var actual = scheduledMessage.Header.Bag[key];
            Assert.IsType(expected.GetType(), actual);
            Assert.Equal(expected, actual);
        }
        Assert.Equal(partitionKey, immediateMessage.Header.PartitionKey.Value);
        Assert.Equal(partitionKey, scheduledMessage.Header.PartitionKey.Value);
        if (cloudEvents)
        {
            using var immediateBody = JsonDocument.Parse(immediateMessage.Body.Value);
            using var scheduledBody = JsonDocument.Parse(scheduledMessage.Body.Value);
            Assert.Equal("tenant-1", immediateBody.RootElement.GetProperty("tenant").GetString());
            Assert.Equal("tenant-1", scheduledBody.RootElement.GetProperty("tenant").GetString());
        }
    }
}
