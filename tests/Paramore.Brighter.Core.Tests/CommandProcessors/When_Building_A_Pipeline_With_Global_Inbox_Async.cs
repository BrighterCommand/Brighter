using System;
using System.Collections.Generic;
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
    //TODO:
    //Respects different global choices i.e. throw, what to capture, context
    //allow a lambda for the context, to override, and pass in a default of typeof() ????
 
    
    public class PipelineGlobalInboxTestsAsync
    {
        private readonly PipelineBuilder<MyCommand> _chainBuilder;
        private AsyncPipelines<MyCommand> _chainOfResponsibility;
        private readonly RequestContext _requestContext;
        private readonly InboxConfiguration _inboxConfiguration;
        private IAmAnInboxAsync _inbox;


        public PipelineGlobalInboxTestsAsync()
        {
            _inbox = new InMemoryInbox();
            var handler = new MyCommandHandlerAsync(new Dictionary<string, Guid>());
           
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyCommandHandlerAsync>();
            
            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactoryAsync(container);

            container.Register<MyCommandHandlerAsync>(handler);
            container.Register<IAmAnInboxAsync>(_inbox);
 
            _requestContext = new RequestContext();
            
            _inboxConfiguration = new InboxConfiguration();

            _chainBuilder = new PipelineBuilder<MyCommand>(registry, handlerFactory, _inboxConfiguration);
            
        }

        [Fact]
        public void When_Building_A_Pipeline_With_Global_Inbox()
        {
           //act
            _chainOfResponsibility = _chainBuilder.BuildAsync(_requestContext, false);
            
            //assert
            var tracer = TracePipeline(_chainOfResponsibility.First());
            tracer.ToString().Should().Contain("UseInboxHandlerAsync");

        }
        
        private PipelineTracer TracePipeline(IHandleRequestsAsync<MyCommand> firstInPipeline)
        {
            var pipelineTracer = new PipelineTracer();
            firstInPipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }
 
    }
}
