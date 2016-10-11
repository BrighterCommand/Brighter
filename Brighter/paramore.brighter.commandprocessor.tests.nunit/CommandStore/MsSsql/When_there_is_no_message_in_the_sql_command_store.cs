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
using System.Data.SqlClient;
using NUnit.Specifications;
using nUnitShouldAdapter;
using NUnit.Framework;
using paramore.brighter.commandprocessor.commandstore.mssql;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles;

namespace paramore.brighter.commandprocessor.tests.nunit.CommandStore.MsSsql
{
    [Ignore("No MsSql ddl etc yet. Also need to add tag")]

    public class When_There_Is_No_Message_In_The_Sql_Command_Store : ContextSpecification
    {
        private static MsSqlTestHelper _msSqlTestHelper;
        private static MsSqlCommandStore s_sqlCommandStore;
        private static MyCommand s_raisedCommand;
        private static MyCommand s_storedCommand;

        private Establish _context = () =>
        {
            _msSqlTestHelper = new MsSqlTestHelper();
            _sqliteConnection = _msSqlTestHelper.CreateDatabase();
            s_sqlCommandStore = new MsSqlCommandStore(_msSqlTestHelper.Configuration, new LogProvider.NoOpLogger());
        };

        private Because _of = () => { s_storedCommand = s_sqlCommandStore.Get<MyCommand>(Guid.NewGuid()); };

        private It _should_return_an_empty_command_on_a_missing_command =
            () => s_storedCommand.Id.ShouldEqual(Guid.Empty);

        private Cleanup _cleanup = () =>
        {
            if (_sqliteConnection != null)
                _sqliteConnection.Dispose();
            _msSqlTestHelper.CleanUpDb();

        };

        private static SqlConnection _sqliteConnection;

    }

}