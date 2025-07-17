using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.ExceptionPolicy.TestDoubles;
using Xunit;
using Paramore.Brighter.Policies.Handlers;
using Polly.Registry;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.Core.Tests.ExceptionPolicy
{
    public class CommandProcessorMissingPolicyFromRegistryAsyncTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new MyCommand();
        private Exception? _exception;

        public CommandProcessorMissingPolicyFromRegistryAsyncTests()
        {
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyDoesNotFailPolicyHandlerAsync>();

            var container = new ServiceCollection();
            container.AddTransient<MyDoesNotFailPolicyHandlerAsync>();
            container.AddTransient<ExceptionPolicyHandlerAsync<MyCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});

            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

            MyDoesNotFailPolicyHandlerAsync.ReceivedCommand = false;

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
        }

        //We have to catch the final exception that bubbles out after retry
        [Fact]
        public async Task When_Sending_A_Command_And_The_Policy_Is_Not_In_The_Registry_Async()
        {
            _exception = await Catch.ExceptionAsync(async () => await _commandProcessor.SendAsync(_myCommand));

            //Should throw an exception
            Assert.IsType<ConfigurationException>(_exception);
            var innerException = _exception.InnerException;
            Assert.NotNull(innerException);
            Assert.IsType<KeyNotFoundException>(innerException);
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
