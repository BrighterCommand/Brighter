using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Inbox.Handlers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline
{
    [Collection("CommandProcessor")]
    public class PipelineGlobalInboxContextTests : IDisposable
    {
        private const string CONTEXT_KEY = "TestHandlerNameOverride";
        private readonly PipelineBuilder<MyCommand> _chainBuilder;
        private IEnumerable<IHandleRequests<MyCommand>> _chainOfResponsibility;
        private readonly RequestContext _requestContext;
        private readonly IAmAnInboxSync _inbox;
        private SubscriberRegistry _subscriberRegistry;


        public PipelineGlobalInboxContextTests()
        {
            _inbox = new InMemoryInbox(new FakeTimeProvider());
            
            _subscriberRegistry = new SubscriberRegistry();
            _subscriberRegistry.Register<MyCommand, MyGlobalInboxCommandHandler>();
            
            var container = new ServiceCollection();
            container.AddTransient<MyGlobalInboxCommandHandler>();
            container.AddSingleton(_inbox);
            container.AddTransient<UseInboxHandler<MyCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});
 
            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());
            
            _requestContext = new RequestContext();
            
            InboxConfiguration inboxConfiguration = new(
                scope: InboxScope.All, 
                context: (handlerType) => CONTEXT_KEY);

            _chainBuilder = new PipelineBuilder<MyCommand>((IAmAHandlerFactorySync)handlerFactory, inboxConfiguration);
            PipelineBuilder<MyCommand>.ClearPipelineCache();
            
        }

        [Fact]
        public void When_Building_A_Pipeline_With_Global_inbox()
        {
            //act
            var observers = _subscriberRegistry.Get<MyCommand>();
            _chainOfResponsibility = observers.Select(o => _chainBuilder.Build(o, _requestContext));
            
            var firstHandler = _chainOfResponsibility.First();
            var myCommmand = new MyCommand();
            firstHandler.Handle(myCommmand);

            //assert
            var exists = _inbox.Exists<MyCommand>(myCommmand.Id, CONTEXT_KEY, 500);
            exists.Should().BeTrue();
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
