using System;
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
        var sub1 = new MockSubscription(typeof(MockChannelFactory), dataType: typeof(string),
            messagePumpType: MessagePumpType.Proactor);
        var factory1 = new MockChannelFactory(sub1);

        var sub2 = new MockSubscription(typeof(MockChannel2Factory), dataType: typeof(string),
            messagePumpType: MessagePumpType.Proactor);
        var factory2 = new MockChannel2Factory(sub2);

        var channelFactory = new CombinedChannelFactory(
            [factory1, factory2]
        );

        var channel = channelFactory.CreateSyncChannel(sub1);
        Assert.NotNull(channel);
        Assert.Equal(factory1.ChannelSync, channel);


        channel = channelFactory.CreateSyncChannel(sub2);
        Assert.NotNull(channel);
        Assert.Equal(factory2.ChannelSync, channel);
    }

    [Fact]
    public void When_constructing_a_async_channel_with_combined_factory()
    {
        var sub1 = new MockSubscription(typeof(MockChannelFactory), dataType: typeof(string),
            messagePumpType: MessagePumpType.Proactor);
        var factory1 = new MockChannelFactory(sub1);

        var sub2 = new MockSubscription(typeof(MockChannel2Factory), dataType: typeof(string),
            messagePumpType: MessagePumpType.Proactor);
        var factory2 = new MockChannel2Factory(sub2);

        var channelFactory = new CombinedChannelFactory(
            [factory1, factory2]
        );

        var channel = channelFactory.CreateAsyncChannel(sub1);
        Assert.NotNull(channel);
        Assert.Equal(factory1.ChannelAsync, channel);


        channel = channelFactory.CreateAsyncChannel(sub2);
        Assert.NotNull(channel);
        Assert.Equal(factory2.ChannelAsync, channel);
    }

    [Fact]
    public async Task When_constructing_a_async_channel_with_combined_factory_async()
    {
        var sub1 = new MockSubscription(typeof(MockChannelFactory), dataType: typeof(string),
            messagePumpType: MessagePumpType.Proactor);
        var factory1 = new MockChannelFactory(sub1);

        var sub2 = new MockSubscription(typeof(MockChannel2Factory), dataType: typeof(string),
            messagePumpType: MessagePumpType.Proactor);
        var factory2 = new MockChannel2Factory(sub2);

        var channelFactory = new CombinedChannelFactory(
            [factory1, factory2]
        );

        var channel = await channelFactory.CreateAsyncChannelAsync(sub1);
        Assert.NotNull(channel);
        Assert.Equal(factory1.ChannelAsync, channel);

        channel = await channelFactory.CreateAsyncChannelAsync(sub2);
        Assert.NotNull(channel);
        Assert.Equal(factory2.ChannelAsync, channel);
    }

    public class MockSubscription : Subscription
    {
        public override Type ChannelFactoryType { get; }

        public MockSubscription(
           Type channelFactoryType, SubscriptionName? name = null, ChannelName? channelName = null,  
           RoutingKey? routingKey = null,  Type? dataType = null, Func<Message, Type>? getRequestType = null,
            int bufferSize = 1, int noOfPerformers = 1, TimeSpan? timeOut = null, int requeueCount = -1, TimeSpan? requeueDelay = null,
            int unacceptableMessageLimit = 0, MessagePumpType messagePumpType = MessagePumpType.Unknown,
            IAmAChannelFactory? channelFactory = null, OnMissingChannel makeChannels = OnMissingChannel.Create,
            TimeSpan? emptyChannelDelay = null, TimeSpan? channelFailureDelay = null) : base(name,
            channelName, routingKey, dataType, getRequestType, bufferSize, noOfPerformers, timeOut, requeueCount, requeueDelay,
            unacceptableMessageLimit, messagePumpType, channelFactory, makeChannels, emptyChannelDelay,
            channelFailureDelay)
        {
            ChannelFactoryType = channelFactoryType;
        }
    }

    public class MockChannelFactory : IAmAChannelFactory
    {
        public IAmAChannelSync ChannelSync { get; }
        public IAmAChannelAsync ChannelAsync { get; }

        public Subscription Subscription { get; }

        public MockChannelFactory(Subscription subscription)
        {
            ChannelSync = A.Fake<IAmAChannelSync>();
            ChannelAsync = A.Fake<IAmAChannelAsync>();
            Subscription = subscription;
        }

        public IAmAChannelSync CreateSyncChannel(Subscription subscription)
        {
            if (Subscription == subscription)
            {
                return ChannelSync;
            }

            throw new Exception();
        }

        public IAmAChannelAsync CreateAsyncChannel(Subscription subscription)
        {
            if (Subscription == subscription)
            {
                return ChannelAsync;
            }

            throw new Exception();
        }

        public Task<IAmAChannelAsync> CreateAsyncChannelAsync(Subscription subscription, CancellationToken ct = default)
        {
            if (Subscription == subscription)
            {
                return Task.FromResult(ChannelAsync);
            }

            throw new Exception();
        }
    }

    public class MockChannel2Factory : IAmAChannelFactory
    {
        public IAmAChannelSync ChannelSync { get; }
        public IAmAChannelAsync ChannelAsync { get; }

        public Subscription Subscription { get; }

        public MockChannel2Factory(Subscription subscription)
        {
            ChannelSync = A.Fake<IAmAChannelSync>();
            ChannelAsync = A.Fake<IAmAChannelAsync>();
            Subscription = subscription;
        }

        public IAmAChannelSync CreateSyncChannel(Subscription subscription)
        {
            if (Subscription == subscription)
            {
                return ChannelSync;
            }

            throw new Exception();
        }

        public IAmAChannelAsync CreateAsyncChannel(Subscription subscription)
        {
            if (Subscription == subscription)
            {
                return ChannelAsync;
            }

            throw new Exception();
        }

        public Task<IAmAChannelAsync> CreateAsyncChannelAsync(Subscription subscription, CancellationToken ct = default)
        {
            if (Subscription == subscription)
            {
                return Task.FromResult(ChannelAsync);
            }

            throw new Exception();
        }
    }
}
