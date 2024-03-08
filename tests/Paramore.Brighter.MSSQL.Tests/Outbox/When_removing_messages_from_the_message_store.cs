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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Outbox.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.Outbox
{
    [Trait("Category", "MSSQL")]
    public class SqlOutboxDeletingMessagesTests : IDisposable
    {
        private readonly MsSqlTestHelper _msSqlTestHelper;
        private readonly Message _firstMessage;
        private readonly Message _secondMessage;
        private readonly Message _thirdMessage;
        private readonly MsSqlOutbox _outbox;

        public SqlOutboxDeletingMessagesTests()
        {
            _msSqlTestHelper = new MsSqlTestHelper();
            _msSqlTestHelper.SetupMessageDb();

            _outbox = new MsSqlOutbox(_msSqlTestHelper.OutboxConfiguration);
            
            _firstMessage = new Message(new MessageHeader(Guid.NewGuid(), "Test", MessageType.MT_COMMAND, DateTime.UtcNow.AddHours(-3)), new MessageBody("Body"));
            _secondMessage = new Message(new MessageHeader(Guid.NewGuid(), "Test2", MessageType.MT_COMMAND, DateTime.UtcNow.AddHours(-2)), new MessageBody("Body2"));
            _thirdMessage = new Message(new MessageHeader(Guid.NewGuid(), "Test3", MessageType.MT_COMMAND, DateTime.UtcNow.AddHours(-1)), new MessageBody("Body3"));
            
            _outbox.Add(_firstMessage);
            _outbox.Add(_secondMessage);
            _outbox.Add(_thirdMessage);
        }

        [Fact]
        public void When_Removing_Messages_From_The_Outbox()
        {
            _outbox.Delete(new Guid[]{_firstMessage.Id});

            var remainingMessages = _outbox.OutstandingMessages(0);

            remainingMessages.Should().HaveCount(2);
            remainingMessages.Should().Contain(_secondMessage);
            remainingMessages.Should().Contain(_thirdMessage);
            
            _outbox.Delete(remainingMessages.Select(m => m.Id).ToArray());

            var messages = _outbox.OutstandingMessages(0);

            messages.Should().HaveCount(0);

        }
        [Fact]
        public async Task When_Removing_Messages_From_The_OutboxAsync()
        {
            await _outbox.DeleteAsync(new Guid[] {_firstMessage.Id}, cancellationToken: CancellationToken.None);

            var remainingMessages = await _outbox.OutstandingMessagesAsync(0);

            remainingMessages.Should().HaveCount(2);
            remainingMessages.Should().Contain(_secondMessage);
            remainingMessages.Should().Contain(_thirdMessage);
            
            await _outbox.DeleteAsync(
                remainingMessages.Select(m => m.Id).ToArray(), 
                cancellationToken: CancellationToken.None
                );

            var messages = await _outbox.OutstandingMessagesAsync(0);

            messages.Should().HaveCount(0);

        }

        private void Release()
        {
            _msSqlTestHelper.CleanUpDb();
        }
        
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Release();
        }

        ~SqlOutboxDeletingMessagesTests()
        {
            Release();
        }
    }
}
