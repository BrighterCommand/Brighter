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
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using nUnitShouldAdapter;
using Newtonsoft.Json;
using NUnit.Specifications;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.messagestore.sqllite;

namespace paramore.brighter.commandprocessor.tests.nunit.MessageStore.SqlLite
{

    public class when_writing_a_message_with_minimal_header_information_to_the_message_store : ContextSpecification
    {
        private static SqlLiteMessageStore s_sqlMessageStore;
        private static Message s_message;
        private static Message s_storedMessage;
        private static SqliteConnection _sqliteConnection;
        private static SqlLiteTestHelper _sqlLiteTestHelper;

        private Establish _context = () =>
        {
            _sqlLiteTestHelper = new SqlLiteTestHelper();
            _sqliteConnection = _sqlLiteTestHelper.CreateMessageStoreConnection();
            s_sqlMessageStore  = new SqlLiteMessageStore(new SqlLiteMessageStoreConfiguration(_sqlLiteTestHelper.ConnectionString, _sqlLiteTestHelper.TableName_Messages), new LogProvider.NoOpLogger());

            s_message = new Message(new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_DOCUMENT), new MessageBody("message body"));
            AddHistoricMessage(s_message).Wait();
        };

        private static async Task AddHistoricMessage(Message message)
        {
            var sql = string.Format("INSERT INTO {0} (MessageId, MessageType, Topic, Timestamp, HeaderBag, Body) VALUES (@MessageId, @MessageType, @Topic, @Timestamp, @HeaderBag, @Body)", _sqlLiteTestHelper.TableName_Messages);
            var parameters = new[]
            {
                new SqliteParameter("MessageId", message.Id.ToString()),
                new SqliteParameter("MessageType", message.Header.MessageType.ToString()),
                new SqliteParameter("Topic", message.Header.Topic),
                new SqliteParameter("Timestamp", SqliteType.Text) { Value =message.Header.TimeStamp.ToString("s")},
                new SqliteParameter("HeaderBag",SqliteType.Text) { Value = JsonConvert.SerializeObject(message.Header.Bag)},
                new SqliteParameter("Body", message.Body.Value),
            };

            using (var connection = new SqliteConnection(_sqlLiteTestHelper.ConnectionString))
            using (var command = connection.CreateCommand())
            {
                await connection.OpenAsync();

                command.CommandText = sql;
                //command.Parameters.AddRange(parameters); used to work... but can't with current sqllite lib. Iterator issue
                for (var index = 0; index < parameters.Length; index++)
                {
                    command.Parameters.Add(parameters[index]);
                }
                await command.ExecuteNonQueryAsync();
            }
        }

        private Because _of = () => { s_storedMessage = s_sqlMessageStore.Get(s_message.Id); };

        private It _should_read_the_message_from_the__sql_message_store = () => s_storedMessage.Body.Value.ShouldEqual(s_message.Body.Value);
        private It _should_read_the_message_header_type_from_the__sql_message_store = () => s_storedMessage.Header.MessageType.ShouldEqual(s_message.Header.MessageType);
        private It _should_read_the_message_header_topic_from_the__sql_message_store = () => s_storedMessage.Header.Topic.ShouldEqual(s_message.Header.Topic);
        private It _should_default_the_timestamp_from_the__sql_message_store = () => s_storedMessage.Header.TimeStamp.ShouldBeGreaterThanOrEqualTo(s_message.Header.TimeStamp); //DateTime set in ctor on way out
        private It _should_read_empty_header_bag_from_the__sql_message_store = () => s_storedMessage.Header.Bag.Keys.Any().ShouldBeFalse();

        private Cleanup _cleanup = () =>
        {
            _sqlLiteTestHelper.CleanUpDb();
        };
    }
}
