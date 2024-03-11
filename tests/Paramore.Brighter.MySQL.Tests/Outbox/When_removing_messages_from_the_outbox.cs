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
        private readonly Message _firstMessage;
        private readonly Message _secondMessage;
        private readonly Message _thirdMessage;

        public MySqlOutboxDeletingMessagesTests()
        {
            _mySqlTestHelper = new MySqlTestHelper();
            _mySqlTestHelper.SetupMessageDb();
            _mySqlOutbox = new MySqlOutbox(_mySqlTestHelper.OutboxConfiguration);

            _firstMessage = new Message(new MessageHeader(Guid.NewGuid(), "Test", MessageType.MT_COMMAND, DateTime.UtcNow.AddHours(-3)), new MessageBody("Body"));
            _secondMessage = new Message(new MessageHeader(Guid.NewGuid(), "Test2", MessageType.MT_COMMAND, DateTime.UtcNow.AddHours(-2)), new MessageBody("Body2"));
            _thirdMessage = new Message(new MessageHeader(Guid.NewGuid(), "Test3", MessageType.MT_COMMAND, DateTime.UtcNow.AddHours(-1)), new MessageBody("Body3"));
            
        }

        [Fact]
        public void When_Removing_Messages_From_The_Outbox()
        {
            _mySqlOutbox.Add(_firstMessage);
            _mySqlOutbox.Add(_secondMessage);
            _mySqlOutbox.Add(_thirdMessage);
            
            _mySqlOutbox.Delete(new Guid[] {_firstMessage.Id});

            var remainingMessages = _mySqlOutbox.OutstandingMessages(0);

            remainingMessages.Should().HaveCount(2);
            remainingMessages.Should().Contain(_secondMessage);
            remainingMessages.Should().Contain(_thirdMessage);
            
            _mySqlOutbox.Delete(new []{_secondMessage.Id, _thirdMessage.Id});

            var messages = _mySqlOutbox.OutstandingMessages(0);

            messages.Should().HaveCount(0);
        }
        
        [Fact]
        public async Task When_Removing_Messages_From_The_OutboxAsync()
        {
            _mySqlOutbox.Add(_firstMessage);
            _mySqlOutbox.Add(_secondMessage);
            _mySqlOutbox.Add(_thirdMessage);

            await _mySqlOutbox.DeleteAsync(new []{_firstMessage.Id}, cancellationToken: CancellationToken.None);

            var remainingMessages = await _mySqlOutbox.OutstandingMessagesAsync(0);
                                                                                                       
            remainingMessages.Should().HaveCount(2);
            remainingMessages.Should().Contain(_secondMessage);
            remainingMessages.Should().Contain(_thirdMessage);
            
            await _mySqlOutbox.DeleteAsync(new []{_secondMessage.Id, _thirdMessage.Id}, cancellationToken: CancellationToken.None);

            var finalMessages = await _mySqlOutbox.OutstandingMessagesAsync(0);

            finalMessages.Should().HaveCount(0);
        }

        [Fact]
        public void Dispose()
        {
            _mySqlTestHelper.CleanUpDb();
        }
    }
}
