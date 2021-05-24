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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Outbox.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.Outbox
{
    [Trait("Category", "MySql")]
    public class MySqlOutboxRangeRequestAsyncTests : IDisposable
    {
        private readonly MySqlTestHelper _mySqlTestHelper;
        private readonly MySqlOutbox _mySqlOutbox;
        private readonly string _TopicFirstMessage = "test_topic";
        private readonly string _TopicLastMessage = "test_topic3";
        private IEnumerable<Message> _messages;
        private readonly Message _message1;
        private readonly Message _message2;
        private readonly Message _messageEarliest;

        public MySqlOutboxRangeRequestAsyncTests()
        {
            _mySqlTestHelper = new MySqlTestHelper();
            _mySqlTestHelper.SetupMessageDb();
            _mySqlOutbox = new MySqlOutbox(_mySqlTestHelper.OutboxConfiguration);
            _messageEarliest = new Message(new MessageHeader(Guid.NewGuid(), _TopicFirstMessage, MessageType.MT_DOCUMENT), new MessageBody("message body"));
            _message1 = new Message(new MessageHeader(Guid.NewGuid(), "test_topic2", MessageType.MT_DOCUMENT), new MessageBody("message body2"));
            _message2 = new Message(new MessageHeader(Guid.NewGuid(), _TopicLastMessage, MessageType.MT_DOCUMENT), new MessageBody("message body3"));
        }

        [Fact]
        public async Task When_There_Are_Multiple_Messages_In_The_Outbox_And_A_Range_Is_Fetched_Async()
        {
            await _mySqlOutbox.AddAsync(_messageEarliest);
            await Task.Delay(100);
            await _mySqlOutbox.AddAsync(_message1);
            await Task.Delay(100);
            await _mySqlOutbox.AddAsync(_message2);

            _messages = await _mySqlOutbox.GetAsync(1, 3);

            //should fetch 1 message
            _messages.Should().HaveCount(1);
            //should fetch expected message
            _messages.First().Header.Topic.Should().Be(_TopicLastMessage);
            //should not fetch null messages
            _messages.Should().NotBeNull();
        }

        public void Dispose()
        {
            _mySqlTestHelper.CleanUpDb();
        }
    }
}
