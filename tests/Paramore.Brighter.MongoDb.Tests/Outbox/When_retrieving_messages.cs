﻿using System;
using System.Linq;
using FluentAssertions;
using Paramore.Brighter.Outbox.MongoDb;
using Xunit;

namespace Paramore.Brighter.MongoDb.Tests.Outbox;

[Trait("Category", "MongoDb")]
public class MongoDbFetchMessageTests : IDisposable
{
    private readonly string _collection;
    private readonly Message _messageEarliest;
    private readonly Message _messageDispatched;
    private readonly Message _messageUnDispatched;
    private readonly MongoDbOutbox _outbox;

    public MongoDbFetchMessageTests()
    {
        _collection = $"outbox-{Guid.NewGuid():N}";
        _outbox = new MongoDbOutbox(Configuration.Create(_collection));
        var routingKey = new RoutingKey("test_topic");

        _messageEarliest = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_DOCUMENT),
            new MessageBody("message body"));
        _messageDispatched = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_DOCUMENT),
            new MessageBody("message body"));
        _messageUnDispatched = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_DOCUMENT),
            new MessageBody("message body"));
    }

    [Fact]
    public void When_Retrieving_Messages()
    {
        var context = new RequestContext();
        _outbox.Add([_messageEarliest, _messageDispatched, _messageUnDispatched], context);
        _outbox.MarkDispatched(_messageEarliest.Id, context, DateTime.UtcNow.AddHours(-3));
        _outbox.MarkDispatched(_messageDispatched.Id, context);

        var messages = _outbox.Get();

        //Assert
        messages.Should().HaveCount(3);
    }

    [Fact]
    public void When_Retrieving_Messages_By_Id()
    {
        var context = new RequestContext();
        _outbox.Add([_messageEarliest, _messageDispatched, _messageUnDispatched], context);
        _outbox.MarkDispatched(_messageEarliest.Id, context, DateTime.UtcNow.AddHours(-3));
        _outbox.MarkDispatched(_messageDispatched.Id, context);

        var messages = _outbox.Get(
            [_messageEarliest.Id, _messageUnDispatched.Id],
            context);

        //Assert
        messages = messages.ToList();
        messages.Should().HaveCount(2);
        messages.Should().Contain(x => x.Id == _messageEarliest.Id);
        messages.Should().Contain(x => x.Id == _messageUnDispatched.Id);
        messages.Should().NotContain(x => x.Id == _messageDispatched.Id);
    }

    [Fact]
    public void When_Retrieving_Message_By_Id()
    {
        var context = new RequestContext();
        _outbox.Add([_messageEarliest, _messageDispatched, _messageUnDispatched], context);
        _outbox.MarkDispatched(_messageEarliest.Id, context, DateTime.UtcNow.AddHours(-3));
        _outbox.MarkDispatched(_messageDispatched.Id, context);

        var messages = _outbox.Get(_messageDispatched.Id, context);

        //Assert
        messages.Id.Should().Be(_messageDispatched.Id);
    }

    public void Dispose()
    {
        Configuration.Cleanup(_collection);
    }
}
