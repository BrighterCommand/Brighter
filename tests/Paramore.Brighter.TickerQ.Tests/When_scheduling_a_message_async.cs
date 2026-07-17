using System;
using System.Linq;
using Paramore.Brighter.TickerQ.Tests.TestDoubles;

namespace Paramore.Brighter.TickerQ.Tests;

[ClassDataSource<TickerQTestHost>(Shared = SharedType.PerAssembly)]
public class TickerQSchedulerMessageAsyncTests(TickerQTestHost host)
{
    [Test]
    public async Task When_scheduler_a_message_with_a_datetimeoffset_async()
    {
        var message = host.BuildMessage<MyEvent>();

        var scheduler = (IAmAMessageSchedulerAsync)host.SchedulerFactory.Create(host.Processor);
        var id = await scheduler.ScheduleAsync(message,
            host.TimeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));

        await Assert.That(id.Length).IsNotEqualTo(0);
        await Assert.That(host.BusContains<MyEvent>(message.Id)).IsFalse();

        await host.EventuallyOnBus<MyEvent>(message.Id);
        await Assert.That(await host.Outbox.GetAsync(message.Id, new RequestContext())).IsEqualTo(message);
    }

    [Test]
    public async Task When_scheduler_a_message_with_a_timespan_async()
    {
        var message = host.BuildMessage<MyEvent>();

        var scheduler = (IAmAMessageSchedulerAsync)host.SchedulerFactory.Create(host.Processor);
        var id = await scheduler.ScheduleAsync(message, TimeSpan.FromSeconds(4));

        await Assert.That(id.Length).IsNotEqualTo(0);
        await Assert.That(host.BusContains<MyEvent>(message.Id)).IsFalse();

        await host.EventuallyOnBus<MyEvent>(message.Id);
        await Assert.That(await host.Outbox.GetAsync(message.Id, new RequestContext())).IsEqualTo(message);
    }

    [Test]
    public async Task When_reschedule_a_message_with_a_datetimeoffset_async()
    {
        var message = host.BuildMessage<MyEvent>();

        var scheduler = (IAmAMessageSchedulerAsync)host.SchedulerFactory.Create(host.Processor);
        var id = await scheduler.ScheduleAsync(message, host.TimeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(2)));

        await Assert.That((id)?.Any()).IsTrue();
        await Assert.That(host.BusContains<MyEvent>(message.Id)).IsFalse();
        await scheduler.ReSchedulerAsync(id, host.TimeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(5)));

        await Task.Delay(TimeSpan.FromSeconds(3));
        await Assert.That(host.BusContains<MyEvent>(message.Id)).IsFalse();

        await host.EventuallyOnBus<MyEvent>(message.Id);
        await Assert.That(await host.Outbox.GetAsync(message.Id, new RequestContext())).IsEqualTo(message);
    }

    [Test]
    public async Task When_reschedule_a_message_with_a_timespan_async()
    {
        var message = host.BuildMessage<MyEvent>();

        var scheduler = (IAmAMessageSchedulerAsync)host.SchedulerFactory.Create(host.Processor);
        var id = await scheduler.ScheduleAsync(message, host.TimeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(2)));

        await Assert.That((id)?.Any()).IsTrue();
        await Assert.That(host.BusContains<MyEvent>(message.Id)).IsFalse();
        var reScheduled = await scheduler.ReSchedulerAsync(id, TimeSpan.FromSeconds(5));
        await Assert.That(reScheduled).IsTrue();

        await Task.Delay(TimeSpan.FromSeconds(3));
        await Assert.That(host.BusContains<MyEvent>(message.Id)).IsFalse();

        await host.EventuallyOnBus<MyEvent>(message.Id);
        await Assert.That(await host.Outbox.GetAsync(message.Id, new RequestContext())).IsEqualTo(message);
    }

    [Test]
    public async Task When_cancel_scheduler_message_with_a_datetimeoffset_async()
    {
        var message = host.BuildMessage<MyEvent>();

        var scheduler = (IAmAMessageSchedulerAsync)host.SchedulerFactory.Create(host.Processor);
        var id = await scheduler.ScheduleAsync(message, TimeSpan.FromHours(1));

        await Assert.That((id)?.Any()).IsTrue();
        await scheduler.CancelAsync(id);

        await Task.Delay(TimeSpan.FromSeconds(2));

        await host.AssertOutboxEmptyForId(message.Id);
        await Assert.That(host.BusContains<MyEvent>(message.Id)).IsFalse();
    }

    [Test]
    public async Task When_cancel_scheduler_request_with_a_timespan_async()
    {
        var message = host.BuildMessage<MyEvent>();

        var scheduler = (IAmAMessageSchedulerAsync)host.SchedulerFactory.Create(host.Processor);
        var id = await scheduler.ScheduleAsync(message, TimeSpan.FromHours(1));

        await Assert.That((id)?.Any()).IsTrue();
        await scheduler.CancelAsync(id);

        await Task.Delay(TimeSpan.FromSeconds(2));

        await host.AssertOutboxEmptyForId(message.Id);
        await Assert.That(host.BusContains<MyEvent>(message.Id)).IsFalse();
    }
}
