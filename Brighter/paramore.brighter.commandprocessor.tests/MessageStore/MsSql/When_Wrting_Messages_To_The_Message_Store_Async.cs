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
using Nito.AsyncEx;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.messagestore.mssql;

namespace paramore.commandprocessor.tests.MessageStore.MsSql
{
    [Subject(typeof(MsSqlMessageStore))]
    public class When_Wrting_Messages_To_The_Message_Store_Async
    {
        private const string ConnectionString = "DataSource=\"" + TestDbPath + "\"";
        private const string TableName = "test_messages";
        private const string TestDbPath = "test.sdf";
        private static Message s_message2;
        private static Message s_messageEarliest;
        private static Message s_messageLatest;
        private static IList<Message> s_retrievedMessages;
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
            Clock.OverrideTime = DateTime.UtcNow.AddHours(-3);
            s_messageEarliest = new Message(new MessageHeader(Guid.NewGuid(), "Test", MessageType.MT_COMMAND),
                new MessageBody("Body"));
            AsyncContext.Run(async () => await s_sqlMessageStore.AddAsync(s_messageEarliest));

            Clock.OverrideTime = DateTime.UtcNow.AddHours(-2);

            s_message2 = new Message(new MessageHeader(Guid.NewGuid(), "Test2", MessageType.MT_COMMAND),
                new MessageBody("Body2"));
            AsyncContext.Run(async () => await s_sqlMessageStore.AddAsync(s_message2));

            Clock.OverrideTime = DateTime.UtcNow.AddHours(-1);

            s_messageLatest = new Message(new MessageHeader(Guid.NewGuid(), "Test3", MessageType.MT_COMMAND),
                new MessageBody("Body3"));
            AsyncContext.Run(async () => await s_sqlMessageStore.AddAsync(s_messageLatest));
        };

        private Because _of = () =>  AsyncContext.Run(async () => s_retrievedMessages = await s_sqlMessageStore.GetAsync()); 

        private It _should_read_first_message_last_from_the__message_store =
            () => s_retrievedMessages.Last().Id.ShouldEqual(s_messageEarliest.Id);

        private It _should_read_last_message_first_from_the__message_store =
            () => s_retrievedMessages.First().Id.ShouldEqual(s_messageLatest.Id);

        private It _should_read_the_messages_from_the__message_store = () => s_retrievedMessages.Count().ShouldEqual(3);

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
