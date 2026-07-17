using System;
using System.Linq;
using Paramore.Brighter.TickerQ.Tests.TestDoubles;

namespace Paramore.Brighter.TickerQ.Tests;

[ClassDataSource<TickerQTestHost>(Shared = SharedType.PerAssembly)]
public class TickerQSchedulerMessageTests(TickerQTestHost host)
{
    [Test]
    public async Task When_scheduler_a_message_with_a_datetimeoffset_sync()
    {
        var message = host.BuildMessage<MyEventSync>();

        var scheduler = (IAmAMessageSchedulerSync)host.SchedulerFactory.Create(host.Processor);
        var id = scheduler.Schedule(message, host.TimeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));

        await Assert.That(id.Length).IsNotEqualTo(0);
        await Assert.That(host.BusContains<MyEventSync>(message.Id)).IsFalse();

        await host.EventuallyOnBus<MyEventSync>(message.Id);
        await Assert.That(await host.Outbox.GetAsync(message.Id, new RequestContext())).IsEqualTo(message);
    }

    [Test]
    public async Task When_scheduler_a_message_with_a_timespan_sync()
    {
        var message = host.BuildMessage<MyEventSync>();

        var scheduler = (IAmAMessageSchedulerSync)host.SchedulerFactory.Create(host.Processor);
        var id = scheduler.Schedule(message, TimeSpan.FromSeconds(1));

        await Assert.That(id.Length).IsNotEqualTo(0);
        await Assert.That(host.BusContains<MyEventSync>(message.Id)).IsFalse();

        await host.EventuallyOnBus<MyEventSync>(message.Id);
        await Assert.That(await host.Outbox.GetAsync(message.Id, new RequestContext())).IsEqualTo(message);
    }

    [Test]
    public async Task When_reschedule_a_message_with_a_datetimeoffset_sync()
    {
        var message = host.BuildMessage<MyEventSync>();

        var scheduler = (IAmAMessageSchedulerSync)host.SchedulerFactory.Create(host.Processor);
        var id = scheduler.Schedule(message, host.TimeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(2)));

        await Assert.That((id)?.Any()).IsTrue();
        await Assert.That(host.BusContains<MyEventSync>(message.Id)).IsFalse();

        scheduler.ReScheduler(id, host.TimeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(5)));

        await Task.Delay(TimeSpan.FromSeconds(3));
        await Assert.That(host.BusContains<MyEventSync>(message.Id)).IsFalse();

        await host.EventuallyOnBus<MyEventSync>(message.Id);
        await Assert.That(await host.Outbox.GetAsync(message.Id, new RequestContext())).IsEqualTo(message);
    }

    [Test]
    public async Task When_reschedule_a_message_with_a_timespan_sync()
    {
        var message = host.BuildMessage<MyEventSync>();

        var scheduler = (IAmAMessageSchedulerSync)host.SchedulerFactory.Create(host.Processor);
        var id = scheduler.Schedule(message, TimeSpan.FromSeconds(2));

        await Assert.That((id)?.Any()).IsTrue();
        await Assert.That(host.BusContains<MyEventSync>(message.Id)).IsFalse();

        scheduler.ReScheduler(id, TimeSpan.FromSeconds(5));

        await Task.Delay(TimeSpan.FromSeconds(3));
        await Assert.That(host.BusContains<MyEventSync>(message.Id)).IsFalse();

        await host.EventuallyOnBus<MyEventSync>(message.Id);
        await Assert.That(await host.Outbox.GetAsync(message.Id, new RequestContext())).IsEqualTo(message);
    }

    [Test]
    public async Task When_cancel_scheduler_message_with_a_datetimeoffset()
    {
        var message = host.BuildMessage<MyEventSync>();

        var scheduler = (IAmAMessageSchedulerSync)host.SchedulerFactory.Create(host.Processor);
        var id = scheduler.Schedule(message, TimeSpan.FromSeconds(2));

        await Assert.That(id.Length).IsNotEqualTo(0);
        scheduler.Cancel(id);

        await Task.Delay(TimeSpan.FromSeconds(3));

        await host.AssertOutboxEmptyForId(message.Id);
        await Assert.That(host.BusContains<MyEventSync>(message.Id)).IsFalse();
    }

    [Test]
    public async Task When_cancel_scheduler_request_with_a_timespan()
    {
        var message = host.BuildMessage<MyEventSync>();

        var scheduler = (IAmAMessageSchedulerSync)host.SchedulerFactory.Create(host.Processor);
        var id = scheduler.Schedule(message, TimeSpan.FromSeconds(2));

        await Assert.That(id.Length).IsNotEqualTo(0);
        scheduler.Cancel(id);

        await Task.Delay(TimeSpan.FromSeconds(3));

        await host.AssertOutboxEmptyForId(message.Id);
        await Assert.That(host.BusContains<MyEventSync>(message.Id)).IsFalse();
    }
}
