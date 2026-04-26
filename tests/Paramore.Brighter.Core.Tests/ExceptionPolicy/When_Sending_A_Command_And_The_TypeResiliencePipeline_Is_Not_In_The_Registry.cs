using System;
using System.Collections.Generic;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.ExceptionPolicy.TestDoubles;
using Paramore.Brighter.Policies.Handlers;
using Polly.Registry;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.Core.Tests.ExceptionPolicy;
public class CommandProcessorMissingTypeResiliencePipelineFromRegistryTests
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
        container.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });
        var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());
        MyDoesNotFailTypeResiliencePipelineHandler.ReceivedCommand = false;
        _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
    }

    //We have to catch the final exception that bubbles out after retry
    [Test]
    public async Task When_Sending_A_Command_And_The_Policy_Is_Not_In_The_Registry()
    {
        _exception = Catch.Exception(() => _commandProcessor.Send(_myCommand));
        //Should throw an exception
        await Assert.That(_exception).IsTypeOf<ConfigurationException>();
        var innerException = _exception.InnerException;
        await Assert.That(innerException).IsNotNull();
        await Assert.That(innerException).IsTypeOf<KeyNotFoundException>();
        //Should give the name of the missing policy
        await Assert.That(innerException.Message).Contains("Unable to find a generic resilience pipeline of 'MyCommand' associated with the key 'MyDivideByZeroPolicy'. Please ensure that either the generic resilience pipeline or the generic builder is registered.");
    }
}