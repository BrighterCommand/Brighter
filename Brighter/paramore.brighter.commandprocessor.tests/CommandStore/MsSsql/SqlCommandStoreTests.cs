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
using System.Data.SqlServerCe;
using System.IO;
using Machine.Specifications;
using paramore.brighter.commandprocessor.commandstore.mssql;
using paramore.brighter.commandprocessor.Logging;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;

namespace paramore.commandprocessor.tests.CommandStore.MsSsql
{
    public class SqlCommandStoreTests
    {
        private const string TestDbPath = "test.sdf";
        private const string ConnectionString = "DataSource=\"" + TestDbPath + "\"";
        private const string TableName = "test_messages";
        private static MsSqlCommandStore s_sqlCommandStore;
        private static MyCommand s_raisedCommand;
        private static MyCommand s_storedCommand;


        private Establish _context = () =>
        {
            CleanUpDb();
            CreateTestDb();

            s_sqlCommandStore = new MsSqlCommandStore(new MsSqlCommandStoreConfiguration(ConnectionString, TableName, MsSqlCommandStoreConfiguration.DatabaseType.SqlCe),
                    new LogProvider.NoOpLogger());
        };

        public class When_writing_a_message_to_the_command_store
        {
            private Establish _context = () =>
            {
                s_raisedCommand = new MyCommand() {Value = "Test"};
                s_sqlCommandStore.Add<MyCommand>(s_raisedCommand);
            };

            private Because _of = () => { s_storedCommand = s_sqlCommandStore.Get<MyCommand>(s_raisedCommand.Id); };

            private It _should_read_the_command_from_the__sql_command_store = () => s_storedCommand.ShouldNotBeNull();
            private It _should_read_the_command_value = () => s_storedCommand.Value.ShouldEqual(s_raisedCommand.Value);
            private It _should_read_the_command_id = () => s_storedCommand.Id.ShouldEqual(s_raisedCommand.Id);

        }

        public class When_there_is_no_message_in_the_sql_command_store
        {
            private Because _of = () => { s_storedCommand = s_sqlCommandStore.Get<MyCommand>(Guid.NewGuid()); };

            private It _should_return_an_empty_command_on_a_missing_command = () => s_storedCommand.Id.ShouldEqual(Guid.Empty);
        }

        public class When_the_message_is_already_in_the_command_store
        {
            private static Exception s_exception;

            private Establish _context = () =>
            {
                s_raisedCommand = new MyCommand() { Value = "Test" };
                s_sqlCommandStore.Add<MyCommand>(s_raisedCommand);
            };

            private Because _of = () => { s_exception = Catch.Exception(() => s_sqlCommandStore.Add(s_raisedCommand)); };

            private It _should_succeed_even_if_the_message_is_a_duplicate = () => s_exception.ShouldBeNull();
        }

        private Cleanup _cleanup = () => CleanUpDb();

        private static void CleanUpDb()
        {
            File.Delete(TestDbPath);
        }

        private static void CreateTestDb()
        {
            var en = new SqlCeEngine(ConnectionString);
            en.CreateDatabase();


            var sql = SqlCommandStoreBuilder.GetDDL(TableName);

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
