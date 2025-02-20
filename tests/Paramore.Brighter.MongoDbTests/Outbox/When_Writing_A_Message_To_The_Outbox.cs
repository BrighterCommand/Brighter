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
using FluentAssertions;
using Paramore.Brighter.Outbox.MongoDb;
using Xunit;

namespace Paramore.Brighter.MongoDbTests.Outbox;

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
        _outbox = new(Configuration.Create(_collection));
        var messageHeader = new MessageHeader(
            messageId: Guid.NewGuid().ToString(),
            topic: new RoutingKey("test_topic"),
            messageType: MessageType.MT_DOCUMENT,
            timeStamp: DateTime.UtcNow.AddDays(-1),
            handledCount: 5,
            delayed: TimeSpan.FromMilliseconds(5),
            correlationId: Guid.NewGuid().ToString(),
            replyTo: new RoutingKey("ReplyTo"),
            contentType: "text/plain",
            partitionKey: Guid.NewGuid().ToString());
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
    public void When_Writing_A_Message_To_The_PostgreSql_Outbox()
    {
        var storedMessage = _outbox.Get(_messageEarliest.Id, _context);

        //should read the message from the sql outbox
        storedMessage.Body.Value.Should().Be(_messageEarliest.Body.Value);
        //should read the header from the sql outbox
        storedMessage.Header.Topic.Should().Be(_messageEarliest.Header.Topic);
        storedMessage.Header.MessageType.Should().Be(_messageEarliest.Header.MessageType);
        storedMessage.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss.fZ")
            .Should().Be(_messageEarliest.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss.fZ"));
        storedMessage.Header.HandledCount.Should().Be(0); // -- should be zero when read from outbox
        storedMessage.Header.Delayed.Should().Be(TimeSpan.Zero); // -- should be zero when read from outbox
        storedMessage.Header.CorrelationId.Should().Be(_messageEarliest.Header.CorrelationId);
        storedMessage.Header.ReplyTo.Should().Be(_messageEarliest.Header.ReplyTo);
        storedMessage.Header.ContentType.Should().Be(_messageEarliest.Header.ContentType);
        storedMessage.Header.PartitionKey.Should().Be(_messageEarliest.Header.PartitionKey);

        //Bag serialization
        storedMessage.Header.Bag.ContainsKey(Key1).Should().BeTrue();
        storedMessage.Header.Bag[Key1].Should().Be(Value1);
        storedMessage.Header.Bag.ContainsKey(Key2).Should().BeTrue();
        storedMessage.Header.Bag[Key2].Should().Be(Value2);
        storedMessage.Header.Bag.ContainsKey(Key3).Should().BeTrue();
        storedMessage.Header.Bag[Key3].Should().Be(Value3);
        storedMessage.Header.Bag.ContainsKey(Key4).Should().BeTrue();
        storedMessage.Header.Bag[Key4].Should().Be(_value4);
        storedMessage.Header.Bag.ContainsKey(Key5).Should().BeTrue();
        storedMessage.Header.Bag[Key5].Should().Be(_value5);
    }

    public void Dispose()
    {
        Configuration.Cleanup(_collection);
    }
}
