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
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.DynamoDB.Tests.TestDoubles;
using Paramore.Brighter.Outbox.DynamoDB;
using Xunit;

namespace Paramore.Brighter.DynamoDB.Tests.Outbox
{
    [Trait("Category", "DynamoDB")]
    public class DynamoDbOutboxWritingUTF8MessageAsyncTests : DynamoDBOutboxBaseTest
    {
        private readonly Message _messageEarliest;
        private readonly string _key1 = "name1";
        private readonly string _key2 = "name2";
        private readonly string _key3 = "name3";
        private readonly string _key4 = "name4";
        private readonly string _key5 = "name5";
        private readonly string _value1 = "value1";
        private readonly string _value2 = "value2";
        private readonly int _value3 = 123;
        private readonly DateTime _value4 = DateTime.UtcNow;
        private readonly Guid _value5 = new Guid();

        private Message _storedMessage;
        private DynamoDbOutbox _dynamoDbOutbox;

        public DynamoDbOutboxWritingUTF8MessageAsyncTests()
        {
            var command = new MyCommand { Value = "Test", WasCancelled = false, TaskCompleted = false };
            var body = JsonSerializer.Serialize(command, JsonSerialisationOptions.Options);

            var messageHeader = new MessageHeader(
                messageId: Guid.NewGuid().ToString(),
                topic: new RoutingKey("test_topic"),
                messageType: MessageType.MT_DOCUMENT,
                timeStamp: DateTime.UtcNow.AddDays(-1),
                handledCount: 5,
                delayed: TimeSpan.FromMilliseconds(5),
                correlationId: Guid.NewGuid().ToString(),
                replyTo: new RoutingKey("ReplyAddress"),
                contentType: "text/plain");
            messageHeader.Bag.Add(_key1, _value1);
            messageHeader.Bag.Add(_key2, _value2);
            messageHeader.Bag.Add(_key3, _value3);
            messageHeader.Bag.Add(_key4, _value4);
            messageHeader.Bag.Add(_key5, _value5);

            _messageEarliest = new Message(messageHeader,
                new MessageBody(body, "application/json", CharacterEncoding.UTF8));
            var fakeTimeProvider = new FakeTimeProvider();
            _dynamoDbOutbox = new DynamoDbOutbox(Client,
                new DynamoDbConfiguration(OutboxTableName),
                fakeTimeProvider);
        }

        [Fact]
        public async Task When_writing_a_utf8_message_to_the_dynamo_db_outbox()
        {
            //arrange
            var context = new RequestContext();
            
            //act
            await _dynamoDbOutbox.AddAsync(_messageEarliest, context);

            _storedMessage = await _dynamoDbOutbox.GetAsync(_messageEarliest.Id, context);

            //assert
            Assert.Equal(_messageEarliest.Body.Value, _storedMessage.Body.Value);
            //should read the header from the outbox
            Assert.Equal(_messageEarliest.Header.Topic, _storedMessage.Header.Topic);
            Assert.Equal(_messageEarliest.Header.MessageType, _storedMessage.Header.MessageType);
            _storedMessage.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ").Should()
                .Be(_messageEarliest.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
            Assert.Equal(0, _storedMessage.Header.HandledCount); // -- should be zero when read from outbox
            Assert.Equal(TimeSpan.Zero, _storedMessage.Header.Delayed); // -- should be zero when read from outbox
            Assert.Equal(_messageEarliest.Header.CorrelationId, _storedMessage.Header.CorrelationId);
            Assert.Equal(_messageEarliest.Header.ReplyTo, _storedMessage.Header.ReplyTo);
            Assert.Equal(_messageEarliest.Header.ContentType, _storedMessage.Header.ContentType);

            //Bag serialization
            Assert.True(_storedMessage.Header.Bag.ContainsKey(_key1));
            Assert.Equal(_value1, _storedMessage.Header.Bag[_key1]);

            Assert.True(_storedMessage.Header.Bag.ContainsKey(_key2));
            Assert.Equal(_value2, _storedMessage.Header.Bag[_key2]);

            Assert.True(_storedMessage.Header.Bag.ContainsKey(_key3));
            Assert.Equal(_value3, _storedMessage.Header.Bag[_key3]);

            Assert.True(_storedMessage.Header.Bag.ContainsKey(_key4));
            Assert.Equal(_value4, _storedMessage.Header.Bag[_key4]);

            Assert.True(_storedMessage.Header.Bag.ContainsKey(_key5));
            Assert.Equal(_value5, _storedMessage.Header.Bag[_key5]);
        }
    }
}
