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

using Microsoft.Data.Sqlite;
using nUnitShouldAdapter;
using paramore.brighter.commandprocessor.commandstore.sqllite;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles;

namespace paramore.brighter.commandprocessor.tests.nunit.CommandStore.Sqlite
{
    public class When_Writing_A_Message_To_The_Command_Store : NUnit.Specifications.ContextSpecification
    {
        private static SqlLiteTestHelper _sqlLiteTestHelper;
        private static SqlLiteCommandStore s_sqlCommandStore;
        private static MyCommand s_raisedCommand;
        private static MyCommand s_storedCommand;

        private Establish _context = () =>
        {
            _sqlLiteTestHelper = new SqlLiteTestHelper();
            _sqliteConnection = _sqlLiteTestHelper.CreateDatabase();

            s_sqlCommandStore = new SqlLiteCommandStore(new SqlLiteCommandStoreConfiguration(_sqlLiteTestHelper.ConnectionString, _sqlLiteTestHelper.TableName), new LogProvider.NoOpLogger());
            s_raisedCommand = new MyCommand() {Value = "Test"};
            s_sqlCommandStore.Add<MyCommand>(s_raisedCommand);
        };

        private Because _of = () => { s_storedCommand = s_sqlCommandStore.Get<MyCommand>(s_raisedCommand.Id); };

        private It _should_read_the_command_from_the__sql_command_store = () => s_storedCommand.ShouldNotBeNull();
        private It _should_read_the_command_value = () => s_storedCommand.Value.ShouldEqual(s_raisedCommand.Value);
        private It _should_read_the_command_id = () => s_storedCommand.Id.ShouldEqual(s_raisedCommand.Id);

        private Cleanup _cleanup = () =>
        {
            _sqlLiteTestHelper.CleanUpDb();
        };

        private static SqliteConnection _sqliteConnection;
    }
}