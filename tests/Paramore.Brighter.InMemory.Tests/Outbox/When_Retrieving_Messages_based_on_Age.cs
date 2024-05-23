using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter;
using Paramore.Brighter.InMemory.Tests.Builders;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Outbox;

[Trait("Category", "InMemory")]
public class When_Retrieving_Messages_based_on_Age
{
    [Fact]
    public void When_outstanding_in_outbox_they_are_retrieved_correctly()
    {
        var minimumAgeInMs = 500;
        var timeProvider = new FakeTimeProvider();
        var outbox = new InMemoryOutbox(timeProvider) { Tracer = new BrighterTracer(timeProvider) };

        var context = new RequestContext();
        outbox.Add(new MessageTestDataBuilder(), context);
        outbox.Add(new MessageTestDataBuilder(), context);
        
        timeProvider.Advance(TimeSpan.FromMilliseconds(minimumAgeInMs));
        
        outbox.Add(new MessageTestDataBuilder(), context);
        outbox.Add(new MessageTestDataBuilder(), context);

        var messagesToDispatch = outbox.OutstandingMessages(minimumAgeInMs, context);
        var allMessages = outbox.OutstandingMessages(0, context);

        foreach (var message in messagesToDispatch)
        {
            outbox.MarkDispatched(message.Id, context);
        }

        var messagesAfterDispatch = outbox.OutstandingMessages(minimumAgeInMs, context);

        Assert.Equal(2, messagesToDispatch.Count());
        Assert.Equal(4, allMessages.Count());
        Assert.Empty(messagesAfterDispatch);
    }
    
    [Fact]
    public async Task When_outstanding_in_outbox_they_are_retrieved_correctly_async()
    {
        var minimumAgeInMs = 1000;
        var timeProvider = new FakeTimeProvider();
        var outbox = new InMemoryOutbox(timeProvider) { Tracer = new BrighterTracer(timeProvider) };

        var context = new RequestContext();
        await outbox.AddAsync(new MessageTestDataBuilder(), context);
        await outbox.AddAsync(new MessageTestDataBuilder(), context);
        
        timeProvider.Advance(TimeSpan.FromMilliseconds(minimumAgeInMs * 2));
        
        await outbox.AddAsync(new MessageTestDataBuilder(), context);
        await outbox.AddAsync(new MessageTestDataBuilder(), context);

        var messagesToDispatch = await outbox.OutstandingMessagesAsync(minimumAgeInMs, context);
        var allMessages = await outbox.OutstandingMessagesAsync(0, context);

        foreach (var message in messagesToDispatch)
        {
            await outbox.MarkDispatchedAsync(message.Id, context);
        }

        var messagesAfterDispatch = await outbox.OutstandingMessagesAsync(minimumAgeInMs, context);

        Assert.Equal(2, messagesToDispatch.Count());
        Assert.Equal(4, allMessages.Count());
        Assert.Empty(messagesAfterDispatch);
    }
}
