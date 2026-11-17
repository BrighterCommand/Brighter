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

public class BlockedOutstandingCheckTests : IDisposable
{
    private const int MaxOutStandingMessages = 10;
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly RoutingKey s_orders = new("orders");
    private readonly TimerCountingTimeProvider _clock = new(s_start);
    private readonly StaleMessageTopicCountingOutbox _outbox;
    private readonly ProducerRegistry _producerRegistry;
    private readonly StaleMessageMetricsFixture _metrics;

    public BlockedOutstandingCheckTests()
    {
        _outbox = new StaleMessageTopicCountingOutbox(_clock);
        _outbox.BlockSyncCountUntilReleased(MaxOutStandingMessages + 1);
        _producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
        {
            { s_orders, new InMemoryMessageProducer(new InternalBus(), new Publication { Topic = s_orders, RequestType = typeof(MyCommand) }) }
        });
        _metrics = new StaleMessageMetricsFixture(services =>
        {
            services.AddSingleton<TimeProvider>(_clock);
            var builder = new ServiceCollectionBrighterBuilder(
                services,
                new ServiceCollectionSubscriberRegistry(services),
                new ServiceCollectionMessageMapperRegistryBuilder(services));
            builder.AddProducers(configuration =>
            {
                configuration.ProducerRegistry = _producerRegistry;
                configuration.Outbox = _outbox;
                configuration.MaxOutStandingMessages = MaxOutStandingMessages;
            });
            builder.UseOutboxStaleMessageMonitor(TimeSpan.FromMinutes(5), options =>
                options.MeasurementInterval = TimeSpan.FromMinutes(1));
        });
    }

    [Fact]
    public async Task When_the_outstanding_check_is_blocked_should_measure_on_schedule()
    {
        //Arrange
        var monitor = _metrics.Services.GetServices<IHostedService>().OfType<TimedOutboxStaleMessageMonitor>().Single();
        await monitor.StartAsync(default);
        Assert.True(SpinWait.SpinUntil(() => _clock.TimersCreated >= 1, TimeSpan.FromSeconds(5)));
        var commandProcessor = CommandProcessorFor(_outbox, _producerRegistry);

        //Act
        commandProcessor.Post(new MyCommand());
        var checkBlocked = SpinWait.SpinUntil(
            () => _outbox.CountCalls.Any(call => call.Synchronous && call.MaxCount == MaxOutStandingMessages + 1),
            TimeSpan.FromSeconds(5));
        _clock.Advance(TimeSpan.FromMinutes(1));
        var measured = SpinWait.SpinUntil(
            () => _outbox.CountCalls.Any(call => !call.Synchronous) && _outbox.TopicCountCalls.Count == 1,
            TimeSpan.FromSeconds(5));

        //Assert
        Assert.True(checkBlocked);
        Assert.True(measured);
        var overall = Assert.Single(_outbox.CountCalls, call => !call.Synchronous);
        Assert.Equal(s_start.AddMinutes(1), overall.CalledAt);
        Assert.Equal(s_start.AddMinutes(1), Assert.Single(_outbox.TopicCountCalls).CalledAt);
    }

    private static Brighter.CommandProcessor CommandProcessorFor(StaleMessageTopicCountingOutbox outbox, ProducerRegistry producerRegistry)
    {
        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyCommandMessageMapper()),
            null);
        messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();
        var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>().AddBrighterDefault();

        var mediator = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry: producerRegistry,
            resiliencePipelineRegistry: resiliencePipelineRegistry,
            mapperRegistry: messageMapperRegistry,
            messageTransformerFactory: new EmptyMessageTransformerFactory(),
            messageTransformerFactoryAsync: new EmptyMessageTransformerFactoryAsync(),
            tracer: new BrighterTracer(),
            publicationFinder: new FindPublicationByPublicationTopicOrRequestType(),
            outbox: outbox,
            maxOutStandingMessages: MaxOutStandingMessages,
            maxOutStandingCheckInterval: TimeSpan.Zero);

        return new Brighter.CommandProcessor(
            new InMemoryRequestContextFactory(),
            new DefaultPolicy(),
            resiliencePipelineRegistry,
            mediator,
            new InMemorySchedulerFactory());
    }

    public void Dispose()
    {
        _outbox.ReleaseSyncCount();
        _metrics.Dispose();
    }
}
