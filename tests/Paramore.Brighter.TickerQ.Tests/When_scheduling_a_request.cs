using System;
using System.Linq;
using Paramore.Brighter.TickerQ.Tests.TestDoubles;

namespace Paramore.Brighter.TickerQ.Tests
{
    [ClassDataSource<TickerQTestHost>(Shared = SharedType.PerAssembly)]
    public class TickerQSchedulerRequestTests(TickerQTestHost host)
    {
        #region Scheduler

        [Test]
        public async Task When_scheduler_send_request_with_a_datetimeoffset()
        {
            var req = new MyEventSync();
            var scheduler = host.SchedulerFactory.CreateSync(host.Processor);
            var id = scheduler.Schedule(req, RequestSchedulerType.Send,
                host.TimeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));

            await Assert.That((id)?.Any()).IsTrue();
            await Assert.That(host.ReceivedMessages).DoesNotContainKey(req.Id);

            await host.EventuallyHandled(req.Id);
            await host.AssertOutboxEmptyForId(req.Id);
        }

        [Test]
        public async Task When_scheduler_send_request_with_a_timespan()
        {
            var req = new MyEventSync();
            var scheduler = host.SchedulerFactory.CreateSync(host.Processor);
            var id = scheduler.Schedule(req, RequestSchedulerType.Send, TimeSpan.FromSeconds(1));

            await Assert.That((id)?.Any()).IsTrue();
            await Assert.That(host.ReceivedMessages).DoesNotContainKey(req.Id);

            await host.EventuallyHandled(req.Id);
            await host.AssertOutboxEmptyForId(req.Id);
        }

        [Test]
        public async Task When_scheduler_publish_request_with_a_datetimeoffset()
        {
            var req = new MyEventSync();
            var scheduler = host.SchedulerFactory.CreateSync(host.Processor);
            var id = scheduler.Schedule(req, RequestSchedulerType.Publish,
                host.TimeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));

            await Assert.That((id)?.Any()).IsTrue();
            await Assert.That(host.ReceivedMessages).DoesNotContainKey(req.Id);

            await host.EventuallyHandled(req.Id);
            await host.AssertOutboxEmptyForId(req.Id);
        }

        [Test]
        public async Task When_scheduler_publish_request_with_a_timespan()
        {
            var req = new MyEventSync();
            var scheduler = host.SchedulerFactory.CreateSync(host.Processor);
            var id = scheduler.Schedule(req, RequestSchedulerType.Publish, TimeSpan.FromSeconds(1));

            await Assert.That((id)?.Any()).IsTrue();
            await Assert.That(host.ReceivedMessages).DoesNotContainKey(req.Id);

            await host.EventuallyHandled(req.Id);
            await host.AssertOutboxEmptyForId(req.Id);
        }

        [Test]
        public async Task When_scheduler_post_request_with_a_datetimeoffset()
        {
            var req = new MyEventSync();
            var scheduler = host.SchedulerFactory.CreateSync(host.Processor);
            var id = scheduler.Schedule(req, RequestSchedulerType.Post,
                host.TimeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));

            await Assert.That((id)?.Any()).IsTrue();
            await Assert.That(host.BusContains<MyEventSync>(req.Id)).IsFalse();

            await host.EventuallyOnBus<MyEventSync>(req.Id);
            await Assert.That(await host.Outbox.GetAsync(req.Id, new RequestContext())).IsNotEqualTo(Message.Empty);
        }

        [Test]
        public async Task When_scheduler_post_request_with_a_timespan()
        {
            var req = new MyEventSync();
            var scheduler = host.SchedulerFactory.CreateSync(host.Processor);
            var id = scheduler.Schedule(req, RequestSchedulerType.Post, TimeSpan.FromSeconds(1));

            await Assert.That((id)?.Any()).IsTrue();
            await Assert.That(host.BusContains<MyEventSync>(req.Id)).IsFalse();

            await host.EventuallyOnBus<MyEventSync>(req.Id);
            await Assert.That(await host.Outbox.GetAsync(req.Id, new RequestContext())).IsNotEqualTo(Message.Empty);
        }

        #endregion

        #region Rescheduler

        [Test]
        [Arguments(RequestSchedulerType.Send)]
        [Arguments(RequestSchedulerType.Publish)]
        public async Task When_reschedule_request_with_a_datetimeoffset(RequestSchedulerType type)
        {
            var req = new MyEventSync();
            var scheduler = host.SchedulerFactory.CreateSync(host.Processor);
            var id = scheduler.Schedule(req, type, host.TimeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(2)));

            await Assert.That((id)?.Any()).IsTrue();
            scheduler.ReScheduler(id, host.TimeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(5)));

            await Assert.That(host.ReceivedMessages).DoesNotContainKey(req.Id);

            await Task.Delay(TimeSpan.FromSeconds(3));
            await Assert.That(host.ReceivedMessages).DoesNotContainKey(req.Id);

            await host.EventuallyHandled(req.Id);
            await host.AssertOutboxEmptyForId(req.Id);
        }

        [Test]
        [Arguments(RequestSchedulerType.Send)]
        [Arguments(RequestSchedulerType.Publish)]
        public async Task When_reschedule_send_request_with_a_timespan(RequestSchedulerType type)
        {
            var req = new MyEventSync();
            var scheduler = host.SchedulerFactory.CreateSync(host.Processor);
            var id = scheduler.Schedule(req, type, TimeSpan.FromSeconds(2));

            await Assert.That((id)?.Any()).IsTrue();
            await Assert.That(host.ReceivedMessages).DoesNotContainKey(req.Id);

            scheduler.ReScheduler(id, TimeSpan.FromSeconds(5));

            await Task.Delay(TimeSpan.FromSeconds(3));
            await Assert.That(host.ReceivedMessages).DoesNotContainKey(req.Id);

            await host.EventuallyHandled(req.Id);
            await host.AssertOutboxEmptyForId(req.Id);
        }

        #endregion

        #region Cancel

        [Test]
        [Arguments(RequestSchedulerType.Send)]
        [Arguments(RequestSchedulerType.Post)]
        [Arguments(RequestSchedulerType.Publish)]
        public async Task When_cancel_scheduler_request_with_a_datetimeoffset(RequestSchedulerType type)
        {
            var req = new MyEventSync();
            var scheduler = host.SchedulerFactory.CreateSync(host.Processor);
            var id = scheduler.Schedule(req, type,
                host.TimeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(2)));

            await Assert.That((id)?.Any()).IsTrue();
            await Assert.That(host.ReceivedMessages).DoesNotContainKey(req.Id);

            scheduler.Cancel(id);

            await Task.Delay(TimeSpan.FromSeconds(3));
            await Assert.That(host.ReceivedMessages).DoesNotContainKey(req.Id);

            await host.AssertOutboxEmptyForId(req.Id);
        }

        [Test]
        [Arguments(RequestSchedulerType.Send)]
        [Arguments(RequestSchedulerType.Post)]
        [Arguments(RequestSchedulerType.Publish)]
        public async Task When_cancel_scheduler_request_with_a_timespan(RequestSchedulerType type)
        {
            var req = new MyEventSync();
            var scheduler = host.SchedulerFactory.CreateSync(host.Processor);
            var id = scheduler.Schedule(req, type, TimeSpan.FromSeconds(2));

            await Assert.That((id)?.Any()).IsTrue();
            await Assert.That(host.ReceivedMessages).DoesNotContainKey(req.Id);

            scheduler.Cancel(id);

            await Task.Delay(TimeSpan.FromSeconds(3));
            await Assert.That(host.ReceivedMessages).DoesNotContainKey(req.Id);

            await host.AssertOutboxEmptyForId(req.Id);
        }

        #endregion
    }
}
