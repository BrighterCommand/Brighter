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
using FluentAssertions;
using Paramore.Brighter.MessageStore.MySql;
using Xunit;

namespace Paramore.Brighter.Tests.MessageStore.MySql
{
    [Trait("Category", "MySql")]
    [Collection("MySql MessageStore")]
    public class MySqlMessageStoreMessageAlreadyExistsAsyncTests : IDisposable
    {
        private Exception _exception;
        private readonly Message _messageEarliest;
        private readonly MySqlMessageStore _sqlMessageStore;
        private readonly MySqlTestHelper _msSqlTestHelper;

        public MySqlMessageStoreMessageAlreadyExistsAsyncTests()
        {
            _msSqlTestHelper = new MySqlTestHelper();
            _msSqlTestHelper.SetupMessageDb();

            _sqlMessageStore = new MySqlMessageStore(_msSqlTestHelper.MessageStoreConfiguration);
            _messageEarliest = new Message(new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_DOCUMENT), new MessageBody("message body"));
        }

        [Fact]
        public async Task When_The_Message_Is_Already_In_The_Message_Store_Async()
        {
            await _sqlMessageStore.AddAsync(_messageEarliest);
            _exception = await Catch.ExceptionAsync(() => _sqlMessageStore.AddAsync(_messageEarliest));
            _exception.Should().BeNull();
        }

        public void Dispose()
        {
            _msSqlTestHelper.CleanUpDb();
        }
    }
}
