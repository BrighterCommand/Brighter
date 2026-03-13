using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Proactor;

public class AsyncChannelDisposalTests
{
    [Fact]
    public async Task When_ChannelAsync_Is_Disposed_Async_Then_Consumer_Is_Disposed()
    {
        // Arrange
        var consumer = new SpyMessageConsumer();
        var channel = new ChannelAsync(
            new ChannelName("test-channel"),
            new RoutingKey("test.topic"),
            consumer
        );

        // Act
        await ((IAsyncDisposable)channel).DisposeAsync();

        // Assert — consumer's DisposeAsync was actually called
        Assert.True(consumer.DisposeAsyncCalled, "ChannelAsync.DisposeAsync should call DisposeAsync on the consumer");

        // Assert — idempotent: second dispose does not throw
        var exception = await Record.ExceptionAsync(async () =>
            await ((IAsyncDisposable)channel).DisposeAsync()
        );
        Assert.Null(exception);
    }
}

/// <summary>
/// Minimal spy consumer that tracks whether DisposeAsync was called.
/// </summary>
internal sealed class SpyMessageConsumer : IAmAMessageConsumerAsync
{
    public bool DisposeAsyncCalled { get; private set; }

    public ValueTask DisposeAsync()
    {
        DisposeAsyncCalled = true;
        return ValueTask.CompletedTask;
    }

    public Task<Message[]> ReceiveAsync(TimeSpan? timeOut = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Array.Empty<Message>());

    public Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<bool> RejectAsync(Message message, MessageRejectionReason? reason = null, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task NackAsync(Message message, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task PurgeAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<bool> RequeueAsync(Message message, TimeSpan? delay = null, CancellationToken cancellationToken = default)
        => Task.FromResult(true);
}
