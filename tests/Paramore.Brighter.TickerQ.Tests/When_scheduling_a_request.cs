using Paramore.Brighter.TickerQ.Tests.TestDoubles;
using Paramore.Brighter.TickerQ.Tests.TestDoubles.Fixtures;


namespace Paramore.Brighter.TickerQ.Tests
{
    [Collection("Scheduler")]
    public class TickerQSchedulerRequestTests : IClassFixture<TickerQRequestTestFixture>, IDisposable
    {
        private readonly TickerQRequestTestFixture _fixture;

        public TickerQSchedulerRequestTests(TickerQRequestTestFixture tickerQTestFixture)
        {
            _fixture = tickerQTestFixture;
        }

        #region Scheduler
        [Fact]
        public void When_scheduler_send_request_with_a_datetimeoffset()
        {
            var req = new MyEvent();
            var scheduler = _fixture.SchedulerFactory.CreateSync(_fixture.Processor);
            var id = scheduler.Schedule(req, RequestSchedulerType.Send,
                _fixture.TimeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));

            Assert.True((id)?.Any());

            Assert.DoesNotContain(nameof(MyEventHandler), _fixture.ReceivedMessages);

            Thread.Sleep(TimeSpan.FromSeconds(2));

            Assert.Contains(nameof(MyEventHandler), _fixture.ReceivedMessages);

            var expected = Message.Empty;
            var actual = _fixture.Outbox.Get(req.Id, new RequestContext());

            Assert.Equivalent(expected.Body, actual.Body);
            Assert.Equal(expected.Id, actual.Id);
            Assert.Equal(expected.Persist, actual.Persist);
            Assert.Equal(expected.Redelivered, actual.Redelivered);
            Assert.Equal(expected.DeliveryTag, actual.DeliveryTag);
            Assert.Equal(expected.Header.MessageType, actual.Header.MessageType);
            Assert.Equal(expected.Header.Topic, actual.Header.Topic);
            Assert.Equal(expected.Header.TimeStamp, actual.Header.TimeStamp, TimeSpan.FromSeconds(1));
            Assert.Equal(expected.Header.CorrelationId, actual.Header.CorrelationId);
            Assert.Equal(expected.Header.ReplyTo, actual.Header.ReplyTo);
            Assert.Equal(expected.Header.ContentType, actual.Header.ContentType);
            Assert.Equal(expected.Header.HandledCount, actual.Header.HandledCount);
        }

        [Fact]
        public void When_scheduler_send_request_with_a_timespan()
        {
            var req = new MyEvent();
            var scheduler = _fixture.SchedulerFactory.CreateSync(_fixture.Processor);
            var id = scheduler.Schedule(req, RequestSchedulerType.Send, TimeSpan.FromSeconds(1));

            Assert.True((id)?.Any());

            Assert.DoesNotContain(nameof(MyEventHandler), _fixture.ReceivedMessages);

            Thread.Sleep(TimeSpan.FromSeconds(2));

            Assert.Contains(nameof(MyEventHandler), _fixture.ReceivedMessages);

            var expected = Message.Empty;
            var actual = _fixture.Outbox.Get(req.Id, new RequestContext());

            Assert.Equivalent(expected.Body, actual.Body);
            Assert.Equal(expected.Id, actual.Id);
            Assert.Equal(expected.Persist, actual.Persist);
            Assert.Equal(expected.Redelivered, actual.Redelivered);
            Assert.Equal(expected.DeliveryTag, actual.DeliveryTag);
            Assert.Equal(expected.Header.MessageType, actual.Header.MessageType);
            Assert.Equal(expected.Header.Topic, actual.Header.Topic);
            Assert.Equal(expected.Header.TimeStamp, actual.Header.TimeStamp, TimeSpan.FromSeconds(1));
            Assert.Equal(expected.Header.CorrelationId, actual.Header.CorrelationId);
            Assert.Equal(expected.Header.ReplyTo, actual.Header.ReplyTo);
            Assert.Equal(expected.Header.ContentType, actual.Header.ContentType);
            Assert.Equal(expected.Header.HandledCount, actual.Header.HandledCount);
        }

        [Fact]
        public void When_scheduler_publish_request_with_a_datetimeoffset()
        {
            var req = new MyEvent();
            var scheduler = _fixture.SchedulerFactory.CreateSync(_fixture.Processor);
            var id = scheduler.Schedule(req, RequestSchedulerType.Publish,
                _fixture.TimeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));

            Assert.True((id)?.Any());

            Assert.DoesNotContain(nameof(MyEventHandler), _fixture.ReceivedMessages);

            Thread.Sleep(TimeSpan.FromSeconds(2));

            Assert.Contains(nameof(MyEventHandler), _fixture.ReceivedMessages);

            var expected = Message.Empty;
            var actual = _fixture.Outbox.Get(req.Id, new RequestContext());

            Assert.Equivalent(expected.Body, actual.Body);
            Assert.Equal(expected.Id, actual.Id);
            Assert.Equal(expected.Persist, actual.Persist);
            Assert.Equal(expected.Redelivered, actual.Redelivered);
            Assert.Equal(expected.DeliveryTag, actual.DeliveryTag);
            Assert.Equal(expected.Header.MessageType, actual.Header.MessageType);
            Assert.Equal(expected.Header.Topic, actual.Header.Topic);
            Assert.Equal(expected.Header.TimeStamp, actual.Header.TimeStamp, TimeSpan.FromSeconds(1));
            Assert.Equal(expected.Header.CorrelationId, actual.Header.CorrelationId);
            Assert.Equal(expected.Header.ReplyTo, actual.Header.ReplyTo);
            Assert.Equal(expected.Header.ContentType, actual.Header.ContentType);
            Assert.Equal(expected.Header.HandledCount, actual.Header.HandledCount);
        }

        [Fact]
        public void When_scheduler_publish_request_with_a_timespan()
        {
            var req = new MyEvent();
            var scheduler = _fixture.SchedulerFactory.CreateSync(_fixture.Processor);
            var id = scheduler.Schedule(req, RequestSchedulerType.Publish, TimeSpan.FromSeconds(1));

            Assert.True((id)?.Any());

            Assert.DoesNotContain(nameof(MyEventHandler), _fixture.ReceivedMessages);

            Thread.Sleep(TimeSpan.FromSeconds(2));

            Assert.Contains(nameof(MyEventHandler), _fixture.ReceivedMessages);

            var expected = Message.Empty;
            var actual = _fixture.Outbox.Get(req.Id, new RequestContext());

            Assert.Equivalent(expected.Body, actual.Body);
            Assert.Equal(expected.Id, actual.Id);
            Assert.Equal(expected.Persist, actual.Persist);
            Assert.Equal(expected.Redelivered, actual.Redelivered);
            Assert.Equal(expected.DeliveryTag, actual.DeliveryTag);
            Assert.Equal(expected.Header.MessageType, actual.Header.MessageType);
            Assert.Equal(expected.Header.Topic, actual.Header.Topic);
            Assert.Equal(expected.Header.TimeStamp, actual.Header.TimeStamp, TimeSpan.FromSeconds(1));
            Assert.Equal(expected.Header.CorrelationId, actual.Header.CorrelationId);
            Assert.Equal(expected.Header.ReplyTo, actual.Header.ReplyTo);
            Assert.Equal(expected.Header.ContentType, actual.Header.ContentType);
            Assert.Equal(expected.Header.HandledCount, actual.Header.HandledCount);
        }

        [Fact]
        public void When_scheduler_post_request_with_a_datetimeoffset()
        {
            var req = new MyEvent();
            var scheduler = _fixture.SchedulerFactory.CreateSync(_fixture.Processor);
            var id = scheduler.Schedule(req, RequestSchedulerType.Post,
                _fixture.TimeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));

            Assert.True((id)?.Any());

            Assert.Empty(_fixture.InternalBus.Stream(_fixture.RoutingKey) ?? []);

            Thread.Sleep(TimeSpan.FromSeconds(2));

            Assert.NotEqual(Message.Empty, _fixture.Outbox.Get(req.Id, new RequestContext()));

            Assert.NotEmpty(_fixture.InternalBus.Stream(_fixture.RoutingKey));
        }

        [Fact]
        public void When_scheduler_post_request_with_a_timespan()
        {
            var req = new MyEvent();
            var scheduler = _fixture.SchedulerFactory.CreateSync(_fixture.Processor);
            var id = scheduler.Schedule(req, RequestSchedulerType.Post, TimeSpan.FromSeconds(1));

            Assert.True((id)?.Any());

            Assert.Empty(_fixture.InternalBus.Stream(_fixture.RoutingKey) ?? []);

            Thread.Sleep(TimeSpan.FromSeconds(2));

            Assert.NotEmpty(_fixture.InternalBus.Stream(_fixture.RoutingKey));

            Assert.NotEqual(Message.Empty, _fixture.Outbox.Get(req.Id, new RequestContext()));
        }

        #endregion

        #region Rescheduler

        [Theory]
        [InlineData(RequestSchedulerType.Send)]
        [InlineData(RequestSchedulerType.Publish)]
        public void When_reschedule_request_with_a_datetimeoffset(RequestSchedulerType type)
        {
            var req = new MyEvent();
            var scheduler = _fixture.SchedulerFactory.CreateSync(_fixture.Processor);
            var id = scheduler.Schedule(req, type, _fixture.TimeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));

            Assert.True((id)?.Any());

            scheduler.ReScheduler(id, _fixture.TimeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(5)));

            Assert.DoesNotContain(nameof(MyEventHandler), _fixture.ReceivedMessages);

            Thread.Sleep(TimeSpan.FromSeconds(2));
            Assert.DoesNotContain(nameof(MyEventHandler), _fixture.ReceivedMessages);

            Thread.Sleep(TimeSpan.FromSeconds(4));
            Assert.Contains(nameof(MyEventHandler), _fixture.ReceivedMessages);

            var expected = Message.Empty;
            var actual = _fixture.Outbox.Get(req.Id, new RequestContext());

            Assert.Equivalent(expected.Body, actual.Body);
            Assert.Equal(expected.Id, actual.Id);
            Assert.Equal(expected.Persist, actual.Persist);
            Assert.Equal(expected.Redelivered, actual.Redelivered);
            Assert.Equal(expected.DeliveryTag, actual.DeliveryTag);
            Assert.Equal(expected.Header.MessageType, actual.Header.MessageType);
            Assert.Equal(expected.Header.Topic, actual.Header.Topic);
            Assert.Equal(expected.Header.TimeStamp, actual.Header.TimeStamp, TimeSpan.FromSeconds(1));
            Assert.Equal(expected.Header.CorrelationId, actual.Header.CorrelationId);
            Assert.Equal(expected.Header.ReplyTo, actual.Header.ReplyTo);
            Assert.Equal(expected.Header.ContentType, actual.Header.ContentType);
            Assert.Equal(expected.Header.HandledCount, actual.Header.HandledCount);
        }

        [Theory]
        [InlineData(RequestSchedulerType.Send)]
        [InlineData(RequestSchedulerType.Publish)]
        public void When_reschedule_send_request_with_a_timespan(RequestSchedulerType type)
        {
            var req = new MyEvent();
            var scheduler = _fixture.SchedulerFactory.CreateSync(_fixture.Processor);
            var id = scheduler.Schedule(req, type, TimeSpan.FromSeconds(1));

            Assert.True((id)?.Any());
            Assert.DoesNotContain(nameof(MyEventHandler), _fixture.ReceivedMessages);

            scheduler.ReScheduler(id, TimeSpan.FromSeconds(5));

            Thread.Sleep(TimeSpan.FromSeconds(2));
            Assert.DoesNotContain(nameof(MyEventHandler), _fixture.ReceivedMessages);

            Thread.Sleep(TimeSpan.FromSeconds(4));
            Assert.Contains(nameof(MyEventHandler), _fixture.ReceivedMessages);

            var expected = Message.Empty;
            var actual = _fixture.Outbox.Get(req.Id, new RequestContext());

            Assert.Equivalent(expected.Body, actual.Body);
            Assert.Equal(expected.Id, actual.Id);
            Assert.Equal(expected.Persist, actual.Persist);
            Assert.Equal(expected.Redelivered, actual.Redelivered);
            Assert.Equal(expected.DeliveryTag, actual.DeliveryTag);
            Assert.Equal(expected.Header.MessageType, actual.Header.MessageType);
            Assert.Equal(expected.Header.Topic, actual.Header.Topic);
            Assert.Equal(expected.Header.TimeStamp, actual.Header.TimeStamp, TimeSpan.FromSeconds(1));
            Assert.Equal(expected.Header.CorrelationId, actual.Header.CorrelationId);
            Assert.Equal(expected.Header.ReplyTo, actual.Header.ReplyTo);
            Assert.Equal(expected.Header.ContentType, actual.Header.ContentType);
            Assert.Equal(expected.Header.HandledCount, actual.Header.HandledCount);
        }

        #endregion

        #region Cancel

        [Theory]
        [InlineData(RequestSchedulerType.Send)]
        [InlineData(RequestSchedulerType.Post)]
        [InlineData(RequestSchedulerType.Publish)]
        public void When_cancel_scheduler_request_with_a_datetimeoffset(RequestSchedulerType type)
        {
            var req = new MyEvent();
            var scheduler = _fixture.SchedulerFactory.CreateSync(_fixture.Processor);
            var id = scheduler.Schedule(req, type,
                _fixture.TimeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));

            Assert.True((id)?.Any());

            Assert.DoesNotContain(nameof(MyEventHandler), _fixture.ReceivedMessages);


            scheduler.Cancel(id);

            Thread.Sleep(TimeSpan.FromSeconds(2));
            Assert.DoesNotContain(nameof(MyEventHandler), _fixture.ReceivedMessages);

            var expected = Message.Empty;
            var actual = _fixture.Outbox.Get(req.Id, new RequestContext());

            Assert.Equivalent(expected.Body, actual.Body);
            Assert.Equal(expected.Id, actual.Id);
            Assert.Equal(expected.Persist, actual.Persist);
            Assert.Equal(expected.Redelivered, actual.Redelivered);
            Assert.Equal(expected.DeliveryTag, actual.DeliveryTag);
            Assert.Equal(expected.Header.MessageType, actual.Header.MessageType);
            Assert.Equal(expected.Header.Topic, actual.Header.Topic);
            Assert.Equal(expected.Header.TimeStamp, actual.Header.TimeStamp, TimeSpan.FromSeconds(1));
            Assert.Equal(expected.Header.CorrelationId, actual.Header.CorrelationId);
            Assert.Equal(expected.Header.ReplyTo, actual.Header.ReplyTo);
            Assert.Equal(expected.Header.ContentType, actual.Header.ContentType);
            Assert.Equal(expected.Header.HandledCount, actual.Header.HandledCount);
        }

        [Theory]
        [InlineData(RequestSchedulerType.Send)]
        [InlineData(RequestSchedulerType.Post)]
        [InlineData(RequestSchedulerType.Publish)]
        public void When_cancel_scheduler_request_with_a_timespan(RequestSchedulerType type)
        {
            var req = new MyEvent();
            var scheduler = _fixture.SchedulerFactory.CreateSync(_fixture.Processor);
            var id = scheduler.Schedule(req, type, TimeSpan.FromSeconds(1));

            Assert.True((id)?.Any());
            Assert.DoesNotContain(nameof(MyEventHandler), _fixture.ReceivedMessages);

            scheduler.Cancel(id);

            Thread.Sleep(TimeSpan.FromSeconds(2));
            Assert.DoesNotContain(nameof(MyEventHandler), _fixture.ReceivedMessages);

            var expected = Message.Empty;
            var actual = _fixture.Outbox.Get(req.Id, new RequestContext());

            Assert.Equivalent(expected.Body, actual.Body);
            Assert.Equal(expected.Id, actual.Id);
            Assert.Equal(expected.Persist, actual.Persist);
            Assert.Equal(expected.Redelivered, actual.Redelivered);
            Assert.Equal(expected.DeliveryTag, actual.DeliveryTag);
            Assert.Equal(expected.Header.MessageType, actual.Header.MessageType);
            Assert.Equal(expected.Header.Topic, actual.Header.Topic);
            Assert.Equal(expected.Header.TimeStamp, actual.Header.TimeStamp, TimeSpan.FromSeconds(1));
            Assert.Equal(expected.Header.CorrelationId, actual.Header.CorrelationId);
            Assert.Equal(expected.Header.ReplyTo, actual.Header.ReplyTo);
            Assert.Equal(expected.Header.ContentType, actual.Header.ContentType);
            Assert.Equal(expected.Header.HandledCount, actual.Header.HandledCount);
        }

        #endregion

        public void Dispose()
        {
            _fixture.Clear();
        }
    }
}
