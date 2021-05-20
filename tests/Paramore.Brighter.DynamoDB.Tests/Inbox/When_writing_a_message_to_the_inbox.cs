﻿#region Licence
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
using FluentAssertions;
using Paramore.Brighter.DynamoDB.Tests.TestDoubles;
using Paramore.Brighter.Inbox.DynamoDB;
using Xunit;

namespace Paramore.Brighter.DynamoDB.Tests.Inbox
{
    [Trait("Category", "DynamoDB")]
    public class DynamoDbInboxAddMessageTests : DynamoDBInboxBaseTest
    {
        private readonly DynamoDbInbox _dynamoDbInbox;
        private readonly MyCommand _raisedCommand;
        private readonly string _contextKey;
        private MyCommand _storedCommand;

        public DynamoDbInboxAddMessageTests()
        {
            _dynamoDbInbox = new DynamoDbInbox(Client);
            
            _raisedCommand = new MyCommand {Value = "Test"};
            _contextKey = "context-key";
            _dynamoDbInbox.Add(_raisedCommand, _contextKey);
        }

        [Fact]
        public void When_writing_a_message_to_the_inbox()
        {
            _storedCommand = _dynamoDbInbox.Get<MyCommand>(_raisedCommand.Id, _contextKey);

            //_should_read_the_command_from_the__dynamo_db_inbox
            AssertionExtensions.Should((object) _storedCommand).NotBeNull();
            //_should_read_the_command_value
            AssertionExtensions.Should((string) _storedCommand.Value).Be(_raisedCommand.Value);
            //_should_read_the_command_id
            AssertionExtensions.Should((Guid) _storedCommand.Id).Be(_raisedCommand.Id);
        }
    }
}
