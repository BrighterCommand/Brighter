using System;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.OnceOnly.TestDoubles;
using Paramore.Brighter.Inbox.Exceptions;
using Polly.Registry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;
using Paramore.Brighter.Inbox.Handlers;

namespace Paramore.Brighter.Core.Tests.OnceOnly
{
    
    [Trait("Fragile", "CI")]
    [Collection("CommandProcessor")]
    public class OnceOnlyAttributeAsyncTests : IDisposable
    {
        private readonly MyCommand _command;
        private readonly IAmAnInboxAsync _inbox;
        private readonly IAmACommandProcessor _commandProcessor;
        
        public OnceOnlyAttributeAsyncTests()
        {
            _inbox = new InMemoryInbox(new FakeTimeProvider());

            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyStoredCommandHandlerAsync>();

            var container = new ServiceCollection();
            container.AddTransient<MyStoredCommandHandlerAsync>();
            container.AddSingleton(_inbox);
            container.AddTransient<UseInboxHandlerAsync<MyCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});
        

            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

            _command = new MyCommand {Value = "My Test String"};

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), 
                new PolicyRegistry(), new ResiliencePipelineRegistry<string>(),new InMemorySchedulerFactory());
        }

        [Fact]
        public async Task When_Handling_A_Command_Only_Once()
        {
            await _commandProcessor.SendAsync(_command);
            
            Exception ex = await Assert.ThrowsAsync<OnceOnlyException>(async () => await _commandProcessor.SendAsync(_command));
            
            Assert.Equal($"A command with id {_command.Id} has already been handled", ex.Message);
 
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
