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
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Outbox
{
    public class InMemoryOutboxTests
    {
        [Fact]
        public void When_reading_from_outbox()
        {
            //Arrange
            var outbox = new InMemoryOutbox();
            
            var messageId = Guid.NewGuid();
            var messageToAdd = new Message(
                new MessageHeader(messageId, "test_topic", MessageType.MT_DOCUMENT), 
                new MessageBody("message body"));
            
            
            //Act
            outbox.Add(messageToAdd);

            var retrievedMessage = outbox.Get(messageId);
            
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
            var outbox = new InMemoryOutbox();
            
            var messageId = Guid.NewGuid();
            var messageToAdd = new Message(
                new MessageHeader(messageId, "test_topic", MessageType.MT_DOCUMENT), 
                new MessageBody("message body"));
            
            
            //Act
            outbox.Add(messageToAdd);
            var dispatchedAt = DateTime.Now;
            outbox.MarkDispatched(messageId, dispatchedAt);

            var dispatchedMessages = outbox.DispatchedMessages(500);

            //Assert
            dispatchedMessages.Count().Should().Be(1);
            dispatchedMessages.First().Id.Should().Be(messageId);

        }

        [Fact]
        public void When_looking_for_undispatched_messages_in_outbox()
        {
            //Arrange
            var outbox = new InMemoryOutbox();
            
            var messageId = Guid.NewGuid();
            var messageToAdd = new Message(
                new MessageHeader(messageId, "test_topic", MessageType.MT_DOCUMENT), 
                new MessageBody("message body"));
            
            
            //Act
            outbox.Add(messageToAdd);

            var outstandingMessages = outbox.OutstandingMessages(5000);
            
            //Assert
            outstandingMessages.Count().Should().Be(1);
            outstandingMessages.First().Id.Should().Be(messageId);

        }
        
        
   }
}
