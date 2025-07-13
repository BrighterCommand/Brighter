using System.Collections.Concurrent;
using System.Text.Json;
using Amazon;
using Amazon.Scheduler;
using Amazon.Scheduler.Model;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Paramore.Brighter.Tasks;
using ResourceNotFoundException = Amazon.Scheduler.Model.ResourceNotFoundException;

namespace Paramore.Brighter.MessageScheduler.Aws;

/// <summary>
/// The AWS Scheduler implementation
/// </summary>
/// <param name="factory">The <see cref="AWSClientFactory"/>.</param>
/// <param name="timeProvider">The <see cref="System.TimeProvider"/>.</param>
/// <param name="getOrCreateMessageSchedulerId">The scheduler id generator for message.</param>
/// <param name="getOrCreateRequestSchedulerId">The scheduler id generator for request.</param>
/// <param name="scheduler">The <see cref="Scheduler"/> configuration</param>
/// <param name="schedulerGroup">The <see cref="SchedulerGroup"/> configuration</param>
public class AwsScheduler(
    AWSClientFactory factory,
    TimeProvider timeProvider,
    Func<Message, string> getOrCreateMessageSchedulerId,
    Func<IRequest, string> getOrCreateRequestSchedulerId,
    Scheduler scheduler,
    SchedulerGroup schedulerGroup) : IAmAMessageSchedulerAsync, IAmAMessageSchedulerSync, IAmARequestSchedulerAsync,
    IAmARequestSchedulerSync
{
    private static readonly ConcurrentDictionary<string, bool> s_checkedGroup = new();
    private static readonly ConcurrentDictionary<string, string?> s_queueUrl = new();
    private static readonly ConcurrentDictionary<string, string?> s_topic = new();

    /// <inheritdoc />
    public async Task<string> ScheduleAsync(Message message, DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        if (at < timeProvider.GetUtcNow())
        {
            throw new ArgumentOutOfRangeException(nameof(at), at, "Invalid at, it should be in the future");
        }

        return await ScheduleAsync(message, getOrCreateMessageSchedulerId(message), at, true, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> ScheduleAsync(Message message, TimeSpan delay,
        CancellationToken cancellationToken = default)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Invalid delay, it can't be negative");
        }

        return await ScheduleAsync(message, timeProvider.GetUtcNow().Add(delay), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> ScheduleAsync<TRequest>(TRequest request, RequestSchedulerType type, DateTimeOffset at,
        CancellationToken cancellationToken = default) where TRequest : class, IRequest
    {
        if (at < timeProvider.GetUtcNow())
        {
            throw new ArgumentOutOfRangeException(nameof(at), at, "Invalid at, it should be in the future");
        }

        var id = getOrCreateRequestSchedulerId(request);
        var message = new Message
        {
            Header =
                new MessageHeader(id, scheduler.SchedulerTopic, MessageType.MT_COMMAND,
                    subject: nameof(FireAwsScheduler)),
            Body = new MessageBody(JsonSerializer.Serialize(new FireAwsScheduler
            {
                SchedulerType = type,
                Async = true,
                RequestType = typeof(TRequest).FullName!,
                RequestData = JsonSerializer.Serialize(request, JsonSerialisationOptions.Options)
            }))
        };

        return await ScheduleAsync(message, id, at, true, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> ScheduleAsync<TRequest>(TRequest request, RequestSchedulerType type, TimeSpan delay,
        CancellationToken cancellationToken = default) where TRequest : class, IRequest
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Invalid delay, it can't be negative");
        }

        return await ScheduleAsync(request, type, timeProvider.GetUtcNow().Add(delay), cancellationToken);
    }

    /// <inheritdoc cref="IAmAMessageSchedulerAsync.ReSchedulerAsync(string,System.DateTimeOffset,System.Threading.CancellationToken)" />
    public async Task<bool> ReSchedulerAsync(string schedulerId, DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        if (at < timeProvider.GetUtcNow())
        {
            throw new ArgumentOutOfRangeException(nameof(at), at, "Invalid at, it should be in the future");
        }

        try
        {
            using var client = factory.CreateSchedulerClient();
            var res = await client.GetScheduleAsync(
                new GetScheduleRequest { Name = schedulerId, GroupName = schedulerGroup.Name }, cancellationToken);

            await client.UpdateScheduleAsync(
                new UpdateScheduleRequest
                {
                    Name = schedulerId,
                    GroupName = schedulerGroup.Name,
                    Target = res.Target,
                    ScheduleExpression = AtExpression(at),
                    ScheduleExpressionTimezone = "UTC",
                    State = ScheduleState.ENABLED,
                    ActionAfterCompletion = ActionAfterCompletion.DELETE,
                    FlexibleTimeWindow = scheduler.ToFlexibleTimeWindow()
                }, cancellationToken);

            return true;
        }
        catch (ResourceNotFoundException)
        {
            // Case the scheduler doesn't exist we are going to ignore it
            return false;
        }
    }

    /// <inheritdoc cref="IAmAMessageSchedulerAsync.ReSchedulerAsync(string,System.TimeSpan,System.Threading.CancellationToken)"/>
    public async Task<bool> ReSchedulerAsync(string schedulerId, TimeSpan delay,
        CancellationToken cancellationToken = default)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Invalid delay, it can't be negative");
        }

        return await ReSchedulerAsync(schedulerId, timeProvider.GetUtcNow().Add(delay), cancellationToken);
    }

    /// <inheritdoc cref="IAmAMessageSchedulerAsync.CancelAsync"/>
    public async Task CancelAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = factory.CreateSchedulerClient();
            await client.DeleteScheduleAsync(new DeleteScheduleRequest { Name = id, GroupName = schedulerGroup.Name },
                cancellationToken);
        }
        catch (ResourceNotFoundException)
        {
            // Case the scheduler doesn't exist we are going to ignore it
        }
    }

    private ValueTask EnsureGroupExistsAsync(CancellationToken cancellationToken)
    {
        if (schedulerGroup.MakeSchedulerGroup == OnMissingSchedulerGroup.Assume ||
            s_checkedGroup.ContainsKey(schedulerGroup.Name))
        {
            return new ValueTask();
        }

        return new ValueTask(CreateSchedulerGroup(cancellationToken));
    }

    private async Task CreateSchedulerGroup(CancellationToken cancellationToken)
    {
        var client = factory.CreateSchedulerClient();
        try
        {
            _ = await client.GetScheduleGroupAsync(new GetScheduleGroupRequest { Name = schedulerGroup.Name },
                cancellationToken);
        }
        catch (ResourceNotFoundException)
        {
            try
            {
                _ = await client.CreateScheduleGroupAsync(
                    new CreateScheduleGroupRequest { Name = schedulerGroup.Name, Tags = schedulerGroup.Tags },
                    cancellationToken);
            }
            catch (ConflictException)
            {
                // Ignoring due concurrency issue
            }
        }

        s_checkedGroup.TryAdd(schedulerGroup.Name, true);
    }

    private async Task<Target> CreateTargetAsync(string id, Message message, bool async)
    {
        var roleArn = scheduler.RoleArn;
        if (scheduler.UseMessageTopicAsTarget)
        {
            var topicArn = await GetTopicAsync(message);
            if (!string.IsNullOrEmpty(topicArn))
            {
                return new Target
                {
                    RoleArn = roleArn,
                    Arn = "arn:aws:scheduler:::aws-sdk:sns:publish",
                    Input = JsonSerializer.Serialize(ToPublishRequest(topicArn!, message))
                };
            }

            var queueUrl = await GetQueueAsync(message);
            if (!string.IsNullOrEmpty(queueUrl))
            {
                return new Target
                {
                    RoleArn = roleArn,
                    Arn = "arn:aws:scheduler:::aws-sdk:sqs:sendMessage",
                    Input = JsonSerializer.Serialize(ToSendMessageRequest(queueUrl!, message))
                };
            }
        }

        var schedulerMessage = message;
        if (message.Header.Subject != nameof(FireAwsScheduler))
        {
            schedulerMessage = new Message
            {
                Header =
                    new MessageHeader(id, scheduler.SchedulerTopic, MessageType.MT_COMMAND,
                        subject: nameof(FireAwsScheduler)),
                Body = new MessageBody(JsonSerializer.Serialize(
                    new FireAwsScheduler { Id = id, Async = async, Message = message },
                    JsonSerialisationOptions.Options))
            };
        }

        var messageSchedulerTopicArn = await GetTopicAsync(message);
        if (!string.IsNullOrEmpty(messageSchedulerTopicArn))
        {
            return new Target
            {
                RoleArn = roleArn,
                Arn = "arn:aws:scheduler:::aws-sdk:sns:publish",
                Input = JsonSerializer.Serialize(ToPublishRequest(messageSchedulerTopicArn!,
                    schedulerMessage))
            };
        }

        var messageSchedulerQueueUrl = await GetQueueAsync(message);
        if (!string.IsNullOrWhiteSpace(messageSchedulerQueueUrl))
        {
            return new Target
            {
                RoleArn = roleArn,
                Arn = "arn:aws:scheduler:::aws-sdk:sqs:sendMessage",
                Input = JsonSerializer.Serialize(ToSendMessageRequest(messageSchedulerQueueUrl!, schedulerMessage))
            };
        }

        throw new InvalidOperationException("Queue or Topic for Scheduler message not found");
    }

    private ValueTask<string?> GetTopicAsync(Message message)
    {
        if (s_topic.TryGetValue(message.Header.Topic, out var topicArn))
        {
            return new ValueTask<string?>(topicArn);
        }

        return new ValueTask<string?>(GetTopicArnAsync(message.Header.Topic));


        async Task<string?> GetTopicArnAsync(string topicName)
        {
            if (Arn.IsArn(topicName))
            {
                s_topic.TryAdd(topicName, topicName);
                return topicName;
            }

            using var client = factory.CreateSnsClient();
            var topic = await client.FindTopicAsync(topicName);
            s_topic.TryAdd(topicName, topic?.TopicArn);
            return topic?.TopicArn;
        }
    }

    private ValueTask<string?> GetQueueAsync(Message message)
    {
        if (s_queueUrl.TryGetValue(message.Header.Topic, out var queueUrl))
        {
            return new ValueTask<string?>(queueUrl);
        }

        return new ValueTask<string?>(GetQueueUrlAsync(message.Header.Topic));

        async Task<string?> GetQueueUrlAsync(string queueName)
        {
            if (Uri.TryCreate(queueName, UriKind.Absolute, out _))
            {
                s_queueUrl.TryAdd(queueName, queueName);
                return queueName;
            }

            using var client = factory.CreateSqsClient();
            try
            {
                var queue = await client.GetQueueUrlAsync(queueName);
                s_queueUrl.TryAdd(queueName, queue.QueueUrl);
                return queue.QueueUrl;
            }
            catch
            {
                s_queueUrl.TryAdd(queueName, null);
                return null;
            }
        }
    }

    private static object ToPublishRequest(string topicArn, Message message)
    {
        if (Id.IsNullOrEmpty(message.Header.CorrelationId))
        {
            message.Header.CorrelationId = Id.Random;
        }

        var messageAttributes = new Dictionary<string, object>
        {
            [HeaderNames.Id] = new { StringValue = message.Header.MessageId, DataType = "String" },
            [HeaderNames.Topic] = new { StringValue = topicArn, DataType = "String" },
            [HeaderNames.ContentType] = new { StringValue = message.Header.ContentType, DataType = "String" },
            [HeaderNames.HandledCount] =
                new { StringValue = Convert.ToString(message.Header.HandledCount), DataType = "String" },
            [HeaderNames.MessageType] =
                new { StringValue = message.Header.MessageType.ToString(), DataType = "String" },
            [HeaderNames.Timestamp] = new
            {
                StringValue = Convert.ToString(message.Header.TimeStamp), DataType = "String"
            },
            [HeaderNames.CorrelationId] = new
            {
                StringValue = Convert.ToString(message.Header.CorrelationId), DataType = "String"
            }
        };

        if (!RoutingKey.IsNullOrEmpty(message.Header.ReplyTo))
        {
            messageAttributes.Add(HeaderNames.ReplyTo,
                new { StringValue = Convert.ToString(message.Header.ReplyTo), DataType = "String" });
        }

        var bagJson = JsonSerializer.Serialize(message.Header.Bag, JsonSerialisationOptions.Options);
        messageAttributes[HeaderNames.Bag] = new { StringValue = Convert.ToString(bagJson), DataType = "String" };

        if (!topicArn.EndsWith(".fifo"))
        {
            return new
            {
                TopicArn = topicArn,
                message.Header.Subject,
                Message = message.Body.Value,
                MessageAttributes = messageAttributes
            };
        }

        if (message.Header.Bag.TryGetValue(HeaderNames.DeduplicationId, out var deduplicationId))
        {
            return new
            {
                TopicArn = topicArn,
                message.Header.Subject,
                Message = message.Body.Value,
                MessageAttributes = messageAttributes,
                MessageGroupId = message.Header.PartitionKey,
                MessageDeduplicationId = deduplicationId
            };
        }

        return new
        {
            TopicArn = topicArn,
            message.Header.Subject,
            Message = message.Body.Value,
            MessageAttributes = messageAttributes,
            MessageGroupId = message.Header.PartitionKey,
        };
    }

    private static object ToSendMessageRequest(string queueUrl, Message message)
    {
        var messageAttributes = new Dictionary<string, object>
        {
            [HeaderNames.Id] = new { StringValue = message.Header.MessageId, DataType = "String" },
            [HeaderNames.Topic] = new { StringValue = queueUrl, DataType = "String" },
            [HeaderNames.ContentType] = new { StringValue = message.Header.ContentType, DataType = "String" },
            [HeaderNames.HandledCount] =
                new { StringValue = Convert.ToString(message.Header.HandledCount), DataType = "String" },
            [HeaderNames.MessageType] =
                new { StringValue = message.Header.MessageType.ToString(), DataType = "String" },
            [HeaderNames.Timestamp] = new
            {
                StringValue = Convert.ToString(message.Header.TimeStamp), DataType = "String"
            }
        };

        if (!RoutingKey.IsNullOrEmpty(message.Header.ReplyTo))
        {
            messageAttributes.Add(HeaderNames.ReplyTo,
                new { StringValue = message.Header.ReplyTo, DataType = "String" });
        }

        if (!string.IsNullOrEmpty(message.Header.Subject))
        {
            messageAttributes.Add(HeaderNames.Subject,
                new { StringValue = message.Header.Subject, DataType = "String" });
        }

        if (!string.IsNullOrEmpty(message.Header.CorrelationId))
        {
            messageAttributes.Add(HeaderNames.CorrelationId,
                new { StringValue = message.Header.CorrelationId, DataType = "String" });
        }

        // we can set up to 10 attributes; we have set 6 above, so use a single JSON object as the bag
        var bagJson = JsonSerializer.Serialize(message.Header.Bag, JsonSerialisationOptions.Options);
        messageAttributes[HeaderNames.Bag] = new { StringValue = bagJson, DataType = "String" };
        if (!queueUrl.EndsWith(".fifo"))
        {
            return new { QueueUrl = queueUrl, MessageAttributes = messageAttributes, MessageBody = message.Body.Value };
        }

        if (message.Header.Bag.TryGetValue(HeaderNames.DeduplicationId, out var deduplicationId))
        {
            return new
            {
                QueueUrl = queueUrl,
                MessageAttributes = messageAttributes,
                MessageBody = message.Body.Value,
                MessageGroupId = message.Header.PartitionKey,
                MessageDeduplicationId = deduplicationId
            };
        }

        return new
        {
            QueueUrl = queueUrl,
            MessageAttributes = messageAttributes,
            MessageBody = message.Body.Value,
            MessageGroupId = message.Header.PartitionKey,
        };
    }

    private async Task<string> ScheduleAsync(
        Message message,
        string id,
        DateTimeOffset at,
        bool async,
        CancellationToken cancellationToken = default)
    {
        await EnsureGroupExistsAsync(cancellationToken);
        var target = await CreateTargetAsync(id, message, async);

        using var client = factory.CreateSchedulerClient();
        try
        {
            await client.CreateScheduleAsync(
                new CreateScheduleRequest
                {
                    Name = id,
                    GroupName = schedulerGroup.Name,
                    Target = target,
                    ScheduleExpression = AtExpression(at),
                    ScheduleExpressionTimezone = "UTC",
                    State = ScheduleState.ENABLED,
                    ActionAfterCompletion = ActionAfterCompletion.DELETE,
                    FlexibleTimeWindow = scheduler.ToFlexibleTimeWindow()
                }, cancellationToken);
        }
        catch (ConflictException)
        {
            if (scheduler.OnConflict == OnSchedulerConflict.Throw)
            {
                throw;
            }

            await client.UpdateScheduleAsync(
                new UpdateScheduleRequest
                {
                    Name = id,
                    GroupName = schedulerGroup.Name,
                    Target = target,
                    ScheduleExpression = AtExpression(at),
                    ScheduleExpressionTimezone = "UTC",
                    State = ScheduleState.ENABLED,
                    ActionAfterCompletion = ActionAfterCompletion.DELETE,
                    FlexibleTimeWindow = scheduler.ToFlexibleTimeWindow()
                }, cancellationToken);
        }

        return id;
    }

    private static string AtExpression(DateTimeOffset publishAt)
        => $"at({publishAt.ToUniversalTime():yyyy-MM-ddTHH:mm:ss})";

    /// <inheritdoc />
    public string Schedule(Message message, DateTimeOffset at)
        => BrighterAsyncContext.Run(async () =>
            await ScheduleAsync(message, getOrCreateMessageSchedulerId(message), at, false));

    /// <inheritdoc />
    public string Schedule(Message message, TimeSpan delay)
        => Schedule(message, timeProvider.GetUtcNow().Add(delay));

    /// <inheritdoc />
    public string Schedule<TRequest>(TRequest request, RequestSchedulerType type, DateTimeOffset at)
        where TRequest : class, IRequest
    {
        var id = getOrCreateRequestSchedulerId(request);

        var message = new Message
        {
            Header =
                new MessageHeader(id, scheduler.SchedulerTopic, MessageType.MT_COMMAND,
                    subject: nameof(FireAwsScheduler)),
            Body = new MessageBody(JsonSerializer.Serialize(new FireAwsScheduler
            {
                SchedulerType = type,
                Async = false,
                RequestType = typeof(TRequest).FullName!,
                RequestData = JsonSerializer.Serialize(request, JsonSerialisationOptions.Options)
            }))
        };

        return BrighterAsyncContext.Run(async () => await ScheduleAsync(message, id, at, false));
    }

    /// <inheritdoc />
    public string Schedule<TRequest>(TRequest request, RequestSchedulerType type, TimeSpan delay)
        where TRequest : class, IRequest
        => Schedule(request, type, timeProvider.GetUtcNow().Add(delay));

    /// <inheritdoc cref="IAmAMessageSchedulerSync.ReScheduler(string,System.DateTimeOffset)"/>
    public bool ReScheduler(string schedulerId, DateTimeOffset at)
        => BrighterAsyncContext.Run(async () => await ReSchedulerAsync(schedulerId, at));

    /// <inheritdoc cref="IAmAMessageSchedulerSync.ReScheduler(string,System.TimeSpan)"/>
    public bool ReScheduler(string schedulerId, TimeSpan delay)
        => ReScheduler(schedulerId, timeProvider.GetUtcNow().Add(delay));

    /// <inheritdoc cref="IAmAMessageSchedulerSync.Cancel" />
    public void Cancel(string id)
        => BrighterAsyncContext.Run(async () => await CancelAsync(id));
}
