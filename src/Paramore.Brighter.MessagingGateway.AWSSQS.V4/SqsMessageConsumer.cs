#region Licence

/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

    private readonly AWSClientFactory _clientFactory;
    private readonly string _queueName;
    private readonly int _batchSize;
    private readonly bool _hasDlq;
    private readonly bool _rawMessageDelivery;
    private readonly Message _noopMessage = new Message();
    private string? _channelUrl;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqsMessageConsumer"/> class.
    /// </summary>
    /// <param name="awsConnection">The awsConnection details used to connect to the SQS queue.</param>
    /// <param name="queueName">The name of the SQS Queue</param>
    /// <param name="batchSize">The maximum number of messages to consume per call to SQS</param>
    /// <param name="hasDlq">Do we have a DLQ attached to this queue?</param>
    /// <param name="isQueueUrl">Is the queue name a queue url?</param>
    /// <param name="rawMessageDelivery">Do we have Raw Message Delivery enabled?</param>
    public SqsMessageConsumer(
        AWSMessagingGatewayConnection awsConnection,
        string? queueName,
        int batchSize = 1,
        bool hasDlq = false,
        bool isQueueUrl = false,
        bool rawMessageDelivery = true)
    {
        if (string.IsNullOrEmpty(queueName))
            throw new ConfigurationException("QueueName is mandatory");

        _clientFactory = new AWSClientFactory(awsConnection);
        _queueName = queueName!;
        if (isQueueUrl)
            _channelUrl = queueName;
        _batchSize = batchSize;
        _hasDlq = hasDlq;
        _rawMessageDelivery = rawMessageDelivery;
    }

    /// <summary>
    /// Acknowledges the specified message.
    /// Sync over Async
    /// </summary>
    /// <param name="message">The message.</param>
    public void Acknowledge(Message message) => BrighterAsyncContext.Run(async () => await AcknowledgeAsync(message));

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

        try
        {
            using var client = _clientFactory.CreateSqsClient();
            await EnsureChannelUrl(client, cancellationToken);
            await client.DeleteMessageAsync(new DeleteMessageRequest(_channelUrl, receiptHandle),
                cancellationToken);

            Log.DeletedMessage(s_logger, message.Id, receiptHandle, _channelUrl!);
        }
        catch (Exception exception)
        {
            Log.ErrorDeletingMessage(s_logger, exception, message.Id, receiptHandle, _queueName);
            throw;
        }
    }

    /// <summary>
    /// Rejects the specified message.
    /// Sync over async
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>True if the message has been removed from the channel, false otherwise</returns>
    public bool Reject(Message message) => BrighterAsyncContext.Run(async () => await RejectAsync(message));

    /// <summary>
    /// Rejects the specified message.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">Cancel the reject operation</param>
    /// <returns>True if the message has been removed from the channel, false otherwise</returns>
    public async Task<bool> RejectAsync(Message message, CancellationToken cancellationToken = default(CancellationToken))
    {
        if (!message.Header.Bag.TryGetValue("ReceiptHandle", out object? value))
            return false;

        var receiptHandle = value.ToString();

        try
        {
            Log.RejectingMessage(s_logger, message.Id, receiptHandle, _queueName);

            using var client = _clientFactory.CreateSqsClient();
            await EnsureChannelUrl(client, cancellationToken);
            if (_hasDlq)
            {
                await client.ChangeMessageVisibilityAsync(
                    new ChangeMessageVisibilityRequest(_channelUrl, receiptHandle, 0),
                    cancellationToken
                );
            }
            else
            {
                await client.DeleteMessageAsync(_channelUrl, receiptHandle, cancellationToken);
            }
        }
        catch (Exception exception)
        {
            Log.ErrorRejectingMessage(s_logger, exception, message.Id, receiptHandle, _queueName);
            throw;
        }

        return true;
    }

    /// <summary>
    /// Purges the specified queue name.
    /// Sync over Async
    /// </summary>
    public void Purge() => BrighterAsyncContext.Run(async () => await PurgeAsync());
        
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
    public Message[] Receive(TimeSpan? timeOut = null) => BrighterAsyncContext.Run(async () => await ReceiveAsync(timeOut));

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
                WaitTimeSeconds = timeOut.Value.Seconds,
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

    public bool Requeue(Message message, TimeSpan? delay = null) => BrighterAsyncContext.Run(async () => await RequeueAsync(message, delay));

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
                    new ChangeMessageVisibilityRequest(_channelUrl, receiptHandle, delay.Value.Seconds),
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
        GC.SuppressFinalize(this);
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return new ValueTask(Task.CompletedTask);
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

        [LoggerMessage(LogLevel.Information, "SqsMessageConsumer: Rejecting the message {Id} with receipt handle {ReceiptHandle} on the queue {ChannelName}")]
        public static partial void RejectingMessage(ILogger logger, string id, string? receiptHandle, string channelName);

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

    }
}