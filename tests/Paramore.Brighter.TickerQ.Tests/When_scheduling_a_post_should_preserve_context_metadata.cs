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
using System.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.MessageMappers;
using Paramore.Brighter.MessageScheduler.TickerQ;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Scheduler.Events;
using Paramore.Brighter.Scheduler.Handlers;
using Paramore.Brighter.TickerQ.Tests.TestDoubles;
using Polly.Registry;
using TickerQ.DependencyInjection;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Interfaces;
using TickerQ.Utilities.Interfaces.Managers;

namespace Paramore.Brighter.TickerQ.Tests;

[Collection("Scheduler")]
public class TickerQScheduledPostContextTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task When_scheduling_a_post_should_preserve_context_metadata(bool isAsync, bool useDateTime)
    {
        //Arrange
        IAmACommandProcessor? processor = null;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTickerQ();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IAmACommandProcessor>(_ => processor!);
        await using var provider = services.BuildServiceProvider();
        TickerQModuleInitializer.EnsureOneTimeSetupTickerQ();
        var factory = new TickerQSchedulerFactory(provider.GetRequiredService<ITimeTickerManager<TimeTickerEntity>>(),
            provider.GetRequiredService<ITickerPersistenceProvider<TimeTickerEntity, CronTickerEntity>>(), TimeProvider.System);
        var bus = new InternalBus();
        var topic = new RoutingKey($"context-{Guid.NewGuid():N}");
        var publications = new[] { new Publication { Topic = topic, RequestType = typeof(MyEvent) } };
        var producerRegistry = new InMemoryProducerRegistryFactory(bus, publications, InstrumentationOptions.None).Create();
        using var mappers = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new CloudEventJsonMessageMapper<MyEvent>()),
            new SimpleMessageMapperFactoryAsync(_ => new CloudEventJsonMessageMapper<MyEvent>()));
        mappers.Register<MyEvent, CloudEventJsonMessageMapper<MyEvent>>();
        mappers.RegisterAsync<MyEvent, CloudEventJsonMessageMapper<MyEvent>>();
        using var mediator = new OutboxProducerMediator<Message, CommittableTransaction>(producerRegistry,
            new ResiliencePipelineRegistry<string>().AddBrighterDefault(), mappers,
            new EmptyMessageTransformerFactory(), new EmptyMessageTransformerFactoryAsync(), null,
            new FindPublicationByPublicationTopicOrRequestType());
        var subscribers = new SubscriberRegistry();
        subscribers.RegisterAsync<FireSchedulerRequest, FireSchedulerRequestHandler>();
        processor = new CommandProcessor(subscribers,
            new SimpleHandlerFactoryAsync(_ => new FireSchedulerRequestHandler(processor!)),
            new InMemoryRequestContextFactory(), new PolicyRegistry(), new ResiliencePipelineRegistry<string>(),
            mediator, factory);
        var context = new RequestContext();
        var headers = new Dictionary<string, object> { ["x-attempt"] = 3 };
        var properties = new Dictionary<string, object> { ["tenant"] = "tenant-1" };
        context.Bag[RequestContextBagNames.Headers] = headers;
        context.Bag[RequestContextBagNames.CloudEventsAdditionalProperties] = properties;
        context.Bag[RequestContextBagNames.PartitionKey] = new PartitionKey("partition-1");
        var request = new MyEvent();
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
        var host = provider.GetRequiredService<ITickerQHostScheduler>();
        try
        {
            await host.StartAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            while (!bus.Stream(topic).Any())
                await Task.Delay(TimeSpan.FromMilliseconds(20), timeout.Token);

            //Assert
            var message = Assert.Single(bus.Stream(topic));
            Assert.Equal(request.Id, message.Id);
            Assert.Equal(3, message.Header.Bag["x-attempt"]);
            Assert.Equal("partition-1", message.Header.PartitionKey.Value);
            using var json = JsonDocument.Parse(message.Body.Value);
            Assert.Equal("tenant-1", json.RootElement.GetProperty("tenant").GetString());
        }
        finally
        {
            await host.StopAsync();
        }
    }
}
