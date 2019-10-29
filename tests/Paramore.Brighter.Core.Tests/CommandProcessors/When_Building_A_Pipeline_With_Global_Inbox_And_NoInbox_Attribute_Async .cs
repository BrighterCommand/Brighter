using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Inbox.Handlers;
using Paramore.Brighter.Core.Tests.FeatureSwitch.TestDoubles;
using TinyIoC;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors
{
    public class PipelineGlobalInboxNoInboxAttributeAsyncTests
    {
        private readonly PipelineBuilder<MyCommand> _chainBuilder;
        private AsyncPipelines<MyCommand> _chainOfResponsibility;
        private readonly RequestContext _requestContext;
        private readonly InboxConfiguration _inboxConfiguration;
        private IAmAnInbox _inbox;


        public PipelineGlobalInboxNoInboxAttributeAsyncTests()
        {
            _inbox = new InMemoryInbox();
            
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyNoInboxCommandHandlerAsync>();
            
            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactoryAsync(container);

            container.Register<IHandleRequestsAsync<MyCommand>, MyNoInboxCommandHandlerAsync>();
            container.Register<IAmAnInbox>(_inbox);
 
            _requestContext = new RequestContext();
            
            _inboxConfiguration = new InboxConfiguration();

            _chainBuilder = new PipelineBuilder<MyCommand>(registry, handlerFactory, _inboxConfiguration);
            
        }

        [Fact]
        public void When_Building_A_Pipeline_With_Global_Inbox_Async()
        {
            //act
            _chainOfResponsibility = _chainBuilder.BuildAsync(_requestContext, false);
            
            //assert
            var tracer = TracePipelineAsync(_chainOfResponsibility.First());
            tracer.ToString().Should().NotContain("UseInboxHandlerAsync");

        }
        
        private PipelineTracer TracePipelineAsync(IHandleRequestsAsync<MyCommand> firstInPipeline)
        {
            var pipelineTracer = new PipelineTracer();
            firstInPipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }
 
    }
}
