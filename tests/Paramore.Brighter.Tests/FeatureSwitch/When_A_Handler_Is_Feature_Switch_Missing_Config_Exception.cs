#region Licence
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

using System;
using FluentAssertions;
using Paramore.Brighter.FeatureSwitch;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Tests.FeatureSwitch.TestDoubles;
using Polly.Registry;
using TinyIoC;
using Xunit;

namespace Paramore.Brighter.Tests.FeatureSwitch
{
    [Collection("Feature Switch Check")]
    public class FeatureSwitchByConfigMissingConfigStrategyExceptionTests : IDisposable
    {
        private readonly MyCommand _myCommand = new MyCommand();
        private readonly SubscriberRegistry _registry;
        private readonly TinyIocHandlerFactory _handlerFactory;
        private readonly IAmAFeatureSwitchRegistry _featureSwitchRegistry;

        private CommandProcessor _commandProcessor;
        private Exception _exception;

        public FeatureSwitchByConfigMissingConfigStrategyExceptionTests()
        {
            _registry = new SubscriberRegistry();
            _registry.Register<MyCommand, MyFeatureSwitchedConfigHandler>();

            var container = new TinyIoCContainer();
            _handlerFactory = new TinyIocHandlerFactory(container);

            container.Register<IHandleRequests<MyCommand>, MyFeatureSwitchedConfigHandler>();
            
            _featureSwitchRegistry = new FakeConfigRegistry();
        }        

        [Fact]
        public void When_Sending_A_Command_To_The_Processor_When_A_Feature_Switch_Has_No_Config_And_Strategy_Is_Exception()
        {
            _featureSwitchRegistry.MissingConfigStrategy = MissingConfigStrategy.Exception;

            _commandProcessor = new CommandProcessor(_registry, 
                                                     _handlerFactory, 
                                                     new InMemoryRequestContextFactory(), 
                                                     new PolicyRegistry(),
                                                     _featureSwitchRegistry);

            _exception = Catch.Exception(() => _commandProcessor.Send(_myCommand));

            _exception.Should().BeOfType<ConfigurationException>();
            _exception.Should().NotBeNull();
            _exception.Message.Should().Contain($"Handler of type {typeof(MyFeatureSwitchedConfigHandler).Name} does not have a Feature Switch configuration!");

            MyFeatureSwitchedConfigHandler.DidReceive(_myCommand).Should().BeFalse();            
        }

        public void Dispose()
        {
            MyFeatureSwitchedConfigHandler.CommandReceived = false;
            _commandProcessor?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
