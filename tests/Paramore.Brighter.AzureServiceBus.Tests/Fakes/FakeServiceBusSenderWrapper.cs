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
    public List<ServiceBusMessage> SentMessages { get; } = new ();
    
    public int ClosedCount { get; private set; } = 0;

    public Exception SendException { get; set; } = null;

    public Task SendAsync(ServiceBusMessage message, CancellationToken cancellationToken = default)
        => SendAsync([message], cancellationToken);

    public Task SendAsync(ServiceBusMessage[] messages, CancellationToken cancellationToken = default)
    {
        if (SendException != null)
            throw SendException;
        SentMessages.AddRange(messages);
        return Task.CompletedTask;
    }

    public Task ScheduleMessageAsync(ServiceBusMessage message, DateTimeOffset scheduleEnqueueTime,
        CancellationToken cancellationToken = default)
        => SendAsync([message], cancellationToken);
    
    public Task CloseAsync()
    {
        ClosedCount++;
        return Task.CompletedTask;
    }
}
