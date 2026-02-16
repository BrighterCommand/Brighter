#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using Paramore.Brighter.Actions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.FeatureSwitch.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.FeatureSwitch.Handlers;
using Paramore.Brighter.Observability;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Core.Tests.FeatureSwitch
{
    [Collection("CommandProcessor")]
    public class CommandProcessorWithFeatureSwitchOffDontAckInPipelineTests : IDisposable
    {
        private readonly MyCommand _myCommand = new();
        private readonly CommandProcessor _commandProcessor;

        public CommandProcessorWithFeatureSwitchOffDontAckInPipelineTests()
        {
            //Arrange
            SubscriberRegistry registry = new();
            registry.Register<MyCommand, MyFeatureSwitchedOffDontAckHandler>();

            var container = new ServiceCollection();
            container.AddSingleton<MyFeatureSwitchedOffDontAckHandler>();
            container.AddTransient<FeatureSwitchHandler<MyCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });

            ServiceProviderHandlerFactory handlerFactory = new(container.BuildServiceProvider());

            MyFeatureSwitchedOffDontAckHandler.CommandReceived = false;

            _commandProcessor = CommandProcessorBuilder
                .StartNew()
                .Handlers(new HandlerConfiguration(registry, handlerFactory))
                .DefaultResilience()
                .NoExternalBus()
                .ConfigureInstrumentation(new BrighterTracer(TimeProvider.System), InstrumentationOptions.All)
                .RequestContextFactory(new InMemoryRequestContextFactory())
                .RequestSchedulerFactory(new InMemorySchedulerFactory())
                .Build();
        }

        [Fact]
        public void When_Sending_A_Command_To_The_Processor_When_A_Feature_Switch_Is_Off_With_DontAck()
        {
            //Act
            var exception = Assert.Throws<DontAckAction>(() => _commandProcessor.Send(_myCommand));

            //Assert
            Assert.False(MyFeatureSwitchedOffDontAckHandler.DidReceive()); // Target handler was NOT called
            Assert.Contains(nameof(MyFeatureSwitchedOffDontAckHandler), exception.Message); // Message contains the handler type name
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
