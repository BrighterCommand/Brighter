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
using Paramore.Brighter.Outbox.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.Outbox
{
    [Trait("Category", "PostgresSql")]
    public class SqlOutboxDeletingMessagesTests : IDisposable
    {
        private readonly PostgresSqlTestHelper _postgresSqlTestHelper;
        private readonly Message _firstMessage;
        private readonly Message _secondMessage;
        private readonly Message _thirdMessage;
        private readonly PostgreSqlOutbox _sqlOutbox;

        public SqlOutboxDeletingMessagesTests()
        {
            _postgresSqlTestHelper = new PostgresSqlTestHelper();
            _postgresSqlTestHelper.SetupMessageDb();

            _sqlOutbox = new PostgreSqlOutbox(_postgresSqlTestHelper.Configuration);
            _firstMessage = new Message(new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("Test"), MessageType.MT_COMMAND, 
                timeStamp:DateTime.UtcNow.AddHours(-3)), new MessageBody("Body")
            );
            _secondMessage = new Message(new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("Test2"), MessageType.MT_COMMAND, 
                timeStamp:DateTime.UtcNow.AddHours(-2)), new MessageBody("Body2")
            );
            _thirdMessage = new Message(new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("Test3"), MessageType.MT_COMMAND, 
                timeStamp:DateTime.UtcNow.AddHours(-1)), new MessageBody("Body3")
            );
            
        }

        [Fact]
        public void When_Removing_Messages_From_The_Outbox()
        {
            //arrange
            var context = new RequestContext();
            
            //act
            _sqlOutbox.Add(_firstMessage, context);
            _sqlOutbox.Add(_secondMessage, context);
            _sqlOutbox.Add(_thirdMessage, context);
            
            _sqlOutbox.Delete([_firstMessage.Id], context);

            //assert
            var remainingMessages = _sqlOutbox.OutstandingMessages(TimeSpan.Zero, context);

            var msgs = remainingMessages as Message[] ?? remainingMessages.ToArray();
            Assert.Equal(2, msgs?.Length);
            Assert.Contains(_secondMessage, msgs);
            Assert.Contains(_thirdMessage, msgs);
            
            _sqlOutbox.Delete(new []{_secondMessage.Id, _thirdMessage.Id}, context);

            var messages = _sqlOutbox.OutstandingMessages(TimeSpan.Zero, context);

            Assert.Empty(messages ?? []);
        }

        private void Release()
        {
            _postgresSqlTestHelper.CleanUpDb();
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
