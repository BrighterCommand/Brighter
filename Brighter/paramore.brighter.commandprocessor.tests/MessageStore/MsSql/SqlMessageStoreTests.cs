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
using Common.Logging.Simple;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.messagestore.mssql;

namespace paramore.commandprocessor.tests.MessageStore.MsSql
{
    [TestClass]
    public class SqlMessageStoreTests
    {
        private const string TestDbPath = "test.sdf";
        private const string ConnectionString = "DataSource=\"" + TestDbPath + "\"";
        private const string TableName = "test_messages";
        
        [TestMethod]
        public void Writing_and_reading_a_message_from_the_store()
        {
            CleanupDb();
            CreateTestDb();

            var sqlMessageStore = new MsSqlMessageStore(new MsSqlMessageStoreConfiguration(ConnectionString, TableName, MsSqlMessageStoreConfiguration.DatabaseType.SqlCe), new NoOpLogger());
            var message = new Message(new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_DOCUMENT), new MessageBody("message body"));
            
            sqlMessageStore.Add(message).Wait();
            var storedMessage = sqlMessageStore.Get(message.Id).Result;

            Assert.AreEqual(message.Body.Value, storedMessage.Body.Value);
        }

        private static void CreateTestDb()
        {
            var en = new SqlCeEngine(ConnectionString);
            en.CreateDatabase();

            var sql = string.Format("CREATE TABLE {0} (" +
                                        "Id uniqueidentifier CONSTRAINT PK_MessageId PRIMARY KEY," +
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

        [TestCleanup]
        public void CleanupDb()
        {
            File.Delete(TestDbPath);
        }
    }
}
