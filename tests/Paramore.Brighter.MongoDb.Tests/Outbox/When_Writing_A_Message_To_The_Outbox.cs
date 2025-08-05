#region Licence

/* The MIT License (MIT)
Copyright © 2014 Francesco Pighi <francesco.pighi@gmail.com>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Net.Mime;
using Paramore.Brighter;                   
using Paramore.Brighter.Observability;     
using Paramore.Brighter.Outbox.MongoDb;
using Xunit;

namespace Paramore.Brighter.MongoDb.Tests.Outbox;

[Trait("Category", "MongoDb")]
public class MongoDbOutboxWritingMessageTests : IDisposable
{
    private readonly string _collection;
    private const string Key1 = "name1";
    private const string Key2 = "name2";
    private const string Key3 = "name3";
    private const string Key4 = "name4";
    private const string Key5 = "name5";
    private readonly Message _messageEarliest;
    private readonly MongoDbOutbox _outbox;
    private const string Value1 = "value1";
    private const string Value2 = "value2";
    private const int Value3 = 123;
    private readonly Guid _value4 = Guid.NewGuid();
    private readonly DateTime _value5 = DateTime.UtcNow;
    private readonly RequestContext _context;

    public MongoDbOutboxWritingMessageTests()
    {
        _collection = $"outbox-{Guid.NewGuid():N}";
        _outbox = new(Configuration.CreateOutbox(_collection));
        var messageHeader = new MessageHeader(
            messageId:       Guid.NewGuid().ToString(),
            topic:           new RoutingKey("test_topic"),
            messageType:     MessageType.MT_DOCUMENT,
            source:          new Uri("https://source.example.com"),
            type:            new CloudEventsType("unit.test.type"),
            timeStamp:       DateTime.UtcNow.AddDays(-1),
            correlationId:   Guid.NewGuid().ToString(),
            replyTo:         new RoutingKey("ReplyTo"),
            contentType:     new ContentType(MediaTypeNames.Text.Plain),
            partitionKey:    Guid.NewGuid().ToString(),
            dataSchema:      new Uri("https://schema.example.com"),
            subject:         "TestSubject",
            handledCount:    5,
            delayed:         TimeSpan.FromMilliseconds(5),
            traceParent:     new TraceParent("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00"),
            traceState:      new TraceState("rojo=1,congo=2"),
            baggage:         new Baggage()
        );
        messageHeader.Bag.Add(Key1, Value1);
        messageHeader.Bag.Add(Key2, Value2);
        messageHeader.Bag.Add(Key3, Value3);
        messageHeader.Bag.Add(Key4, _value4);
        messageHeader.Bag.Add(Key5, _value5);

        _context = new RequestContext();

        _messageEarliest = new Message(messageHeader, new MessageBody("message body"));
        _outbox.Add(_messageEarliest, _context);
    }

    [Fact]
    public void When_Writing_A_Message_To_The_MongoDb_Outbox()
    {
        var storedMessage = _outbox.Get(_messageEarliest.Id, _context);

        //should read the message from the sql outbox
        Assert.Equal(_messageEarliest.Body.Value, storedMessage.Body.Value);
        //should read the header from the sql outbox
        Assert.Equal(_messageEarliest.Header.Topic, storedMessage.Header.Topic);
        Assert.Equal(_messageEarliest.Header.MessageType, storedMessage.Header.MessageType);
        Assert.Equal(_messageEarliest.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss.fZ"), storedMessage.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss.fZ"));
        Assert.Equal(0, storedMessage.Header.HandledCount); // -- should be zero when read from outbox
        Assert.Equal(TimeSpan.Zero, storedMessage.Header.Delayed); // -- should be zero when read from outbox
        Assert.Equal(_messageEarliest.Header.CorrelationId,  storedMessage.Header.CorrelationId);
        Assert.Equal(_messageEarliest.Header.ReplyTo,  storedMessage.Header.ReplyTo);
        Assert.Equal(_messageEarliest.Header.ContentType,  storedMessage.Header.ContentType);
        Assert.Equal(_messageEarliest.Header.PartitionKey,  storedMessage.Header.PartitionKey);
        Assert.Equal(_messageEarliest.Header.Source,  storedMessage.Header.Source);
        Assert.Equal(_messageEarliest.Header.Type,  storedMessage.Header.Type);
        Assert.Equal(_messageEarliest.Header.SpecVersion, storedMessage.Header.SpecVersion);
        Assert.Equal(_messageEarliest.Header.DataSchema, storedMessage.Header.DataSchema);
        Assert.Equal(_messageEarliest.Header.Subject,  storedMessage.Header.Subject);
        Assert.Equal(_messageEarliest.Header.TraceParent, storedMessage.Header.TraceParent);
        Assert.Equal(_messageEarliest.Header.TraceState, storedMessage.Header.TraceState);

        //Bag serialization
        Assert.True(storedMessage.Header.Bag.ContainsKey(Key1));
        Assert.Equal(Value1, storedMessage.Header.Bag[Key1]);
        Assert.True(storedMessage.Header.Bag.ContainsKey(Key2));
        Assert.Equal(Value2, storedMessage.Header.Bag[Key2]);
        Assert.True(storedMessage.Header.Bag.ContainsKey(Key3));
        Assert.Equal(Value3, storedMessage.Header.Bag[Key3]);
        Assert.True(storedMessage.Header.Bag.ContainsKey(Key4));
        Assert.Equal(_value4, storedMessage.Header.Bag[Key4]);
        Assert.True(storedMessage.Header.Bag.ContainsKey(Key5));
        Assert.Equal(_value5, storedMessage.Header.Bag[Key5]);
    }

    public void Dispose()
    {
        Configuration.Cleanup(_collection);
    }
}
