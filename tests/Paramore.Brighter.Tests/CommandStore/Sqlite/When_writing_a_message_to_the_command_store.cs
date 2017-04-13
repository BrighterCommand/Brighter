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
using FluentAssertions;
using Xunit;
using Paramore.Brighter.CommandStore.Sqlite;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;

namespace Paramore.Brighter.Tests.CommandStore.Sqlite
{
    [Trait("Category", "Sqlite")]
    [Collection("Sqlite CommandStore")]
    public class SqliteCommandStoreAddMessageTests : IDisposable
    {
        private readonly SqliteTestHelper _sqliteTestHelper;
        private readonly SqliteCommandStore _sqlCommandStore;
        private readonly MyCommand _raisedCommand;
        private MyCommand _storedCommand;

        public SqliteCommandStoreAddMessageTests()
        {
            _sqliteTestHelper = new SqliteTestHelper();
            _sqliteTestHelper.SetupCommandDb();

            _sqlCommandStore = new SqliteCommandStore(new SqliteCommandStoreConfiguration(_sqliteTestHelper.ConnectionString, _sqliteTestHelper.TableName));
            _raisedCommand = new MyCommand {Value = "Test"};
            _sqlCommandStore.Add(_raisedCommand);
        }

        [Fact]
        public void When_Writing_A_Message_To_The_Command_Store()
        {
            _storedCommand = _sqlCommandStore.Get<MyCommand>(_raisedCommand.Id);

            //_should_read_the_command_from_the__sql_command_store
            _storedCommand.Should().NotBeNull();
            //_should_read_the_command_value
            _storedCommand.Value.Should().Be(_raisedCommand.Value);
            //_should_read_the_command_id
            _storedCommand.Id.Should().Be(_raisedCommand.Id);
        }

        public void Dispose()
        {
            _sqliteTestHelper.CleanUpDb();
        }
    }
}