using System;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.ExceptionPolicy.TestDoubles;
using Paramore.Brighter.Policies.Handlers;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.Core.Tests.ExceptionPolicy;
public class CommandProcessorWithCircuitBreakerAndResiliencePipelineTests
{
    private readonly CommandProcessor _commandProcessor;
    private readonly MyCommand _myCommand = new MyCommand();
    private Exception _thirdException;
    private Exception _firstException;
    private Exception _secondException;
    public CommandProcessorWithCircuitBreakerAndResiliencePipelineTests()
    {
        var registry = new SubscriberRegistry();
        registry.Register<MyCommand, MyFailsWithDivideByZeroWithResiliencePipelineHandler>();
        var container = new ServiceCollection();
        container.AddSingleton<MyFailsWithDivideByZeroWithResiliencePipelineHandler>();
        container.AddSingleton<ResilienceExceptionPolicyHandler<MyCommand>>();
        container.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });
        var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());
        var resiliencePipeline = new ResiliencePipelineRegistry<string>();
        resiliencePipeline.TryAddBuilder("MyDivideByZeroPolicy", (builder, _) => builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions { MinimumThroughput = 2, BreakDuration = TimeSpan.FromMinutes(1) }));
        MyFailsWithDivideByZeroWithResiliencePipelineHandler.ReceivedCommand = false;
        _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), resiliencePipeline, new InMemorySchedulerFactory());
    }

    //We have to catch the final exception that bubbles out after retry
    [Test]
    public async Task When_Sending_A_Command_That_Repeatedly_Fails_Break_The_Circuit()
    {
        //First two should be caught, and increment the count
        _firstException = Catch.Exception(() => _commandProcessor.Send(_myCommand));
        _secondException = Catch.Exception(() => _commandProcessor.Send(_myCommand));
        //this one should tell us that the circuit is broken
        _thirdException = Catch.Exception(() => _commandProcessor.Send(_myCommand));
        // Should send the command to the command handler
        await Assert.That(MyFailsWithDivideByZeroWithResiliencePipelineHandler.ShouldReceive(_myCommand)).IsTrue();
        // Should bubble up the first exception
        await Assert.That(_firstException).IsTypeOf<DivideByZeroException>();
        // Should bubble up the second exception
        await Assert.That(_secondException).IsTypeOf<DivideByZeroException>();
        // Should break the circuit after two fails
        await Assert.That(_thirdException).IsTypeOf<BrokenCircuitException>();
    }
}