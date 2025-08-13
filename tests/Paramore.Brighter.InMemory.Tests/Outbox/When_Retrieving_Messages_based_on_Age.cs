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
        var timeProvider = new FakeTimeProvider();
        var outbox = new InMemoryOutbox(timeProvider) { Tracer = new BrighterTracer(timeProvider) };

        var context = new RequestContext();
        outbox.Add(new MessageTestDataBuilder(), context);
        outbox.Add(new MessageTestDataBuilder(), context);
        
        timeProvider.Advance(TimeSpan.FromSeconds(5));
        
        outbox.Add(new MessageTestDataBuilder(), context);
        outbox.Add(new MessageTestDataBuilder(), context);

        var messagesToDispatch = outbox.OutstandingMessages(TimeSpan.FromMilliseconds(2000), context);
        var allMessages = outbox.OutstandingMessages(TimeSpan.Zero, context).ToArray();

        foreach (var message in allMessages)
        {
            outbox.MarkDispatched(message.Id, context);
        }

        var messagesAfterDispatch = outbox.OutstandingMessages(TimeSpan.Zero, context);

        Assert.Equal(2, messagesToDispatch.Count());
        Assert.Equal(4, allMessages.Length);
        Assert.Empty(messagesAfterDispatch);
    }
    
    [Fact]
    public async Task When_outstanding_in_outbox_they_are_retrieved_correctly_async()
    {
        var timeProvider = new FakeTimeProvider();
        var outbox = new InMemoryOutbox(timeProvider) { Tracer = new BrighterTracer(timeProvider) };

        var context = new RequestContext();
        await outbox.AddAsync(new MessageTestDataBuilder(), context);
        await outbox.AddAsync(new MessageTestDataBuilder(), context);
        
        timeProvider.Advance(TimeSpan.FromSeconds(5));
        
        await outbox.AddAsync(new MessageTestDataBuilder(), context);
        await outbox.AddAsync(new MessageTestDataBuilder(), context);

        var messagesToDispatch = await outbox.OutstandingMessagesAsync(TimeSpan.FromMilliseconds(2000), context);
        var allMessages = (await outbox.OutstandingMessagesAsync(TimeSpan.Zero, context)).ToArray();

        foreach (var message in allMessages)
        {
            await outbox.MarkDispatchedAsync(message.Id, context);
        }

        var messagesAfterDispatch = await outbox.OutstandingMessagesAsync(TimeSpan.Zero, context);

        Assert.Equal(2, messagesToDispatch.Count());
        Assert.Equal(4, allMessages.Length);
        Assert.Empty(messagesAfterDispatch);
    }
}
