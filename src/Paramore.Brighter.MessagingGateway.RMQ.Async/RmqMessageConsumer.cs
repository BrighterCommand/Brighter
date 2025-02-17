#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Tasks;
using Polly.CircuitBreaker;
using RabbitMQ.Client.Exceptions;

namespace Paramore.Brighter.MessagingGateway.RMQ.Async;

/// <summary>
/// Class RmqMessageConsumer.
/// The <see cref="RmqMessageConsumer"/> is used on the server to receive messages from the broker. It abstracts away the details of 
/// inter-process communication tasks from the server. It handles subscription establishment, request reception and dispatching, 
/// result sending, and error handling.
/// </summary>
public class RmqMessageConsumer : RmqMessageGateway, IAmAMessageConsumerSync, IAmAMessageConsumerAsync
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<RmqMessageConsumer>();

    private PullConsumer? _consumer;
    private readonly ChannelName _queueName;
    private readonly RoutingKeys _routingKeys;
    private readonly bool _isDurable;
    private readonly RmqMessageCreator _messageCreator;
    private readonly Message _noopMessage = new();
    private readonly string _consumerTag;
    private readonly OnMissingChannel _makeChannels;
    private readonly ushort _batchSize;
    private readonly bool _highAvailability;
    private readonly ChannelName? _deadLetterQueueName;
    private readonly RoutingKey? _deadLetterRoutingKey;
    private readonly bool _hasDlq;
    private readonly TimeSpan? _ttl;
    private readonly int? _maxQueueLength;

    /// <summary>
    /// Initializes a new instance of the <see cref="RmqMessageGateway" /> class.
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="queueName">The queue name.</param>
    /// <param name="routingKey">The routing key.</param>
    /// <param name="isDurable">Is the queue definition persisted</param>
    /// <param name="highAvailability">Is the queue available on all nodes in a cluster</param>
    /// <param name="batchSize">How many messages to retrieve at one time; ought to be size of channel buffer</param>
    /// <param name="deadLetterQueueName">The dead letter queue</param>
    /// <param name="deadLetterRoutingKey">The routing key for dead letter messages</param>
    /// <param name="ttl">How long before a message on the queue expires. Defaults to infinite</param>
    /// <param name="maxQueueLength">How lare can the buffer grow before we stop accepting new work?</param>
    /// <param name="makeChannels">Should we validate, or create missing channels</param>
    public RmqMessageConsumer(
        RmqMessagingGatewayConnection connection,
        ChannelName queueName,
        RoutingKey routingKey,
        bool isDurable,
        bool highAvailability = false,
        int batchSize = 1,
        ChannelName? deadLetterQueueName = null,
        RoutingKey? deadLetterRoutingKey = null,
        TimeSpan? ttl = null,
        int? maxQueueLength = null,
        OnMissingChannel makeChannels = OnMissingChannel.Create)
        : this(connection, queueName, new RoutingKeys([routingKey]), isDurable, highAvailability,
            batchSize, deadLetterQueueName, deadLetterRoutingKey, ttl, maxQueueLength, makeChannels)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RmqMessageGateway" /> class.
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="queueName">The queue name.</param>
    /// <param name="routingKeys">The routing keys.</param>
    /// <param name="isDurable">Is the queue persisted to disk</param>
    /// <param name="highAvailability">Are the queues mirrored across nodes of the cluster</param>
    /// <param name="batchSize">How many messages to retrieve at one time; ought to be size of channel buffer</param>
    /// <param name="deadLetterQueueName">The dead letter queue</param>
    /// <param name="deadLetterRoutingKey">The routing key for dead letter messages</param>
    /// <param name="ttl">How long before a message on the queue expires. Defaults to infinite</param>
    /// <param name="maxQueueLength">The maximum number of messages on the queue before we begin to reject publication of messages</param>
    /// <param name="makeChannels">Should we validate or create missing channels</param>
    public RmqMessageConsumer(
        RmqMessagingGatewayConnection connection,
        ChannelName queueName,
        RoutingKeys routingKeys,
        bool isDurable,
        bool highAvailability = false,
        int batchSize = 1,
        ChannelName? deadLetterQueueName = null,
        RoutingKey? deadLetterRoutingKey = null,
        TimeSpan? ttl = null,
        int? maxQueueLength = null,
        OnMissingChannel makeChannels = OnMissingChannel.Create)
        : base(connection)
    {
        _queueName = queueName;
        _routingKeys = routingKeys;
        _isDurable = isDurable;
        _highAvailability = highAvailability;
        _messageCreator = new RmqMessageCreator();
        _batchSize = Convert.ToUInt16(batchSize);
        _makeChannels = makeChannels;
        _consumerTag = Connection.Name + Guid.NewGuid();
        _deadLetterQueueName = deadLetterQueueName;
        _deadLetterRoutingKey = deadLetterRoutingKey;
        _hasDlq = !string.IsNullOrEmpty(deadLetterQueueName!) && !string.IsNullOrEmpty(_deadLetterRoutingKey!);
        _ttl = ttl;
        _maxQueueLength = maxQueueLength;
    }

    /// <summary>
    /// Acknowledges the specified message.
    /// </summary>
    /// <param name="message">The message.</param>
    public void Acknowledge(Message message) => BrighterAsyncContext.Run(async () =>await AcknowledgeAsync(message));

    public async Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default)
    {
        var deliveryTag = message.DeliveryTag;
        try
        {
            await EnsureBrokerAsync(cancellationToken: cancellationToken);
            
            if (Channel is null) throw new ChannelFailureException($"RmqMessageConsumer: channel {_queueName.Value} is null");
            
            s_logger.LogInformation(
                "RmqMessageConsumer: Acknowledging message {Id} as completed with delivery tag {DeliveryTag}",
                message.Id, deliveryTag);
            await Channel.BasicAckAsync(deliveryTag, false, cancellationToken);
        }
        catch (Exception exception)
        {
            s_logger.LogError(exception,
                "RmqMessageConsumer: Error acknowledging message {Id} as completed with delivery tag {DeliveryTag}",
                message.Id, deliveryTag);
            throw;
        }
    }

    /// <summary>
    /// Purges the specified queue name.
    /// </summary>
    public void Purge() => BrighterAsyncContext.Run(async () => await PurgeAsync());

    public async Task PurgeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            //Why bind a queue? Because we use purge to initialize a queue for RPC
            await EnsureChannelAsync(cancellationToken);
            
            if (Channel is null) throw new ChannelFailureException($"RmqMessageConsumer: channel {_queueName.Value} is null");

            s_logger.LogDebug("RmqMessageConsumer: Purging channel {ChannelName}", _queueName.Value);

            try
            {
                await Channel.QueuePurgeAsync(_queueName.Value, cancellationToken);
            }
            catch (OperationInterruptedException operationInterruptedException)
            {
                if (operationInterruptedException.ShutdownReason?.ReplyCode == 404)
                {
                    return;
                }

                throw;
            }
        }
        catch (Exception exception)
        {
            s_logger.LogError(exception, "RmqMessageConsumer: Error purging channel {ChannelName}",
                _queueName.Value);
            throw;
        }
    }
    
    /// <summary>
    /// Receives the specified queue name.
    /// </summary>
    /// <remarks>
    /// Sync over async as RMQ does not support a sync consumer - Brighter pauses the message pump
    /// whilst waiting anyway,  so it is unlikely to deadlock 
    /// </remarks>
    /// <param name="timeOut">The timeout in milliseconds. We retry on timeout 5 ms intervals, with a min of 5ms
    /// until the timeout value is reached. </param>
    /// <returns>Message.</returns>
    public Message[] Receive(TimeSpan? timeOut = null) => BrighterAsyncContext.Run(async () => await ReceiveAsync(timeOut)); 

    /// <summary>
    /// Receives the specified queue name.
    /// </summary>
    /// <param name="timeOut">The timeout in milliseconds. We retry on timeout 5 ms intervals, with a min of 5ms
    /// until the timeout value is reached. </param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> for the receive operation</param>
    /// <returns>Message.</returns>
    public async Task<Message[]> ReceiveAsync(TimeSpan? timeOut = null, CancellationToken cancellationToken = default(CancellationToken))
    {

        timeOut ??= TimeSpan.FromMilliseconds(5);

        try
        {
            await EnsureChannelAsync(cancellationToken);
            
            if (_consumer is null) throw new ChannelFailureException($"RmwMessageConsumer: consumer for {_queueName.Value} is null");
            if (Connection.Exchange is null) throw new ConfigurationException($"RmqMessageConsumer: exchange for {_queueName.Value} is null");
           if (Connection.AmpqUri is null) throw new ConfigurationException($"RmqMessageConsumer: ampqUri for {_queueName.Value} is null");

            s_logger.LogDebug(
                "RmqMessageConsumer: Preparing to retrieve next message from queue {ChannelName} with routing key {RoutingKeys} via exchange {ExchangeName} on subscription {URL}",
                _queueName.Value,
                string.Join(";", _routingKeys.Select(rk => rk.Value)),
                Connection.Exchange.Name,
                Connection.AmpqUri.GetSanitizedUri()
            );
        
            var (resultCount, results) = await _consumer.DeQueue(timeOut.Value, _batchSize);

            if (results is not null && results.Length == 0) return [_noopMessage];
            
            var messages = new Message[resultCount];
            for (var i = 0; i < resultCount; i++)
            {
                var message = _messageCreator.CreateMessage(results![i]);
                messages[i] = message;

                s_logger.LogInformation(
                    "RmqMessageConsumer: Received message from queue {ChannelName} with routing key {RoutingKeys} via exchange {ExchangeName} on subscription {URL}, message: {Request}",
                    _queueName.Value,
                    string.Join(";", _routingKeys.Select(rk => rk.Value)),
                    Connection.Exchange.Name,
                    Connection.AmpqUri.GetSanitizedUri(),
                    JsonSerializer.Serialize(message, JsonSerialisationOptions.Options)
                );
            }

            return messages;

        }
        catch (Exception exception) when (exception is BrokerUnreachableException ||
                                          exception is AlreadyClosedException ||
                                          exception is TimeoutException)
        {
            await HandleExceptionAsync(exception, true, cancellationToken);
        }
        catch (Exception exception) when (exception is EndOfStreamException ||
                                          exception is OperationInterruptedException ||
                                          exception is NotSupportedException ||
                                          exception is BrokenCircuitException)
        {
            await HandleExceptionAsync(exception, cancellationToken: cancellationToken);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(exception, cancellationToken: cancellationToken);
        }

        return [_noopMessage]; // Default return in case of exception
    }

    /// <summary>
    /// Requeues the specified message.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="timeout">Time to delay delivery of the message.</param>
    /// <returns>True if message deleted, false otherwise</returns>
    public bool Requeue(Message message, TimeSpan? timeout = null) => BrighterAsyncContext.Run(async () => await RequeueAsync(message, timeout));

    public async Task<bool> RequeueAsync(Message message, TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        timeout ??= TimeSpan.Zero;

        try
        {
            s_logger.LogDebug("RmqMessageConsumer: Re-queueing message {Id} with a delay of {Delay} milliseconds",
                message.Id, timeout.Value.TotalMilliseconds);
            
            await EnsureChannelAsync(cancellationToken);
            
            if (Channel is null) throw new ChannelFailureException($"RmqMessageConsumer: channel {_queueName.Value} is null");

            var rmqMessagePublisher = new RmqMessagePublisher(Channel, Connection);
            if (DelaySupported)
            {
                await rmqMessagePublisher.RequeueMessageAsync(message, _queueName, timeout.Value, cancellationToken);
            }
            else
            {
                if (timeout > TimeSpan.Zero)
                {
                    await Task.Delay(timeout.Value, cancellationToken);
                }

                await rmqMessagePublisher.RequeueMessageAsync(message, _queueName, TimeSpan.Zero, cancellationToken);
            }

            //ack the original message to remove it from the queue
            var deliveryTag = message.DeliveryTag;
            s_logger.LogInformation(
                "RmqMessageConsumer: Deleting message {Id} with delivery tag {DeliveryTag} as re-queued",
                message.Id, deliveryTag);
            await Channel.BasicAckAsync(deliveryTag, false, cancellationToken);

            return true;
        }
        catch (Exception exception)
        {
            s_logger.LogError(exception, "RmqMessageConsumer: Error re-queueing message {Id}", message.Id);
            return false;
        }
    }

    /// <summary>
    /// Rejects the specified message.
    /// </summary>
    /// <param name="message">The message.</param>
    public void Reject(Message message) => BrighterAsyncContext.Run(async () => await RejectAsync(message));

    public async Task RejectAsync(Message message, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureBrokerAsync(_queueName, cancellationToken: cancellationToken);
            
            if (Channel is null) throw new InvalidOperationException($"RmqMessageConsumer: channel {_queueName.Value} is null");
            
            s_logger.LogInformation("RmqMessageConsumer: NoAck message {Id} with delivery tag {DeliveryTag}",
                message.Id, message.DeliveryTag);
            //if we have a DLQ, this will force over to the DLQ
            await Channel.BasicRejectAsync(message.DeliveryTag, false, cancellationToken);
        }
        catch (Exception exception)
        {
            s_logger.LogError(exception, "RmqMessageConsumer: Error try to NoAck message {Id}", message.Id);
            throw;
        }
    }

    protected virtual async Task EnsureChannelAsync(CancellationToken cancellationToken = default)
    {
        if (Channel == null || Channel.IsClosed)
        {
            await EnsureBrokerAsync(_queueName, cancellationToken: cancellationToken);

            if (_makeChannels == OnMissingChannel.Create)
            {
                await CreateQueueAsync(cancellationToken);
                await BindQueueAsync(cancellationToken);
            }
            else if (_makeChannels == OnMissingChannel.Validate)
            {
                await ValidateQueueAsync(cancellationToken);
            }
            else if (_makeChannels == OnMissingChannel.Assume)
            {
                //-- pass, here for clarity on fall through to use of queue directly on assume
            }

            await CreateConsumerAsync(cancellationToken);
            
            if (Channel is null) throw new ChannelFailureException($"RmqMessageConsumer: channel {_queueName.Value} is null");
            if (Connection.Exchange is null) throw new ConfigurationException($"RmqMessageConsumer: exchange for {_queueName.Value} is null");
           if (Connection.AmpqUri is null) throw new ConfigurationException($"RmqMessageConsumer: ampqUri for {_queueName.Value} is null");

            s_logger.LogInformation(
                "RmqMessageConsumer: Created rabbitmq channel {ConsumerNumber} for queue {ChannelName} with routing key/s {RoutingKeys} via exchange {ExchangeName} on subscription {URL}",
                Channel.ChannelNumber,
                _queueName.Value,
                string.Join(";", _routingKeys.Select(rk => rk.Value)),
                Connection.Exchange.Name,
                Connection.AmpqUri.GetSanitizedUri()
            );
        }
    }

    private async Task CancelConsumerAsync(CancellationToken cancellationToken)
    {
        if (_consumer != null && Channel != null)
        {
            if (_consumer.IsRunning)
            {
                await Channel.BasicCancelAsync(_consumerTag, cancellationToken: cancellationToken);
            }

            _consumer = null;
        }
    }

    private async Task CreateConsumerAsync(CancellationToken cancellationToken)
    {
        if (Channel is null) throw new ChannelFailureException($"RmqMessageConsumer: channel {_queueName.Value} is null");
        if (Connection.Exchange is null) throw new ConfigurationException($"RmqMessageConsumer: exchange for {_queueName.Value} is null");
       if (Connection.AmpqUri is null) throw new ConfigurationException($"RmqMessageConsumer: ampqUri for {_queueName.Value} is null");
        
        _consumer = new PullConsumer(Channel);
        if (_consumer is null) throw new InvalidOperationException($"RmqMessageConsumer: consumer for {_queueName.Value} is null");
        
        await _consumer.SetChannelBatchSizeAsync(_batchSize);

        await Channel.BasicConsumeAsync(_queueName.Value,
            false,
            _consumerTag,
            true,
            false,
            SetQueueArguments(),
            _consumer,
            cancellationToken: cancellationToken);

        await _consumer.HandleBasicConsumeOkAsync(_consumerTag, cancellationToken);

        s_logger.LogInformation(
            "RmqMessageConsumer: Created consumer for queue {ChannelName} with routing key {Topic} via exchange {ExchangeName} on subscription {URL}",
            _queueName.Value,
            string.Join(";", _routingKeys.Select(rk => rk.Value)),
            Connection.Exchange.Name,
            Connection.AmpqUri.GetSanitizedUri()
        );
    }

    private async Task CreateQueueAsync(CancellationToken cancellationToken)
    {
        if (Channel is null) throw new ChannelFailureException($"RmqMessageConsumer: channel {_queueName.Value} is null");
        if (Connection.Exchange is null) throw new ConfigurationException($"RmqMessageConsumer: exchange for {_queueName.Value} is null");
       if (Connection.AmpqUri is null) throw new ConfigurationException($"RmqMessageConsumer: ampqUri for {_queueName.Value} is null");
        
        s_logger.LogDebug("RmqMessageConsumer: Creating queue {ChannelName} on subscription {URL}",
            _queueName.Value, Connection.AmpqUri.GetSanitizedUri());
        await Channel.QueueDeclareAsync(_queueName.Value, _isDurable, false, false, SetQueueArguments(),
            cancellationToken: cancellationToken);
        
        if (_hasDlq)
        {
            await Channel.QueueDeclareAsync(_deadLetterQueueName!.Value, _isDurable, false, false,
                cancellationToken: cancellationToken);
        }
    }

    private async Task BindQueueAsync(CancellationToken cancellationToken)
    {
        if (Channel is null) throw new ChannelFailureException($"RmqMessageConsumer: channel {_queueName.Value} is null");
        if (Connection.Exchange is null) throw new ConfigurationException($"RmqMessageConsumer: exchange for {_queueName.Value} is null");
       if (Connection.AmpqUri is null) throw new ConfigurationException($"RmqMessageConsumer: ampqUri for {_queueName.Value} is null");
        
        foreach (var key in _routingKeys)
        {
            await Channel.QueueBindAsync(_queueName.Value, Connection.Exchange.Name, key,
                cancellationToken: cancellationToken);
        }

        if (_hasDlq)
        {
            await Channel.QueueBindAsync(_deadLetterQueueName!.Value, GetDeadletterExchangeName(),
                _deadLetterRoutingKey!.Value, cancellationToken: cancellationToken);
        }
    }

    private async Task HandleExceptionAsync(Exception exception, bool resetConnection = false, CancellationToken cancellationToken = default)
    {
        if (Connection.Exchange is null) throw new ConfigurationException($"RmqMessageConsumer: exchange for {_queueName.Value} is null", exception);
       if (Connection.AmpqUri is null) throw new ConfigurationException($"RmqMessageConsumer: ampqUri for {_queueName.Value} is null", exception);
        
        s_logger.LogError(exception,
            "RmqMessageConsumer: There was an error listening to queue {ChannelName} via exchange {RoutingKeys} via exchange {ExchangeName} on subscription {URL}",
            _queueName.Value,
            string.Join(";", _routingKeys.Select(rk => rk.Value)),
            Connection.Exchange.Name,
            Connection.AmpqUri.GetSanitizedUri()
        );
        
        if (resetConnection) await ResetConnectionToBrokerAsync(cancellationToken);
        throw new ChannelFailureException("Error connecting to RabbitMQ, see inner exception for details", exception);
    }

    private async Task ValidateQueueAsync(CancellationToken cancellationToken)
    {
        if (Channel is null) throw new ChannelFailureException($"RmqMessageConsumer: channel {_queueName.Value} is null");
        if (Connection.Exchange is null) throw new ConfigurationException($"RmqMessageConsumer: exchange for {_queueName.Value} is null");
       if (Connection.AmpqUri is null) throw new ConfigurationException($"RmqMessageConsumer: ampqUri for {_queueName.Value} is null");

        s_logger.LogDebug("RmqMessageConsumer: Validating queue {ChannelName} on subscription {URL}",
            _queueName.Value, Connection.AmpqUri.GetSanitizedUri());

        try
        {
            await Channel.QueueDeclarePassiveAsync(_queueName.Value, cancellationToken);
        }
        catch (Exception e)
        {
            throw new BrokerUnreachableException(e);
        }
    }

    private Dictionary<string, object?> SetQueueArguments()
    {
        var arguments = new Dictionary<string, object?>();
        if (_highAvailability)
        {
            // Only work for RabbitMQ Server version before 3.0
            //http://www.rabbitmq.com/blog/2012/11/19/breaking-things-with-rabbitmq-3-0/
            arguments.Add("x-ha-policy", "all");
        }

        if (_hasDlq)
        {
            //You can set a different exchange for the DLQ to the Queue
            arguments.Add("x-dead-letter-exchange", GetDeadletterExchangeName());
            arguments.Add("x-dead-letter-routing-key", _deadLetterRoutingKey?.Value);
        }

        if (_ttl.HasValue)
        {
            arguments.Add("x-message-ttl", _ttl.Value.Milliseconds);
        }

        if (_maxQueueLength.HasValue)
        {
            arguments.Add("x-max-length", _maxQueueLength.Value);
            if (_hasDlq)
            {
                arguments.Add("x-overflow", "reject-publish-dlx");
            }

            arguments.Add("x-overflow", "reject-publish");
        }

        return arguments;
    }

    private string GetDeadletterExchangeName()
    {
        //never likely to happen as caller will generally have asserted this
        if (Connection.Exchange is null) throw new ConfigurationException($"RmqMessageConsumer: exchange for {_queueName.Value} is null");
        
        return Connection.DeadLetterExchange is not null ? Connection.DeadLetterExchange.Name : Connection.Exchange.Name;
    }


    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public override void Dispose()
    {
        // A wait here, will die in the Brighter synchronization context
        // CancelConsumerAsync(CancellationToken.None).GetAwaiter().GetResult();
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    public override async ValueTask DisposeAsync()
    {
        await CancelConsumerAsync(CancellationToken.None);
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~RmqMessageConsumer()
    {
        Dispose(false);
    }
}
