using System.Linq;
using FluentAssertions;
using Paramore.Brighter.Inbox.Handlers;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Tests.FeatureSwitch.TestDoubles;
using TinyIoC;
using Xunit;

namespace Paramore.Brighter.Tests.CommandProcessors
{
    //TODO:
    //Already has an Inbox attribute, with different defaults frx throws
    //Has a NoInbox attribute that opts out of any global (or does nothing if no global i.e. marker not handler attribute)
    //Respects different global choices i.e. throw, what to capture, context
    //allow a lambda for the context, to override, and pass in a default of typeof() ????
 
    
    public class PipelineGlobalInboxTests
    {
        private readonly PipelineBuilder<MyCommand> _chainBuilder;
        private IHandleRequests<MyCommand> _chainOfResponsibility;
        private readonly RequestContext _requestContext;
        private readonly InboxConfiguration _inboxConfiguration;
        private IAmAnInbox _inbox;


        public PipelineGlobalInboxTests()
        {
            _inbox = new InMemoryInbox();
            
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyCommandHandler>();
            
            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);

            container.Register<IHandleRequests<MyCommand>, MyCommandHandler>();
            container.Register<IAmAnInbox>(_inbox);
 
            _requestContext = new RequestContext();
            
            _inboxConfiguration = new InboxConfiguration();

            _chainBuilder = new PipelineBuilder<MyCommand>(registry, handlerFactory, _inboxConfiguration);
            
        }

        [Fact]
        public void When_Building_A_Pipeline_With_Global_inbox()
        {
            //act
            _chainOfResponsibility = _chainBuilder.Build(_requestContext).First();
            
            //assert
            var tracer = TracePipeline(_chainOfResponsibility);
            tracer.ToString().Should().Contain("UseInboxHandler");

        }
        
        private PipelineTracer TracePipeline(IHandleRequests<MyCommand> firstInPipeline)
        {
            var pipelineTracer = new PipelineTracer();
            firstInPipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }
 
    }
}
