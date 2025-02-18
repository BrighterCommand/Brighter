using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessageScheduler.Azure;

/// <summary>
/// The Azure Scheduler
/// </summary>
/// <param name="sender">The <see cref="AzureServiceBusScheduler"/>.</param>
/// <param name="schedulerTopic">The scheduler topic or queue</param>
/// <param name="timeProvider">The <see cref="TimeProvider"/>.</param>
public class AzureServiceBusScheduler(
    ServiceBusSender sender,
    RoutingKey schedulerTopic,
    TimeProvider timeProvider)
    : IAmAMessageSchedulerAsync, IAmAMessageSchedulerSync, IAmARequestSchedulerAsync, IAmARequestSchedulerSync
{
    private static readonly ILogger Logger = ApplicationLogging.CreateLogger<AzureServiceBusScheduler>();

    /// <inheritdoc />
    public async Task<string> ScheduleAsync(Message message, DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        var seq = await sender.ScheduleMessageAsync(
            ConvertToServiceBusMessage(new Message
            {
                Header =
                    new MessageHeader(message.Id, schedulerTopic, MessageType.MT_EVENT,
                        subject: nameof(FireAzureScheduler)),
                Body = new MessageBody(JsonSerializer.Serialize(
                    new FireAzureScheduler { Id = message.Id, Async = true, Message = message },
                    JsonSerialisationOptions.Options))
            }), at, cancellationToken);

        return seq.ToString();
    }

    /// <inheritdoc />
    public async Task<string> ScheduleAsync(Message message, TimeSpan delay,
        CancellationToken cancellationToken = default)
        => await ScheduleAsync(message, timeProvider.GetUtcNow().ToOffset(delay), cancellationToken);

    /// <inheritdoc />
    public async Task<string> ScheduleAsync<TRequest>(TRequest request, RequestSchedulerType type, DateTimeOffset at,
        CancellationToken cancellationToken = default) where TRequest : class, IRequest
    {
        var id = Guid.NewGuid().ToString();
        var seq = await sender.ScheduleMessageAsync(
            ConvertToServiceBusMessage(new Message
            {
                Header =
                    new MessageHeader(id, schedulerTopic, MessageType.MT_EVENT,
                        subject: nameof(FireAzureScheduler)),
                Body = new MessageBody(JsonSerializer.Serialize(
                    new FireAzureScheduler
                    {
                        Id = id,
                        Async = true,
                        SchedulerType = type,
                        RequestType = typeof(TRequest).FullName!,
                        RequestData = JsonSerializer.Serialize(request, JsonSerialisationOptions.Options)
                    }, JsonSerialisationOptions.Options))
            }), at, cancellationToken);
        return seq.ToString();
    }

    /// <inheritdoc />
    public async Task<string> ScheduleAsync<TRequest>(TRequest request, RequestSchedulerType type, TimeSpan delay,
        CancellationToken cancellationToken = default)
        where TRequest : class, IRequest
        => await ScheduleAsync(request, type, timeProvider.GetUtcNow().ToOffset(delay), cancellationToken);

    /// <inheritdoc cref="IAmAMessageSchedulerAsync.ReSchedulerAsync(string,System.DateTimeOffset,System.Threading.CancellationToken)"/>
    public Task<bool> ReSchedulerAsync(string schedulerId, DateTimeOffset at,
        CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    /// <inheritdoc cref="IAmAMessageSchedulerAsync.ReSchedulerAsync(string,System.TimeSpan,System.Threading.CancellationToken)"/>
    public Task<bool> ReSchedulerAsync(string schedulerId, TimeSpan delay,
        CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    /// <inheritdoc cref="IAmAMessageSchedulerAsync.CancelAsync"/>
    public async Task CancelAsync(string id, CancellationToken cancellationToken = default)
    {
        if (long.TryParse(id, out var seq))
        {
            await sender.CancelScheduledMessageAsync(seq, cancellationToken);
        }
        else
        {
            Logger.LogWarning("Could not cancel message as schedulerId is not a sequence number");
        }
    }

    private static ServiceBusMessage ConvertToServiceBusMessage(Message message)
    {
        var azureServiceBusMessage = new ServiceBusMessage(message.Body.Bytes);
        azureServiceBusMessage.ApplicationProperties.Add(ASBConstants.MessageTypeHeaderBagKey,
            message.Header.MessageType.ToString());
        azureServiceBusMessage.ApplicationProperties.Add(ASBConstants.HandledCountHeaderBagKey,
            message.Header.HandledCount);
        azureServiceBusMessage.ApplicationProperties.Add(ASBConstants.ReplyToHeaderBagKey, message.Header.ReplyTo);

        foreach (var header in message.Header.Bag.Where(h => !ASBConstants.ReservedHeaders.Contains(h.Key)))
        {
            azureServiceBusMessage.ApplicationProperties.Add(header.Key, header.Value);
        }

        if (!string.IsNullOrEmpty(message.Header.CorrelationId))
        {
            azureServiceBusMessage.CorrelationId = message.Header.CorrelationId;
        }
        
        azureServiceBusMessage.ContentType = message.Header.ContentType;
        azureServiceBusMessage.MessageId = message.Header.MessageId;
        if (message.Header.Bag.TryGetValue(ASBConstants.SessionIdKey, out object? value))
            azureServiceBusMessage.SessionId = value.ToString();

        return azureServiceBusMessage;
    }

    internal static class ASBConstants
    {
        public const string LockTokenHeaderBagKey = "LockToken";
        public const string MessageTypeHeaderBagKey = "MessageType";
        public const string HandledCountHeaderBagKey = "HandledCount";
        public const string ReplyToHeaderBagKey = "ReplyTo";
        public const string SessionIdKey = "SessionId";

        public static readonly string[] ReservedHeaders =
            new[]
            {
                LockTokenHeaderBagKey, MessageTypeHeaderBagKey, HandledCountHeaderBagKey, ReplyToHeaderBagKey,
                SessionIdKey
            };
    }

    /// <inheritdoc />
    public string Schedule(Message message, DateTimeOffset at)
    {
        var seq = BrighterAsyncContext.Run(async () => await sender.ScheduleMessageAsync(
            ConvertToServiceBusMessage(new Message
            {
                Header =
                    new MessageHeader(message.Id, schedulerTopic, MessageType.MT_EVENT,
                        subject: nameof(FireAzureScheduler)),
                Body = new MessageBody(JsonSerializer.Serialize(
                    new FireAzureScheduler { Id = message.Id, Async = true, Message = message },
                    JsonSerialisationOptions.Options))
            }), at));

        return seq.ToString();
    }

    /// <inheritdoc />
    public string Schedule(Message message, TimeSpan delay) =>
        Schedule(message, timeProvider.GetUtcNow().ToOffset(delay));

    /// <inheritdoc />
    public string Schedule<TRequest>(TRequest request, RequestSchedulerType type, DateTimeOffset at)
        where TRequest : class, IRequest
    {
        var id = Guid.NewGuid().ToString();
        var seq = BrighterAsyncContext.Run(async () => await sender.ScheduleMessageAsync(
            ConvertToServiceBusMessage(new Message
            {
                Header =
                    new MessageHeader(id, schedulerTopic, MessageType.MT_EVENT,
                        subject: nameof(FireAzureScheduler)),
                Body = new MessageBody(JsonSerializer.Serialize(
                    new FireAzureScheduler
                    {
                        Id = id,
                        Async = true,
                        SchedulerType = type,
                        RequestType = typeof(TRequest).FullName!,
                        RequestData = JsonSerializer.Serialize(request, JsonSerialisationOptions.Options)
                    }, JsonSerialisationOptions.Options))
            }), at));
        return seq.ToString();
    }

    /// <inheritdoc />
    public string Schedule<TRequest>(TRequest request, RequestSchedulerType type, TimeSpan delay)
        where TRequest : class, IRequest
        => Schedule(request, type, timeProvider.GetUtcNow().ToOffset(delay));

    /// <inheritdoc cref="IAmAMessageSchedulerSync.ReScheduler(string,System.DateTimeOffset)"/>
    public bool ReScheduler(string schedulerId, DateTimeOffset at)
        => BrighterAsyncContext.Run(async () => await ReSchedulerAsync(schedulerId, at));

    /// <inheritdoc cref="IAmAMessageSchedulerSync.ReScheduler(string,System.TimeSpan)"/>
    public bool ReScheduler(string schedulerId, TimeSpan delay)
        => ReScheduler(schedulerId, timeProvider.GetUtcNow().ToOffset(delay));

    /// <inheritdoc cref="IAmAMessageSchedulerSync.Cancel"/>
    public void Cancel(string id)
        => BrighterAsyncContext.Run(async () => await CancelAsync(id));
}
