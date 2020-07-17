using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Inbox;
using Paramore.Brighter.Inbox.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;
using Paramore.Brighter.Inbox.Handlers;

namespace Paramore.Brighter.Core.Tests.CommandProcessors
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
            
            var container = new ServiceCollection();
            container.AddTransient<MyCommandInboxedHandlerAsync>();
            container.AddSingleton<IAmAnInboxAsync>((IAmAnInboxAsync)_inbox);
            container.AddTransient<UseInboxHandlerAsync<MyCommand>>();

            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());


 
            _requestContext = new RequestContext();
            
            _inboxConfiguration = new InboxConfiguration(
                scope: InboxScope.All, 
                onceOnly: true, 
                actionOnExists: OnceOnlyAction.Throw);

            _chainBuilder = new PipelineBuilder<MyCommand>(registry, (IAmAHandlerFactoryAsync)handlerFactory, _inboxConfiguration);
            
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
