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
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.Observability.StaleMessages.TestDoubles;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages;

/// <summary>
/// Posts until the outstanding-message check reports more messages than the limit, with the feature and without it.
/// The test cannot see the mediator store the count, which it does after the double counts the return, or clear its
/// in-flight flag, which it does after the count's log entry. A thread held before the store can make the next post
/// read the old count, so <see cref="OutboxLimitReachedException"/> comes on a later post in one run than in the other.
/// A thread held before either step can make the next post skip its check, so the wait for that post times out. Each
/// window can only make the test fail.
/// </summary>
[Collection(StaleMessageLoggingCollection.Name)]
public class OutstandingCheckUnchangedTests
{
    private const int MaxOutStandingMessages = 5;
    private const int MaxPosts = 10;
    private const string MediatorCategory = "Paramore.Brighter.CommandProcessor";
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly RoutingKey s_orders = new("orders");
    private readonly CapturingLoggerProvider _logs;

    public OutstandingCheckUnchangedTests(StaleMessageLoggingFixture fixture)
    {
        _logs = fixture.Logs;
    }

    [Fact]
    public async Task When_the_feature_is_enabled_should_leave_the_outstanding_check_unchanged()
    {
        //Arrange
        _logs.Reset();
        _logs.Capture(MediatorCategory);
        var disabled = await PostUntilTheLimit(withTheFeature: false);

        //Act
        var enabled = await PostUntilTheLimit(withTheFeature: true);

        //Assert
        Assert.Equal(0, disabled.CallsBeforeTheFirstPost);
        Assert.Equal(0, enabled.CallsBeforeTheFirstPost);
        Assert.All(disabled.Checked, Assert.True);
        Assert.All(enabled.Checked, Assert.True);
        Assert.Equal(disabled.Calls.Length, enabled.Calls.Length);
        foreach (var (withoutTheFeature, withTheFeature) in disabled.Calls.Zip(enabled.Calls))
        {
            Assert.Equal(withoutTheFeature.DispatchedSince, withTheFeature.DispatchedSince);
            Assert.Equal(withoutTheFeature.MaxCount, withTheFeature.MaxCount);
            Assert.Equal(withoutTheFeature.Args, withTheFeature.Args);
        }
        Assert.IsType<OutboxLimitReachedException>(disabled.Thrown);
        Assert.IsType<OutboxLimitReachedException>(enabled.Thrown);
        Assert.True(disabled.Posts >= 7);
        Assert.Equal(disabled.Posts, enabled.Posts);
    }

    private async Task<LimitRun> PostUntilTheLimit(bool withTheFeature)
    {
        var clock = new TimerCountingTimeProvider(s_start);
        var outbox = new StaleMessageCountingOutbox(clock);
        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
        {
            { s_orders, new InMemoryMessageProducer(new InternalBus(), new Publication { Topic = s_orders, RequestType = typeof(MyCommand) }) }
        });
        using var metrics = new StaleMessageMetricsFixture(services =>
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
                configuration.MaxOutStandingMessages = MaxOutStandingMessages;
            });
            if (withTheFeature)
                builder.UseOutboxStaleMessageMonitor(TimeSpan.FromMinutes(5), options =>
                    options.MeasurementInterval = TimeSpan.FromMinutes(1));
        });

        var monitor = metrics.Services.GetServices<IHostedService>().OfType<TimedOutboxStaleMessageMonitor>().SingleOrDefault();
        if (monitor is not null)
            await monitor.StartAsync(default);
        var callsBeforeTheFirstPost = outbox.CountCalls.Count(call => call.Synchronous);

        var commandProcessor = CommandProcessorFor(outbox, producerRegistry,
            metrics.Services.GetRequiredService<IAmProducersConfiguration>());
        _logs.ClearEntries();
        var checkedInTime = new List<bool>();
        Exception? thrown = null;
        var posts = 0;
        while (thrown is null && posts < MaxPosts)
        {
            thrown = Record.Exception(() => commandProcessor.Post(new MyCommand()));
            posts++;
            if (thrown is null)
                checkedInTime.Add(SpinWait.SpinUntil(
                    () => outbox.SyncCountReturns >= posts && OutstandingCountEntries() >= posts,
                    TimeSpan.FromSeconds(5)));
        }

        return new LimitRun(
            callsBeforeTheFirstPost,
            checkedInTime,
            outbox.CountCalls.Where(call => call.Synchronous).ToArray(),
            thrown,
            posts);
    }

    private int OutstandingCountEntries() =>
        _logs.Entries.Count(entry =>
            entry.Category == MediatorCategory
            && entry.LogLevel == LogLevel.Debug
            && entry.Message.StartsWith("Current outstanding count", StringComparison.Ordinal));

    private static Brighter.CommandProcessor CommandProcessorFor(StaleMessageCountingOutbox outbox,
        ProducerRegistry producerRegistry, IAmProducersConfiguration configuration)
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
            maxOutStandingMessages: configuration.MaxOutStandingMessages,
            maxOutStandingCheckInterval: configuration.MaxOutStandingCheckInterval,
            outBoxBag: configuration.OutBoxBag);

        return new Brighter.CommandProcessor(
            new InMemoryRequestContextFactory(),
            new DefaultPolicy(),
            resiliencePipelineRegistry,
            mediator,
            new InMemorySchedulerFactory());
    }

    private sealed record LimitRun(
        int CallsBeforeTheFirstPost,
        IReadOnlyList<bool> Checked,
        StaleCountCall[] Calls,
        Exception? Thrown,
        int Posts);
}
