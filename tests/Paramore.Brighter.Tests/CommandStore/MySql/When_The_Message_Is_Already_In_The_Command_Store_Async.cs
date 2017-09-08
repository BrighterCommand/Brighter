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
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Paramore.Brighter.CommandStore.MsSql;
using Paramore.Brighter.CommandStore.MySql;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;

namespace Paramore.Brighter.Tests.CommandStore.MySql
{
    [Trait("Category", "MySql")]
    [Collection("MySql CommandStore")]
    public class SqlCommandStoreDuplicateMessageAsyncTests : IDisposable
    {
        private readonly MySqlTestHelper _mysqlTestHelper;
        private readonly MySqlCommandStore _mysqlCommandStore;
        private readonly MyCommand _raisedCommand;
        private Exception _exception;

        public SqlCommandStoreDuplicateMessageAsyncTests()
        {
            _mysqlTestHelper = new MySqlTestHelper();
            _mysqlTestHelper.SetupCommandDb();

            _mysqlCommandStore = new MySqlCommandStore(_mysqlTestHelper.CommandStoreConfiguration);
            _raisedCommand = new MyCommand { Value = "Test" };
        }

        [Fact]
        public async Task When_The_Message_Is_Already_In_The_Command_Store_Async()
        {
            await _mysqlCommandStore.AddAsync(_raisedCommand);

            _exception = await Catch.ExceptionAsync(() => _mysqlCommandStore.AddAsync(_raisedCommand));

           //_should_succeed_even_if_the_message_is_a_duplicate
            _exception.Should().BeNull();
        }

        public void Dispose()
        {
            _mysqlTestHelper.CleanUpDb();
        }
    }
}
