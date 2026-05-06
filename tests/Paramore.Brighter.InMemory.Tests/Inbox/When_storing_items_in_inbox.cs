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
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.InMemory.Tests.Data;

namespace Paramore.Brighter.InMemory.Tests.Inbox
{
    [Category("InMemory")]
     public class InMemoryInboxTests
    {

        [Test]
        public async Task When_storing_a_seen_message_in_the_inbox()
        {
            //Arrange
            var inbox = new InMemoryInbox(new FakeTimeProvider());
            const string contextKey = "Developer_Test";

            var command = new SimpleCommand();
            
            //Act
            await inbox.AddAsync(command, contextKey, null);

            var storedCommand = inbox.Get<SimpleCommand>(command.Id, contextKey, null);

            //Assert
            await Assert.That(storedCommand).IsNotNull();
            await Assert.That(storedCommand.Id).IsEqualTo(command.Id);

        }

        [Test]
        public async Task When_testing_for_a_message_in_the_inbox()
        {
            //Arrange
            var inbox = new InMemoryInbox(new FakeTimeProvider());
            const string contextKey = "Developer_Test";

            var command = new SimpleCommand();
            
            //Act
            await inbox.AddAsync(command, contextKey, null);

            var exists = await inbox.ExistsAsync<SimpleCommand>(command.Id, contextKey, null);

            //Assert
            await Assert.That(exists).IsTrue();
        }

        [Test]
        public async Task When_testing_for_a_missing_command()
        {
            //Arrange
            var inbox = new InMemoryInbox(new FakeTimeProvider());
            const string contextKey = "Developer_Test";

            var command = new SimpleCommand();
            
            //Act
            var exists = await inbox.ExistsAsync<SimpleCommand>(command.Id, contextKey, null);

            //Assert
            await Assert.That(exists).IsFalse();

        }

        [Test]
        public async Task When_storing_multiple_entries_retrieve_the_right_one()
        {
           //Arrange
           var inbox = new InMemoryInbox(new FakeTimeProvider());
           const string contextKey = "Developer_Test";

           var commands = new SimpleCommand[] {new SimpleCommand(), new SimpleCommand(), new SimpleCommand(), new SimpleCommand(), new SimpleCommand()};
           foreach (var command in commands)
           {
               await inbox.AddAsync(command, contextKey, null);
           }
            
           //Act
           var firstCommand = inbox.Get<SimpleCommand>(commands[0].Id, contextKey, null);
           var lastCommand = inbox.Get<SimpleCommand>(commands[4].Id, contextKey, null);

           //Assert
           await Assert.That(firstCommand).IsNotNull();
           await Assert.That(lastCommand).IsNotNull();

           await Assert.That(firstCommand.Id).IsEqualTo(commands[0].Id);
           await Assert.That(lastCommand.Id).IsEqualTo(commands[4].Id);

        }

        [Test]
        public async Task When_storing_multiple_entries_exists_should_find()
        {
            //Arrange
            var inbox = new InMemoryInbox(new FakeTimeProvider());
            const string contextKey = "Developer_Test";
 
            var commands = new SimpleCommand[] {new SimpleCommand(), new SimpleCommand(), new SimpleCommand(), new SimpleCommand(), new SimpleCommand()};
            foreach (var command in commands)
            {
                await inbox.AddAsync(command, contextKey, null);
            }
             
            //Act
            var firstCommandExists = await inbox.ExistsAsync<SimpleCommand>(commands[0].Id, contextKey, null);
            var lastCommandExists = await inbox.ExistsAsync<SimpleCommand>(commands[4].Id, contextKey, null);
 
            //Assert
            await Assert.That(firstCommandExists).IsTrue();
            await Assert.That(lastCommandExists).IsTrue();

        }

        [Test]
        public async Task When_storing_many_but_not_requested_exists_should_not_find()
        {
            //Arrange
            var inbox = new InMemoryInbox(new FakeTimeProvider());
            const string contextKey = "Developer_Test";
 
            var commands = new SimpleCommand[] {new SimpleCommand(), new SimpleCommand(), new SimpleCommand(), new SimpleCommand(), new SimpleCommand()};
            foreach (var command in commands)
            {
                await inbox.AddAsync(command, contextKey, null);
            }
             
            //Act
            var firstCommandExists = await inbox.ExistsAsync<SimpleCommand>(Guid.NewGuid().ToString(), contextKey, null);
 
            //Assert
            await Assert.That(firstCommandExists).IsFalse();
             
        }

    }
}
