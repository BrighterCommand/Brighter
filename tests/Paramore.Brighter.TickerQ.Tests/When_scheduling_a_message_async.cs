using System;
using System.Text.Json;
using Paramore.Brighter.TickerQ.Tests.TestDoubles;
using Paramore.Brighter.TickerQ.Tests.TestDoubles.Fixtures;


namespace Paramore.Brighter.TickerQ.Tests;

[Collection("Scheduler")]
public class TickerQSchedulerMessageAsyncTests : IClassFixture<TickerQMessageTestFixture>, IDisposable
{
    private readonly TickerQMessageTestFixture _fixture;

    public TickerQSchedulerMessageAsyncTests(TickerQMessageTestFixture tickerQTestFixture)
    {
        _fixture = tickerQTestFixture;
    }

    [Fact]
    public async Task When_scheduler_a_message_with_a_datetimeoffset_async()
    {
        Message message = GetMessage();

        var scheduler = (IAmAMessageSchedulerAsync)_fixture.SchedulerFactory.Create(_fixture.Processor);
        var id = await scheduler.ScheduleAsync(message,
            _fixture.TimeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));

        Assert.NotEqual(0, id.Length);

        Assert.Empty(_fixture.InternalBus.Stream(_fixture.RoutingKey));

        await Task.Delay(TimeSpan.FromSeconds(2));

        Assert.Equivalent(message, await _fixture.Outbox.GetAsync(message.Id, new RequestContext()));

        Assert.NotEmpty(_fixture.InternalBus.Stream(_fixture.RoutingKey));
    }

    [Fact]
    public async Task When_scheduler_a_message_with_a_timespan_async()
    {
        Message message = GetMessage();

        var scheduler = (IAmAMessageSchedulerAsync)_fixture.SchedulerFactory.Create(_fixture.Processor);
        var id = await scheduler.ScheduleAsync(message, TimeSpan.FromSeconds(1));

        Assert.NotEqual(0, id.Length);

        Assert.Empty(_fixture.InternalBus.Stream(_fixture.RoutingKey));

        await Task.Delay(TimeSpan.FromSeconds(2));

        Assert.Equivalent(message, await _fixture.Outbox.GetAsync(message.Id, new RequestContext()));

        Assert.NotEmpty(_fixture.InternalBus.Stream(_fixture.RoutingKey));
    }

    [Fact]
    public async Task When_reschedule_a_message_with_a_datetimeoffset_async()
    {
        var message = GetMessage();

        var scheduler = (IAmAMessageSchedulerAsync)_fixture.SchedulerFactory.Create(_fixture.Processor);
        var id = await scheduler.ScheduleAsync(message, _fixture.TimeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(1)));

        
        //Assert.True((id)?.Any());
        //Assert.Empty(_fixture.InternalBus.Stream(_fixture.RoutingKey) ?? []);
        await scheduler.ReSchedulerAsync(id, _fixture.TimeProvider.GetUtcNow().Add(TimeSpan.FromSeconds(5)));

        //await Task.Delay(TimeSpan.FromSeconds(2));
        //Assert.Empty(_fixture.InternalBus.Stream(_fixture.RoutingKey) ?? []);

        //await Task.Delay(TimeSpan.FromSeconds(4));


        //Assert.NotEmpty(_fixture.InternalBus.Stream(_fixture.RoutingKey));
        //Assert.Equivalent(message, await _fixture.Outbox.GetAsync(message.Id, new RequestContext()));
        await Task.Delay(TimeSpan.FromSeconds(40));
    }

    [Fact]
    public async Task When_reschedule_a_message_with_a_timespan_async()
    {
        var message = GetMessage();

        var scheduler = (IAmAMessageSchedulerAsync)_fixture.SchedulerFactory.Create(_fixture.Processor);
        var id = await scheduler.ScheduleAsync(message, TimeSpan.FromSeconds(1));
        Assert.True((id)?.Any());
        Assert.Empty(_fixture.InternalBus.Stream(_fixture.RoutingKey) ?? []);
        await scheduler.ReSchedulerAsync(id, TimeSpan.FromSeconds(5));

        await Task.Delay(TimeSpan.FromSeconds(2));
        Assert.Empty(_fixture.InternalBus.Stream(_fixture.RoutingKey) ?? []);

        await Task.Delay(TimeSpan.FromSeconds(4));

        Assert.NotEmpty(_fixture.InternalBus.Stream(_fixture.RoutingKey));
        Assert.Equivalent(message, await _fixture.Outbox.GetAsync(message.Id, new RequestContext()));
    }

    [Fact]
    public async Task When_cancel_scheduler_message_with_a_datetimeoffset_async()
    {
        var message = GetMessage();

        var scheduler = (IAmAMessageSchedulerAsync)_fixture.SchedulerFactory.Create(_fixture.Processor);
        var id = await scheduler.ScheduleAsync(message, TimeSpan.FromHours(1));

        Assert.True((id)?.Any());

        await scheduler.CancelAsync(id);

        await Task.Delay(TimeSpan.FromSeconds(2));

        var expected = Message.Empty;
        var actual = await _fixture.Outbox.GetAsync(message.Id, new RequestContext());

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
    public async Task When_cancel_scheduler_request_with_a_timespan_async()
    {
        var message = GetMessage();

        var scheduler = (IAmAMessageSchedulerAsync)_fixture.SchedulerFactory.Create(_fixture.Processor);
        var id = await scheduler.ScheduleAsync(message, TimeSpan.FromHours(1));

        Assert.True((id)?.Any());

        await scheduler.CancelAsync(id);

        await Task.Delay(TimeSpan.FromSeconds(2));

        var expected = Message.Empty;
        var actual = await _fixture.Outbox.GetAsync(message.Id, new RequestContext());

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
        _fixture.Clear();
    }
}

