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

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.FeatureSwitch.TestDoubles;
using Paramore.Brighter.FeatureSwitch;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;
using Paramore.Brighter.FeatureSwitch.Handlers;

namespace Paramore.Brighter.Core.Tests.FeatureSwitch
{
    [Collection("CommandProcessor")] 
    public class FeatureSwitchByConfigMissingConfigStrategySilentOffTests : IDisposable
    {
        private readonly MyCommand _myCommand = new();
        private readonly MyCommandAsync _myAsyncCommand = new();

        private readonly CommandProcessor _commandProcessor;
        private readonly IServiceProvider _provider;

        public FeatureSwitchByConfigMissingConfigStrategySilentOffTests()
        {
            SubscriberRegistry registry = new();
            registry.Register<MyCommand, MyFeatureSwitchedConfigHandler>();
            registry.RegisterAsync<MyCommandAsync, MyFeatureSwitchedConfigHandlerAsync>();

            var container = new ServiceCollection();
            container.AddSingleton<MyFeatureSwitchedConfigHandler>();
            container.AddSingleton<MyFeatureSwitchedConfigHandlerAsync>();
            container.AddTransient<FeatureSwitchHandler<MyCommand>>();
            container.AddTransient<FeatureSwitchHandlerAsync<MyCommandAsync>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});

            _provider = container.BuildServiceProvider();
            ServiceProviderHandlerFactory handlerFactory = new(_provider);

            IAmAFeatureSwitchRegistry featureSwitchRegistry = new FakeConfigRegistry
            {
                MissingConfigStrategy = MissingConfigStrategy.SilentOff
            };
            
            _commandProcessor = CommandProcessorBuilder
                .With()
                .ConfigureFeatureSwitches(featureSwitchRegistry)
                .Handlers(new HandlerConfiguration(registry, handlerFactory))
                .DefaultPolicy()
                .NoExternalBus()
                .RequestContextFactory(new InMemoryRequestContextFactory())
                .Build();
        }        

        [Fact]
        public void When_Sending_A_Command_To_The_Processor_When_A_Feature_Switch_Has_No_Config_And_Strategy_Is_SilentOff()
        {
            _commandProcessor.Send(_myCommand);

            _provider.GetService<MyFeatureSwitchedConfigHandler>().DidReceive().Should().BeFalse();
        }    

        [Fact]
        public async Task When_Sending_A_Async_Command_To_The_Processor_When_A_Feature_Switch_Has_No_Config_And_Strategy_Is_SilentOff()
        {
            await _commandProcessor.SendAsync(_myAsyncCommand);

            _provider.GetService<MyFeatureSwitchedConfigHandlerAsync>().DidReceive().Should().BeFalse();
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
