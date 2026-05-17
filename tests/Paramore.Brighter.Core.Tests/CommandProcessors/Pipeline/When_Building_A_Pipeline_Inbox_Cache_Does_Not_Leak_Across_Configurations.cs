using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Inbox.Handlers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline;

public class When_Building_A_Pipeline_Inbox_Cache_Does_Not_Leak_Across_Configurations
{
    [Fact]
    public void When_Building_A_Pipeline_With_Inbox_Then_Without_Inbox_No_Leakage()
    {
        PipelineBuilder<MyCommand>.ClearPipelineCache();

        // Step 1: Build a pipeline WITH inbox configuration — populates the static cache
        var inboxRegistry = new SubscriberRegistry();
        inboxRegistry.Register<MyCommand, MyCommandHandler>();

        var inboxContainer = new ServiceCollection();
        inboxContainer.AddTransient<MyCommandHandler>(_ => new MyCommandHandler(new Dictionary<string, string>()));
        inboxContainer.AddSingleton<IAmAnInboxSync>(new InMemoryInbox(new FakeTimeProvider()));
        inboxContainer.AddTransient<UseInboxHandler<MyCommand>>();
        inboxContainer.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });

        var inboxHandlerFactory = new ServiceProviderHandlerFactory(inboxContainer.BuildServiceProvider());
        var inboxBuilder = new PipelineBuilder<MyCommand>(
            inboxRegistry, (IAmAHandlerFactorySync)inboxHandlerFactory, new InboxConfiguration());

        var withInbox = inboxBuilder.Build(new MyCommand(), new RequestContext());
        var withInboxTrace = TracePipeline(withInbox.First());
        Assert.Contains("UseInboxHandler`", withInboxTrace);

        // Step 2: Build a pipeline WITHOUT inbox configuration — same handler type, sharing the static cache
        var noInboxRegistry = new SubscriberRegistry();
        noInboxRegistry.Register<MyCommand, MyCommandHandler>();

        var noInboxContainer = new ServiceCollection();
        noInboxContainer.AddTransient<MyCommandHandler>(_ => new MyCommandHandler(new Dictionary<string, string>()));
        noInboxContainer.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });

        var noInboxHandlerFactory = new ServiceProviderHandlerFactory(noInboxContainer.BuildServiceProvider());
        var noInboxBuilder = new PipelineBuilder<MyCommand>(
            noInboxRegistry, (IAmAHandlerFactorySync)noInboxHandlerFactory);

        var withoutInbox = noInboxBuilder.Build(new MyCommand(), new RequestContext());
        var withoutInboxTrace = TracePipeline(withoutInbox.First());
        Assert.DoesNotContain("UseInboxHandler`", withoutInboxTrace);
    }

    [Fact]
    public void When_Building_A_Pipeline_Without_Inbox_Then_With_Inbox_Still_Gets_Inbox()
    {
        PipelineBuilder<MyCommand>.ClearPipelineCache();

        // Step 1: Build a pipeline WITHOUT inbox configuration — primes the static cache
        var noInboxRegistry = new SubscriberRegistry();
        noInboxRegistry.Register<MyCommand, MyCommandHandler>();

        var noInboxContainer = new ServiceCollection();
        noInboxContainer.AddTransient<MyCommandHandler>(_ => new MyCommandHandler(new Dictionary<string, string>()));
        noInboxContainer.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });

        var noInboxHandlerFactory = new ServiceProviderHandlerFactory(noInboxContainer.BuildServiceProvider());
        var noInboxBuilder = new PipelineBuilder<MyCommand>(
            noInboxRegistry, (IAmAHandlerFactorySync)noInboxHandlerFactory);

        var withoutInbox = noInboxBuilder.Build(new MyCommand(), new RequestContext());
        var withoutInboxTrace = TracePipeline(withoutInbox.First());
        Assert.DoesNotContain("UseInboxHandler`", withoutInboxTrace);

        // Step 2: Build a pipeline WITH inbox configuration — cache already primed without inbox
        var inboxRegistry = new SubscriberRegistry();
        inboxRegistry.Register<MyCommand, MyCommandHandler>();

        var inboxContainer = new ServiceCollection();
        inboxContainer.AddTransient<MyCommandHandler>(_ => new MyCommandHandler(new Dictionary<string, string>()));
        inboxContainer.AddSingleton<IAmAnInboxSync>(new InMemoryInbox(new FakeTimeProvider()));
        inboxContainer.AddTransient<UseInboxHandler<MyCommand>>();
        inboxContainer.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });

        var inboxHandlerFactory = new ServiceProviderHandlerFactory(inboxContainer.BuildServiceProvider());
        var inboxBuilder = new PipelineBuilder<MyCommand>(
            inboxRegistry, (IAmAHandlerFactorySync)inboxHandlerFactory, new InboxConfiguration());

        var withInbox = inboxBuilder.Build(new MyCommand(), new RequestContext());
        var withInboxTrace = TracePipeline(withInbox.First());
        Assert.Contains("UseInboxHandler`", withInboxTrace);
    }

    [Fact]
    public void When_Building_An_Async_Pipeline_With_Inbox_Then_Without_Inbox_No_Leakage()
    {
        PipelineBuilder<MyCommand>.ClearPipelineCache();

        // Step 1: Build an async pipeline WITH inbox configuration — populates the static cache
        var inboxRegistry = new SubscriberRegistry();
        inboxRegistry.RegisterAsync<MyCommand, MyCommandHandlerAsync>();

        var inboxContainer = new ServiceCollection();
        inboxContainer.AddSingleton(new MyCommandHandlerAsync(new Dictionary<string, string>()));
        inboxContainer.AddSingleton<IAmAnInboxAsync>(new InMemoryInbox(new FakeTimeProvider()));
        inboxContainer.AddTransient<UseInboxHandlerAsync<MyCommand>>();
        inboxContainer.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });

        var inboxHandlerFactory = new ServiceProviderHandlerFactory(inboxContainer.BuildServiceProvider());
        var inboxBuilder = new PipelineBuilder<MyCommand>(
            inboxRegistry, (IAmAHandlerFactoryAsync)inboxHandlerFactory, new InboxConfiguration());

        var withInbox = inboxBuilder.BuildAsync(new MyCommand(), new RequestContext(), false);
        var withInboxTrace = TraceAsyncPipeline(withInbox.First());
        Assert.Contains("UseInboxHandlerAsync`", withInboxTrace);

        // Step 2: Build an async pipeline WITHOUT inbox configuration — same handler type, sharing the static cache
        var noInboxRegistry = new SubscriberRegistry();
        noInboxRegistry.RegisterAsync<MyCommand, MyCommandHandlerAsync>();

        var noInboxContainer = new ServiceCollection();
        noInboxContainer.AddSingleton(new MyCommandHandlerAsync(new Dictionary<string, string>()));
        noInboxContainer.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });

        var noInboxHandlerFactory = new ServiceProviderHandlerFactory(noInboxContainer.BuildServiceProvider());
        var noInboxBuilder = new PipelineBuilder<MyCommand>(
            noInboxRegistry, (IAmAHandlerFactoryAsync)noInboxHandlerFactory);

        var withoutInbox = noInboxBuilder.BuildAsync(new MyCommand(), new RequestContext(), false);
        var withoutInboxTrace = TraceAsyncPipeline(withoutInbox.First());
        Assert.DoesNotContain("UseInboxHandlerAsync`", withoutInboxTrace);
    }

    [Fact]
    public void When_Building_An_Async_Pipeline_Without_Inbox_Then_With_Inbox_Still_Gets_Inbox()
    {
        PipelineBuilder<MyCommand>.ClearPipelineCache();

        // Step 1: Build an async pipeline WITHOUT inbox configuration — primes the static cache
        var noInboxRegistry = new SubscriberRegistry();
        noInboxRegistry.RegisterAsync<MyCommand, MyCommandHandlerAsync>();

        var noInboxContainer = new ServiceCollection();
        noInboxContainer.AddSingleton(new MyCommandHandlerAsync(new Dictionary<string, string>()));
        noInboxContainer.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });

        var noInboxHandlerFactory = new ServiceProviderHandlerFactory(noInboxContainer.BuildServiceProvider());
        var noInboxBuilder = new PipelineBuilder<MyCommand>(
            noInboxRegistry, (IAmAHandlerFactoryAsync)noInboxHandlerFactory);

        var withoutInbox = noInboxBuilder.BuildAsync(new MyCommand(), new RequestContext(), false);
        var withoutInboxTrace = TraceAsyncPipeline(withoutInbox.First());
        Assert.DoesNotContain("UseInboxHandlerAsync`", withoutInboxTrace);

        // Step 2: Build an async pipeline WITH inbox configuration — cache already primed without inbox
        var inboxRegistry = new SubscriberRegistry();
        inboxRegistry.RegisterAsync<MyCommand, MyCommandHandlerAsync>();

        var inboxContainer = new ServiceCollection();
        inboxContainer.AddSingleton(new MyCommandHandlerAsync(new Dictionary<string, string>()));
        inboxContainer.AddSingleton<IAmAnInboxAsync>(new InMemoryInbox(new FakeTimeProvider()));
        inboxContainer.AddTransient<UseInboxHandlerAsync<MyCommand>>();
        inboxContainer.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });

        var inboxHandlerFactory = new ServiceProviderHandlerFactory(inboxContainer.BuildServiceProvider());
        var inboxBuilder = new PipelineBuilder<MyCommand>(
            inboxRegistry, (IAmAHandlerFactoryAsync)inboxHandlerFactory, new InboxConfiguration());

        var withInbox = inboxBuilder.BuildAsync(new MyCommand(), new RequestContext(), false);
        var withInboxTrace = TraceAsyncPipeline(withInbox.First());
        Assert.Contains("UseInboxHandlerAsync`", withInboxTrace);
    }

    private static string TracePipeline(IHandleRequests<MyCommand> firstInPipeline)
    {
        var pipelineTracer = new PipelineTracer();
        firstInPipeline.DescribePath(pipelineTracer);
        return pipelineTracer.ToString();
    }

    private static string TraceAsyncPipeline(IHandleRequestsAsync<MyCommand> firstInPipeline)
    {
        var pipelineTracer = new PipelineTracer();
        firstInPipeline.DescribePath(pipelineTracer);
        return pipelineTracer.ToString();
    }
}
