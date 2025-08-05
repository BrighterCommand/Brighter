#region Licence

/* The MIT License (MIT)
Copyright © 2014 Francesco Pighi <francesco.pighi@gmail.com>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Outbox.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.Outbox
{
    [Trait("Category", "PostgresSql")]
    public class SqlOutboxDeletingMessagesAsyncTests : IDisposable
    {
        private readonly PostgresSqlTestHelper _postgresSqlTestHelper;
        private readonly Message _messageEarliest;
        private readonly Message _message2;
        private readonly Message _messageLatest;
        private IEnumerable<Message> _retrievedMessages;
        private readonly PostgreSqlOutboxAsync _sqlOutboxAsync;

        public SqlOutboxDeletingMessagesAsyncTests()
        {
            _postgresSqlTestHelper = new PostgresSqlTestHelper();
            _postgresSqlTestHelper.SetupMessageDb();

            _sqlOutboxAsync = new PostgreSqlOutboxAsync(_postgresSqlTestHelper.OutboxConfiguration);
            _messageEarliest = new Message(new MessageHeader(Guid.NewGuid(), "Test", MessageType.MT_COMMAND, DateTime.UtcNow.AddHours(-3)), new MessageBody("Body"));

            _message2 = new Message(new MessageHeader(Guid.NewGuid(), "Test2", MessageType.MT_COMMAND, DateTime.UtcNow.AddHours(-2)), new MessageBody("Body2"));

            _messageLatest = new Message(new MessageHeader(Guid.NewGuid(), "Test3", MessageType.MT_COMMAND, DateTime.UtcNow.AddHours(-1)), new MessageBody("Body3"));
        }

        [Fact]
        public async Task When_Removing_Messages_From_The_Outbox_Async()
        {
            await _sqlOutboxAsync.AddAsync(_messageEarliest);
            await _sqlOutboxAsync.AddAsync(_message2);
            await _sqlOutboxAsync.AddAsync(_messageLatest);
            
            _retrievedMessages = await _sqlOutboxAsync.GetAsync();

            await _sqlOutboxAsync.DeleteAsync(new[] { _retrievedMessages.First().Id }, CancellationToken.None);

            var remainingMessages = await _sqlOutboxAsync.GetAsync();

            remainingMessages.Should().HaveCount(2);
            remainingMessages.Should().Contain(_retrievedMessages.ToList()[1]);
            remainingMessages.Should().Contain(_retrievedMessages.ToList()[2]);
            
            await _sqlOutboxAsync.DeleteAsync(remainingMessages.Select(m => m.Id).ToArray(), CancellationToken.None);

            var messages = await _sqlOutboxAsync.GetAsync();

            messages.Should().HaveCount(0);
        }

        public void Dispose()
        {
            _postgresSqlTestHelper.CleanUpDb();
        }
    }
}
