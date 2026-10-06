using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.Archiving.TestDoubles;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Post;

// The limit on outstanding messages is meant to hold for any outbox, whether it supports sync and async calls or only
// async calls. The check that counts outstanding messages used to look only at the sync side of the outbox.
public class OutstandingLimitAnyOutboxTests
{
    private readonly RoutingKey _routingKey = new("MyCommand");

    [Fact]
    public async Task When_the_outstanding_limit_is_exceeded_with_an_async_only_outbox_should_throw()
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        var outbox = new AsyncOnlyOutboxWrapper(new InMemoryOutbox(timeProvider));

        //Act
        var reachedLimit = await PostUntilLimitReached(outbox, timeProvider);

        //Assert
        Assert.True(reachedLimit, "OutboxLimitReachedException was never thrown");
    }

    [Fact]
    public async Task When_the_outstanding_limit_is_exceeded_with_a_sync_and_async_outbox_should_throw()
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        var outbox = new InMemoryOutbox(timeProvider);

        //Act
        var reachedLimit = await PostUntilLimitReached(outbox, timeProvider);

        //Assert
        Assert.True(reachedLimit);
    }

    private async Task<bool> PostUntilLimitReached(IAmAnOutbox outbox, FakeTimeProvider timeProvider)
    {
        var producer = new FakeErroringMessageProducerAsync
        {
            Publication = { Topic = _routingKey, RequestType = typeof(MyCommand) }
        };

        var messageMapperRegistry = new MessageMapperRegistry(
            null,
            new SimpleMessageMapperFactoryAsync(_ => new MyCommandMessageMapperAsync()));
        messageMapperRegistry.RegisterAsync<MyCommand, MyCommandMessageMapperAsync>();

        var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>().AddBrighterDefault();
        var producerRegistry = new ProducerRegistry(
            new Dictionary<RoutingKey, IAmAMessageProducer> { { _routingKey, producer } });

        IAmAnOutboxProducerMediator mediator = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry,
            resiliencePipelineRegistry,
            messageMapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            null,
            new FindPublicationByPublicationTopicOrRequestType(),
            outbox,
            maxOutStandingMessages: 3,
            maxOutStandingCheckInterval: TimeSpan.FromMilliseconds(250),
            timeProvider: timeProvider);

        var commandProcessor = new CommandProcessor(
            new InMemoryRequestContextFactory(),
            new DefaultPolicy(),
            resiliencePipelineRegistry,
            mediator,
            new InMemorySchedulerFactory());

        try
        {
            for (var i = 0; i < 10; i++)
            {
                await commandProcessor.PostAsync(new MyCommand { Value = $"Hello World: {i + 1}" });

                timeProvider.Advance(TimeSpan.FromMilliseconds(500));

                // The check runs on a background thread, give it a moment to publish its count
                await Task.Delay(50);
            }
        }
        catch (OutboxLimitReachedException)
        {
            return true;
        }

        return false;
    }
}
