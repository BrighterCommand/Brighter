#region Licence
/* The MIT License (MIT)
Copyright © 2015 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace Paramore.Brighter.Tests.Outbox.DynamoDB
{
    [Trait("Category", "DynamoDB")]
    [Collection("DynamoDB OutBox")]
    public class DynamoDbOutboxWritingMessagesAsyncTests : BaseDynamoDBOutboxTests
    {
        private readonly Message _messageEarliest;
        private readonly Message _message2;
        private readonly Message _messageLatest;
        private readonly Guid[] _guids;

        public DynamoDbOutboxWritingMessagesAsyncTests()
        {
            _guids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

            _messageEarliest = new Message(new MessageHeader(_guids[0], "Test", MessageType.MT_COMMAND, DateTime.UtcNow.AddHours(-3)), new MessageBody("Body"));
            _message2 = new Message(new MessageHeader(_guids[1], "Test2", MessageType.MT_COMMAND, DateTime.UtcNow.AddHours(-2)), new MessageBody("Body2"));            
            _messageLatest = new Message(new MessageHeader(_guids[2], "Test3", MessageType.MT_COMMAND, DateTime.UtcNow.AddHours(-1)), new MessageBody("Body3"));
        }

        [Fact]
        public async Task When_writing_messages_to_the_outbox_async()
        {            
            await DynamoDbOutbox.AddAsync(_messageEarliest);
            await DynamoDbOutbox.AddAsync(_message2);
            await DynamoDbOutbox.AddAsync(_messageLatest);

            var retrievedMessages = (await _dynamoDbTestHelper.Scan()).ToList();

            //_should_read_the_messages_from_the__message_store 
            retrievedMessages.Should().HaveCount(3);
            retrievedMessages.Single(m => m.MessageId == _guids[0].ToString()).Should().NotBeNull();
            retrievedMessages.Single(m => m.MessageId == _guids[1].ToString()).Should().NotBeNull();
            retrievedMessages.Single(m => m.MessageId == _guids[2].ToString()).Should().NotBeNull();        
        }
    }
}
