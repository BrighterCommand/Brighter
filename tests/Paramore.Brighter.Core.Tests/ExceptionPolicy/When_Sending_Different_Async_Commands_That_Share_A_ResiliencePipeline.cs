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

public class CommandProcessorWithSharedResiliencePipelineAsyncTests
{
    private readonly CommandProcessor _commandProcessor;
    private readonly MyCommand _myCommand = new MyCommand();
    private readonly MyOtherCommand _myOtherCommand = new MyOtherCommand();

    public CommandProcessorWithSharedResiliencePipelineAsyncTests()
    {
        //Arrange
        var registry = new SubscriberRegistry();
        registry.RegisterAsync<MyCommand, MyCommandHandlerWithSharedPipelineAsync>();
        registry.RegisterAsync<MyOtherCommand, MyOtherCommandHandlerWithSharedPipelineAsync>();

        var container = new ServiceCollection();
        container.AddTransient<MyCommandHandlerWithSharedPipelineAsync>();
        container.AddTransient<MyOtherCommandHandlerWithSharedPipelineAsync>();
        container.AddTransient<ResilienceExceptionPolicyHandlerAsync<MyCommand>>();
        container.AddTransient<ResilienceExceptionPolicyHandlerAsync<MyOtherCommand>>();
        container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});

        var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

        var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>();
        resiliencePipelineRegistry.TryAddBuilder("SharedRetryPolicy",
            (builder, _) => builder.AddRetry(new RetryStrategyOptions
            {
                BackoffType = DelayBackoffType.Linear,
                Delay = TimeSpan.FromSeconds(1),
                MaxRetryAttempts = 1
            }));

        MyCommandHandlerWithSharedPipelineAsync.ReceivedCommand = false;
        MyOtherCommandHandlerWithSharedPipelineAsync.ReceivedCommand = false;

        _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), resiliencePipelineRegistry, new InMemorySchedulerFactory());
    }

    [Fact]
    public async Task When_Sending_Different_Async_Commands_That_Share_A_ResiliencePipeline()
    {
        //Act
        await _commandProcessor.SendAsync(_myCommand);
        await _commandProcessor.SendAsync(_myOtherCommand);

        //Assert
        Assert.True(MyCommandHandlerWithSharedPipelineAsync.ReceivedCommand);
        Assert.True(MyOtherCommandHandlerWithSharedPipelineAsync.ReceivedCommand);
    }
}
