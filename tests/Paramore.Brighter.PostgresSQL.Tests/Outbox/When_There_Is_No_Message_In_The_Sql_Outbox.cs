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
using Paramore.Brighter.Outbox.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.Outbox
{
    [Trait("Category", "PostgreSql")]
    [Collection("PostgreSql OutBox")]
    public class PostgreSqlOutboxEmptyStoreTests : IDisposable
    {
        private readonly PostgreSqlTestHelper _PostgreSqlTestHelper;
        private readonly Message _messageEarliest;
        private readonly PostgreSqlOutbox _sqlOutbox;
        private Message _storedMessage;

        public PostgreSqlOutboxEmptyStoreTests()
        {
            _PostgreSqlTestHelper = new PostgreSqlTestHelper();
            _PostgreSqlTestHelper.SetupMessageDb();

            _sqlOutbox = new PostgreSqlOutbox(_PostgreSqlTestHelper.OutboxConfiguration);
            _messageEarliest = new Message(new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_DOCUMENT), new MessageBody("message body"));
        }

        [Fact]
        public void When_There_Is_No_Message_In_The_Sql_Outbox()
        {
            _storedMessage = _sqlOutbox.Get(_messageEarliest.Id);

            //_should_return_a_empty_message
            _storedMessage.Header.MessageType.Should().Be(MessageType.MT_NONE);
        }

        public void Dispose()
        {
            _PostgreSqlTestHelper.CleanUpTable();
        }
    }
}
