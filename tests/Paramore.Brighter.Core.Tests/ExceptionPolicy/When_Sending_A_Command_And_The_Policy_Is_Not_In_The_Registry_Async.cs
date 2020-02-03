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
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.ExceptionPolicy.TestDoubles;
using Xunit;
using Paramore.Brighter.Policies.Handlers;
using Polly.Registry;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.Core.Tests.ExceptionPolicy
{
    public class CommandProcessorMissingPolicyFromRegistryAsyncTests
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new MyCommand();
        private Exception _exception;

        public CommandProcessorMissingPolicyFromRegistryAsyncTests()
        {
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyDoesNotFailPolicyHandlerAsync>();

            var container = new ServiceCollection();
            container.AddTransient<MyDoesNotFailPolicyHandlerAsync>();
            container.AddTransient<ExceptionPolicyHandlerAsync<MyCommand>>();

            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

            MyDoesNotFailPolicyHandler.ReceivedCommand = false;

            _commandProcessor = new CommandProcessor(registry, (IAmAHandlerFactoryAsync)handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());
        }

        //We have to catch the final exception that bubbles out after retry
        [Fact]
        public async Task When_Sending_A_Command_And_The_Policy_Is_Not_In_The_Registry_Async()
        {
            _exception = await Catch.ExceptionAsync(() => _commandProcessor.SendAsync(_myCommand));

            //_should_throw_an_exception
            _exception.Should().BeOfType<KeyNotFoundException>();
            //_should_give_the_name_of_the_missing_policy
            _exception.Should().NotBeNull();
            _exception.Message.Should().Contain("The given key 'MyDivideByZeroPolicy' was not present in the dictionary");
        }
    }
}
