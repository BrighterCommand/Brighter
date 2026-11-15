using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.Archiving.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;

namespace Paramore.Brighter.Core.Tests.Archiving;

public class TimedOutboxArchiverAsyncOnlyTests
{
    [Test]
    public async Task When_archiving_with_async_only_outbox_should_call_async_archive()
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        var innerOutbox = new InMemoryOutbox(timeProvider) { Tracer = new BrighterTracer() };
        var asyncOnlyOutbox = new AsyncOnlyOutboxWrapper(innerOutbox);
        var archiveProvider = new InMemoryArchiveProvider();
        var archiver = new OutboxArchiver<Message, CommittableTransaction>(asyncOnlyOutbox, archiveProvider);
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
        await innerOutbox.AddAsync(message, context);
        await innerOutbox.MarkDispatchedAsync(message.Id, context);

        timeProvider.Advance(TimeSpan.FromSeconds(30));

        //Act
        using var cts = new CancellationTokenSource();
        await timedArchiver.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(2));
        await timedArchiver.StopAsync(cts.Token);

        //Assert
        await Assert.That(archiver.HasAsyncOutbox()).IsTrue().Because("Should have an async outbox");
        await Assert.That(archiver.HasOutbox()).IsFalse().Because("Should not have a sync outbox");
        await Assert.That(innerOutbox.EntryCount).IsZero();
        await Assert.That(
            archiveProvider.ArchivedMessages
        ).Contains(new KeyValuePair<string, Message>(message.Id, message));
    }
}
