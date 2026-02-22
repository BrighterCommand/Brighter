#region Licence

/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.AWSSQS.V4;

/// <summary>
/// Read messages from an SQS queue
/// </summary>
public partial class SqsMessageConsumer : IAmAMessageConsumerSync, IAmAMessageConsumerAsync
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<SqsMessageConsumer>();

    private readonly AWSMessagingGatewayConnection _connection;
    private readonly AWSClientFactory _clientFactory;
    private readonly string _queueName;
    private readonly int _batchSize;
    private readonly RoutingKey? _deadLetterRoutingKey;
    private readonly RoutingKey? _invalidMessageRoutingKey;
    private readonly OnMissingChannel _makeChannels;
    private readonly bool _rawMessageDelivery;
    private readonly SqsAttributes _queueAttributes;
    private readonly Message _noopMessage = new Message();
    private readonly Lazy<SqsMessageProducer?>? _deadLetterProducer;
    private readonly Lazy<SqsMessageProducer?>? _invalidMessageProducer;
    private string? _channelUrl;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqsMessageConsumer"/> class.
    /// </summary>
    /// <param name="awsConnection">The awsConnection details used to connect to the SQS queue.</param>
    /// <param name="queueName">The name of the SQS Queue</param>
    /// <param name="batchSize">The maximum number of messages to consume per call to SQS</param>
    /// <param name="deadLetterRoutingKey">The routing key for the dead letter queue, if configured</param>
    /// <param name="invalidMessageRoutingKey">The routing key for the invalid message queue, if configured</param>
    /// <param name="makeChannels">Should we create channels if they are missing?</param>
    /// <param name="isQueueUrl">Is the queue name a queue url?</param>
    /// <param name="rawMessageDelivery">Do we have Raw Message Delivery enabled?</param>
    /// <param name="queueAttributes">The <see cref="SqsAttributes"/> for the queue (used by DLQ producers for FIFO support)</param>
    public SqsMessageConsumer(
        AWSMessagingGatewayConnection awsConnection,
        string? queueName,
        int batchSize = 1,
        RoutingKey? deadLetterRoutingKey = null,
        RoutingKey? invalidMessageRoutingKey = null,
        OnMissingChannel makeChannels = OnMissingChannel.Create,
        bool isQueueUrl = false,
        bool rawMessageDelivery = true,
        SqsAttributes? queueAttributes = null)
    {
        if (string.IsNullOrEmpty(queueName))
            throw new ConfigurationException("QueueName is mandatory");

        _connection = awsConnection;
        _clientFactory = new AWSClientFactory(awsConnection);
        _queueName = queueName!;
        if (isQueueUrl)
            _channelUrl = queueName;
        _batchSize = batchSize;
        _deadLetterRoutingKey = deadLetterRoutingKey;
        _invalidMessageRoutingKey = invalidMessageRoutingKey;
        _makeChannels = makeChannels;
        _rawMessageDelivery = rawMessageDelivery;
        _queueAttributes = queueAttributes ?? SqsAttributes.Empty;

        // LazyThreadSafetyMode.None: message pumps are single-threaded per consumer, so no
        // thread-safety mode is needed. None does not cache exceptions, allowing the factory
        // to retry on the next .Value access after a transient failure.
        if (_deadLetterRoutingKey != null)
        {
            _deadLetterProducer = new Lazy<SqsMessageProducer?>(CreateDeadLetterProducer, LazyThreadSafetyMode.None);
        }

        if (_invalidMessageRoutingKey != null)
        {
            _invalidMessageProducer = new Lazy<SqsMessageProducer?>(CreateInvalidMessageProducer, LazyThreadSafetyMode.None);
        }
    }

    /// <summary>
    /// Acknowledges the specified message.
    /// Sync over Async
    /// </summary>
    /// <param name="message">The message.</param>
    public void Acknowledge(Message message) => BrighterAsyncContext.Run(() => AcknowledgeAsync(message));

    /// <summary>
    /// Acknowledges the specified message.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">Cancels the ackowledge operation</param>
    public async Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default(CancellationToken))
    {
        if (!message.Header.Bag.TryGetValue("ReceiptHandle", out object? value))
            return;

        var receiptHandle = value.ToString();
        await DeleteSourceMessageAsync(receiptHandle!, message.Id, cancellationToken);
    }

    /// <summary>
    /// Rejects the specified message.
    /// Sync over async
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="reason">The <see cref="MessageRejectionReason"/> that explains why we rejected the message</param>
    /// <returns>True if the message has been removed from the channel, false otherwise</returns>
    public bool Reject(Message message, MessageRejectionReason? reason) => BrighterAsyncContext.Run(() => RejectAsync(message, reason));

    /// <summary>
    /// Rejects the specified message.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="reason">The <see cref="MessageRejectionReason"/> that explains why we rejected the message</param>
    /// <param name="cancellationToken">Cancel the reject operation</param>
    /// <returns>True if the message has been removed from the channel, false otherwise</returns>
    public async Task<bool> RejectAsync(Message message, MessageRejectionReason? reason, CancellationToken cancellationToken = default(CancellationToken))
    {
        if (!message.Header.Bag.TryGetValue("ReceiptHandle", out object? value))
            return false;

        var receiptHandle = value.ToString();
        var reasonString = reason is null ? nameof(RejectionReason.DeliveryError) : reason.RejectionReason.ToString();
        var description = reason is null ? "unknown" : reason.Description ?? "unknown";

        Log.RejectingMessage(s_logger, message.Id, receiptHandle, _queueName, reasonString, description);

        // If no channels configured, just delete the original message
        if (_deadLetterProducer == null && _invalidMessageProducer == null)
        {
            if (reason != null)
            {
                Log.NoChannelsConfiguredForRejection(s_logger, message.Id, reason.RejectionReason.ToString());
            }

            await AcknowledgeAsync(message, cancellationToken);
            return true;
        }

        var rejectionReason = reason?.RejectionReason ?? RejectionReason.None;

        try
        {
            RefreshMetadata(message, reason);

            var (routingKey, shouldRoute, isFallingBackToDlq) = DetermineRejectionRoute(
                rejectionReason, _invalidMessageProducer != null, _deadLetterProducer != null);

            SqsMessageProducer? producer = null;
            if (shouldRoute)
            {
                message.Header.Topic = routingKey!;
                if (isFallingBackToDlq)
                    Log.FallingBackToDlq(s_logger, message.Id);

                if (routingKey == _invalidMessageRoutingKey)
                    producer = _invalidMessageProducer?.Value;
                else if (routingKey == _deadLetterRoutingKey)
                    producer = _deadLetterProducer?.Value;
            }

            if (producer != null)
            {
                await producer.SendAsync(message, cancellationToken);
                Log.MessageSentToRejectionChannel(s_logger, message.Id, rejectionReason.ToString());
            }
            else
            {
                Log.NoChannelsConfiguredForRejection(s_logger, message.Id, rejectionReason.ToString());
            }
        }
        catch (Exception ex)
        {
            // Sending to DLQ failed — delete the original to prevent infinite
            // reprocessing. The message is lost rather than stuck in a retry loop.
            Log.ErrorSendingToRejectionChannel(s_logger, ex, message.Id, rejectionReason.ToString());
            await DeleteSourceMessageAsync(receiptHandle!, message.Id, cancellationToken);
            return true;
        }

        await DeleteSourceMessageAsync(receiptHandle!, message.Id, cancellationToken);
        return true;
    }

    /// <summary>
    /// Purges the specified queue name.
    /// Sync over Async
    /// </summary>
    public void Purge() => BrighterAsyncContext.Run(() => PurgeAsync());

    /// <summary>
    /// Purges the specified queue name.
    /// </summary>
    public async Task PurgeAsync(CancellationToken cancellationToken = default(CancellationToken))
    {
        try
        {
            using var client = _clientFactory.CreateSqsClient();
            Log.PurgingQueue(s_logger, _queueName);

            await EnsureChannelUrl(client, cancellationToken);
            await client.PurgeQueueAsync(_channelUrl, cancellationToken);

            Log.PurgedQueue(s_logger, _queueName);
        }
        catch (Exception exception)
        {
            Log.ErrorPurgingQueue(s_logger, exception, _queueName);
            throw;
        }
    }

    /// <summary>
    /// Receives the specified queue name.
    /// Sync over async
    /// </summary>
    /// <param name="timeOut">The timeout. AWS uses whole seconds. Anything greater than 0 uses long-polling.  </param>
    public Message[] Receive(TimeSpan? timeOut = null) => BrighterAsyncContext.Run(() => ReceiveAsync(timeOut));

    /// <summary>
    /// Receives the specified queue name.
    /// </summary>
    /// <param name="timeOut">The timeout. AWS uses whole seconds. Anything greater than 0 uses long-polling.  </param>
    /// <param name="cancellationToken">Cancel the receive operation</param>
    public async Task<Message[]> ReceiveAsync(TimeSpan? timeOut = null,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        AmazonSQSClient? client = null;
        Amazon.SQS.Model.Message[] sqsMessages;
        try
        {
            client = _clientFactory.CreateSqsClient();

            await EnsureChannelUrl(client, cancellationToken);
            timeOut ??= TimeSpan.Zero;

            Log.RetrievingNextMessage(s_logger,_channelUrl!);

            var request = new ReceiveMessageRequest(_channelUrl)
            {
                MaxNumberOfMessages = _batchSize,
                WaitTimeSeconds = Convert.ToInt32(timeOut.Value.TotalSeconds),
                MessageAttributeNames = ["All"],
                MessageSystemAttributeNames = ["All"]
            };

            var receiveResponse = await client.ReceiveMessageAsync(request, cancellationToken);

            sqsMessages = receiveResponse.Messages?.ToArray() ?? [];
        }
        catch (InvalidOperationException ioe)
        {
            Log.CouldNotDetermineNumberOfMessagesToRetrieve(s_logger);
            throw new ChannelFailureException("Error connecting to SQS, see inner exception for details", ioe);
        }
        catch (OperationCanceledException oce)
        {
            Log.CouldNotFindMessagesToRetrieve(s_logger);
            throw new ChannelFailureException("Error connecting to SQS, see inner exception for details", oce);
        }
        catch (Exception e)
        {
            Log.ErrorListeningToQueue(s_logger, e, _queueName);
            throw;
        }
        finally
        {
            client?.Dispose();
        }

        if (sqsMessages.Length == 0)
        {
            return [_noopMessage];
        }

        var messages = new Message[sqsMessages.Length];
        for (int i = 0; i < sqsMessages.Length; i++)
        {
            var message = SqsMessageCreatorFactory.Create(_rawMessageDelivery).CreateMessage(sqsMessages[i]);
            Log.ReceivedMessageFromQueue(s_logger, _queueName, Environment.NewLine, JsonSerializer.Serialize(message, JsonSerialisationOptions.Options));
            messages[i] = message;
        }

        return messages;
    }

    public bool Requeue(Message message, TimeSpan? delay = null) => BrighterAsyncContext.Run(() => RequeueAsync(message, delay));

    /// <summary>
    /// Re-queues the specified message.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="delay">Time to delay delivery of the message. AWS uses seconds. 0s is immediate requeue. Default is 0ms</param>
    /// <param name="cancellationToken">Cancels the requeue</param>
    /// <returns>True if the message was requeued successfully</returns>
    public async Task<bool> RequeueAsync(Message message, TimeSpan? delay = null,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        if (!message.Header.Bag.TryGetValue("ReceiptHandle", out object? value))
            return false;

        delay ??= TimeSpan.Zero;

        var receiptHandle = value.ToString();

        try
        {
            Log.RequeueingMessage(s_logger, message.Id);

            using (var client = _clientFactory.CreateSqsClient())
            {
                await EnsureChannelUrl(client, cancellationToken);
                await client.ChangeMessageVisibilityAsync(
                    new ChangeMessageVisibilityRequest(_channelUrl, receiptHandle, Convert.ToInt32(delay.Value.TotalSeconds)),
                    cancellationToken
                );
            }

            Log.RequeuedMessage(s_logger, message.Id);

            return true;
        }
        catch (Exception exception)
        {
            Log.ErrorRequeueingMessage(s_logger, exception, message.Id, receiptHandle, _queueName);
            return false;
        }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        if (_deadLetterProducer?.IsValueCreated == true)
            _deadLetterProducer.Value?.Dispose();

        if (_invalidMessageProducer?.IsValueCreated == true)
            _invalidMessageProducer.Value?.Dispose();

        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_deadLetterProducer?.IsValueCreated == true && _deadLetterProducer.Value is IAsyncDisposable deadLetterAsync)
            await deadLetterAsync.DisposeAsync();
        else if (_deadLetterProducer?.IsValueCreated == true)
            _deadLetterProducer.Value?.Dispose();

        if (_invalidMessageProducer?.IsValueCreated == true && _invalidMessageProducer.Value is IAsyncDisposable invalidAsync)
            await invalidAsync.DisposeAsync();
        else if (_invalidMessageProducer?.IsValueCreated == true)
            _invalidMessageProducer.Value?.Dispose();

        GC.SuppressFinalize(this);
    }

    private SqsMessageProducer? CreateDeadLetterProducer()
    {
        var publication = new SqsPublication(
            channelName: new ChannelName(_deadLetterRoutingKey!.Value),
            queueAttributes: _queueAttributes,
            makeChannels: _makeChannels);

        try
        {
            // Queue existence is confirmed on first SendAsync via ConfirmQueueExistsAsync.
            // We must NOT call the sync ConfirmQueueExists here because the lazy is resolved
            // inside RejectAsync, which may already be running inside BrighterAsyncContext.Run
            // from the sync Reject path — nesting would deadlock.
            return new SqsMessageProducer(_connection, publication);
        }
        catch (Exception e)
        {
            Log.ErrorCreatingDlqProducerException(s_logger, e, _deadLetterRoutingKey.Value);
            return null;
        }
    }

    private SqsMessageProducer? CreateInvalidMessageProducer()
    {
        var publication = new SqsPublication(
            channelName: new ChannelName(_invalidMessageRoutingKey!.Value),
            queueAttributes: _queueAttributes,
            makeChannels: _makeChannels);

        try
        {
            // Queue existence is confirmed on first SendAsync via ConfirmQueueExistsAsync.
            return new SqsMessageProducer(_connection, publication);
        }
        catch (Exception e)
        {
            Log.ErrorCreatingInvalidMessageProducerException(s_logger, e, _invalidMessageRoutingKey.Value);
            return null;
        }
    }

    private static void RefreshMetadata(Message message, MessageRejectionReason? reason)
    {
        // Keys use camelCase because the bag is JSON-serialized with CamelCase naming policy
        message.Header.Bag["originalTopic"] = message.Header.Topic.Value;
        message.Header.Bag["rejectionTimestamp"] = DateTimeOffset.UtcNow.ToString("o");
        message.Header.Bag["originalMessageType"] = message.Header.MessageType.ToString();

        // Remove SQS-specific headers that will be reset when sent to the DLQ
        message.Header.Bag.Remove("ReceiptHandle");

        if (reason == null) return;

        message.Header.Bag["rejectionReason"] = reason.RejectionReason.ToString();
        if (!string.IsNullOrEmpty(reason.Description))
        {
            message.Header.Bag["rejectionMessage"] = reason.Description ?? string.Empty;
        }
    }

    private async Task DeleteSourceMessageAsync(string receiptHandle, string messageId, CancellationToken cancellationToken)
    {
        try
        {
            using var client = _clientFactory.CreateSqsClient();
            await EnsureChannelUrl(client, cancellationToken);
            await client.DeleteMessageAsync(new DeleteMessageRequest(_channelUrl, receiptHandle),
                cancellationToken);

            Log.DeletedMessage(s_logger, messageId, receiptHandle, _channelUrl!);
        }
        catch (Exception exception)
        {
            Log.ErrorDeletingMessage(s_logger, exception, messageId, receiptHandle, _queueName);
            throw;
        }
    }

    private (RoutingKey? routingKey, bool foundProducer, bool isFallingBackToDlq) DetermineRejectionRoute(
        RejectionReason rejectionReason,
        bool hasInvalidProducer,
        bool hasDeadLetterProducer)
    {
        switch (rejectionReason)
        {
            case RejectionReason.Unacceptable:
                if (hasInvalidProducer)
                    return (_invalidMessageRoutingKey, true, false);
                if (hasDeadLetterProducer)
                    return (_deadLetterRoutingKey, true, true);
                return (null, false, false);

            case RejectionReason.DeliveryError:
            case RejectionReason.None:
            default:
                if (hasDeadLetterProducer)
                    return (_deadLetterRoutingKey, true, false);
                return (null, false, false);
        }
    }

    private async Task EnsureChannelUrl(AmazonSQSClient client, CancellationToken cancellationToken)
    {
        //only grab the queue url once
        if (_channelUrl is not null)
            return;

        var urlResponse = await client.GetQueueUrlAsync(_queueName, cancellationToken);
        _channelUrl = urlResponse.QueueUrl;
    }

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Information, "SqsMessageConsumer: Deleted the message {Id} with receipt handle {ReceiptHandle} on the queue {Url}")]
        public static partial void DeletedMessage(ILogger logger, string id, string? receiptHandle, string url);

        [LoggerMessage(LogLevel.Error, "SqsMessageConsumer: Error during deleting the message {Id} with receipt handle {ReceiptHandle} on the queue {ChannelName}")]
        public static partial void ErrorDeletingMessage(ILogger logger, Exception exception, string id, string? receiptHandle, string channelName);

        [LoggerMessage(LogLevel.Information, "SqsMessageConsumer: Rejecting the message {Id} with receipt handle {ReceiptHandle} on the queue {ChannelName} due to {Reason} because of {Description}")]
        public static partial void RejectingMessage(ILogger logger, string id, string? receiptHandle, string channelName, string? reason, string description);

        [LoggerMessage(LogLevel.Error, "SqsMessageConsumer: Error during rejecting the message {Id} with receipt handle {ReceiptHandle} on the queue {ChannelName}")]
        public static partial void ErrorRejectingMessage(ILogger logger, Exception exception, string id, string? receiptHandle, string channelName);

        [LoggerMessage(LogLevel.Information, "SqsMessageConsumer: Purging the queue {ChannelName}")]
        public static partial void PurgingQueue(ILogger logger, string channelName);

        [LoggerMessage(LogLevel.Information, "SqsMessageConsumer: Purged the queue {ChannelName}")]
        public static partial void PurgedQueue(ILogger logger, string channelName);

        [LoggerMessage(LogLevel.Error, "SqsMessageConsumer: Error purging queue {ChannelName}")]
        public static partial void ErrorPurgingQueue(ILogger logger, Exception exception, string channelName);

        [LoggerMessage(LogLevel.Debug, "SqsMessageConsumer: Preparing to retrieve next message from queue {Url}")]
        public static partial void RetrievingNextMessage(ILogger logger, string url);

        [LoggerMessage(LogLevel.Debug, "SqsMessageConsumer: Could not determine number of messages to retrieve")]
        public static partial void CouldNotDetermineNumberOfMessagesToRetrieve(ILogger logger);

        [LoggerMessage(LogLevel.Debug, "SqsMessageConsumer: Could not find messages to retrieve")]
        public static partial void CouldNotFindMessagesToRetrieve(ILogger logger);

        [LoggerMessage(LogLevel.Error, "SqsMessageConsumer: There was an error listening to queue {ChannelName}")]
        public static partial void ErrorListeningToQueue(ILogger logger, Exception exception, string channelName);

        [LoggerMessage(LogLevel.Information, "SqsMessageConsumer: Received message from queue {ChannelName}, message: {NewLine}{Request}")]
        public static partial void ReceivedMessageFromQueue(ILogger logger, string channelName, string newLine, string request);

        [LoggerMessage(LogLevel.Information, "SqsMessageConsumer: re-queueing the message {Id}")]
        public static partial void RequeueingMessage(ILogger logger, string id);

        [LoggerMessage(LogLevel.Information, "SqsMessageConsumer: re-queued the message {Id}")]
        public static partial void RequeuedMessage(ILogger logger, string id);

        [LoggerMessage(LogLevel.Error, "SqsMessageConsumer: Error during re-queueing the message {Id} with receipt handle {ReceiptHandle} on the queue {ChannelName}")]
        public static partial void ErrorRequeueingMessage(ILogger logger, Exception exception, string id, string? receiptHandle, string channelName);

        [LoggerMessage(LogLevel.Warning, "SqsMessageConsumer: No DLQ or invalid message channels configured for message {Id} with rejection reason {Reason}")]
        public static partial void NoChannelsConfiguredForRejection(ILogger logger, string id, string reason);

        [LoggerMessage(LogLevel.Information, "SqsMessageConsumer: Message {Id} sent to rejection channel for reason {Reason}")]
        public static partial void MessageSentToRejectionChannel(ILogger logger, string id, string reason);

        [LoggerMessage(LogLevel.Warning, "SqsMessageConsumer: No invalid message channel configured for message {Id}, falling back to DLQ")]
        public static partial void FallingBackToDlq(ILogger logger, string id);

        [LoggerMessage(LogLevel.Error, "SqsMessageConsumer: Error sending message {Id} to rejection channel for reason {Reason}")]
        public static partial void ErrorSendingToRejectionChannel(ILogger logger, Exception exception, string id, string reason);

        [LoggerMessage(LogLevel.Error, "SqsMessageConsumer: Exception creating DLQ producer for queue {QueueName}")]
        public static partial void ErrorCreatingDlqProducerException(ILogger logger, Exception exception, string queueName);

        [LoggerMessage(LogLevel.Error, "SqsMessageConsumer: Exception creating invalid message producer for queue {QueueName}")]
        public static partial void ErrorCreatingInvalidMessageProducerException(ILogger logger, Exception exception, string queueName);

    }
}
