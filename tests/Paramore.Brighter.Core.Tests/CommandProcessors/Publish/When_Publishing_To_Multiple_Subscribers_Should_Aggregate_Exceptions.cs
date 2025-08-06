using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Extensions.DependencyInjection;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Publish
{
    [Collection("CommandProcessor")]
    public class PublishingToMultipleSubscribersTests: IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly IDictionary<string, string> _receivedMessages = new Dictionary<string, string>();
        private readonly MyEvent _myEvent = new MyEvent();
        private Exception _exception;

        public PublishingToMultipleSubscribersTests()
        {
            var registry = new SubscriberRegistry();
            registry.Register<MyEvent, MyEventHandler>();
            registry.Register<MyEvent, MyOtherEventHandler>();
            registry.Register<MyEvent, MyThrowingEventHandler>();

            var container = new ServiceCollection();
            container.AddTransient<MyEventHandler>();
            container.AddTransient<MyOtherEventHandler>();
            container.AddTransient<MyThrowingEventHandler>();
            container.AddSingleton(_receivedMessages);
            container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});
 
            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());


            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
            PipelineBuilder<MyCommand>.ClearPipelineCache();
        }

        [Fact]
        public void When_Publishing_To_Multiple_Subscribers_Should_Aggregate_Exceptions()
        {
            _exception = Catch.Exception(() => _commandProcessor.Publish(_myEvent));

            //Should throw an aggregate exception
            Assert.IsType<AggregateException>(_exception);
            //Should have an inner exception from the handler
            Assert.IsType<InvalidOperationException>(((AggregateException)_exception).InnerException);
            //Should publish the command to the first event handler
            Assert.Contains(new KeyValuePair<string, string>(nameof(MyEventHandler), _myEvent.Id), _receivedMessages);
            //Should publish the command to the second event handler
            Assert.Contains(new KeyValuePair<string, string>(nameof(MyOtherEventHandler), _myEvent.Id), _receivedMessages);

        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
