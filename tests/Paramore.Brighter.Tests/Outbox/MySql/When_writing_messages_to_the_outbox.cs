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
using FluentAssertions;
using Paramore.Brighter.Outbox.MySql;
using Xunit;

namespace Paramore.Brighter.Tests.Outbox.MySql
{
    [Trait("Category", "MySql")]
    public class MySqlOutboxWritngMessagesTests 
    {
        private readonly MySqlTestHelper _mySqlTestHelper;
        private readonly MySqlOutbox _mySqlOutbox;
        private readonly Message _messageEarliest;
        private readonly Message _messageLatest;
        private IEnumerable<Message> _retrievedMessages;

        public MySqlOutboxWritngMessagesTests()
        {
            _mySqlTestHelper = new MySqlTestHelper();
            _mySqlTestHelper.SetupMessageDb();
            _mySqlOutbox = new MySqlOutbox(_mySqlTestHelper.OutboxConfiguration);

            _messageEarliest = new Message(new MessageHeader(Guid.NewGuid(), "Test", MessageType.MT_COMMAND, DateTime.UtcNow.AddHours(-3)), new MessageBody("Body"));
            _mySqlOutbox.Add(_messageEarliest);

            var message2 = new Message(new MessageHeader(Guid.NewGuid(), "Test2", MessageType.MT_COMMAND, DateTime.UtcNow.AddHours(-2)), new MessageBody("Body2"));
            _mySqlOutbox.Add(message2);

            _messageLatest = new Message(new MessageHeader(Guid.NewGuid(), "Test3", MessageType.MT_COMMAND, DateTime.UtcNow.AddHours(-1)), new MessageBody("Body3"));
            _mySqlOutbox.Add(_messageLatest);
        }

        [Fact]
        public void When_Writing_Messages_To_The_Outbox()
        {
            _retrievedMessages = _mySqlOutbox.Get();

            // _should_read_first_message_last_from_the__outbox
            _retrievedMessages.Last().Id.Should().Be(_messageEarliest.Id);
            //_should_read_last_message_first_from_the__outbox
            _retrievedMessages.First().Id.Should().Be(_messageLatest.Id);
            //_should_read_the_messages_from_the__outbox
            _retrievedMessages.Should().HaveCount(3);
        }

        [Fact]
        public void Dispose()
        {
            _mySqlTestHelper.CleanUpDb();
        }
    }
}
