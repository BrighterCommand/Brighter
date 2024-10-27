using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Paramore.Brighter.AzureServiceBus.Tests.Fakes;

public class FakeMessageProducer : IAmAMessageProducerAsync, IAmAMessageProducerSync
{
    public List<Message> SentMessages { get; } = new List<Message>();
    public Publication Publication { get; }
    public Activity Span { get; set; }
    public Task SendAsync(Message message)
    {
        Send(message);
        return Task.CompletedTask;
    }

    public void Send(Message message)
        => SentMessages.Add(message);

    public void SendWithDelay(Message message, TimeSpan? delay = null)
        => Send(message);
    
    public void Dispose()
    {
        // TODO release managed resources here
    }
}
