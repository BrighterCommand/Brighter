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

public class CommandProcessorMissingTypeResiliencePipelineFromRegistryTests : IDisposable
{
    private readonly CommandProcessor _commandProcessor;
    private readonly MyCommand _myCommand = new MyCommand();
    private Exception? _exception;

    public CommandProcessorMissingTypeResiliencePipelineFromRegistryTests()
    {
        var registry = new SubscriberRegistry();
        registry.Register<MyCommand, MyDoesNotFailTypeResiliencePipelineHandler>();

        var container = new ServiceCollection();
        container.AddTransient<MyDoesNotFailTypeResiliencePipelineHandler>();
        container.AddTransient<ResilienceExceptionPolicyHandler<MyCommand>>();
        container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});


        var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());
            
        MyDoesNotFailTypeResiliencePipelineHandler.ReceivedCommand = false;

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
        //Should give the name of the missing policy
        Assert.Contains("Unable to find a generic resilience pipeline of 'MyCommand' associated with the key 'MyDivideByZeroPolicy'. Please ensure that either the generic resilience pipeline or the generic builder is registered.", innerException.Message);
    }

    public void Dispose()
    {
        CommandProcessor.ClearServiceBus();
    }
}
