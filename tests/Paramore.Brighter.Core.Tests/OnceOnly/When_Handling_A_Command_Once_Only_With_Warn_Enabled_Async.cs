﻿#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.OnceOnly.TestDoubles;
using Polly.Registry;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;
using Paramore.Brighter.Inbox.Handlers;

namespace Paramore.Brighter.Core.Tests.OnceOnly
{
    [Trait("Fragile", "CI")]
    [Collection("CommandProcessor")]
    public class OnceOnlyAttributeWithWarnExceptionAsyncTests
    {
        private readonly MyCommand _command;
        private readonly IAmAnInboxAsync _inbox;
        private readonly IAmACommandProcessor _commandProcessor;

        public OnceOnlyAttributeWithWarnExceptionAsyncTests()
        {
            _inbox = new InMemoryInbox();
            
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyStoredCommandToWarnHandlerAsync>();

            var container = new ServiceCollection();
            container.AddTransient<MyStoredCommandToWarnHandlerAsync>();
            container.AddSingleton(_inbox);
            container.AddTransient<UseInboxHandlerAsync<MyCommand>>();

            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());
            
            _command = new MyCommand {Value = "My Test String"};
            
            _commandProcessor = new CommandProcessor(registry, (IAmAHandlerFactoryAsync)handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());
        }

        [Fact]
        public async Task When_Handling_A_Command_Once_Only_With_Warn_Enabled()
        {
            await _commandProcessor.SendAsync(_command);
            await _commandProcessor.SendAsync(_command);
            
            MyStoredCommandToWarnHandlerAsync.ReceivedCount.Should().Be(1);
        }
    }
}
