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

namespace paramore.brighter.commandprocessor.tests.nunit.messagestore.sqlite
{
    [TestFixture]
    public class SqliteMessageStoreWritingMessageTests
    {
        private SqliteTestHelper _sqliteTestHelper;
        private SqliteMessageStore _SqlMessageStore;
        private readonly string key1 = "name1";
        private readonly string key2 = "name2";
        private Message _messageEarliest;
        private Message _storedMessage;
        private readonly string value1 = "value1";
        private readonly string value2 = "value2";

        [SetUp]
        public void Establish()
        {
            _sqliteTestHelper = new SqliteTestHelper();
            _sqliteTestHelper.SetupMessageDb();
            _SqlMessageStore = new SqliteMessageStore(new SqliteMessageStoreConfiguration(_sqliteTestHelper.ConnectionString, _sqliteTestHelper.TableName_Messages));
            var messageHeader = new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_DOCUMENT,
                DateTime.UtcNow.AddDays(-1), 5, 5);
            messageHeader.Bag.Add(key1, value1);
            messageHeader.Bag.Add(key2, value2);

            _messageEarliest = new Message(messageHeader, new MessageBody("message body"));
            _SqlMessageStore.Add(_messageEarliest);
        }

        [Test]
        public void When_Writing_A_Message_To_The_Message_Store()
        {
            _storedMessage = _SqlMessageStore.Get(_messageEarliest.Id);

            //_should_read_the_message_from_the__sql_message_store
            Assert.AreEqual(_messageEarliest.Body.Value, _storedMessage.Body.Value);
            //_should_read_the_message_header_first_bag_item_from_the__sql_message_store
            Assert.True(_storedMessage.Header.Bag.ContainsKey(key1));
            Assert.AreEqual(value1, _storedMessage.Header.Bag[key1]);
            //_should_read_the_message_header_second_bag_item_from_the__sql_message_store
            Assert.True(_storedMessage.Header.Bag.ContainsKey(key2));
            Assert.AreEqual(value2, _storedMessage.Header.Bag[key2]);
            //_should_read_the_message_header_timestamp_from_the__sql_message_store
            Assert.AreEqual(_messageEarliest.Header.TimeStamp, _storedMessage.Header.TimeStamp);
            //_should_read_the_message_header_topic_from_the__sql_message_store
            Assert.AreEqual(_messageEarliest.Header.Topic, _storedMessage.Header.Topic);
            //_should_read_the_message_header_type_from_the__sql_message_store
            Assert.AreEqual(_messageEarliest.Header.MessageType, _storedMessage.Header.MessageType);
        }


        [TearDown]
        public void Cleanup()
        {
            _sqliteTestHelper.CleanUpDb();
        }
    }
}