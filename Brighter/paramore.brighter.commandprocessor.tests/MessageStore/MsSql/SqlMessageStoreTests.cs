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
using System.Data.SqlServerCe;
using System.IO;
using System.Linq;
using Machine.Specifications;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.messagestore.mssql;

namespace paramore.commandprocessor.tests.MessageStore.MsSql
{
    public class SqlMessageStoreTests
    {
        private const string TestDbPath = "test.sdf";
        private const string ConnectionString = "DataSource=\"" + TestDbPath + "\"";
        private const string TableName = "test_messages";

        private Establish _context = () =>
        {
            CleanUpDb();
            CreateTestDb();

            s_sqlMessageStore = new MsSqlMessageStore(
                    new MsSqlMessageStoreConfiguration(ConnectionString, TableName, MsSqlMessageStoreConfiguration.DatabaseType.SqlCe),
                    new LogProvider.NoOpLogger());
        };

        public class when_writing_a_message_to_the_message_store
        {
            private Establish _context = () =>
            {
                var messageHeader = new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_DOCUMENT, DateTime.UtcNow.AddDays(-1), 5, 5);
                messageHeader.Bag.Add(key1, value1 );
                messageHeader.Bag.Add(key2, value2);
                
                s_messageEarliest = new Message(messageHeader, new MessageBody("message body"));
                s_sqlMessageStore.Add(s_messageEarliest).Wait();
            };

            private Because _of = () => { s_storedMessage = s_sqlMessageStore.Get(s_messageEarliest.Id).Result; };

            private It _should_read_the_message_from_the__sql_message_store = () => s_storedMessage.Body.Value.ShouldEqual(s_messageEarliest.Body.Value);
            private It _should_read_the_message_header_type_from_the__sql_message_store = () => s_storedMessage.Header.MessageType.ShouldEqual(s_messageEarliest.Header.MessageType);
            private It _should_read_the_message_header_topic_from_the__sql_message_store = () => s_storedMessage.Header.Topic.ShouldEqual(s_messageEarliest.Header.Topic);
            private It _should_read_the_message_header_timestamp_from_the__sql_message_store = () => s_storedMessage.Header.TimeStamp.ShouldEqual(s_messageEarliest.Header.TimeStamp);
            private It _should_read_the_message_header_first_bag_item_from_the__sql_message_store = () =>
                                {
                                    s_storedMessage.Header.Bag.ContainsKey(key1).ShouldBeTrue();
                                    s_storedMessage.Header.Bag[key1].ShouldEqual(value1);
                                };
            private It _should_read_the_message_header_second_bag_item_from_the__sql_message_store = () =>
            {
                s_storedMessage.Header.Bag.ContainsKey(key2).ShouldBeTrue();
                s_storedMessage.Header.Bag[key2].ShouldEqual(value2);
            };
                
            private static string key1 = "name1";
            private static string key2 = "name2";
            private static string value1 = "value1";
            private static string value2 = "value2";
        }

        public class when_writing_messages_to_the_message_store
        {
            private Establish _context = () =>
            {
                Clock.OverrideTime = DateTime.UtcNow.AddHours(-3);
                s_messageEarliest = new Message(new MessageHeader(Guid.NewGuid(), "Test", MessageType.MT_COMMAND), new MessageBody("Body"));
                s_sqlMessageStore.Add(s_messageEarliest).Wait();

                Clock.OverrideTime = DateTime.UtcNow.AddHours(-2);
                
                s_message2 = new Message(new MessageHeader(Guid.NewGuid(), "Test2", MessageType.MT_COMMAND), new MessageBody("Body2"));
                s_sqlMessageStore.Add(s_message2).Wait();

                Clock.OverrideTime = DateTime.UtcNow.AddHours(-1);

                s_messageLatest = new Message(new MessageHeader(Guid.NewGuid(), "Test3", MessageType.MT_COMMAND), new MessageBody("Body3"));
                s_sqlMessageStore.Add(s_messageLatest).Wait();
            };

            private Because _of = () => { s_retrievedMessages = s_sqlMessageStore.Get().Result; };

            private It _should_read_the_messages_from_the__message_store = () => s_retrievedMessages.Count().ShouldEqual(3);
            private It _should_read_last_message_first_from_the__message_store = () => s_retrievedMessages.First().Id.ShouldEqual(s_messageLatest.Id);
            private It _should_read_first_message_last_from_the__message_store = () => s_retrievedMessages.Last().Id.ShouldEqual(s_messageEarliest.Id);
            private static Message s_message2;
            private static Message s_messageLatest;
            private static IEnumerable<Message> s_retrievedMessages;
        }

        public class when_there_is_no_message_in_the_sql_message_store
        {
            private Establish _context = () => { s_messageEarliest = new Message(new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_DOCUMENT), new MessageBody("message body")); };
            private Because _of = () => { s_storedMessage = s_sqlMessageStore.Get(s_messageEarliest.Id).Result; };
            private It _should_return_a_empty_message = () => s_storedMessage.Header.MessageType.ShouldEqual(MessageType.MT_NONE);
        }


        public class when_the_message_is_already_in_the_message_store
        {
            private static Exception s_exception;

            private Establish _context = () =>
            {
                s_messageEarliest = new Message(new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_DOCUMENT), new MessageBody("message body"));
                s_sqlMessageStore.Add(s_messageEarliest).Wait();
            };

            private Because _of = () => { s_exception = Catch.Exception(() => s_sqlMessageStore.Add(s_messageEarliest).Wait()); };

            private It _should_ignore_the_duplcate_key_and_still_succeed = () => { s_exception.ShouldBeNull(); };
        }

        public class when_there_are_multiple_messages_in_the_message_store_and_a_range_is_fetched
        {
            private Establish _context = () =>
            {
                s_messageEarliest = new Message(new MessageHeader(Guid.NewGuid(), _TopicFirstMessage, MessageType.MT_DOCUMENT), new MessageBody("message body"));
                s_message1 = new Message(new MessageHeader(Guid.NewGuid(), "test_topic2", MessageType.MT_DOCUMENT), new MessageBody("message body2"));
                s_message2 = new Message(new MessageHeader(Guid.NewGuid(), _TopicLastMessage, MessageType.MT_DOCUMENT), new MessageBody("message body3"));
                s_sqlMessageStore.Add(s_messageEarliest).Wait();
                s_sqlMessageStore.Add(s_message1).Wait();
                s_sqlMessageStore.Add(s_message2).Wait();
            };

            private Because _of = () => { messages = s_sqlMessageStore.Get(1, 3).Result; };

            private It _should_not_fetch_null_messages = () => { messages.ShouldNotBeNull(); };
            private It _should_fetch_1_message = () => { messages.Count.ShouldEqual(1); };
            private It _should_fetch_expected_message = () => { messages[0].Header.Topic.ShouldEqual(_TopicLastMessage); };

            private static Message s_message1;
            private static Message s_message2;
            private static IList<Message> messages;
            private static string _TopicFirstMessage = "test_topic";
            private static string _TopicLastMessage = "test_topic3";
        }

        private Cleanup _cleanup = () => CleanUpDb();
        private static MsSqlMessageStore s_sqlMessageStore;
        private static Message s_storedMessage;
        private static Message s_messageEarliest;

        private static void CleanUpDb()
        {
            File.Delete(TestDbPath);
        }

        private static void CreateTestDb()
        {
            var en = new SqlCeEngine(ConnectionString);
            en.CreateDatabase();

            var sql = SqlMessageStoreBuilder.GetDDL(TableName);

            using (var cnn = new SqlCeConnection(ConnectionString))
            using (var cmd = cnn.CreateCommand())
            {
                cmd.CommandText = sql;
                cnn.Open();
                cmd.ExecuteNonQuery();
            }
        }
    }
}
