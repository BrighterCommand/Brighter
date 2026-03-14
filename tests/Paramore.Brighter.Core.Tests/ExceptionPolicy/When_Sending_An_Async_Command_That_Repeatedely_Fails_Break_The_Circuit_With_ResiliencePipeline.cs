using System;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.ExceptionPolicy.TestDoubles;
using Xunit;
using Paramore.Brighter.Policies.Handlers;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.Core.Tests.ExceptionPolicy;

public class CommandProcessorWithCircuitBreakerAndResiliencePipelineAsyncTests
{
    private readonly CommandProcessor _commandProcessor;
    private readonly MyCommand _myCommand = new MyCommand();
    private Exception _thirdException;
    private Exception _firstException;
    private Exception _secondException;

    public CommandProcessorWithCircuitBreakerAndResiliencePipelineAsyncTests()
    {
        var registry = new SubscriberRegistry();
        registry.RegisterAsync<MyCommand, MyFailsWithDivideByZeroWithResiliencePipelineHandlerAsync>();

        var container = new ServiceCollection();
        container.AddSingleton<MyFailsWithDivideByZeroWithResiliencePipelineHandlerAsync>();
        container.AddSingleton<ResilienceExceptionPolicyHandlerAsync<MyCommand>>();
        container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});

        var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

        var resiliencePipeline = new ResiliencePipelineRegistry<string>();
        resiliencePipeline.TryAddBuilder("MyDivideByZeroPolicy",
            (builder, _) => builder
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                {
                    MinimumThroughput = 2,
                    BreakDuration = TimeSpan.FromMinutes(1)
                }));

        MyFailsWithDivideByZeroWithResiliencePipelineHandlerAsync.ReceivedCommand = false;

        _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(),
            new PolicyRegistry(), resiliencePipeline, new InMemorySchedulerFactory());
    }

    [Fact]
    public async Task When_Sending_An_Async_Command_That_Repeatedly_Fails_Break_The_Circuit()
    {
        //First two should be caught, and increment the count
        _firstException = await Catch.ExceptionAsync(() => _commandProcessor.SendAsync(_myCommand));
        _secondException = await Catch.ExceptionAsync(() => _commandProcessor.SendAsync(_myCommand));
        //this one should tell us that the circuit is broken
        _thirdException = await Catch.ExceptionAsync(() => _commandProcessor.SendAsync(_myCommand));

        // Should send the command to the command handler
        Assert.True(MyFailsWithDivideByZeroWithResiliencePipelineHandlerAsync.ShouldReceive(_myCommand));
        // Should bubble up the first exception
        Assert.IsType<DivideByZeroException>(_firstException);
        // Should bubble up the second exception
        Assert.IsType<DivideByZeroException>(_secondException);
        // Should break the circuit after two fails
        Assert.IsType<BrokenCircuitException>(_thirdException);
    }
}
