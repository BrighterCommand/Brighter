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
using Paramore.Brighter.Outbox.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.Outbox
{
    [Trait("Category", "MySql")]
    public class MySqlOutboxDeletingMessagesTests 
    {
        private readonly MySqlTestHelper _mySqlTestHelper;
        private readonly MySqlOutbox _mySqlOutbox;
        private readonly Message _messageEarliest;
        private readonly Message _message2;
        private readonly Message _messageLatest;
        private IEnumerable<Message> _retrievedMessages;

        public MySqlOutboxDeletingMessagesTests()
        {
            _mySqlTestHelper = new MySqlTestHelper();
            _mySqlTestHelper.SetupMessageDb();
            _mySqlOutbox = new MySqlOutbox(_mySqlTestHelper.OutboxConfiguration);

            _messageEarliest = new Message(new MessageHeader(Guid.NewGuid(), "Test", MessageType.MT_COMMAND, DateTime.UtcNow.AddHours(-3)), new MessageBody("Body"));
            _message2 = new Message(new MessageHeader(Guid.NewGuid(), "Test2", MessageType.MT_COMMAND, DateTime.UtcNow.AddHours(-2)), new MessageBody("Body2"));
            _messageLatest = new Message(new MessageHeader(Guid.NewGuid(), "Test3", MessageType.MT_COMMAND, DateTime.UtcNow.AddHours(-1)), new MessageBody("Body3"));
            
        }

        [Fact]
        public void When_Removing_Messages_From_The_Outbox()
        {
            _mySqlOutbox.Add(_messageEarliest);
            _mySqlOutbox.Add(_message2);
            _mySqlOutbox.Add(_messageLatest);
            _retrievedMessages = _mySqlOutbox.Get();

            _mySqlOutbox.Delete(_retrievedMessages.First().Id);

            var remainingMessages = _mySqlOutbox.Get();

            remainingMessages.Should().HaveCount(2);
            remainingMessages.Should().Contain(_retrievedMessages.ToList()[1]);
            remainingMessages.Should().Contain(_retrievedMessages.ToList()[2]);
            
            _mySqlOutbox.Delete(remainingMessages.Select(m => m.Id).ToArray());

            var messages = _mySqlOutbox.Get();

            messages.Should().HaveCount(0);
        }
        
        [Fact]
        public async Task When_Removing_Messages_From_The_OutboxAsync()
        {
            var messages = new List<Message> { _messageEarliest, _message2, _messageLatest };
            _mySqlOutbox.Add(messages);
            _retrievedMessages = await _mySqlOutbox.GetAsync();

            await _mySqlOutbox.DeleteAsync(CancellationToken.None, _retrievedMessages.First().Id);

            var remainingMessages = await _mySqlOutbox.GetAsync();

            remainingMessages.Should().HaveCount(2);
            remainingMessages.Should().Contain(_retrievedMessages.ToList()[1]);
            remainingMessages.Should().Contain(_retrievedMessages.ToList()[2]);
            
            await _mySqlOutbox.DeleteAsync(CancellationToken.None, remainingMessages.Select(m => m.Id).ToArray());

            var finalMessages = await _mySqlOutbox.GetAsync();

            finalMessages.Should().HaveCount(0);
        }

        [Fact]
        public void Dispose()
        {
            _mySqlTestHelper.CleanUpDb();
        }
    }
}
