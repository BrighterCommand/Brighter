#region Licence

/* The MIT License (MIT)
Copyright © 2020 Ian Cooper <ian_hammond_cooper@yahoo.co.uk> 

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
using Paramore.Brighter.InMemory.Tests.Data;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Inbox
{
    [Trait("Category", "InMemory")]
     public class InMemoryInboxTests
    {

        [Fact]
        public void When_storing_a_seen_message_in_the_inbox()
        {
            //Arrange
            var inbox = new InMemoryInbox();
            const string contextKey = "Developer_Test";

            var command = new SimpleCommand();

            //Act
            inbox.Add(command, contextKey);

            var storedCommand = inbox.Get<SimpleCommand>(command.Id, contextKey);

            //Assert
            storedCommand.Should().NotBeNull();
            storedCommand.Id.Should().Be(command.Id);
        }

        [Fact]
        public void When_testing_for_a_message_in_the_inbox()
        {
            //Arrange
            var inbox = new InMemoryInbox();
            const string contextKey = "Developer_Test";

            var command = new SimpleCommand();

            //Act
            inbox.Add(command, contextKey);

            var exists = inbox.Exists<SimpleCommand>(command.Id, contextKey);

            //Assert
            exists.Should().BeTrue();
        }

        [Fact]
        public void When_testing_for_a_missing_command()
        {
            //Arrange
            var inbox = new InMemoryInbox();
            const string contextKey = "Developer_Test";

            var command = new SimpleCommand();

            //Act
            var exists = inbox.Exists<SimpleCommand>(command.Id, contextKey);

            //Assert
            exists.Should().BeFalse();
        }

        [Fact]
        public void When_storing_multiple_entries_retrieve_the_right_one()
        {
           //Arrange
           var inbox = new InMemoryInbox();
           const string contextKey = "Developer_Test";

           var commands = new SimpleCommand[] {new SimpleCommand(), new SimpleCommand(), new SimpleCommand(), new SimpleCommand(), new SimpleCommand()};
           foreach (var command in commands)
           {
               inbox.Add(command, contextKey);
           }

           //Act
           var firstCommand = inbox.Get<SimpleCommand>(commands[0].Id, contextKey);
           var lastCommand = inbox.Get<SimpleCommand>(commands[4].Id, contextKey);

           //Assert
           firstCommand.Should().NotBeNull();
           lastCommand.Should().NotBeNull();

           firstCommand.Id.Should().Be(commands[0].Id, contextKey);
           lastCommand.Id.Should().Be(commands[4].Id, contextKey);
        }

        [Fact]
        public void When_storing_multiple_entries_exists_should_find()
        {
            //Arrange
            var inbox = new InMemoryInbox();
            const string contextKey = "Developer_Test";

            var commands = new SimpleCommand[] {new SimpleCommand(), new SimpleCommand(), new SimpleCommand(), new SimpleCommand(), new SimpleCommand()};
            foreach (var command in commands)
            {
                inbox.Add(command, contextKey);
            }

            //Act
            var firstCommandExists = inbox.Exists<SimpleCommand>(commands[0].Id, contextKey);
            var lastCommandExists = inbox.Exists<SimpleCommand>(commands[4].Id, contextKey);

            //Assert
            firstCommandExists.Should().BeTrue();
            lastCommandExists.Should().BeTrue();
        }

        [Fact]
        public void When_storing_many_but_not_requested_exists_should_not_find()
        {
            //Arrange
            var inbox = new InMemoryInbox();
            const string contextKey = "Developer_Test";

            var commands = new SimpleCommand[] {new SimpleCommand(), new SimpleCommand(), new SimpleCommand(), new SimpleCommand(), new SimpleCommand()};
            foreach (var command in commands)
            {
                inbox.Add(command, contextKey);
            }

            //Act
            var firstCommandExists = inbox.Exists<SimpleCommand>(Guid.NewGuid(), contextKey);

            //Assert
            firstCommandExists.Should().BeFalse();
        }
    }
}
