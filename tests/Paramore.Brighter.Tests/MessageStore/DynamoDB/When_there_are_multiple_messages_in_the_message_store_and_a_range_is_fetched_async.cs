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
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Paramore.Brighter.Tests.MessageStore.DynamoDB
{
    [Trait("Category", "AWS")]
    [Collection("DynamoDB MessageStore")]
    public class DynamoDbMessageStoreRangeRequestAsyncTests : BaseDynamoDBMessageStoreTests
    {        
        private readonly string _TopicFirstMessage = "test_topic";
        private readonly string _TopicLastMessage = "test_topic3";
        private readonly Message _message1, _message2, _messageEarliest;

        public DynamoDbMessageStoreRangeRequestAsyncTests()
        {
            _messageEarliest = new Message(new MessageHeader(Guid.NewGuid(), _TopicFirstMessage, MessageType.MT_DOCUMENT), new MessageBody("message body"));
            _message1 = new Message(new MessageHeader(Guid.NewGuid(), "test_topic2", MessageType.MT_DOCUMENT), new MessageBody("message body2"));
            _message2 = new Message(new MessageHeader(Guid.NewGuid(), _TopicLastMessage, MessageType.MT_DOCUMENT), new MessageBody("message body3"));            
        }

        [Fact]
        public async Task When_there_are_multiple_messages_in_the_message_store_and_a_range_by_index_is_fetched()
        {
            await _dynamoDbMessageStore.AddAsync(_messageEarliest);
            await Task.Delay(100);
            await _dynamoDbMessageStore.AddAsync(_message1);
            await Task.Delay(100);
            await _dynamoDbMessageStore.AddAsync(_message2);

            var exception = await Catch.ExceptionAsync(() => _dynamoDbMessageStore.GetAsync(1, 3));
            exception.Should().BeOfType<NotSupportedException>();
        }
    }
}
