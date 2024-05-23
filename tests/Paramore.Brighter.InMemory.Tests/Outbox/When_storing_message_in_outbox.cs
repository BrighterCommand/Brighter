#region Licence

/* The MIT License (MIT)
Copyright © 2014 Francesco Pighi <francesco.pighi@gmail.com>
Copyright © 2020 Ian Cooper <ian_hammond_cooper@yahoo.co.uk> 

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
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.InMemory.Tests.Builders;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Outbox
{
    [Trait("Category", "InMemory")]
    public class InMemoryOutboxTests
    {
        [Fact]
        public void When_reading_from_outbox()
        {
            //Arrange
            var outbox = new InMemoryOutbox(new FakeTimeProvider()) { Tracer = new BrighterTracer() };

            var messageId = Guid.NewGuid().ToString();
            var messageToAdd = new Message(
                new MessageHeader(messageId, "test_topic", MessageType.MT_DOCUMENT), 
                new MessageBody("message body"));
            
            
            //Act
            var context = new RequestContext();
            outbox.Add(messageToAdd, context);

            var retrievedMessage = outbox.Get(messageId, context);
            
            //Assert
            retrievedMessage.Should().NotBeNull();
            retrievedMessage.Id.Should().Be(messageId);
            retrievedMessage.Header.Topic.Should().Be(messageToAdd.Header.Topic);
            retrievedMessage.Header.MessageType.Should().Be(messageToAdd.Header.MessageType);
            retrievedMessage.Body.Value.Should().Be(messageToAdd.Body.Value);

        }

        [Fact]
        public void When_marking_dispatched_in_outbox()
        {
            //Arrange
            var outbox = new InMemoryOutbox(new FakeTimeProvider()){Tracer = new BrighterTracer()};
            
            var messageId = Guid.NewGuid().ToString();
            var messageToAdd = new Message(
                new MessageHeader(messageId, "test_topic", MessageType.MT_DOCUMENT), 
                new MessageBody("message body"));
            
            
            //Act
            var context = new RequestContext();
            outbox.Add(messageToAdd, context);
            var dispatchedAt = DateTime.UtcNow;
            outbox.MarkDispatched(messageId, context, dispatchedAt);

            var dispatchedMessages = outbox.DispatchedMessages(500, context);

            //Assert
            dispatchedMessages.Count().Should().Be(1);
            dispatchedMessages.First().Id.Should().Be(messageId);

        }

        [Fact]
        public void When_looking_for_undispatched_messages_in_outbox()
        {
            //Arrange
            var timeProvider = new FakeTimeProvider();
            var outbox = new InMemoryOutbox(timeProvider){Tracer = new BrighterTracer()};
            
            var messageId = Guid.NewGuid().ToString();
            var messageToAdd = new Message(
                new MessageHeader(messageId, "test_topic", MessageType.MT_DOCUMENT), 
                new MessageBody("message body"));
            
            
            //Act
            var context = new RequestContext();
            outbox.Add(messageToAdd, context);
            
            timeProvider.Advance(TimeSpan.FromMilliseconds(500));

            var outstandingMessages = outbox.OutstandingMessages(0, context);
            
            //Assert
            outstandingMessages.Count().Should().Be(1);
            outstandingMessages.First().Id.Should().Be(messageId);

        }

        [Fact]
        public void When_there_are_multiple_items_retrieve_by_id()
        {
            //Arrange
            var outbox = new InMemoryOutbox( new FakeTimeProvider()){Tracer = new BrighterTracer()};

            var messageIds = new[] { Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), };
            var context = new RequestContext();
            for(int i =0; i <= 4; i++)
            {
                RequestContext requestContext = context;
                outbox.Add(new MessageTestDataBuilder().WithId(messageIds[i]), requestContext);
            }

            //Act 
            var message = outbox.Get(messageIds[2], context);
            
            //Assert
            message.Id.Should().Be(messageIds[2]);
        }

        [Fact]
        public async Task When_there_are_multiple_items_and_some_are_dispatched()
        {
            //Arrange
            var timeProvider = new FakeTimeProvider();
            var outbox = new InMemoryOutbox(timeProvider){Tracer = new BrighterTracer()};

            var messageIds = new string[] { Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), };
            var context = new RequestContext();
            for(int i =0; i <= 4; i++)
            {
                RequestContext requestContext = context;
                outbox.Add(new MessageTestDataBuilder().WithId(messageIds[i]), requestContext);
            }

            //Act 
            var now = DateTime.UtcNow;
            outbox.MarkDispatched(messageIds[0], context, now);
            outbox.MarkDispatched(messageIds[4], context, now);

            timeProvider.Advance(TimeSpan.FromMilliseconds(500));

            var sentMessages = outbox.DispatchedMessages(5000, context);
            var outstandingMessages = outbox.OutstandingMessages(0, context);

            //Assert
            sentMessages.Count().Should().Be(2);
            outstandingMessages.Count().Should().Be(3);
            sentMessages.Any(msg => msg.Id == messageIds[0]).Should().BeTrue();
            sentMessages.Any(msg => msg.Id == messageIds[4]).Should().BeTrue();
            outstandingMessages.Any(msg => msg.Id == messageIds[1]).Should().BeTrue();
            outstandingMessages.Any(msg => msg.Id == messageIds[2]).Should().BeTrue();
            outstandingMessages.Any(msg => msg.Id == messageIds[3]).Should().BeTrue();
        }
   }
}
