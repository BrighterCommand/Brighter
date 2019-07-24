#region Licence
/* The MIT License (MIT)
Copyright © 2015 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Amazon;
using FluentAssertions;
using Paramore.Brighter.Inbox.DynamoDB;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Tests.Inbox.DynamoDB
{
    [Trait("Category", "DynamoDB")]
    [Collection("DynamoDB Inbox")]
    public class DynamoDbInboxDuplicateMessageAsyncTests : DynamoDBInboxBaseTest
    {
        private readonly DynamoDbInbox _dynamoDbInbox;
        private readonly MyCommand _raisedCommand;
        private readonly string _contextKey;
        private Exception _exception;

        public DynamoDbInboxDuplicateMessageAsyncTests()
        {
            _dynamoDbInbox = new DynamoDbInbox(new DynamoDbInboxConfiguration(Credentials, RegionEndpoint.EUWest1, TableName));
 
            _raisedCommand = new MyCommand {Value = "Test"};
            _contextKey = "context-key";
        }

        [Fact]
        public async Task When_the_message_is_already_in_the_Inbox_async()
        {
            _dynamoDbInbox.Add(_raisedCommand, _contextKey);

            _exception = await Catch.ExceptionAsync(() => _dynamoDbInbox.AddAsync(_raisedCommand, _contextKey));

            //_should_succeed_even_if_the_message_is_a_duplicate
            _exception.Should().BeNull();
        }

    }
}
