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
using System.Linq;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using Xunit;
using Paramore.Brighter.MessageStore.Sqlite;

namespace Paramore.Brighter.Tests.messagestore.sqlite
{
    public class SQlMessageStoreMigrationTests : IDisposable
    {
        private SqliteMessageStore _sqlMessageStore;
        private Message _message;
        private Message _storedMessage;
        private SqliteTestHelper _sqliteTestHelper;

        public SQlMessageStoreMigrationTests()
        {
            _sqliteTestHelper = new SqliteTestHelper();
            _sqliteTestHelper.SetupMessageDb();
            _sqlMessageStore  = new SqliteMessageStore(new SqliteMessageStoreConfiguration(_sqliteTestHelper.ConnectionString, _sqliteTestHelper.TableName_Messages));

            _message = new Message(new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_DOCUMENT), new MessageBody("message body"));
            AddHistoricMessage(_message);
        }

        private void AddHistoricMessage(Message message)
        {
            var sql = string.Format("INSERT INTO {0} (MessageId, MessageType, Topic, Timestamp, HeaderBag, Body) VALUES (@MessageId, @MessageType, @Topic, @Timestamp, @HeaderBag, @Body)", _sqliteTestHelper.TableName_Messages);
            var parameters = new[]
            {
                new SqliteParameter("MessageId", message.Id.ToString()),
                new SqliteParameter("MessageType", message.Header.MessageType.ToString()),
                new SqliteParameter("Topic", message.Header.Topic),
                new SqliteParameter("Timestamp", SqliteType.Text) { Value =message.Header.TimeStamp.ToString("s")},
                new SqliteParameter("HeaderBag",SqliteType.Text) { Value = JsonConvert.SerializeObject(message.Header.Bag)},
                new SqliteParameter("Body", message.Body.Value),
            };

            using (var connection = new SqliteConnection(_sqliteTestHelper.ConnectionString))
            using (var command = connection.CreateCommand())
            {
                connection.Open();

                command.CommandText = sql;
                //command.Parameters.AddRange(parameters); used to work... but can't with current Sqlite lib. Iterator issue
                for (var index = 0; index < parameters.Length; index++)
                {
                    command.Parameters.Add(parameters[index]);
                }
                command.ExecuteNonQuery();
            }
        }

        [Fact]
        public void When_writing_a_message_with_minimal_header_information_to_the_message_store()
        {
            _storedMessage = _sqlMessageStore.Get(_message.Id);

            //_should_read_the_message_from_the__sql_message_store
            Assert.AreEqual(_message.Body.Value, _storedMessage.Body.Value);
            //_should_read_the_message_header_type_from_the__sql_message_store
            Assert.AreEqual(_message.Header.MessageType, _storedMessage.Header.MessageType);
            //_should_read_the_message_header_topic_from_the__sql_message_store
            Assert.AreEqual(_message.Header.Topic, _storedMessage.Header.Topic);
            //_should_default_the_timestamp_from_the__sql_message_store
            Assert.GreaterOrEqual(_storedMessage.Header.TimeStamp, _message.Header.TimeStamp);
            //_should_read_empty_header_bag_from_the__sql_message_store
            Assert.False(_storedMessage.Header.Bag.Keys.Any());
        }

        public void Dispose()
        {
            _sqliteTestHelper.CleanUpDb();
        }
    }
}
