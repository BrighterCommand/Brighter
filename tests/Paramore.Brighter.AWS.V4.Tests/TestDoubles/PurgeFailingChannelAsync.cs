using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS.Model;

namespace Paramore.Brighter.AWS.V4.Tests.TestDoubles;

/// <summary>
/// A channel that fails to purge, standing in for the SQS throttle that makes teardown throw:
/// PurgeQueue is allowed once per queue every sixty seconds, and a busy test run can exceed that.
/// Every other call is passed to the real channel.
/// </summary>
public class PurgeFailingChannelAsync(IAmAChannelAsync channel) : IAmAChannelAsync
{
    public ChannelName Name => channel.Name;

    public RoutingKey RoutingKey => channel.RoutingKey;

    public Task PurgeAsync(CancellationToken cancellationToken = default)
        => throw new PurgeQueueInProgressException(
            $"Only one PurgeQueue operation on {channel.Name.Value} is allowed every 60 seconds.");

    public Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default)
        => channel.AcknowledgeAsync(message, cancellationToken);

    public Task<Message> ReceiveAsync(TimeSpan? timeout, CancellationToken cancellationToken = default)
        => channel.ReceiveAsync(timeout, cancellationToken);

    public Task<bool> RejectAsync(Message message, MessageRejectionReason? reason = null, CancellationToken cancellationToken = default)
        => channel.RejectAsync(message, reason, cancellationToken);

    public Task NackAsync(Message message, CancellationToken cancellationToken = default)
        => channel.NackAsync(message, cancellationToken);

    public Task<bool> RequeueAsync(Message message, TimeSpan? timeOut = null, CancellationToken cancellationToken = default)
        => channel.RequeueAsync(message, timeOut, cancellationToken);

    public void Enqueue(params Message[] message) => channel.Enqueue(message);

    public void Stop(RoutingKey topic) => channel.Stop(topic);

    public void Dispose() => channel.Dispose();

    public ValueTask DisposeAsync() => channel.DisposeAsync();
}
