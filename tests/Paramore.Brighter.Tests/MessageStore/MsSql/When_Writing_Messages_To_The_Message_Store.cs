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
using Xunit;
using Paramore.Brighter.MessageStore.MsSql;
using Paramore.Brighter.Time;

namespace Paramore.Brighter.Tests.MessageStore.MsSql
{
    [Trait("Category", "MSSQL")]
    [Collection("MSSQL MessageStore")]
    public class SqlMessageStoreWritngMessagesTests : IDisposable
    {
        private readonly MsSqlTestHelper _msSqlTestHelper;
        private readonly Message _messageEarliest;
        private readonly Message _messageLatest;
        private IEnumerable<Message> _retrievedMessages;
        private readonly MsSqlMessageStore _sqlMessageStore;

        public SqlMessageStoreWritngMessagesTests()
        {
            _msSqlTestHelper = new MsSqlTestHelper();
            _msSqlTestHelper.SetupMessageDb();

            _sqlMessageStore = new MsSqlMessageStore(_msSqlTestHelper.MessageStoreConfiguration);
            Clock.OverrideTime = DateTime.UtcNow.AddHours(-3);
            _messageEarliest = new Message(new MessageHeader(Guid.NewGuid(), "Test", MessageType.MT_COMMAND), new MessageBody("Body"));
            _sqlMessageStore.Add(_messageEarliest);

            Clock.OverrideTime = DateTime.UtcNow.AddHours(-2);

            var message2 = new Message(new MessageHeader(Guid.NewGuid(), "Test2", MessageType.MT_COMMAND), new MessageBody("Body2"));
            _sqlMessageStore.Add(message2);

            Clock.OverrideTime = DateTime.UtcNow.AddHours(-1);

            _messageLatest = new Message(new MessageHeader(Guid.NewGuid(), "Test3", MessageType.MT_COMMAND), new MessageBody("Body3"));
            _sqlMessageStore.Add(_messageLatest);
        }

        [Fact]
        public void When_Writing_Messages_To_The_Message_Store()
        {
            _retrievedMessages = _sqlMessageStore.Get();

            //_should_read_first_message_last_from_the__message_store
            _retrievedMessages.Last().Id.Should().Be(_messageEarliest.Id);
            //_should_read_last_message_first_from_the__message_store
            _retrievedMessages.First().Id.Should().Be(_messageLatest.Id);
            //_should_read_the_messages_from_the__message_store
            _retrievedMessages.Should().HaveCount(3);
        }

        public void Dispose()
        {
            _msSqlTestHelper.CleanUpDb();
        }
    }
}