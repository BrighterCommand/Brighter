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
using NUnit.Framework;
using paramore.brighter.commandprocessor.messagestore.mssql;

namespace paramore.brighter.commandprocessor.tests.nunit.MessageStore.MsSql
{
    [TestFixture]
    public class SqlMessageStoreWritingMessageTests
    {
        private readonly string key1 = "name1";
        private readonly string key2 = "name2";
        private Message _messageEarliest;
        private MsSqlMessageStore _sqlMessageStore;
        private Message _storedMessage;
        private readonly string value1 = "value1";
        private readonly string value2 = "value2";
        private MsSqlTestHelper _msSqlTestHelper;

        [SetUp]
        public void Establish()
        {
            _msSqlTestHelper = new MsSqlTestHelper();
            _msSqlTestHelper.SetupMessageDb();

            _sqlMessageStore = new MsSqlMessageStore(_msSqlTestHelper.MessageStoreConfiguration);
            var messageHeader = new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_DOCUMENT, DateTime.UtcNow.AddDays(-1), 5, 5);
            messageHeader.Bag.Add(key1, value1);
            messageHeader.Bag.Add(key2, value2);

            _messageEarliest = new Message(messageHeader, new MessageBody("message body"));
            _sqlMessageStore.Add(_messageEarliest);
        }

        [Test]
        public void When_Writing_A_Message_To_The_MSSQL_Message_Store()
        {
            _storedMessage = _sqlMessageStore.Get(_messageEarliest.Id);

            //_should_read_the_message_from_the__sql_message_store
            _storedMessage.Body.Value.ShouldEqual(_messageEarliest.Body.Value);
            //_should_read_the_message_header_first_bag_item_from_the__sql_message_store
            _storedMessage.Header.Bag.ContainsKey(key1).ShouldBeTrue();
            _storedMessage.Header.Bag[key1].ShouldEqual(value1);
            //_should_read_the_message_header_second_bag_item_from_the__sql_message_store
            _storedMessage.Header.Bag.ContainsKey(key2).ShouldBeTrue();
            _storedMessage.Header.Bag[key2].ShouldEqual(value2);
            //_should_read_the_message_header_timestamp_from_the__sql_message_store
            _storedMessage.Header.TimeStamp.ShouldEqual(_messageEarliest.Header.TimeStamp);
           //_should_read_the_message_header_topic_from_the__sql_message_store
            _storedMessage.Header.Topic.ShouldEqual(_messageEarliest.Header.Topic);
            //_should_read_the_message_header_type_from_the__sql_message_store
            _storedMessage.Header.MessageType.ShouldEqual(_messageEarliest.Header.MessageType);
        }

        [TearDown]
        public void Cleanup()
        {
            CleanUpDb();
        }

        private void CleanUpDb()
        {
            _msSqlTestHelper.CleanUpDb();
        }
    }
}