using System;
using System.Collections.Generic;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.ExceptionPolicy.TestDoubles;
using Xunit;
using Paramore.Brighter.Policies.Handlers;
using Polly.Registry;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.Core.Tests.ExceptionPolicy;

public class CommandProcessorMissingResiliencePipelineFromRegistryTests : IDisposable
{
    private readonly CommandProcessor _commandProcessor;
    private readonly MyCommand _myCommand = new MyCommand();
    private Exception? _exception;

    public CommandProcessorMissingResiliencePipelineFromRegistryTests()
    {
        var registry = new SubscriberRegistry();
        registry.Register<MyCommand, MyDoesNotFailResiliencePipelineHandler>();

        var container = new ServiceCollection();
        container.AddTransient<MyDoesNotFailResiliencePipelineHandler>();
        container.AddTransient<ResilienceExceptionPolicyHandler<MyCommand>>();
        container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});


        var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());
            
        MyDoesNotFailResiliencePipelineHandler.ReceivedCommand = false;

        _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
    }

    //We have to catch the final exception that bubbles out after retry
    [Fact]
    public void When_Sending_A_Command_And_The_Policy_Is_Not_In_The_Registry()
    {
        _exception = Catch.Exception(() => _commandProcessor.Send(_myCommand));

        //Should throw an exception
        Assert.IsType<ConfigurationException>(_exception);
        var innerException = _exception.InnerException;
        Assert.NotNull(innerException);
        Assert.IsType<KeyNotFoundException>(innerException);
    }

    public void Dispose()
    {
        CommandProcessor.ClearServiceBus();
    }
}
