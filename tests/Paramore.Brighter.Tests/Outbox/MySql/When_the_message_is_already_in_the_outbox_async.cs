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
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Outbox.MySql;
using Xunit;

namespace Paramore.Brighter.Tests.Outbox.MySql
{
    [Trait("Category", "MySql")]
    [Collection("MySql OutBox")]
    public class MySqlOutboxMessageAlreadyExistsAsyncTests : IDisposable
    {
        private Exception _exception;
        private readonly Message _messageEarliest;
        private readonly MySqlOutbox _sqlOutbox;
        private readonly MySqlTestHelper _msSqlTestHelper;

        public MySqlOutboxMessageAlreadyExistsAsyncTests()
        {
            _msSqlTestHelper = new MySqlTestHelper();
            _msSqlTestHelper.SetupMessageDb();

            _sqlOutbox = new MySqlOutbox(_msSqlTestHelper.OutboxConfiguration);
            _messageEarliest = new Message(new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_DOCUMENT), new MessageBody("message body"));
        }

        [Fact]
        public async Task When_The_Message_Is_Already_In_The_Outbox_Async()
        {
            await _sqlOutbox.AddAsync(_messageEarliest);
            _exception = await Catch.ExceptionAsync(() => _sqlOutbox.AddAsync(_messageEarliest));
            _exception.Should().BeNull();
        }

        public void Dispose()
        {
            _msSqlTestHelper.CleanUpDb();
        }
    }
}
