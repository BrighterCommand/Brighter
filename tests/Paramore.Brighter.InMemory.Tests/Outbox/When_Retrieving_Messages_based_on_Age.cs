using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter;
using Paramore.Brighter.InMemory.Tests.Builders;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Outbox;

[Trait("Category", "InMemory")]
public class When_Retrieving_Messages_based_on_Age
{
    [Fact]
    public void When_outstanding_in_outbox_they_are_retrieved_correctly()
    {
        var minimumAgeInMs = 500;
        var outbox = new InMemoryOutbox();

        outbox.Add(new MessageTestDataBuilder());
        outbox.Add(new MessageTestDataBuilder());

        Thread.Sleep(minimumAgeInMs);

        outbox.Add(new MessageTestDataBuilder());
        outbox.Add(new MessageTestDataBuilder());

        var messagesToDispatch = outbox.OutstandingMessages(minimumAgeInMs);
        var allMessages = outbox.OutstandingMessages(0);

        foreach (var message in messagesToDispatch)
        {
            outbox.MarkDispatched(message.Id);
        }

        var messagesAfterDispatch = outbox.OutstandingMessages(minimumAgeInMs);

        Assert.Equal(2, messagesToDispatch.Count());
        Assert.Equal(4, allMessages.Count());
        Assert.Empty(messagesAfterDispatch);
    }

    [Fact]
    public async Task When_outstanding_in_outbox_they_are_retrieved_correctly_async()
    {
        var minimumAgeInMs = 1000;
        var outbox = new InMemoryOutbox();

        await outbox.AddAsync(new MessageTestDataBuilder());
        await outbox.AddAsync(new MessageTestDataBuilder());

        await Task.Delay(minimumAgeInMs * 2);

        await outbox.AddAsync(new MessageTestDataBuilder());
        await outbox.AddAsync(new MessageTestDataBuilder());

        var messagesToDispatch = await outbox.OutstandingMessagesAsync(minimumAgeInMs);
        var allMessages = await outbox.OutstandingMessagesAsync(0);

        foreach (var message in messagesToDispatch)
        {
            await outbox.MarkDispatchedAsync(message.Id);
        }

        var messagesAfterDispatch = await outbox.OutstandingMessagesAsync(minimumAgeInMs);

        Assert.Equal(2, messagesToDispatch.Count());
        Assert.Equal(4, allMessages.Count());
        Assert.Empty(messagesAfterDispatch);
    }
}
