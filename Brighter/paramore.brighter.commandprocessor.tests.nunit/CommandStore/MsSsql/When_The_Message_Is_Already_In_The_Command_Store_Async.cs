#region Licence
/* The MIT License (MIT)
Copyright © 2015 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using NUnit.Specifications;
using nUnitShouldAdapter;
using Microsoft.Data.Sqlite;
using Nito.AsyncEx;
using paramore.brighter.commandprocessor.commandstore.sqllite;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles;

namespace paramore.brighter.commandprocessor.tests.nunit.CommandStore.MsSsql
{
    internal class When_The_Message_Is_Already_In_The_Command_Store_Async : NUnit.Specifications.ContextSpecification
    {
        private const string TestDbPath = "test.db";
        private const string ConnectionString = "DataSource=\"" + TestDbPath + "\"";
        private const string TableName = "test_messages";
        private static SqlLiteCommandStore s_sqlCommandStore;
        private static MyCommand s_raisedCommand;
        private static Exception s_exception;

        private Establish _context = () =>
        {
            _sqliteConnection = DatabaseHelper.CreateDatabaseWithTable(ConnectionString, SqlLiteCommandStoreBuilder.GetDDL(TableName));

            s_sqlCommandStore =
                new SqlLiteCommandStore(new SqlLiteCommandStoreConfiguration(ConnectionString, TableName), new LogProvider.NoOpLogger());
            s_raisedCommand = new MyCommand() {Value = "Test"};
            AsyncContext.Run(async () => await s_sqlCommandStore.AddAsync<MyCommand>(s_raisedCommand));
        };

        private Because _of =
            () =>
            {
                s_exception =
                    Catch.Exception(
                        () => AsyncContext.Run(async () => await s_sqlCommandStore.AddAsync(s_raisedCommand)));
            };

        private It _should_succeed_even_if_the_message_is_a_duplicate = () => s_exception.ShouldBeNull();

        private Cleanup _cleanup = () =>
        {
            if (_sqliteConnection != null)
                _sqliteConnection.Dispose();
        };

        private static SqliteConnection _sqliteConnection;
    }
}
