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
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Scheduler.Events;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class ScheduledRequestContextTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task When_scheduling_a_request_should_preserve_supported_context_metadata(bool publish, bool isAsync, bool useDateTime)
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        await using var provider = BuildProvider(timeProvider);
        var processor = provider.GetRequiredService<IAmACommandProcessor>();
        var headers = new Dictionary<string, object> { ["x-attempt"] = 3 };
        var properties = new Dictionary<string, object> { ["tenant"] = "tenant-1" };
        var context = new RequestContext();
        context.Bag[RequestContextBagNames.Headers] = headers;
        context.Bag[RequestContextBagNames.CloudEventsAdditionalProperties] = properties;
        context.Bag[RequestContextBagNames.PartitionKey] = new PartitionKey("partition-1");
        var jobId = Id.Random();
        var workflowId = Id.Random();
        var causationId = Id.Random();
        context.Bag[RequestContextBagNames.CausationId] = causationId;
        context.Bag[RequestContextBagNames.JobId] = jobId;
        context.Bag[RequestContextBagNames.WorkflowId] = workflowId;
        context.Bag["custom-value"] = 42;
        var delay = TimeSpan.FromSeconds(1);
        var at = timeProvider.GetUtcNow().Add(delay);

        //Act
        if (isAsync)
            await ScheduleAsync(new ScheduledContextEventAsync());
        else
            await ScheduleAsync(new ScheduledContextEvent());

        headers["x-attempt"] = 4;
        properties["tenant"] = "changed";
        context.Bag["custom-value"] = 99;
        Assert.Empty(Received(provider, isAsync));
        timeProvider.Advance(delay);

        //Assert
        var restored = Assert.Single(Received(provider, isAsync));
        Assert.NotSame(context, restored);
        Assert.Equal(3, restored.GetHeaders()!["x-attempt"]);
        Assert.Equal("tenant-1", restored.GetCloudEventAdditionalProperties()!["tenant"]);
        Assert.Equal("partition-1", restored.GetPartitionKey().Value);
        Assert.Equal(causationId, restored.Bag[RequestContextBagNames.CausationId]);
        Assert.Equal(jobId, restored.GetJobId());
        Assert.Equal(workflowId, restored.GetWorkflowId());
        Assert.False(restored.Bag.ContainsKey("custom-value"));

        async Task ScheduleAsync<TRequest>(TRequest request) where TRequest : class, IRequest
        {
            _ = (publish, isAsync, useDateTime) switch
            {
                (false, false, false) => processor.Send(delay, request, context),
                (false, false, true) => processor.Send(at, request, context),
                (false, true, false) => await processor.SendAsync(delay, request, context),
                (false, true, true) => await processor.SendAsync(at, request, context),
                (true, false, false) => processor.Publish(delay, request, context),
                (true, false, true) => processor.Publish(at, request, context),
                (true, true, false) => await processor.PublishAsync(delay, request, context),
                (true, true, true) => await processor.PublishAsync(at, request, context)
            };
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_firing_a_legacy_scheduled_request_should_handle_it_without_context_data(bool isAsync)
    {
        //Arrange
        await using var provider = BuildProvider(new FakeTimeProvider());
        var processor = provider.GetRequiredService<IAmACommandProcessor>();
        var command = new FireSchedulerRequest
        {
            Async = isAsync,
            SchedulerType = RequestSchedulerType.Send,
            RequestType = (isAsync ? typeof(ScheduledContextEventAsync) : typeof(ScheduledContextEvent)).FullName!,
            RequestData = JsonSerializer.Serialize(new ScheduledContextEvent(), JsonSerialisationOptions.Options)
        };

        //Act
        await processor.SendAsync(command);

        //Assert
        var context = Assert.Single(Received(provider, isAsync));
        Assert.Null(context.GetHeaders());
        Assert.Equal(PartitionKey.Empty, context.GetPartitionKey());
    }

    private static ServiceProvider BuildProvider(FakeTimeProvider timeProvider)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ScheduledContextEventHandler>();
        services.AddSingleton<ScheduledContextEventHandlerAsync>();
        services.AddBrighter()
            .UseScheduler(new InMemorySchedulerFactory { TimeProvider = timeProvider });
        return services.BuildServiceProvider();
    }

    private static List<IRequestContext> Received(ServiceProvider provider, bool isAsync)
        => isAsync ? provider.GetRequiredService<ScheduledContextEventHandlerAsync>().Received
            : provider.GetRequiredService<ScheduledContextEventHandler>().Received;
}
