using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.AzureServiceBus.Tests.Fakes;

public class FakeMessageProducer : IAmAMessageProducerAsync, IAmAMessageProducerSync
{
    public List<Message> SentMessages { get; } = new List<Message>();
    public Publication Publication { get; }
    public Activity Span { get; set; }
    public Task SendAsync(Message message, CancellationToken cancellationToken = default)
    {
        Send(message);
        return Task.CompletedTask;
    }

    public async Task SendWithDelayAsync(Message message, TimeSpan? delay, CancellationToken cancellationToken = default)
        => await SendAsync(message, cancellationToken);

    public void Send(Message message)
        => SentMessages.Add(message);

    public void SendWithDelay(Message message, TimeSpan? delay = null)
        => Send(message);
    
    public void Dispose()
    {
    }

    public ValueTask DisposeAsync()
    {
        return new ValueTask(Task.CompletedTask);
    }
}
