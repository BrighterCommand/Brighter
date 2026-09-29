using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using System.Threading.Tasks;
namespace Paramore.Brighter.Core.Tests.MessageDispatch.Reactor;

public class ChannelDoubleDisposalTests
{
    [Test]
    public async Task When_a_channel_is_disposed_twice_should_dispose_its_consumer_once()
    {
        // Arrange
        var consumer = new SpyDisposeCountingConsumer();
        var channel = new Channel(new ChannelName("test-channel"), new RoutingKey("test.topic"), consumer);

        // Act — the Reactor disposes the channel when it quits, then Performer.Dispose disposes it again
        channel.Dispose();
        channel.Dispose();

        // Assert
        await Assert.That(consumer.DisposeCount).IsEqualTo(1);
    }
}
