#region Licence

/* The MIT License (MIT)
Copyright � 2014 Francesco Pighi <francesco.pighi@gmail.com>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the �Software�), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED �AS IS�, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
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
using NUnit.Framework;
using Paramore.Brighter.Messagestore.MsSql;
using Paramore.Brighter.Time;

namespace Paramore.Brighter.Tests.MessageStore.MsSql
{
    [Category("MSSQL")]
    [TestFixture]
    public class SqlMessageStoreWritngMessagesTests
    {
        private MsSqlTestHelper _msSqlTestHelper;
        private Message _message2;
        private Message _messageEarliest;
        private Message _messageLatest;
        private IEnumerable<Message> _retrievedMessages;
        private MsSqlMessageStore _sqlMessageStore;

        [SetUp]
        public void Establish()
        {
            _msSqlTestHelper = new MsSqlTestHelper();
            _msSqlTestHelper.SetupMessageDb();

            _sqlMessageStore = new MsSqlMessageStore(_msSqlTestHelper.MessageStoreConfiguration);
            Clock.OverrideTime = DateTime.UtcNow.AddHours(-3);
            _messageEarliest = new Message(new MessageHeader(Guid.NewGuid(), "Test", MessageType.MT_COMMAND),
                new MessageBody("Body"));
            _sqlMessageStore.Add(_messageEarliest);

            Clock.OverrideTime = DateTime.UtcNow.AddHours(-2);

            _message2 = new Message(new MessageHeader(Guid.NewGuid(), "Test2", MessageType.MT_COMMAND),
                new MessageBody("Body2"));
            _sqlMessageStore.Add(_message2);

            Clock.OverrideTime = DateTime.UtcNow.AddHours(-1);

            _messageLatest = new Message(new MessageHeader(Guid.NewGuid(), "Test3", MessageType.MT_COMMAND),
                new MessageBody("Body3"));
            _sqlMessageStore.Add(_messageLatest);
        }

        [Test]
        public void When_Writing_Messages_To_The_Message_Store()
        {
            _retrievedMessages = _sqlMessageStore.Get();

            //_should_read_first_message_last_from_the__message_store
            Assert.AreEqual(_messageEarliest.Id, _retrievedMessages.Last().Id);
            //_should_read_last_message_first_from_the__message_store
            Assert.AreEqual(_messageLatest.Id, _retrievedMessages.First().Id);
            //_should_read_the_messages_from_the__message_store
            Assert.AreEqual(3, _retrievedMessages.Count());
        }

        [TearDown]
        public void Cleanup()
        {
            CleanUpDb();
        }

        public void CleanUpDb()
        {
            _msSqlTestHelper.CleanUpDb();
        }
    }
}