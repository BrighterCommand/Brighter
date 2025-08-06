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
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.OnceOnly.TestDoubles;
using Polly.Registry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;
using Paramore.Brighter.Inbox.Handlers;

namespace Paramore.Brighter.Core.Tests.OnceOnly
{
    [Collection("CommandProcessor")]
    public class CommandProcessorUsingInboxTests : IDisposable
    {
        private readonly MyCommand _command;
        private readonly IAmAnInboxSync _inbox;
        private readonly IAmACommandProcessor _commandProcessor;
        private readonly string _contextKey;

        public CommandProcessorUsingInboxTests()
        {
            _inbox = new InMemoryInbox(new FakeTimeProvider());

            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyStoredCommandHandler>();
            registry.Register<MyCommandToFail, MyStoredCommandToFailHandler>();

            var container = new ServiceCollection();
            container.AddTransient<MyStoredCommandHandler>();
            container.AddTransient<MyStoredCommandToFailHandler>();
            container.AddSingleton(_inbox);
            container.AddTransient<UseInboxHandler<MyCommand>>();
            container.AddTransient<UseInboxHandler<MyCommandToFail>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});

            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

            _command = new MyCommand {Value = "My Test String"};

            _contextKey = typeof(MyStoredCommandHandler).FullName;
            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), 
                new PolicyRegistry(), new ResiliencePipelineRegistry<string>(),new InMemorySchedulerFactory());

        }

        [Fact]
        public void When_Handling_A_Command_With_A_Inbox_Enabled()
        {
            _commandProcessor.Send(_command);

            //should_store_the_command_to_the_inbox
            Assert.Equal(_command.Value, _inbox.Get<MyCommand>(_command.Id, _contextKey, null).Value);
        }

        [Fact]
        public void Command_Is_Not_Stored_If_The_Handler_Is_Not_Successful()
        {
            string id = Guid.NewGuid().ToString();

            Assert.Throws<NotImplementedException>(() => _commandProcessor.Send(new MyCommandToFail { Id = id}));

            Assert.False(_inbox.Exists<MyCommandToFail>(id, typeof(MyStoredCommandToFailHandler).FullName, null));
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
