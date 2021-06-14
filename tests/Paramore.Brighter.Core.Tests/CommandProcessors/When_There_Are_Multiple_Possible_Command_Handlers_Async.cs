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
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Polly.Registry;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors
{
    [Collection("CommandProcessor")]
    public class CommandProcessorSendWithMultipleMatchesAsyncTests
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly IDictionary<string, Guid> _receivedMessages = new Dictionary<string, Guid>();
        private readonly MyCommand _myCommand = new MyCommand();
        private Exception _exception;

        public CommandProcessorSendWithMultipleMatchesAsyncTests()
        {
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyCommandHandlerAsync>();
            registry.RegisterAsync<MyCommand, MyImplicitHandlerAsync>();

            var container = new ServiceCollection();
            container.AddTransient<MyCommandHandlerAsync>();
            container.AddTransient<MyImplicitHandlerAsync>();
            container.AddTransient<MyLoggingHandlerAsync<MyCommand>>();
            container.AddSingleton(_receivedMessages);

            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());
            
            _commandProcessor = new CommandProcessor(registry, (IAmAHandlerFactoryAsync)handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());
            PipelineBuilder<MyCommand>.ClearPipelineCache();
        }

        //Ignore any errors about adding System.Runtime from the IDE. See https://social.msdn.microsoft.com/Forums/en-US/af4dc0db-046c-4728-bfe0-60ceb93f7b9f/vs2012net-45-rc-compiler-error-when-using-actionblock-missing-reference-to?forum=tpldataflow
        [Fact]
        public async Task When_There_Are_Multiple_Possible_Command_Handlers_Async()
        {
            _exception = await Catch.ExceptionAsync(async () => await _commandProcessor.SendAsync(_myCommand));

            //_should_fail_because_multiple_receivers_found
            _exception.Should().BeOfType<ArgumentException>();
            //_should_have_an_error_message_that_tells_you_why = () => _exception
            _exception.Should().NotBeNull();
            _exception.Message.Should().Contain("More than one handler was found for the typeof command Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles.MyCommand - a command should only have one handler.");
        }
    }
}
