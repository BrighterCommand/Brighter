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
using System.IO;
using NUnit.Specifications;
using nUnitShouldAdapter;
using Microsoft.Data.Sqlite;
using Nito.AsyncEx;
using paramore.brighter.commandprocessor.commandstore.sqllite;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles;

namespace paramore.brighter.commandprocessor.tests.nunit.CommandStore.MsSsql
{
    public class When_There_Is_No_Message_In_The_Sql_Command_Store_Async : NUnit.Specifications.ContextSpecification
    {
        private const string TestDbPath = "test.db";
        private const string ConnectionString = "Data Source=\"" + TestDbPath + "\"";
        private const string TableName = "test_messages";
        private static SqlLiteCommandStore s_sqlCommandStore;
        private static MyCommand s_raisedCommand;
        private static MyCommand s_storedCommand;

        private Establish _context = () =>
        {
            // "Data Source=:memory:"
            _sqliteConnection = DatabaseHelper.CreateDatabaseWithTable(ConnectionString, SqlLiteCommandStoreBuilder.GetDDL(TableName));

            s_sqlCommandStore = new SqlLiteCommandStore(new SqlLiteCommandStoreConfiguration(ConnectionString, TableName), new LogProvider.NoOpLogger());
        };

        private Because _of = () => { s_storedCommand = AsyncContext.Run(async () => await s_sqlCommandStore.GetAsync<MyCommand>(Guid.NewGuid())); };

        private It _should_return_an_empty_command_on_a_missing_command = () => s_storedCommand.Id.ShouldEqual(Guid.Empty);

        private Cleanup _cleanup = () =>
        {
            if (_sqliteConnection != null)
                _sqliteConnection.Dispose();
        };

        private static SqliteConnection _sqliteConnection;

        private static void CleanUpDb()
        {
            File.Delete(TestDbPath);
        }
    }
}
