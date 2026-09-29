using System;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Paramore.Brighter.Core.Tests.Archiving.TestDoubles;
using Paramore.Brighter.Outbox.Hosting;

namespace Paramore.Brighter.Core.Tests.Archiving;

public class TimedOutboxArchiverNoOutboxTests
{
    [Test]
    public async Task When_archiving_with_no_outbox_configured_should_not_throw()
    {
        //Arrange — NullOutbox implements only IAmAnOutbox (neither sync nor async)
        var nullOutbox = new NullOutbox();
        var archiveProvider = new InMemoryArchiveProvider();
        var archiver = new OutboxArchiver<Message, CommittableTransaction>(nullOutbox, archiveProvider);
        var distributedLock = new InMemoryLock();
        var options = new TimedOutboxArchiverOptions
        {
            TimerInterval = 5,
            MinimumAge = TimeSpan.FromMilliseconds(500)
        };
        var timedArchiver = new TimedOutboxArchiver<Message, CommittableTransaction>(archiver, distributedLock, options);

        //Act — should complete without throwing (FR3)
        using var cts = new CancellationTokenSource();
        await timedArchiver.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(2));
        await timedArchiver.StopAsync(cts.Token);

        //Assert
        await Assert.That(archiver.HasAsyncOutbox()).IsFalse().Because("Should not have an async outbox");
        await Assert.That(archiver.HasOutbox()).IsFalse().Because("Should not have a sync outbox");
        await Assert.That(archiveProvider.ArchivedMessages).IsEmpty();
    }
}
