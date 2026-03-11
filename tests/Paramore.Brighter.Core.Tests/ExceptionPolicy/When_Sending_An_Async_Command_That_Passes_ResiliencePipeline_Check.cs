using System;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.ExceptionPolicy.TestDoubles;
using Xunit;
using Paramore.Brighter.Policies.Handlers;
using Polly;
using Polly.Registry;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Polly.Retry;

namespace Paramore.Brighter.Core.Tests.ExceptionPolicy;

public class CommandProcessorWithExceptionResiliencePipelineNothingThrowAsyncTests
{
    private readonly CommandProcessor _commandProcessor;
    private readonly MyCommand _myCommand = new MyCommand();
    private int _retryCount;

    public CommandProcessorWithExceptionResiliencePipelineNothingThrowAsyncTests()
    {
        //Arrange
        var registry = new SubscriberRegistry();
        registry.RegisterAsync<MyCommand, MyDoesNotFailResiliencePipelineHandlerAsync>();

        var container = new ServiceCollection();
        container.AddTransient<MyDoesNotFailResiliencePipelineHandlerAsync>();
        container.AddTransient<ResilienceExceptionPolicyHandlerAsync<MyCommand>>();
        container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});

        var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

        var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>();
        resiliencePipelineRegistry.TryAddBuilder("MyDivideByZeroPolicy",
            (builder, _) => builder.AddRetry(new RetryStrategyOptions
            {
                BackoffType = DelayBackoffType.Linear,
                Delay = TimeSpan.FromSeconds(1),
                OnRetry = _ =>
                {
                    _retryCount++;
                    return new ValueTask();
                }
            }));

        MyDoesNotFailResiliencePipelineHandlerAsync.ReceivedCommand = false;

        _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), resiliencePipelineRegistry, new InMemorySchedulerFactory());
    }

    [Fact]
    public async Task When_Sending_An_Async_Command_That_Passes_ResiliencePipeline_Check()
    {
        //Act
        await _commandProcessor.SendAsync(_myCommand);

        //Assert
        Assert.True(MyDoesNotFailResiliencePipelineHandlerAsync.Shouldreceive(_myCommand));
        Assert.Equal(0, _retryCount);
    }
}
