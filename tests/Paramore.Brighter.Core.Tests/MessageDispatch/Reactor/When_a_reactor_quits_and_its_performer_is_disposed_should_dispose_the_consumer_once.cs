using System;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.Testing;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Reactor;

public class ReactorQuitConsumerDisposalTests
{
    private readonly RoutingKey _routingKey = new("MyTopic");

    [Fact]
    public async Task When_a_reactor_quits_and_its_performer_is_disposed_should_dispose_the_consumer_once()
    {
        // Arrange
        var consumer = new SpyDisposeCountingConsumer();
        var channel = new Channel(new ChannelName("MyChannel"), _routingKey, consumer);

        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
            null);
        messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

        var messagePump = new ServiceActivator.Reactor(new SpyCommandProcessor(), _ => typeof(MyEvent),
            messageMapperRegistry, new EmptyMessageTransformerFactory(), new InMemoryRequestContextFactory(), channel)
        {
            Channel = channel,
            TimeOut = TimeSpan.FromMilliseconds(5000)
        };

        var performer = new Performer(channel, messagePump);

        // Act — the Reactor disposes the channel on quit; the Dispatcher then disposes the performer
        var performerTask = performer.Run();
        performer.Stop(_routingKey);
        await performerTask.WaitAsync(TimeSpan.FromSeconds(10));
        performer.Dispose();

        // Assert
        Assert.Equal(1, consumer.DisposeCount);
    }
}
