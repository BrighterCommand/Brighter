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
using Paramore.Brighter.Outbox.Sqlite;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.Outbox
{
    [Trait("Category", "Sqlite")]
    public class SqlOutboxDeletingMessagesTests : IAsyncDisposable
    {
        private readonly SqliteTestHelper _sqliteTestHelper;
        private readonly SqliteOutbox _outbox;
        private readonly Message _firstMessage;
        private readonly Message _secondMessage;
        private readonly Message _thirdMessage;

        public SqlOutboxDeletingMessagesTests()
        {
            _sqliteTestHelper = new SqliteTestHelper();
            _sqliteTestHelper.SetupMessageDb();
            _outbox = new SqliteOutbox(_sqliteTestHelper.OutboxConfiguration);

            _firstMessage =
                new Message(
                    new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("Test"), MessageType.MT_COMMAND, 
                        timeStamp: DateTime.UtcNow.AddHours(-3)
                    ),
                    new MessageBody("Body"));
            _secondMessage =
                new Message(
                    new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("Test2"), MessageType.MT_COMMAND, 
                        timeStamp: DateTime.UtcNow.AddHours(-2)
                ),
                    new MessageBody("Body2"));
            _thirdMessage =
                new Message(
                    new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("Test3"), MessageType.MT_COMMAND, 
                        timeStamp: DateTime.UtcNow.AddHours(-1)
                    ),
                    new MessageBody("Body3")
                );
        }

        [Fact]
        public void When_Removing_Messages_From_The_Outbox()
        {
            var context = new RequestContext();
            _outbox.Add(_firstMessage, context);
            _outbox.Add(_secondMessage, context);
            _outbox.Add(_thirdMessage, context);

            _outbox.Delete([_firstMessage.Id, _thirdMessage.Id], context);

            Assert.Equal(MessageType.MT_COMMAND, _outbox.Get(_secondMessage.Id, context).Header.MessageType);
            Assert.Equal(MessageType.MT_NONE, _outbox.Get(_firstMessage.Id, context).Header.MessageType);
            Assert.Equal(MessageType.MT_NONE, _outbox.Get(_thirdMessage.Id, context).Header.MessageType);
            
            _outbox.Delete([_secondMessage.Id], context);

            Assert.NotNull(_outbox.Get(_secondMessage.Id, context));
        }

        public async ValueTask DisposeAsync()
        {
            await _sqliteTestHelper.CleanUpDbAsync();
        }
    }
}
