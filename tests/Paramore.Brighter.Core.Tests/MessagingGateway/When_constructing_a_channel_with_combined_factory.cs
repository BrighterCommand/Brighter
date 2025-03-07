using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessagingGateway;

public class CombinedChannelFactoryTest
{
    [Fact]
    public void When_constructing_a_sync_channel_with_combined_factory()
    {
        var factory1 = A.Fake<IAmAChannelFactory>();
        var channel1 = A.Fake<IAmAChannelSync>();
        var sub1 = new Subscription(typeof(string), messagePumpType: MessagePumpType.Proactor);
        A.CallTo(() => factory1.CreateSyncChannel(sub1))
            .Returns(channel1);

        var factory2 = A.Fake<IAmAChannelFactory>();
        var channel2 = A.Fake<IAmAChannelSync>();
        var sub2 = new Subscription(typeof(int), messagePumpType: MessagePumpType.Proactor);
        A.CallTo(() => factory2.CreateSyncChannel(sub2))
            .Returns(channel2);

        A.CallTo(() => factory1.CreateSyncChannel(sub2))
            .Throws(new ConfigurationException());

        var channelFactory = new CombinedChannelFactory(
            [factory1, factory2]
        );

        var channel = channelFactory.CreateSyncChannel(sub1);
        Assert.NotNull(channel);
        Assert.Equal(channel1, channel);


        channel = channelFactory.CreateSyncChannel(sub2);
        Assert.NotNull(channel);
        Assert.Equal(channel2, channel);
    }

    [Fact]
    public void When_constructing_a_async_channel_with_combined_factory()
    {
        var factory1 = A.Fake<IAmAChannelFactory>();
        var channel1 = A.Fake<IAmAChannelAsync>();
        var sub1 = new Subscription(typeof(string), messagePumpType: MessagePumpType.Proactor);
        A.CallTo(() => factory1.CreateAsyncChannel(sub1))
            .Returns(channel1);

        var factory2 = A.Fake<IAmAChannelFactory>();
        var channel2 = A.Fake<IAmAChannelAsync>();
        var sub2 = new Subscription(typeof(int), messagePumpType: MessagePumpType.Proactor);
        A.CallTo(() => factory2.CreateAsyncChannel(sub2))
            .Returns(channel2);

        A.CallTo(() => factory1.CreateAsyncChannel(sub2))
            .Throws(new ConfigurationException());

        var channelFactory = new CombinedChannelFactory(
            [factory1, factory2]
        );

        var channel = channelFactory.CreateAsyncChannel(sub1);
        Assert.NotNull(channel);
        Assert.Equal(channel1, channel);


        channel = channelFactory.CreateAsyncChannel(sub2);
        Assert.NotNull(channel);
        Assert.Equal(channel2, channel);
    }
    
    [Fact]
    public async Task When_constructing_a_async_channel_with_combined_factory_async()
    {
        var factory1 = A.Fake<IAmAChannelFactory>();
        var channel1 = A.Fake<IAmAChannelAsync>();
        var sub1 = new Subscription(typeof(string), messagePumpType: MessagePumpType.Proactor);
        A.CallTo(() => factory1.CreateAsyncChannelAsync(sub1, CancellationToken.None))
            .Returns(channel1);

        var factory2 = A.Fake<IAmAChannelFactory>();
        var channel2 = A.Fake<IAmAChannelAsync>();
        var sub2 = new Subscription(typeof(int), messagePumpType: MessagePumpType.Proactor);
        A.CallTo(() => factory2.CreateAsyncChannelAsync(sub2, CancellationToken.None))
            .Returns(channel2);

        A.CallTo(() => factory1.CreateAsyncChannelAsync(sub2, CancellationToken.None))
            .Throws(new ConfigurationException());

        var channelFactory = new CombinedChannelFactory(
            [factory1, factory2]
        );

        var channel = await channelFactory.CreateAsyncChannelAsync(sub1);
        Assert.NotNull(channel);
        Assert.Equal(channel1, channel);


        channel = await channelFactory.CreateAsyncChannelAsync(sub2);
        Assert.NotNull(channel);
        Assert.Equal(channel2, channel);
    }
}
