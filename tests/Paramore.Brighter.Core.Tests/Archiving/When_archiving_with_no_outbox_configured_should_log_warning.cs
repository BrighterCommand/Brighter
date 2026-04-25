using System;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Paramore.Brighter.Core.Tests.Archiving.TestDoubles;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Archiving;

public class TimedOutboxArchiverNoOutboxTests
{
    [Fact]
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
        Assert.False(archiver.HasAsyncOutbox(), "Should not have an async outbox");
        Assert.False(archiver.HasOutbox(), "Should not have a sync outbox");
        Assert.Empty(archiveProvider.ArchivedMessages);
    }
}
