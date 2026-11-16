#region Licence
/* The MIT License (MIT)
Copyright © 2026 Guilherme Appel

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
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.Observability.StaleMessages.TestDoubles;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages;

public class BlockedThreadsPostingTests : IDisposable
{
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly RoutingKey s_orders = new("orders");
    private readonly Id[] _ids = Enumerable.Range(0, 200).Select(_ => Id.Random()).ToArray();
    private readonly List<StaleMessageMetricsFixture> _fixtures = [];
    private StaleMessageTopicCountingOutbox? _blocked;

    [Fact]
    public async Task When_both_counts_block_their_threads_should_not_delay_posting()
    {
        //Arrange
        var disabled = await PostBesideTheCounts(withTheFeature: false);

        //Act
        var enabled = await PostBesideTheCounts(withTheFeature: true);

        //Assert
        Assert.True(enabled.CountsBlocked);
        Assert.Null(disabled.Posting);
        Assert.Null(enabled.Posting);
        Assert.True(disabled.AllMarked);
        Assert.True(enabled.AllMarked);
        Assert.Equal(_ids.Length, enabled.Adds.Length);
        Assert.Equal(disabled.Adds, enabled.Adds);
        Assert.Equivalent(disabled.Marks, enabled.Marks, strict: true);
    }

    private async Task<PostingRun> PostBesideTheCounts(bool withTheFeature)
    {
        var clock = new TimerCountingTimeProvider(s_start);
        var outbox = new StaleMessageTopicCountingOutbox(clock);
        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
        {
            { s_orders, new InMemoryMessageProducer(new InternalBus(), new Publication { Topic = s_orders, RequestType = typeof(MyCommand) }) }
        });
        if (withTheFeature)
        {
            outbox.BlockThreadUntilReleased();
            outbox.BlockTopicThreadUntilReleased();
            _blocked = outbox;
        }

        var metrics = new StaleMessageMetricsFixture(services =>
        {
            services.AddSingleton<TimeProvider>(clock);
            var builder = new ServiceCollectionBrighterBuilder(
                services,
                new ServiceCollectionSubscriberRegistry(services),
                new ServiceCollectionMessageMapperRegistryBuilder(services));
            builder.AddProducers(configuration =>
            {
                configuration.ProducerRegistry = producerRegistry;
                configuration.Outbox = outbox;
            });
            if (withTheFeature)
                builder.UseOutboxStaleMessageMonitor(TimeSpan.FromMinutes(5), options =>
                    options.MeasurementInterval = TimeSpan.FromMinutes(1));
        });
        _fixtures.Add(metrics);

        var countsBlocked = false;
        var monitor = metrics.Services.GetServices<IHostedService>().OfType<TimedOutboxStaleMessageMonitor>().SingleOrDefault();
        if (monitor is not null)
        {
            await monitor.StartAsync(default);
            Assert.True(SpinWait.SpinUntil(() => clock.TimersCreated >= 1, TimeSpan.FromSeconds(5)));
        }

        clock.Advance(TimeSpan.FromMinutes(1));
        if (monitor is not null)
            countsBlocked = SpinWait.SpinUntil(
                () => outbox.CountCalls.Count == 1 && outbox.TopicCountCalls.Count == 1,
                TimeSpan.FromSeconds(5));

        var commandProcessor = CommandProcessorFor(outbox, producerRegistry);
        var posting = await Record.ExceptionAsync(() => Task.Run(async () =>
        {
            foreach (var id in _ids.Take(100))
                commandProcessor.Post(new MyCommand { Id = id });
            foreach (var id in _ids.Skip(100))
                await commandProcessor.PostAsync(new MyCommand { Id = id });
        }).WaitAsync(TimeSpan.FromSeconds(30)));

        var allMarked = SpinWait.SpinUntil(() => outbox.Marks.Count == _ids.Length, TimeSpan.FromSeconds(5));
        return new PostingRun(countsBlocked, posting, allMarked, outbox.Adds.ToArray(), outbox.Marks.ToArray());
    }

    private static Brighter.CommandProcessor CommandProcessorFor(StaleMessageTopicCountingOutbox outbox, ProducerRegistry producerRegistry)
    {
        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyCommandMessageMapper()),
            new SimpleMessageMapperFactoryAsync(_ => new MyCommandMessageMapperAsync()));
        messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();
        messageMapperRegistry.RegisterAsync<MyCommand, MyCommandMessageMapperAsync>();
        var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>().AddBrighterDefault();

        var mediator = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry: producerRegistry,
            resiliencePipelineRegistry: resiliencePipelineRegistry,
            mapperRegistry: messageMapperRegistry,
            messageTransformerFactory: new EmptyMessageTransformerFactory(),
            messageTransformerFactoryAsync: new EmptyMessageTransformerFactoryAsync(),
            tracer: new BrighterTracer(),
            publicationFinder: new FindPublicationByPublicationTopicOrRequestType(),
            outbox: outbox);

        return new Brighter.CommandProcessor(
            new InMemoryRequestContextFactory(),
            new DefaultPolicy(),
            resiliencePipelineRegistry,
            mediator,
            new InMemorySchedulerFactory());
    }

    public void Dispose()
    {
        if (_blocked is not null)
        {
            _blocked.ReleaseThread();
            _blocked.ReleaseTopicThread();
        }

        foreach (var metrics in _fixtures)
            metrics.Dispose();
    }

    private sealed record PostingRun(bool CountsBlocked, Exception? Posting, bool AllMarked, Id[] Adds, Id[] Marks);
}
