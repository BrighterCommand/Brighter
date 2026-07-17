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
using System.Threading.Tasks;
using Paramore.Brighter.Actions;
using Paramore.Brighter.Core.Tests.FeatureSwitch.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.FeatureSwitch.Handlers;
using Paramore.Brighter.Observability;
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.Core.Tests.FeatureSwitch
{
    [NotInParallel("CommandProcessor")]
    public class CommandProcessorWithFeatureSwitchOffDontAckAsyncInPipelineTests
    {
        private readonly MyCommandAsync _myAsyncCommand = new();
        private readonly CommandProcessor _commandProcessor;
        public CommandProcessorWithFeatureSwitchOffDontAckAsyncInPipelineTests()
        {
            //Arrange
            SubscriberRegistry registry = new();
            registry.RegisterAsync<MyCommandAsync, MyFeatureSwitchedOffDontAckHandlerAsync>();
            var container = new ServiceCollection();
            container.AddSingleton<MyFeatureSwitchedOffDontAckHandlerAsync>();
            container.AddTransient<FeatureSwitchHandlerAsync<MyCommandAsync>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });
            ServiceProviderHandlerFactory handlerFactory = new(container.BuildServiceProvider());
            MyFeatureSwitchedOffDontAckHandlerAsync.CommandReceived = false;
            _commandProcessor = CommandProcessorBuilder.StartNew().Handlers(new HandlerConfiguration(registry, handlerFactory)).DefaultResilience().NoExternalBus().ConfigureInstrumentation(new BrighterTracer(TimeProvider.System), InstrumentationOptions.All).RequestContextFactory(new InMemoryRequestContextFactory()).RequestSchedulerFactory(new InMemorySchedulerFactory()).Build();
        }

        [Test]
        public async Task When_Sending_An_Async_Command_To_The_Processor_When_A_Feature_Switch_Is_Off_With_DontAck()
        {
            //Act
            var exception = await Assert.That(() => _commandProcessor.SendAsync(_myAsyncCommand)).ThrowsExactly<DontAckAction>();
            //Assert
            await Assert.That(MyFeatureSwitchedOffDontAckHandlerAsync.DidReceive()).IsFalse(); // Target handler was NOT called
            await Assert.That(exception.Message).Contains(nameof(MyFeatureSwitchedOffDontAckHandlerAsync)); // Message contains the handler type name
        }

        [After(Test)]
        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}