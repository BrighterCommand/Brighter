using System.Linq;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
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
            
            var container = new ServiceCollection();
            container.AddTransient<MyNoInboxCommandHandlerAsync>();
            container.AddSingleton<IAmAnInbox>(_inbox);

            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

            _requestContext = new RequestContext();
            
            _inboxConfiguration = new InboxConfiguration();

            _chainBuilder = new PipelineBuilder<MyCommand>(registry, (IAmAHandlerFactoryAsync)handlerFactory, _inboxConfiguration);
            
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
