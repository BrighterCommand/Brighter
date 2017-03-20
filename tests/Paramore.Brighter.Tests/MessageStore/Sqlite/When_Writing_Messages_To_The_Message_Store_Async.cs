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
using Nito.AsyncEx;
using Xunit;
using Paramore.Brighter.MessageStore.Sqlite;
using Paramore.Brighter.Time;

namespace Paramore.Brighter.Tests.messagestore.sqlite
{
    public class SqlMessageStoreWritngMessagesAsyncTests : IDisposable
    {
        private SqliteTestHelper _sqliteTestHelper;
        private SqliteMessageStore _sSqlMessageStore;
        private Message _message2;
        private Message _messageEarliest;
        private Message _messageLatest;
        private IList<Message> _retrievedMessages;

        public SqlMessageStoreWritngMessagesAsyncTests()
        {
            _sqliteTestHelper = new SqliteTestHelper();
            _sqliteTestHelper.SetupMessageDb();
            _sSqlMessageStore = new SqliteMessageStore(new SqliteMessageStoreConfiguration(_sqliteTestHelper.ConnectionString, _sqliteTestHelper.TableName_Messages));
            Clock.OverrideTime = DateTime.UtcNow.AddHours(-3);
            _messageEarliest = new Message(new MessageHeader(Guid.NewGuid(), "Test", MessageType.MT_COMMAND),
                new MessageBody("Body"));
            AsyncContext.Run(async () => await _sSqlMessageStore.AddAsync(_messageEarliest));

            Clock.OverrideTime = DateTime.UtcNow.AddHours(-2);

            _message2 = new Message(new MessageHeader(Guid.NewGuid(), "Test2", MessageType.MT_COMMAND),
                new MessageBody("Body2"));
            AsyncContext.Run(async () => await _sSqlMessageStore.AddAsync(_message2));

            Clock.OverrideTime = DateTime.UtcNow.AddHours(-1);

            _messageLatest = new Message(new MessageHeader(Guid.NewGuid(), "Test3", MessageType.MT_COMMAND),
                new MessageBody("Body3"));
            AsyncContext.Run(async () => await _sSqlMessageStore.AddAsync(_messageLatest));
        }

        [Fact]
        public void When_Writing_Messages_To_The_Message_Store_Async()
        {
            AsyncContext.Run(async () => _retrievedMessages = await _sSqlMessageStore.GetAsync());

            //_should_read_first_message_last_from_the__message_store
            _retrievedMessages.Last().Id.Should().Be(_messageEarliest.Id);
            //_should_read_last_message_first_from_the__message_store
            _retrievedMessages.First().Id.Should().Be(_messageLatest.Id);
            //_should_read_the_messages_from_the__message_store
            _retrievedMessages.Count().Should().Be(3);
        }

        public void Dispose()
        {
            _sqliteTestHelper.CleanUpDb();
        }
    }
}
