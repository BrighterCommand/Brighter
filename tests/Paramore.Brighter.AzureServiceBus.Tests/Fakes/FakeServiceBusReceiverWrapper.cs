using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;

namespace Paramore.Brighter.AzureServiceBus.Tests.Fakes;

public class FakeServiceBusReceiverWrapper : IServiceBusReceiverWrapper
{
    public List<IBrokeredMessageWrapper> MessageQueue { get; set; } = new();

    public Exception DeadLetterException = null;
    public Exception CompleteException = null;
    public Exception ReceiveException = null;
    
    public Task<IEnumerable<IBrokeredMessageWrapper>> ReceiveAsync(int batchSize, TimeSpan serverWaitTime)
    {
        if (IsClosedOrClosing)
            throw new Exception("Connection not Open");
        if (ReceiveException != null)
            throw ReceiveException;
        
        var messages = new List<IBrokeredMessageWrapper>();
        for (var i = 0; i < batchSize; i++)
        {
            if (!MessageQueue.Any())
                return Task.FromResult(messages.AsEnumerable());
            messages.Add(MessageQueue[0]);
            MessageQueue.RemoveAt(0);
        }

        return Task.FromResult(messages.AsEnumerable());
    }

    public Task CompleteAsync(string lockToken)
    {
        if (CompleteException != null)
            throw CompleteException;

        return Task.CompletedTask;
    }

    public Task AbandonAsync(string lockToken)
    {
        return Task.CompletedTask;
    }

    public Task DeadLetterAsync(string lockToken)
    {
        if (DeadLetterException != null)
            throw DeadLetterException;

        return Task.CompletedTask;
    }

    public void Close()
        => IsClosedOrClosing = true;

    public Task CloseAsync()
    {
        Close();
        return Task.CompletedTask;
    }

    public bool IsClosedOrClosing { get; private set; } = false;
}
