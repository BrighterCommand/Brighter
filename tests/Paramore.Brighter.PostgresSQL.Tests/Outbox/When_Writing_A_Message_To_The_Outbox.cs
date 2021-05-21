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
using FluentAssertions;
using Paramore.Brighter.Outbox.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.Outbox
{
    [Trait("Category", "PostgresSql")]
    public class SqlOutboxWritingMessageTests : IDisposable
    {
        private readonly string _key1 = "name1";
        private readonly string _key2 = "name2";
        private readonly string _key3 = "name3";
        private readonly string _key4 = "name4";
        private readonly string _key5 = "name5";
        private readonly Message _messageEarliest;
        private readonly PostgreSqlOutbox _sqlOutbox;
        private Message _storedMessage;
        private readonly string _value1 = "value1";
        private readonly string _value2 = "value2";
        private readonly int _value3 = 123;
        private readonly Guid _value4 = Guid.NewGuid();
        private readonly DateTime _value5 = DateTime.UtcNow;
        private readonly PostgresSqlTestHelper _postgresSqlTestHelper;

        public SqlOutboxWritingMessageTests()
        {
            _postgresSqlTestHelper = new PostgresSqlTestHelper();
            _postgresSqlTestHelper.SetupMessageDb();

            _sqlOutbox = new PostgreSqlOutbox(_postgresSqlTestHelper.OutboxConfiguration);
            var messageHeader = new MessageHeader(
                messageId:Guid.NewGuid(), 
                topic: "test_topic", 
                messageType: MessageType.MT_DOCUMENT, 
                timeStamp: DateTime.UtcNow.AddDays(-1), 
                handledCount:5, 
                delayedMilliseconds:5,
                correlationId: Guid.NewGuid(),
                replyTo: "ReplyTo",
                contentType: "text/plain");
            messageHeader.Bag.Add(_key1, _value1);
            messageHeader.Bag.Add(_key2, _value2);
            messageHeader.Bag.Add(_key3, _value3);
            messageHeader.Bag.Add(_key4, _value4);
            messageHeader.Bag.Add(_key5, _value5);

            _messageEarliest = new Message(messageHeader, new MessageBody("message body"));
            _sqlOutbox.Add(_messageEarliest);
        }

        [Fact]
        public void When_Writing_A_Message_To_The_PostgreSql_Outbox()
        {
            _storedMessage = _sqlOutbox.Get(_messageEarliest.Id);

            //should read the message from the sql outbox
            _storedMessage.Body.Value.Should().Be(_messageEarliest.Body.Value);
            //should read the header from the sql outbox
            _storedMessage.Header.Topic.Should().Be(_messageEarliest.Header.Topic);
            _storedMessage.Header.MessageType.Should().Be(_messageEarliest.Header.MessageType);
            _storedMessage.Header.TimeStamp.Should().Be(_messageEarliest.Header.TimeStamp);
            _storedMessage.Header.HandledCount.Should().Be(0); // -- should be zero when read from outbox
            _storedMessage.Header.DelayedMilliseconds.Should().Be(0); // -- should be zero when read from outbox
            _storedMessage.Header.CorrelationId.Should().Be(_messageEarliest.Header.CorrelationId);
            _storedMessage.Header.ReplyTo.Should().Be(_messageEarliest.Header.ReplyTo);
            _storedMessage.Header.ContentType.Should().Be(_messageEarliest.Header.ContentType);
             
            
            //Bag serialization
            _storedMessage.Header.Bag.ContainsKey(_key1).Should().BeTrue();
            _storedMessage.Header.Bag[_key1].Should().Be(_value1);
            _storedMessage.Header.Bag.ContainsKey(_key2).Should().BeTrue();
            _storedMessage.Header.Bag[_key2].Should().Be(_value2);
            _storedMessage.Header.Bag.ContainsKey(_key3).Should().BeTrue();
            _storedMessage.Header.Bag[_key3].Should().Be(_value3);
            _storedMessage.Header.Bag.ContainsKey(_key4).Should().BeTrue();
            _storedMessage.Header.Bag[_key4].Should().Be(_value4);
            _storedMessage.Header.Bag.ContainsKey(_key5).Should().BeTrue();
            _storedMessage.Header.Bag[_key5].Should().Be(_value5);
        }

        public void Dispose()
        {
            _postgresSqlTestHelper.CleanUpDb();
        }
    }
}
