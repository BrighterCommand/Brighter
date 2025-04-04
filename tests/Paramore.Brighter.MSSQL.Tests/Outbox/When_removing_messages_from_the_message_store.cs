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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
            
            _firstMessage = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("Test"), MessageType.MT_COMMAND, 
                    timeStamp: DateTime.UtcNow.AddHours(-3)
                ), 
                new MessageBody("Body")
            );
            _secondMessage = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("Test2"), MessageType.MT_COMMAND, 
                    timeStamp: DateTime.UtcNow.AddHours(-2)
                ), 
                new MessageBody("Body2")
            );
            _thirdMessage = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("Test3"), MessageType.MT_COMMAND, 
                    timeStamp:DateTime.UtcNow.AddHours(-1)
                ), 
                new MessageBody("Body3")
            );

            var context = new RequestContext();
            _outbox.Add(_firstMessage, context);
            _outbox.Add(_secondMessage, context);
            _outbox.Add(_thirdMessage, context);
        }

        [Fact]
        public void When_Removing_Messages_From_The_Outbox()
        {
            var context = new RequestContext();
            _outbox.Delete([_firstMessage.Id], context);

            var remainingMessages = _outbox.OutstandingMessages(TimeSpan.Zero, context);

            Assert.Equal(2, (remainingMessages)?.Count());
            Assert.Contains(_secondMessage, remainingMessages);
            Assert.Contains(_thirdMessage, remainingMessages);
            
            _outbox.Delete(remainingMessages.Select(m => m.Id).ToArray(), context);

            var messages = _outbox.OutstandingMessages(TimeSpan.Zero, context);

            Assert.Empty(messages ?? []);

        }
        [Fact]
        public async Task When_Removing_Messages_From_The_OutboxAsync()
        {
            var context = new RequestContext();
            await _outbox.DeleteAsync([_firstMessage.Id], context, cancellationToken: CancellationToken.None);

            var remainingMessages = await _outbox.OutstandingMessagesAsync(TimeSpan.Zero, context);

            Assert.Equal(2, (remainingMessages)?.Count());
            Assert.Contains(_secondMessage, remainingMessages);
            Assert.Contains(_thirdMessage, remainingMessages);
            
            await _outbox.DeleteAsync(
                remainingMessages.Select(m => m.Id).ToArray(), 
                context,
                cancellationToken: CancellationToken.None
                );

            var messages = await _outbox.OutstandingMessagesAsync(TimeSpan.Zero, context);

            Assert.Empty(messages ?? []);

        }

        private void Release()
        {
            _msSqlTestHelper.CleanUpDb();
        }
        
        public void Dispose()
        {
            Release();
        }

        ~SqlOutboxDeletingMessagesTests()
        {
            Release();
        }
    }
}
