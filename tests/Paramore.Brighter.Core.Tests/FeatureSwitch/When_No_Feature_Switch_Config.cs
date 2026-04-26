using System;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.FeatureSwitch.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.FeatureSwitch.Handlers;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Core.Tests.FeatureSwitch
{
    public class CommandProcessorWithNullFeatureSwitchConfig
    {
        private readonly MyCommand _myCommand = new();
        private readonly MyCommandAsync _myAsyncCommand = new();
        private readonly ServiceProvider _provider;
        private readonly CommandProcessor _commandProcessor;
        public CommandProcessorWithNullFeatureSwitchConfig()
        {
            SubscriberRegistry registry = new();
            registry.Register<MyCommand, MyFeatureSwitchedConfigHandler>();
            registry.RegisterAsync<MyCommandAsync, MyFeatureSwitchedConfigHandlerAsync>();
            var container = new ServiceCollection();
            container.AddSingleton<MyFeatureSwitchedConfigHandler>();
            container.AddSingleton<MyFeatureSwitchedConfigHandlerAsync>();
            container.AddTransient<FeatureSwitchHandler<MyCommand>>();
            container.AddTransient<FeatureSwitchHandlerAsync<MyCommandAsync>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });
            _provider = container.BuildServiceProvider();
            ServiceProviderHandlerFactory handlerFactory = new(_provider);
            _commandProcessor = CommandProcessorBuilder.StartNew().Handlers(new HandlerConfiguration(registry, handlerFactory)).DefaultResilience().NoExternalBus().ConfigureInstrumentation(new BrighterTracer(), InstrumentationOptions.All).RequestContextFactory(new InMemoryRequestContextFactory()).RequestSchedulerFactory(new InMemorySchedulerFactory()).Build();
        }

        [Test]
        public async Task When_a_sending_a_command_to_the_processor_when_null_feature_switch_config()
        {
            _commandProcessor.Send(_myCommand);
            await Assert.That(_provider.GetService<MyFeatureSwitchedConfigHandler>()!.DidReceive()).IsTrue();
        }

        [Test]
        public async Task When_a_sending_a_async_command_to_the_processor_when_null_feature_switch_config()
        {
            await _commandProcessor.SendAsync(_myAsyncCommand);
            await Assert.That(_provider.GetService<MyFeatureSwitchedConfigHandlerAsync>()!.DidReceive()).IsTrue();
        }
    }
}