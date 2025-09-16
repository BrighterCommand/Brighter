#region Licence

/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.AWSSQS.V4;

/// <summary>
/// The SQS Message producer
/// </summary>
public partial class SqsMessageProducer : AwsMessagingGateway, IAmAMessageProducerAsync, IAmAMessageProducerSync
{
    private readonly SqsPublication _publication;
    private readonly AWSClientFactory _clientFactory;

    /// <summary>
    /// The publication configuration for this producer
    /// </summary>
    public Publication Publication => _publication;

    /// <summary>
    /// The OTel Span we are writing Producer events too
    /// </summary>
    public Activity? Span { get; set; }

    /// <inheritdoc />
    public IAmAMessageScheduler? Scheduler { get; set; }

    /// <summary>
    /// Initialize a new instance of the <see cref="SqsMessageProducer"/>.
    /// </summary>
    /// <param name="connection">How do we connect to AWS in order to manage middleware</param>
    /// <param name="publication">Configuration of a producer. Required.</param>
    public SqsMessageProducer(AWSMessagingGatewayConnection connection, SqsPublication publication)
        : base(connection)
    {
        _publication = publication ?? throw new ArgumentNullException(nameof(publication));
        if (_publication.ChannelName is null) 
            throw new InvalidOperationException($"We must have a valid Channel Name on the Publication, either a queue name or a Url");
        _clientFactory = new AWSClientFactory(connection);

        if (publication.FindQueueBy == QueueFindBy.Url)
        {
            ChannelQueueUrl = publication.ChannelName!.Value;
        }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public ValueTask DisposeAsync() => new();

    /// <summary>
    /// Confirm the queue exists.
    /// </summary>
    /// <param name="queue">The queue name.</param>
    public bool ConfirmQueueExists(string? queue = null)
        => BrighterAsyncContext.Run(async () => await ConfirmQueueExistsAsync());

    /// <summary>
    /// Confirm the queue exists.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
    /// <returns>Return true if the queue exists otherwise return false</returns>
    public async Task<bool> ConfirmQueueExistsAsync(CancellationToken cancellationToken = default)
    {
        //Only do this on first send for a queue for efficiency; won't auto-recreate when goes missing at runtime as a result
        if (!string.IsNullOrEmpty(ChannelQueueUrl))
            return true;
        
        if (_publication is null)
            throw new ConfigurationException("No publication specified for producer");
        
        if (_publication.ChannelName is null)
            throw new ConfigurationException("No channel name specified for publication");

        var queueUrl = await EnsureQueueAsync(
            _publication.ChannelName!,
            _publication.ChannelType,
            _publication.FindQueueBy,
            _publication.QueueAttributes,
            _publication.MakeChannels,
            cancellationToken);
        
        ChannelQueueUrl = queueUrl;

        return !string.IsNullOrEmpty(queueUrl);
    }

    /// <inheritdoc />
    public async Task SendAsync(Message message, CancellationToken cancellationToken = default)
        => await SendWithDelayAsync(message, TimeSpan.Zero, cancellationToken);

    /// <inheritdoc />
    public async Task SendWithDelayAsync(Message message, TimeSpan? delay, CancellationToken cancellationToken = default)
        => await SendWithDelayAsync(message, delay, true, cancellationToken);
    
    
    private async Task SendWithDelayAsync(Message message, TimeSpan? delay, bool useAsyncScheduler, CancellationToken cancellationToken = default)
    {
        if (_publication is null)
            throw new ConfigurationException("No publication specified for producer");
        
        delay ??= TimeSpan.Zero;
        // SQS support delay until 15min, more than that we are going to use scheduler
        if (delay > TimeSpan.FromMinutes(15))
        {
            if (useAsyncScheduler)
            {
                var schedulerAsync = (IAmAMessageSchedulerAsync)Scheduler!;
                await schedulerAsync.ScheduleAsync(message, delay.Value, cancellationToken);
                return;
            }

            var schedulerSync = (IAmAMessageSchedulerSync)Scheduler!;
            schedulerSync.Schedule(message, delay.Value);
            return;
        }
        
        Log.PublishingMessage(s_logger, message.Header.Topic, message.Id, message.Body);

        await ConfirmQueueExistsAsync(cancellationToken);

        using var client = _clientFactory.CreateSqsClient();
        var sender = new SqsMessageSender(ChannelQueueUrl!, client);
        var messageId = await sender.SendAsync(message, delay, cancellationToken);

        if (messageId == null)
        {
            throw new InvalidOperationException(
                $"Failed to publish message with topic {message.Header.Topic} and id {message.Id} and message: {message.Body}");
        }

        Log.PublishedMessage(s_logger, message.Header.Topic, message.Id, messageId);
    }

    public void Send(Message message) => SendWithDelay(message, null);

    public void SendWithDelay(Message message, TimeSpan? delay)
        => BrighterAsyncContext.Run(async () => await SendWithDelayAsync(message, delay, false));


    private static partial class Log
    {
        [LoggerMessage(LogLevel.Debug, "SQSMessageProducer: Publishing message with topic {Topic} and id {Id} and message: {Request}")]
        public static partial void PublishingMessage(ILogger logger, string topic, string id, MessageBody request);

        [LoggerMessage(LogLevel.Debug, "SQSMessageProducer: Published message with topic {Topic}, Brighter messageId {MessageId} and SNS messageId {SNSMessageId}")]
        public static partial void PublishedMessage(ILogger logger, string topic, string messageId, string snsMessageId);
    }
}

