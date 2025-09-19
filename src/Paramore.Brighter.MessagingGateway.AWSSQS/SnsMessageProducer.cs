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

namespace Paramore.Brighter.MessagingGateway.AWSSQS;

/// <summary>
/// Class SnsMessageProducer.
/// </summary>
public partial class SnsMessageProducer : AwsMessagingGateway, IAmAMessageProducerSync, IAmAMessageProducerAsync
{
    private SnsPublication _publication;
    private readonly AWSClientFactory _clientFactory;

    /// <summary>
    /// The publication configuration for this producer
    /// </summary>
    public Publication Publication
    {
        get => _publication;
        set => _publication = value as SnsPublication ?? throw new ConfigurationException("Publication must be of type SnsPublication");
    }

    /// <summary>
    /// The OTel Span we are writing Producer events too
    /// </summary>
    public Activity? Span { get; set; }

    /// <inheritdoc />
    public IAmAMessageScheduler? Scheduler { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SnsMessageProducer"/> class.
    /// </summary>
    /// <param name="connection">How do we connect to AWS in order to manage middleware</param>
    /// <param name="publication">Configuration of a producer</param>
    public SnsMessageProducer(AWSMessagingGatewayConnection connection, SnsPublication publication)
        : base(connection)
    {
        _publication = publication;
        _clientFactory = new AWSClientFactory(connection);

        if (publication.TopicArn != null)
        {
            ChannelTopicArn = publication.TopicArn;
        }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose() { }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public ValueTask DisposeAsync() => new();

    public bool ConfirmTopicExists(string? topic = null) =>
        BrighterAsyncContext.Run(async () => await ConfirmTopicExistsAsync(topic));

    public async Task<bool> ConfirmTopicExistsAsync(string? topic = null,
        CancellationToken cancellationToken = default)
    {
        //Only do this on first send for a topic for efficiency; won't auto-recreate when goes missing at runtime as a result
        if (!string.IsNullOrEmpty(ChannelTopicArn))
        {
            return true;
        }

        RoutingKey? routingKey = null;
        if (topic is not null)
        {
            routingKey = new RoutingKey(topic);
        }
        else if (_publication.Topic is not null)
        {
            routingKey = _publication.Topic;
        }

        if (RoutingKey.IsNullOrEmpty(routingKey))
        {
            throw new ConfigurationException("No topic specified for producer");
        }

        var topicArn = await EnsureTopicAsync(
            routingKey,
            _publication.FindTopicBy,
            _publication.TopicAttributes,
            _publication.MakeChannels,
            cancellationToken);

        return !string.IsNullOrEmpty(topicArn);
    }

    /// <summary>
    /// Sends the specified message.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">Allows cancellation of the Send operation</param>
    public async Task SendAsync(Message message, CancellationToken cancellationToken = default)
        => await SendWithDelayAsync(message, TimeSpan.Zero, cancellationToken);

    /// <summary>
    /// Sends the specified message.
    /// Sync over Async
    /// </summary>
    /// <param name="message">The message.</param>
    public void Send(Message message) => SendWithDelay(message, TimeSpan.Zero);

    /// <summary>
    /// Sends the specified message, with a delay.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="delay">The sending delay</param>
    /// <returns>Task.</returns>
    public void SendWithDelay(Message message, TimeSpan? delay = null)
        => BrighterAsyncContext.Run(async () => await SendWithDelayAsync(message, TimeSpan.Zero, false, CancellationToken.None));

    /// <summary>
    /// Sends the specified message, with a delay
    /// </summary>
    /// <param name="message">The message</param>
    /// <param name="delay">The sending delay</param>
    /// <param name="cancellationToken">Cancels the send operation</param>
    /// <exception cref="NotImplementedException"></exception>
    public async Task SendWithDelayAsync(Message message, TimeSpan? delay, CancellationToken cancellationToken = default)
        => await SendWithDelayAsync(message, delay, true, cancellationToken);

    private async Task SendWithDelayAsync(Message message, TimeSpan? delay, bool useAsyncScheduler, CancellationToken cancellationToken)
    {
        delay ??= TimeSpan.Zero;
        if (delay != TimeSpan.Zero)
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

        await ConfirmTopicExistsAsync(message.Header.Topic, cancellationToken);

        if (string.IsNullOrEmpty(ChannelAddress))
            throw new InvalidOperationException(
                $"Failed to publish message with topic {message.Header.Topic} and id {message.Id} and message: {message.Body} as the topic does not exist");

        using var client = _clientFactory.CreateSnsClient();
        var publisher = new SnsMessagePublisher(ChannelAddress!, client);
        var messageId = await publisher.PublishAsync(message);

        if (messageId == null)
            throw new InvalidOperationException(
                $"Failed to publish message with topic {message.Header.Topic} and id {message.Id} and message: {message.Body}");

        Log.PublishedMessage(s_logger, message.Header.Topic, message.Id, messageId);
    }

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Debug, "SNSMessageProducer: Publishing message with topic {Topic} and id {Id} and message: {Request}")]
        public static partial void PublishingMessage(ILogger logger, string topic, string id, MessageBody request);

        [LoggerMessage(LogLevel.Debug, "SNSMessageProducer: Published message with topic {Topic}, Brighter messageId {MessageId} and SNS messageId {SNSMessageId}")]
        public static partial void PublishedMessage(ILogger logger, string topic, string messageId, string snsMessageId);
    }
}
