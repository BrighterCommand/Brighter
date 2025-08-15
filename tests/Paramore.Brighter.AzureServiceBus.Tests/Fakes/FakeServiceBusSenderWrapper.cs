using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;
using Exception = System.Exception;

namespace Paramore.Brighter.AzureServiceBus.Tests.Fakes;

public class FakeServiceBusSenderWrapper : IServiceBusSenderWrapper
{

    public Dictionary<ServiceBusMessageBatch, List<ServiceBusMessage>> Batches { get; } = new();

    public List<ServiceBusMessage> SentMessages { get; } = new();
    
    public int ClosedCount { get; private set; } = 0;

    public Exception SendException { get; set; } = null;

    public Func<ServiceBusMessage, bool>? TryAddMessageCallBack { get; set; }

    public Task SendAsync(ServiceBusMessage message, CancellationToken cancellationToken = default)
        => Send(message);

    public Task ScheduleMessageAsync(ServiceBusMessage message, DateTimeOffset scheduleEnqueueTime,
        CancellationToken cancellationToken = default)
        => Send(message);

    public Task SendAsync(ServiceBusMessageBatch batch, CancellationToken cancellationToken = default)
    {
        foreach (var messageBatch in Batches[batch])
        {
            SentMessages.AddRange(messageBatch);
        }

        return Task.CompletedTask;
    }

    public ValueTask<ServiceBusMessageBatch> CreateMessageBatchAsync(
        CancellationToken cancellationToken = default)
    {
        if (SendException != null)
            throw SendException;

        long batchSize = 1048576;// 1MB in bytes
        var messagebatch = new List<ServiceBusMessage>();

        var batch = ServiceBusModelFactory.ServiceBusMessageBatch(
              batchSize, messagebatch, tryAddCallback: TryAddMessageCallBack);

        Batches.Add(batch, messagebatch);

        return new ValueTask<ServiceBusMessageBatch>(batch);
    }

    private Task Send(ServiceBusMessage message)
    {
        if (SendException != null)
            throw SendException;

        SentMessages.Add(message);

        return Task.CompletedTask;
    }

    public Task CloseAsync()
    {
        ClosedCount++;
        return Task.CompletedTask;
    }
}
