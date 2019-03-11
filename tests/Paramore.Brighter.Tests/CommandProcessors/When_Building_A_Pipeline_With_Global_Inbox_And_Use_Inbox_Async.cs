using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Inbox;
using Paramore.Brighter.Inbox.Exceptions;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using TinyIoC;
using Xunit;

namespace Paramore.Brighter.Tests.CommandProcessors
{
    public class PipelineGlobalInboxWhenUseInboxAsyncTests
    {
        private readonly PipelineBuilder<MyCommand> _chainBuilder;
        private AsyncPipelines<MyCommand> _chainOfResponsibility;
        private readonly RequestContext _requestContext;
        private readonly InboxConfiguration _inboxConfiguration;
        private IAmAnInbox _inbox;


        public PipelineGlobalInboxWhenUseInboxAsyncTests()
        {
            _inbox = new InMemoryInbox();
            
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyCommandInboxedHandlerAsync>();
            
            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactoryAsync(container);

            container.Register<IHandleRequestsAsync<MyCommand>, MyCommandInboxedHandlerAsync>();
            container.Register<IAmAnInboxAsync>((IAmAnInboxAsync)_inbox);
 
            _requestContext = new RequestContext();
            
            _inboxConfiguration = new InboxConfiguration(
                scope: InboxScope.All, 
                onceOnly: true, 
                actionOnExists: OnceOnlyAction.Throw);

            _chainBuilder = new PipelineBuilder<MyCommand>(registry, handlerFactory, _inboxConfiguration);
            
        }

        [Fact]
        public async Task When_Building_A_Pipeline_With_Global_Inbox()
        {
            // Settings for UseInbox on MyCommandInboxedHandler
            // [UseInbox(step:0, contextKey: typeof(MyCommandInboxedHandler), onceOnly: false)]
            // Settings for InboxConfifguration as above
            // _inboxConfiguration = new InboxConfiguration(InboxScope.All, context: true, onceOnly: true);
            // so global will not allow repeated requests ans calls, but local should override this and allow

            
            //act
            _chainOfResponsibility = _chainBuilder.BuildAsync(_requestContext, false);

            var chain = _chainOfResponsibility.First();
            var myCommand = new MyCommand();
            
            //First pass not impacted by UseInbox Handler
            await chain.HandleAsync(myCommand);

            bool noException = true;
            try
            {
                await chain.HandleAsync(myCommand);
            }
            catch (OnceOnlyException)
            {
                noException = false;
            }

            //assert
            noException.Should().BeTrue();

        }
        
        private PipelineTracer TracePipeline(IHandleRequests<MyCommand> firstInPipeline)
        {
            var pipelineTracer = new PipelineTracer();
            firstInPipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }
 
    }
}
