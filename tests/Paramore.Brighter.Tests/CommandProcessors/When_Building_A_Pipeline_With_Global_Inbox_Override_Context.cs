using System.Linq;
using FluentAssertions;
using Paramore.Brighter.Inbox.Handlers;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Tests.FeatureSwitch.TestDoubles;
using TinyIoC;
using Xunit;

namespace Paramore.Brighter.Tests.CommandProcessors
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
            
            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);

            container.Register<IHandleRequests<MyCommand>, MyCommandHandler>();
            container.Register<IAmAnInbox>(_inbox);
 
            _requestContext = new RequestContext();
            
            _inboxConfiguration = new InboxConfiguration(
                scope: InboxScope.All, 
                context: (handlerType) => CONTEXT_KEY);

            _chainBuilder = new PipelineBuilder<MyCommand>(registry, handlerFactory, _inboxConfiguration);
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
