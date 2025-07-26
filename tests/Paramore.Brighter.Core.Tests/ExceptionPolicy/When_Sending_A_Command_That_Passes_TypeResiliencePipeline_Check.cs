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

public class CommandProcessorWithExceptionTypeResiliencePipelineNothingThrowTests : IDisposable
{
    private readonly CommandProcessor _commandProcessor;
    private readonly MyCommand _myCommand = new MyCommand();
    private int _retryCount;

    public CommandProcessorWithExceptionTypeResiliencePipelineNothingThrowTests()
    {
        var registry = new SubscriberRegistry();
        registry.Register<MyCommand, MyDoesNotFailTypeResiliencePipelineHandler>();

        var container = new ServiceCollection();
        container.AddTransient<MyDoesNotFailTypeResiliencePipelineHandler>();
        container.AddTransient<ResilienceExceptionPolicyHandler<MyCommand>>();
        container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});

        var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());
            
        var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>();
        resiliencePipelineRegistry.TryAddBuilder<MyCommand>("MyDivideByZeroPolicy", 
            (builder, _) => builder.AddRetry(new RetryStrategyOptions<MyCommand>
            {
                BackoffType = DelayBackoffType.Linear,
                Delay = TimeSpan.FromSeconds(1),
                OnRetry = _ =>
                {
                    _retryCount++;
                    return new ValueTask();
                }
            }));

        MyDoesNotFailTypeResiliencePipelineHandler.ReceivedCommand = false;

        _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), resiliencePipelineRegistry, new InMemorySchedulerFactory());
    }

    //We have to catch the final exception that bubbles out after retry
    [Fact]
    public void When_Sending_A_Command_That_Passes_Policy_Check()
    {
        _commandProcessor.Send(_myCommand);

        // Should send the command to the command handler
        Assert.True(MyDoesNotFailTypeResiliencePipelineHandler.Shouldreceive(_myCommand));
        // Should not retry
        Assert.Equal(0, _retryCount);
    }

    public void Dispose()
    {
        CommandProcessor.ClearServiceBus();
    }
}
