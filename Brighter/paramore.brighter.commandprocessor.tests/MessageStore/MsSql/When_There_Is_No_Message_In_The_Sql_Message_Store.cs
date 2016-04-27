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
using Machine.Specifications;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.messagestore.mssql;

namespace paramore.commandprocessor.tests.MessageStore.MsSql
{
    [Subject(typeof(MsSqlMessageStore))]
    [Tags("MessageStoreTests")]
    public class When_There_Is_No_Message_In_The_Sql_Message_Store
    {
        private const string ConnectionString = "DataSource=\"" + TestDbPath + "\"";
        private const string TableName = "test_messages";
        private const string TestDbPath = "test.sdf";
        private static Message s_messageEarliest;
        private static MsSqlMessageStore s_sqlMessageStore;
        private static Message s_storedMessage;

        private Cleanup _cleanup = () => CleanUpDb();

        private Establish _context = () =>
        {
            CleanUpDb();
            CreateTestDb();

            s_sqlMessageStore = new MsSqlMessageStore(
                new MsSqlMessageStoreConfiguration(ConnectionString, TableName,
                    MsSqlMessageStoreConfiguration.DatabaseType.SqlCe),
                new LogProvider.NoOpLogger());
            s_messageEarliest = new Message(new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_DOCUMENT),
                new MessageBody("message body"));
        };

        private Because _of = () => { s_storedMessage = s_sqlMessageStore.Get(s_messageEarliest.Id); };

        private It _should_return_a_empty_message =
            () => s_storedMessage.Header.MessageType.ShouldEqual(MessageType.MT_NONE);

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