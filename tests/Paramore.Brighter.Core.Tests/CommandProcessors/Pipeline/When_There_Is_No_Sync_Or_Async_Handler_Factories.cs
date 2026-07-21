using System;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Extensions.DependencyInjection;
using Polly.Registry;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline;
public class CommandProcessorNoHandlerFactoriesTests
{
    private Exception _exception;
    [Test]
    public async Task When_There_Are_No_Command_Handlers_Async()
    {
        var container = new ServiceCollection();
        container.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });
        _exception = Catch.Exception(() => new CommandProcessor(new SubscriberRegistry(), null, new InMemoryRequestContextFactory(), new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory()));
        await Assert.That(_exception).IsTypeOf<ArgumentException>();
        //_should_have_an_error_message_that_tells_you_why
        await Assert.That(_exception).IsNotNull();
        await Assert.That(_exception.Message).Contains("No HandlerFactory has been set - either an instance of IAmAHandlerFactorySync or IAmAHandlerFactoryAsync needs to be set");
    }

    [Test]
    public async Task When_using_IAmAHandlerFactory()
    {
        var container = new ServiceCollection();
        container.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });
        _exception = Catch.Exception(() => new CommandProcessor(new SubscriberRegistry(), new DummyHandlerFactory(), new InMemoryRequestContextFactory(), new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory()));
        //_should_fail_because_no_handler_factories_have_been_set
        await Assert.That(_exception).IsTypeOf<ArgumentException>();
        //_should_have_an_error_message_that_tells_you_why
        await Assert.That(_exception).IsNotNull();
        await Assert.That(_exception.Message).Contains("No HandlerFactory has been set - either an instance of IAmAHandlerFactorySync or IAmAHandlerFactoryAsync needs to be set");
    }

    sealed class DummyHandlerFactory : Paramore.Brighter.IAmAHandlerFactory;
}