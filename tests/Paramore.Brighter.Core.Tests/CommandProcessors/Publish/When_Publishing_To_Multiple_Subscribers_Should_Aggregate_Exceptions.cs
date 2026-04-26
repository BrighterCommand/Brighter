using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Extensions.DependencyInjection;
using Polly.Registry;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Publish
{
    public class PublishingToMultipleSubscribersTests
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly IDictionary<string, string> _receivedMessages = new ConcurrentDictionary<string, string>();
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
            container.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });
            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());
            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
        }

        [Test]
        public async Task When_Publishing_To_Multiple_Subscribers_Should_Aggregate_Exceptions()
        {
            _exception = Catch.Exception(() => _commandProcessor.Publish(_myEvent));
            //Should throw an aggregate exception
            await Assert.That(_exception).IsTypeOf<AggregateException>();
            //Should have an inner exception from the handler
            await Assert.That(((AggregateException)_exception).InnerException).IsTypeOf<InvalidOperationException>();
            //Should publish the command to the first event handler
            await Assert.That(_receivedMessages).Contains(new KeyValuePair<string, string>(nameof(MyEventHandler), _myEvent.Id));
            //Should publish the command to the second event handler
            await Assert.That(_receivedMessages).Contains(new KeyValuePair<string, string>(nameof(MyOtherEventHandler), _myEvent.Id));
        }
    }
}