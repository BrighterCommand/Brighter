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
    public class MsSqlOutboxRangeRequestAsyncTests : IDisposable
    {
        private readonly MsSqlTestHelper _msSqlTestHelper;
        private readonly string _testTopicOne = "test_topic";
        private string _testTopicTwo = "test_topic2";
        private readonly string _testTopicThree = "test_topic3";
        private IEnumerable<Message> _messages;
        private readonly Message _messageTwo;
        private readonly Message _messageThree;
        private readonly Message _messageOne;
        private readonly MsSqlOutbox _sqlOutbox;

        public MsSqlOutboxRangeRequestAsyncTests()
        {
            _msSqlTestHelper = new MsSqlTestHelper();
            _msSqlTestHelper.SetupMessageDb();

            _sqlOutbox = new MsSqlOutbox(_msSqlTestHelper.OutboxConfiguration);
            _messageOne = new Message(new MessageHeader(Guid.NewGuid(), _testTopicOne, MessageType.MT_DOCUMENT), new MessageBody("message body"));
            _messageTwo = new Message(new MessageHeader(Guid.NewGuid(), _testTopicTwo, MessageType.MT_DOCUMENT), new MessageBody("message body2"));
            _messageThree = new Message(new MessageHeader(Guid.NewGuid(), _testTopicThree, MessageType.MT_DOCUMENT), new MessageBody("message body3"));
        }

        [Fact]
        public async Task When_There_Are_Multiple_Messages_In_The_Outbox_And_A_Range_Is_Fetched_Async()
        {
            await _sqlOutbox.AddAsync(_messageOne);
            await Task.Delay(100);
            await _sqlOutbox.AddAsync(_messageTwo);
            await Task.Delay(100);
            await _sqlOutbox.AddAsync(_messageThree);

             _messages = await _sqlOutbox.GetAsync(1, 3);

            //should fetch 1 message
            _messages.Should().HaveCount(1);
            //should fetch expected message
            _messages.First().Header.Topic.Should().Be(_messageThree.Header.Topic);
            //should not fetch null messages
            _messages.Should().NotBeNull();
        }

        public void Dispose()
        {
            _msSqlTestHelper.CleanUpDb();
        }
    }
}
