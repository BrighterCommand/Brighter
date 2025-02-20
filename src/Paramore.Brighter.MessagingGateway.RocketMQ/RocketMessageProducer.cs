using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Org.Apache.Rocketmq;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.RocketMQ;

/// <summary>
/// The RocketMQ message producer
/// </summary>
public class RocketMessageProducer : IAmAMessageProducerSync, IAmAMessageProducerAsync
{
    private readonly Producer _producer;
    private readonly RocketMessagingGatewayConnection _connection;

    private readonly RocketPublication _publication;
    
    /// <inheritdoc />
    public Publication Publication => _publication;

    /// <inheritdoc />
    public Activity? Span { get; set; }
    
    public RocketMessageProducer(RocketMessagingGatewayConnection connection, Producer producer, RocketPublication publication)
    {
        _connection = connection;
        _producer = producer;
        _publication = publication;
    }

    /// <inheritdoc />
    public void Send(Message message)
        => SendWithDelay(message, TimeSpan.Zero);

    /// <inheritdoc />
    public void SendWithDelay(Message message, TimeSpan? delay)
        => BrighterAsyncContext.Run(async () => await SendWithDelayAsync(message, delay));
    

    /// <inheritdoc />
    public Task SendAsync(Message message, CancellationToken cancellationToken = default)
        => SendWithDelayAsync(message, TimeSpan.Zero, cancellationToken);

    /// <inheritdoc />
    public async Task SendWithDelayAsync(Message message, TimeSpan? delay, CancellationToken cancellationToken = default)
    {
        var builder = new Org.Apache.Rocketmq.Message.Builder()
            .SetBody(message.Body.Bytes)
            .SetTopic(message.Header.Topic.Value);

        builder.AddProperty(HeaderNames.MessageId, message.Id)
            .AddProperty(HeaderNames.Topic, message.Header.Topic.Value)
            .AddProperty(HeaderNames.HandledCount, message.Header.HandledCount.ToString())
            .AddProperty(HeaderNames.MessageType, message.Header.MessageType.ToString())
            .AddProperty(HeaderNames.TimeStamp, Convert.ToString(message.Header.TimeStamp));
        
        if (!string.IsNullOrEmpty(message.Header.Subject))
        {
            builder.AddProperty(HeaderNames.Subject, message.Header.Subject);
        }

        if (!string.IsNullOrEmpty(message.Header.CorrelationId))
        {
            builder.AddProperty(HeaderNames.CorrelationId, message.Header.CorrelationId);
        }
        
        if (!string.IsNullOrEmpty(message.Header.ReplyTo))
        {
            builder.AddProperty(HeaderNames.ReplyTo, message.Header.ReplyTo);
        }
        
        foreach (var (key, val) in message.Header.Bag
                     .Where(x => x.Key != HeaderNames.Keys))
        {
            builder.AddProperty(key, val.ToString());
        }

        if (message.Header.Bag.TryGetValue(HeaderNames.Keys, out var keys))
        {
            if (keys is string[] keysArray)
            {
                builder.SetKeys(keysArray);
            }
        }
        else
        {
            builder.SetKeys(message.Id);
        }
        
        if (_publication.Tag != null)
        {
            builder.SetTag(_publication.Tag);
        }
        
        if (delay.HasValue && delay.Value != TimeSpan.Zero)
        {
            builder
                .SetDeliveryTimestamp(_connection.TimerProvider.GetUtcNow().Add(delay.Value).UtcDateTime);
        }
        
        if (!string.IsNullOrEmpty(message.Header.PartitionKey))
        {
            builder.SetMessageGroup(message.Header.PartitionKey);
        }
        
        await _producer.Send(builder.Build());
    }
    
    /// <inheritdoc />
    public void Dispose()
    {
    }
    
    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return new ValueTask();
    }
}
