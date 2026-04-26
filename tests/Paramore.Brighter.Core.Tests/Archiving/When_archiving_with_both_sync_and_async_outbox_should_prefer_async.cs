using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;

namespace Paramore.Brighter.Core.Tests.Archiving;

public class TimedOutboxArchiverPrefersAsyncTests
{
    [Test]
    public async Task When_archiving_with_both_sync_and_async_outbox_should_prefer_async()
    {
        //Arrange — InMemoryOutbox implements both IAmAnOutboxSync and IAmAnOutboxAsync
        var timeProvider = new FakeTimeProvider();
        var outbox = new InMemoryOutbox(timeProvider) { Tracer = new BrighterTracer() };
        var archiveProvider = new InMemoryArchiveProvider();
        var archiver = new OutboxArchiver<Message, CommittableTransaction>(outbox, archiveProvider);
        var distributedLock = new InMemoryLock();
        var options = new TimedOutboxArchiverOptions
        {
            TimerInterval = 5,
            MinimumAge = TimeSpan.FromMilliseconds(500)
        };
        var timedArchiver = new TimedOutboxArchiver<Message, CommittableTransaction>(archiver, distributedLock, options);

        var context = new RequestContext();
        var routingKey = new RoutingKey("test-topic");
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND),
            new MessageBody("test content")
        );
        await outbox.AddAsync(message, context);
        await outbox.MarkDispatchedAsync(message.Id, context);

        timeProvider.Advance(TimeSpan.FromSeconds(30));

        //Act
        using var cts = new CancellationTokenSource();
        await timedArchiver.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(2));
        await timedArchiver.StopAsync(cts.Token);

        //Assert
        await Assert.That(archiver.HasAsyncOutbox()).IsTrue().Because("Should have an async outbox");
        await Assert.That(archiver.HasOutbox()).IsTrue().Because("Should have a sync outbox");
        await Assert.That(outbox.EntryCount).IsZero();
        await Assert.That(
            archiveProvider.ArchivedMessages
        ).Contains(new KeyValuePair<string, Message>(message.Id, message));
    }
}
