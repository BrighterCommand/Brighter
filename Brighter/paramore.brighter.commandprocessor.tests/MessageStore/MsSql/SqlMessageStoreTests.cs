// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
using System.Data.SqlServerCe;
using System.IO;
using System.Runtime.InteropServices;

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
                s_message = new Message(new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_DOCUMENT), new MessageBody("message body"));
                s_sqlMessageStore.Add(s_message).Wait();
            };

            private Because _of = () => { s_storedMessage = s_sqlMessageStore.Get(s_message.Id).Result; };

            private It _should_read_the_message_from_the__sql_message_store = () => s_storedMessage.Body.Value.ShouldEqual(s_message.Body.Value);
        }

        public class when_there_is_no_message_in_the_sql_message_store
        {
            private Establish _context = () => { s_message = new Message(new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_DOCUMENT), new MessageBody("message body")); };
            private Because _of = () => { s_storedMessage = s_sqlMessageStore.Get(s_message.Id).Result; };
            private It _should_return_a_empty_message = () => s_storedMessage.Header.MessageType.ShouldEqual(MessageType.MT_NONE);
        }


        public class when_the_message_is_already_in_the_message_store
        {
            private static Exception s_exception;

            private Establish _context = () =>
            {
                s_message = new Message(new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_DOCUMENT), new MessageBody("message body"));
                s_sqlMessageStore.Add(s_message).Wait();
            };

            private Because _of = () => { s_exception = Catch.Exception(() => s_sqlMessageStore.Add(s_message).Wait()); };

            private It _should_ignore_the_duplcate_key_and_still_succeed = () => { s_exception.ShouldBeNull(); };
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

            var sql = string.Format("CREATE TABLE {0} (" +
                                        "MessageId uniqueidentifier CONSTRAINT PK_MessageId PRIMARY KEY," +
                                        "Topic nvarchar(255)," +
                                        "MessageType nvarchar(32)," +
                                        "Body ntext" +
                                    ")", TableName);

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
