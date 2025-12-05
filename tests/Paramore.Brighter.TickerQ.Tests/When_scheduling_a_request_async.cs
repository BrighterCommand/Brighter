using System;
using Paramore.Brighter.TickerQ.Tests.TestDoubles;
using Paramore.Brighter.TickerQ.Tests.TestDoubles.Fixtures;


namespace Paramore.Brighter.TickerQ.Tests
{
    [Collection("Scheduler")]
    public class TickerQSchedulerRequestAsyncTests : IClassFixture<TickerQRequestAsyncTestFixture>, IDisposable
    {
        private readonly TickerQRequestAsyncTestFixture _fixture;

        public TickerQSchedulerRequestAsyncTests(TickerQRequestAsyncTestFixture tickerQTestFixture)
        {
            _fixture = tickerQTestFixture;
        }

        #region Scheduler

        [Fact]
        public async Task When_scheduler_send_request_with_a_datetimeoffset_async()
        {
            var req = new MyEvent();
            var scheduler = _fixture.SchedulerFactory.CreateAsync(_fixture.Processor);
            var id = await scheduler.ScheduleAsync(req, RequestSchedulerType.Send,
                _fixture.TimeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));

            Assert.True((id)?.Any());

            Assert.DoesNotContain(nameof(MyEventHandlerAsync), _fixture.ReceivedMessages);

            await Task.Delay(TimeSpan.FromSeconds(2));

            Assert.Contains(nameof(MyEventHandlerAsync), _fixture.ReceivedMessages);

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
        public async Task When_scheduler_send_request_with_a_timespan_asc()
        {
            var req = new MyEvent();
            var scheduler = _fixture.SchedulerFactory.CreateAsync(_fixture.Processor);
            var id = await scheduler.ScheduleAsync(req, RequestSchedulerType.Send, TimeSpan.FromSeconds(1));

            Assert.True((id)?.Any());

            Assert.DoesNotContain(nameof(MyEventHandlerAsync), _fixture.ReceivedMessages);

            await Task.Delay(TimeSpan.FromSeconds(2));

            Assert.Contains(nameof(MyEventHandlerAsync), _fixture.ReceivedMessages);

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
        public async Task When_scheduler_publish_request_with_a_datetimeoffset_async()
        {
            var req = new MyEvent();
            var scheduler = _fixture.SchedulerFactory.CreateAsync(_fixture.Processor);
            var id = await scheduler.ScheduleAsync(req, RequestSchedulerType.Publish,
                _fixture.TimeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));

            Assert.True((id)?.Any());

            Assert.DoesNotContain(nameof(MyEventHandlerAsync), _fixture.ReceivedMessages);

            await Task.Delay(TimeSpan.FromSeconds(2));

            Assert.Contains(nameof(MyEventHandlerAsync), _fixture.ReceivedMessages);

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
        public async Task When_scheduler_publish_request_with_a_timespan()
        {
            var req = new MyEvent();
            var scheduler = _fixture.SchedulerFactory.CreateAsync(_fixture.Processor);
            var id = await scheduler.ScheduleAsync(req, RequestSchedulerType.Publish, TimeSpan.FromSeconds(1));

            Assert.True((id)?.Any());

            Assert.DoesNotContain(nameof(MyEventHandlerAsync), _fixture.ReceivedMessages);

            await Task.Delay(TimeSpan.FromSeconds(2));

            Assert.Contains(nameof(MyEventHandlerAsync), _fixture.ReceivedMessages);

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
        public async Task When_scheduler_post_request_with_a_datetimeoffset_async()
        {
            var req = new MyEvent();
            var scheduler = _fixture.SchedulerFactory.CreateAsync(_fixture.Processor);
            var id = await scheduler.ScheduleAsync(req, RequestSchedulerType.Post,
                _fixture.TimeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));

            Assert.True((id)?.Any());

            Assert.Empty(_fixture.InternalBus.Stream(_fixture.RoutingKey) ?? []);

            await Task.Delay(TimeSpan.FromSeconds(2));

            Assert.NotEqual(Message.Empty, _fixture.Outbox.Get(req.Id, new RequestContext()));

            Assert.NotEmpty(_fixture.InternalBus.Stream(_fixture.RoutingKey));
        }

        [Fact]
        public async Task When_scheduler_post_request_with_a_timespan_async()
        {
            var req = new MyEvent();
            var scheduler = _fixture.SchedulerFactory.CreateAsync(_fixture.Processor);
            var id = await scheduler.ScheduleAsync(req, RequestSchedulerType.Post, TimeSpan.FromSeconds(1));

            Assert.True((id)?.Any());

            Assert.Empty(_fixture.InternalBus.Stream(_fixture.RoutingKey) ?? []);

            await Task.Delay(TimeSpan.FromSeconds(2));

            Assert.NotEmpty(_fixture.InternalBus.Stream(_fixture.RoutingKey));

            Assert.NotEqual(Message.Empty, _fixture.Outbox.Get(req.Id, new RequestContext()));
        }

        #endregion

        #region Rescheduler

        [Theory]
        [InlineData(RequestSchedulerType.Send)]
        [InlineData(RequestSchedulerType.Publish)]
        public async Task When_reschedule_request_with_a_datetimeoffset_async(RequestSchedulerType type)
        {
            var req = new MyEvent();
            var scheduler = _fixture.SchedulerFactory.CreateAsync(_fixture.Processor);
            var id = await scheduler.ScheduleAsync(req, type, _fixture.TimeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(2)));
        
            Assert.True((id)?.Any());

            await scheduler.ReSchedulerAsync(id, _fixture.TimeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(5)));

            Assert.DoesNotContain(nameof(MyEventHandlerAsync), _fixture.ReceivedMessages);

            await Task.Delay(TimeSpan.FromSeconds(2));
            Assert.DoesNotContain(nameof(MyEventHandlerAsync), _fixture.ReceivedMessages);

            await Task.Delay(TimeSpan.FromSeconds(5));
            Assert.Contains(nameof(MyEventHandlerAsync), _fixture.ReceivedMessages);

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
        public async Task When_reschedule_send_request_with_a_timespan_async(RequestSchedulerType type)
        {
            var req = new MyEvent();
            var scheduler = _fixture.SchedulerFactory.CreateAsync(_fixture.Processor);
            var id = await scheduler.ScheduleAsync(req, type, TimeSpan.FromSeconds(2));

            Assert.True((id)?.Any());
            Assert.DoesNotContain(nameof(MyEventHandlerAsync), _fixture.ReceivedMessages);

            await scheduler.ReSchedulerAsync(id, TimeSpan.FromSeconds(5));

            await Task.Delay(TimeSpan.FromSeconds(2));
            Assert.DoesNotContain(nameof(MyEventHandlerAsync), _fixture.ReceivedMessages);

            await Task.Delay(TimeSpan.FromSeconds(5));
            Assert.Contains(nameof(MyEventHandlerAsync), _fixture.ReceivedMessages);

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
        public async Task When_cancel_scheduler_request_with_a_datetimeoffset(RequestSchedulerType type)
        {
            var req = new MyEvent();
            var scheduler = _fixture.SchedulerFactory.CreateAsync(_fixture.Processor);
            var id = await scheduler.ScheduleAsync(req, type,
                _fixture.TimeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(2)));

            Assert.True((id)?.Any());

            Assert.DoesNotContain(nameof(MyEventHandlerAsync), _fixture.ReceivedMessages);

            await scheduler.CancelAsync(id);

            await Task.Delay(TimeSpan.FromSeconds(3));
            Assert.DoesNotContain(nameof(MyEventHandlerAsync), _fixture.ReceivedMessages);

            var expected = Message.Empty;
            var actual = await _fixture.Outbox.GetAsync(req.Id, new RequestContext());

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
        public async Task When_cancel_scheduler_request_with_a_timespan_async(RequestSchedulerType type)
        {
            var req = new MyEvent();
            var scheduler = _fixture.SchedulerFactory.CreateAsync(_fixture.Processor);
            var id = await scheduler.ScheduleAsync(req, type, TimeSpan.FromSeconds(2));

            Assert.True((id)?.Any());
            Assert.DoesNotContain(nameof(MyEventHandlerAsync), _fixture.ReceivedMessages);

            await scheduler.CancelAsync(id);

            await Task.Delay(TimeSpan.FromSeconds(3));
            Assert.DoesNotContain(nameof(MyEventHandlerAsync), _fixture.ReceivedMessages);

            var expected = Message.Empty;
            var actual = await _fixture.Outbox.GetAsync(req.Id, new RequestContext());

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
