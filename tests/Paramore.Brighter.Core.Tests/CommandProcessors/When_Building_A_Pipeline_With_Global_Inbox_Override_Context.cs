using System.Linq;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;
using Paramore.Brighter.Inbox.Handlers;

namespace Paramore.Brighter.Core.Tests.CommandProcessors
{
    public class PipelineGlobalInboxContextTests
    {
        private const string CONTEXT_KEY = "TestHandlerNameOverride";
        private readonly PipelineBuilder<MyCommand> _chainBuilder;
        private Pipelines<MyCommand> _chainOfResponsibility;
        private readonly RequestContext _requestContext;
        private readonly InboxConfiguration _inboxConfiguration;
        private IAmAnInbox _inbox;


        public PipelineGlobalInboxContextTests()
        {
            _inbox = new InMemoryInbox();
            
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyGlobalInboxCommandHandler>();
            
            var container = new ServiceCollection();
            container.AddTransient<MyGlobalInboxCommandHandler>();
            container.AddSingleton<IAmAnInbox>(_inbox);
            container.AddTransient<UseInboxHandler<MyCommand>>();

            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());
            
            _requestContext = new RequestContext();
            
            _inboxConfiguration = new InboxConfiguration(
                scope: InboxScope.All, 
                context: (handlerType) => CONTEXT_KEY);

            _chainBuilder = new PipelineBuilder<MyCommand>(registry, (IAmAHandlerFactory)handlerFactory, _inboxConfiguration);
            PipelineBuilder<MyCommand>.ClearPipelineCache();
            
        }

        [Fact]
        public void When_Building_A_Pipeline_With_Global_inbox()
        {
            //act
            _chainOfResponsibility = _chainBuilder.Build(_requestContext);
            
            var firstHandler = _chainOfResponsibility.First();
            var myCommmand = new MyCommand();
            firstHandler.Handle(myCommmand);

            //assert
            var exists = _inbox.Exists<MyCommand>(myCommmand.Id, CONTEXT_KEY, 500);
            exists.Should().BeTrue();
        }
        

    }
}
