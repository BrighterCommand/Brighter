using System;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Extensions.DependencyInjection;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline;

[Collection("CommandProcessor")]
public class CommandProcessorNoHandlerFactoriesTests : IDisposable
{
    private Exception _exception;
    
    [Fact]
    public void When_There_Are_No_Command_Handlers_Async()
    {
        var container = new ServiceCollection();
        container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});

        _exception = Catch.Exception(() => new CommandProcessor(
            new SubscriberRegistry(),
            null,
            new InMemoryRequestContextFactory(),
            new PolicyRegistry(),
            new InMemorySchedulerFactory()));

        //_should_fail_because_no_handler_factories_have_been_set
        _exception.Should().BeOfType<ArgumentException>();
        
        //_should_have_an_error_message_that_tells_you_why
        _exception.Should().NotBeNull();
        _exception.Message.Should().Contain("No HandlerFactory has been set - either an instance of IAmAHandlerFactorySync or IAmAHandlerFactoryAsync needs to be set");
    }    
    
    [Fact]
    public void When_using_IAmAHandlerFactory()
    {
        var container = new ServiceCollection();
        container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});

        _exception = Catch.Exception(() => new CommandProcessor(
            new SubscriberRegistry(),
            new DummyHandlerFactory(),
            new InMemoryRequestContextFactory(),
            new PolicyRegistry(),
            new InMemorySchedulerFactory()));

        //_should_fail_because_no_handler_factories_have_been_set
        _exception.Should().BeOfType<ArgumentException>();
        
        //_should_have_an_error_message_that_tells_you_why
        _exception.Should().NotBeNull();
        _exception.Message.Should().Contain("No HandlerFactory has been set - either an instance of IAmAHandlerFactorySync or IAmAHandlerFactoryAsync needs to be set");
    }
    
    public void Dispose()
    {
        CommandProcessor.ClearServiceBus();
    }

    sealed class DummyHandlerFactory : IAmAHandlerFactory
    {
        
    }
}
