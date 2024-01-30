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
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Outbox.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.Outbox
{
    [Trait("Category", "MSSQL")]
    public class SqlOutboxWritingMessagesAsyncTests : IDisposable
    {
        private readonly MsSqlTestHelper _msSqlTestHelper;
        private Message _messageTwo;
        private Message _messageOne;
        private Message _messageThree;
        private IList<Message> _retrievedMessages;
        private readonly MsSqlOutbox _sqlOutbox;

        public SqlOutboxWritingMessagesAsyncTests()
        {
            _msSqlTestHelper = new MsSqlTestHelper();
            _msSqlTestHelper.SetupMessageDb();

            _sqlOutbox = new MsSqlOutbox(_msSqlTestHelper.OutboxConfiguration);
        }

        [Fact]
        public async Task When_Writing_Messages_To_The_Outbox_Async()
        {
            await SetUpMessagesAsync();

            _retrievedMessages = await _sqlOutbox.GetAsync();

            //should read last message last from the outbox
            _retrievedMessages.Any(m => m.Id == _messageThree.Id).Should().BeTrue();
            //should read first message first from the outbox
            _retrievedMessages.Any(m => m.Id == _messageOne.Id).Should().BeTrue();
            //should read the messages from the outbox
            _retrievedMessages.Should().HaveCount(3);
        }
        
        [Fact]
        public async Task When_Bulk_Writing_Messages_To_The_Outbox_Async()
        {
            _messageOne = new Message(new MessageHeader(Guid.NewGuid(), "Test", MessageType.MT_COMMAND, DateTime.UtcNow.AddHours(-3)), new MessageBody("Body"));
            _messageTwo = new Message(new MessageHeader(Guid.NewGuid(), "Test2", MessageType.MT_COMMAND, DateTime.UtcNow.AddHours(-2)), new MessageBody("Body2"));
            _messageThree = new Message(new MessageHeader(Guid.NewGuid(), "Test3", MessageType.MT_COMMAND, DateTime.UtcNow.AddHours(-1)), new MessageBody("Body3"));
            
            var messages = new List<Message> { _messageOne, _messageTwo, _messageThree };
            await _sqlOutbox.AddAsync(messages);

            _retrievedMessages = await _sqlOutbox.GetAsync();

            //should read last message last from the outbox
            foreach (Message message in messages)
            {
                _retrievedMessages.Any(m => m.Id == message.Id).Should().BeTrue();
            }

            //should read the messages from the outbox
            _retrievedMessages.Should().HaveCount(3);
        }

        private async Task SetUpMessagesAsync()
        {
            _messageOne = new Message(new MessageHeader(Guid.NewGuid(), "Test", MessageType.MT_COMMAND, DateTime.UtcNow.AddHours(-3)), new MessageBody("Body"));
            await _sqlOutbox.AddAsync(_messageOne);

            _messageTwo = new Message(new MessageHeader(Guid.NewGuid(), "Test2", MessageType.MT_COMMAND, DateTime.UtcNow.AddHours(-2)), new MessageBody("Body2"));
            await _sqlOutbox.AddAsync(_messageTwo);

            _messageThree = new Message(new MessageHeader(Guid.NewGuid(), "Test3", MessageType.MT_COMMAND, DateTime.UtcNow.AddHours(-1)), new MessageBody("Body3"));
            await _sqlOutbox.AddAsync(_messageThree);
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

        ~SqlOutboxWritingMessagesAsyncTests()
        {
            Release();
        }
    }
}
