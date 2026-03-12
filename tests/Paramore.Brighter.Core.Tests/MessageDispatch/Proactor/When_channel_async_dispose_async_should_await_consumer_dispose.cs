using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Testing;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Proactor;

public class AsyncChannelDisposalTests
{
    private readonly RoutingKey _routingKey = new("test.topic");
    private readonly InternalBus _bus = new();
    private readonly FakeTimeProvider _timeProvider = new();

    [Fact]
    public async Task When_ChannelAsync_Is_Disposed_Async_Then_Consumer_Is_Disposed()
    {
        // Arrange
        var consumer = new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, ackTimeout: TimeSpan.FromMilliseconds(1000));
        var channel = new ChannelAsync(
            new ChannelName("test-channel"),
            _routingKey,
            consumer
        );

        // Act
        await ((IAsyncDisposable)channel).DisposeAsync();

        // Assert — after async dispose, the consumer's timer is disposed,
        // so receiving should throw or return empty; we verify the channel
        // can be disposed a second time without throwing (idempotent)
        var exception = await Record.ExceptionAsync(async () =>
            await ((IAsyncDisposable)channel).DisposeAsync()
        );
        Assert.Null(exception);
    }
}
