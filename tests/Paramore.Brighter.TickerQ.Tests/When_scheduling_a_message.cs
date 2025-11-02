using System;
using System.Text.Json;
using Paramore.Brighter.TickerQ.Tests.TestDoubles;


namespace Paramore.Brighter.TickerQ.Tests;

[Collection("Scheduler")]
public class TickerQSchedulerMessageTests : IClassFixture<TickerQTestFixture>, IDisposable
{
    private readonly TickerQTestFixture _fixture;


    public TickerQSchedulerMessageTests(TickerQTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void When_scheduler_a_message_with_a_datetimeoffset_sync()
    {
        Message message = GetMessage();

        var scheduler = (IAmAMessageSchedulerSync)_fixture.SchedulerFactory.Create(_fixture.Processor);
        var id = scheduler.Schedule(message, _fixture.TimeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));

        Assert.NotEqual(0, id.Length);

        Assert.Empty(_fixture.InternalBus.Stream(_fixture.RoutingKey));

        Thread.Sleep(TimeSpan.FromSeconds(2));

        Assert.Equivalent(message, _fixture.Outbox.Get(message.Id, new RequestContext()));

        Assert.NotEmpty(_fixture.InternalBus.Stream(_fixture.RoutingKey));
    }

    [Fact]
    public void When_scheduler_a_message_with_a_timespan_sync()
    {
        Message message = GetMessage();

        var scheduler = (IAmAMessageSchedulerSync)_fixture.SchedulerFactory.Create(_fixture.Processor);
        var id = scheduler.Schedule(message, TimeSpan.FromSeconds(1));

        Assert.NotEqual(0, id.Length);

        Assert.Empty(_fixture.InternalBus.Stream(_fixture.RoutingKey));

        Thread.Sleep(TimeSpan.FromSeconds(2));

        Assert.Equivalent(message, _fixture.Outbox.Get(message.Id, new RequestContext()));

        Assert.NotEmpty(_fixture.InternalBus.Stream(_fixture.RoutingKey));
    }

    [Fact]
    public void When_reschedule_a_message_with_a_datetimeoffset_sync()
    {
        var message = GetMessage();

        var scheduler = (IAmAMessageSchedulerSync)_fixture.SchedulerFactory.Create(_fixture.Processor);
        var id = scheduler.Schedule(message, _fixture.TimeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));

        Assert.True((id)?.Any());
        Assert.Empty(_fixture.InternalBus.Stream(_fixture.RoutingKey) ?? []);

        scheduler.ReScheduler(id, _fixture.TimeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(5)));

        Thread.Sleep(TimeSpan.FromSeconds(2));
        Assert.Empty(_fixture.InternalBus.Stream(_fixture.RoutingKey) ?? []);

        Thread.Sleep(TimeSpan.FromSeconds(4));

        Assert.NotEmpty(_fixture.InternalBus.Stream(_fixture.RoutingKey));
        Assert.Equivalent(message, _fixture.Outbox.Get(message.Id, new RequestContext()));
    }

    [Fact]
    public async Task When_reschedule_a_message_with_a_timespan_sync()
    {
        var message = GetMessage();

        var scheduler = (IAmAMessageSchedulerSync)_fixture.SchedulerFactory.Create(_fixture.Processor);
        var id = scheduler.Schedule(message, TimeSpan.FromSeconds(1));

        Assert.True((id)?.Any());
        Assert.Empty(_fixture.InternalBus.Stream(_fixture.RoutingKey) ?? []);

        scheduler.ReScheduler(id, TimeSpan.FromSeconds(5));

        Thread.Sleep(TimeSpan.FromSeconds(2));
        Assert.Empty(_fixture.InternalBus.Stream(_fixture.RoutingKey) ?? []);

        await Task.Delay(TimeSpan.FromSeconds(4));

        Assert.NotEmpty(_fixture.InternalBus.Stream(_fixture.RoutingKey));
        Assert.Equivalent(message, _fixture.Outbox.Get(message.Id, new RequestContext()));
    }
    [Fact]
    public void When_cancel_scheduler_message_with_a_datetimeoffset()
    {
        var message = GetMessage();

        var scheduler = (IAmAMessageSchedulerSync)_fixture.SchedulerFactory.Create(_fixture.Processor);
        var id = scheduler.Schedule(message, TimeSpan.FromSeconds(1));

        Assert.NotEqual(0, id.Length);

        scheduler.Cancel(id);

        Thread.Sleep(TimeSpan.FromSeconds(2));

        var expected = Message.Empty;
        var actual = _fixture.Outbox.Get(message.Id, new RequestContext());

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
    public void When_cancel_scheduler_request_with_a_timespan()
    {
        var message = GetMessage();

        var scheduler = (IAmAMessageSchedulerSync)_fixture.SchedulerFactory.Create(_fixture.Processor);
        var id = scheduler.Schedule(message, TimeSpan.FromSeconds(1));

        Assert.NotEqual(0, id.Length);

        scheduler.Cancel(id);

        Thread.Sleep(TimeSpan.FromSeconds(2));

        var expected = Message.Empty;
        var actual = _fixture.Outbox.Get(message.Id, new RequestContext());

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

    private Message GetMessage()
    {
        var req = new MyEvent();
        var message =
            new Message(
                new MessageHeader { MessageId = req.Id, MessageType = MessageType.MT_EVENT, Topic = _fixture.RoutingKey },
                new MessageBody(JsonSerializer.Serialize(req)));
        return message;
    }

    public void Dispose()
    {
        _fixture.ClearBus();
    }
}

