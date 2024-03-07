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
using System.Linq;
using FluentAssertions;
using Paramore.Brighter.Outbox.EventStore;
using Xunit;

namespace Paramore.Brighter.EventStore.Tests.Outbox
{
    [Trait("Category", "EventStore")]
    [Collection("EventStore")]
    public class EventStoreOutboxTests(EventStoreFixture fixture) 
    {
        [Fact]
        public void When_Writing_Messages_To_The_Outbox()
        {
            // arrange
            var eventStoreOutbox = new EventStoreOutbox(fixture.Connection);
            var streamName = $"{Guid.NewGuid()}";

            var body = new MessageBody("{companyId:123}");
            var header = new MessageHeader(
                messageId:Guid.NewGuid(), 
                topic: "Topic", 
                messageType: MessageType.MT_EVENT,
                timeStamp: DateTime.UtcNow,
                correlationId: Guid.NewGuid(),
                replyTo:"ReplyTo",
                contentType: "text/plain");
            header.Bag.Add("impersonatorId", 123);
            header.Bag.Add("eventNumber", 0);
            header.Bag.Add("streamId", streamName);
            var conversationId = Guid.NewGuid();
            header.Bag.Add("conversationId", conversationId);
            var now = DateTime.UtcNow;
            header.Bag.Add("timeStamp", now);
            
            var message1 = new Message(header, body);

            var body1 = new MessageBody("{companyId:123}");
            var header1 = new MessageHeader(
                messageId:Guid.NewGuid(), 
                topic: "Topic", 
                messageType: MessageType.MT_EVENT,
                timeStamp: DateTime.UtcNow,
                correlationId: Guid.NewGuid(),
                replyTo:"ReplyTo",
                contentType: "text/plain");
            header1.Bag.Add("impersonatorId", 123);
            header1.Bag.Add("eventNumber", 1);
            header1.Bag.Add("streamId", streamName);
            header1.Bag.Add("conversationId", conversationId);
            header1.Bag.Add("timeStamp", now);
            
            var message2 = new Message(header1, body1);
            
            // act
            eventStoreOutbox.Add(message1);
            eventStoreOutbox.Add(message2);   
            
            // assert
            var messages = eventStoreOutbox.Get(streamName, 0, 2);
            
            //should read the message from the outbox
            messages[0].Body.Value.Should().Be(message1.Body.Value);
            //should read the header from the outbox
            messages[0].Header.Topic.Should().Be(message1.Header.Topic);
            messages[0].Header.MessageType.Should().Be(message1.Header.MessageType);
            messages[0].Header.TimeStamp.Should().Be(message1.Header.TimeStamp);
            messages[0].Header.HandledCount.Should().Be(0); // -- should be zero when read from outbox
            messages[0].Header.DelayedMilliseconds.Should().Be(0); // -- should be zero when read from outbox
            messages[0].Header.CorrelationId.Should().Be(message1.Header.CorrelationId);
            messages[0].Header.ReplyTo.Should().Be(message1.Header.ReplyTo);
            messages[0].Header.ContentType.Should().Be(message1.Header.ContentType);
             
            
            //Bag serialization
            messages[0].Header.Bag["impersonatorId"].Should().Be(123);
            messages[0].Header.Bag["eventNumber"].Should().Be(0);
            messages[0].Header.Bag["streamId"].Should().Be(streamName);
            messages[0].Header.Bag["conversationId"].Should().Be(conversationId);
            messages[0].Header.Bag["timeStamp"].Should().Be(now);
            
            messages[1].Header.Bag["impersonatorId"].Should().Be(123);
            messages[1].Header.Bag["eventNumber"].Should().Be(1);
            messages[1].Header.Bag["streamId"].Should().Be(streamName);
            messages[1].Header.Bag["conversationId"].Should().Be(conversationId);
            messages[1].Header.Bag["timeStamp"].Should().Be(now);

        }
    }
}
