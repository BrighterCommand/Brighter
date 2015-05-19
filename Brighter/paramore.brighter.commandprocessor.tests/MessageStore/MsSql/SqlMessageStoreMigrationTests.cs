﻿#region Licence
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
using System.Data.SqlClient;
using System.Data.SqlServerCe;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Machine.Specifications;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.messagestore.mssql;

namespace paramore.commandprocessor.tests.MessageStore.MsSql
{
    public class SqlMessageStoreMigrationTests
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

        public class when_writing_a_message_with_minimal_header_information_to_the_message_store
        {
            private Establish _context = () =>
            {
                s_message = new Message(new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_DOCUMENT), new MessageBody("message body"));
                AddHistoricMessage(s_sqlMessageStore, s_message).Wait();
            };

            private async static Task AddHistoricMessage(MsSqlMessageStore sSqlMessageStore, Message message)
            {
                var sql = string.Format("INSERT INTO {0} (MessageId, MessageType, Topic, Body) VALUES (@MessageId, @MessageType, @Topic, @Body)", TableName);
                var parameters = new[]
                {
                    new SqlCeParameter("MessageId", message.Id),
                    new SqlCeParameter("MessageType", message.Header.MessageType.ToString()),
                    new SqlCeParameter("Topic", message.Header.Topic),
                    new SqlCeParameter("Body", message.Body.Value),
                };

                using (var connection = new SqlCeConnection(ConnectionString))
                using (var command = connection.CreateCommand())
                {
                    await connection.OpenAsync();

                    command.CommandText = sql;
                    command.Parameters.AddRange(parameters);
                    await command.ExecuteNonQueryAsync();
                }
            }

            private Because _of = () => { s_storedMessage = s_sqlMessageStore.Get(s_message.Id).Result; };

            private It _should_read_the_message_from_the__sql_message_store = () => s_storedMessage.Body.Value.ShouldEqual(s_message.Body.Value);
            private It _should_read_the_message_header_type_from_the__sql_message_store = () => s_storedMessage.Header.MessageType.ShouldEqual(s_message.Header.MessageType);
            private It _should_read_the_message_header_topic_from_the__sql_message_store = () => s_storedMessage.Header.Topic.ShouldEqual(s_message.Header.Topic);
            private It _should_default_the_timestamp_from_the__sql_message_store = () => s_storedMessage.Header.TimeStamp.ShouldBeGreaterThanOrEqualTo(s_message.Header.TimeStamp); //DateTime set in ctor on way out
            private It _should_read_empty_header_bag_from_the__sql_message_store = () => s_storedMessage.Header.Bag.Keys.Any().ShouldBeFalse();
        }

        private Cleanup _cleanup = () => CleanUpDb();
        private static MsSqlMessageStore s_sqlMessageStore;
        private static Message s_message;
        private static Message s_storedMessage;

        private static void CleanUpDb()
        {
            File.Delete(TestDbPath);
        }

        private static void CreateTestDb()
        {
            var en = new SqlCeEngine(ConnectionString);
            en.CreateDatabase();

            var oldSchemaCreationSql = string.Format("CREATE TABLE {0} (" +
                                        "MessageId uniqueidentifier CONSTRAINT PK_MessageId PRIMARY KEY," +
                                        "Topic nvarchar(255)," +
                                        "MessageType nvarchar(32)," +
                                        "Body ntext" +
                                    ")", TableName);

            using (var cnn = new SqlCeConnection(ConnectionString))
            using (var cmd = cnn.CreateCommand())
            {
                cmd.CommandText = oldSchemaCreationSql;
                cnn.Open();
                cmd.ExecuteNonQuery();
            }
        }
    }
}
