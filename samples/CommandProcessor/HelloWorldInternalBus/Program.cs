using HelloWorldInternalBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Observability;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;

var routingKey = new RoutingKey("greeting.command");

var bus = new InternalBus();

var publications = new[] { new Publication { Topic = routingKey, RequestType = typeof(GreetingCommand)} }; 

var subscriptions = new[]
{
    new Subscription<GreetingCommand>(
        new SubscriptionName("GreetingCommandSubscription"),
        new ChannelName("GreetingCommand"),
        routingKey
    )
};

var host = Host.CreateDefaultBuilder()
    .ConfigureServices((context, services) =>
    {
        services.AddConsumers(options =>
        {
            options.Subscriptions = subscriptions;
            options.DefaultChannelFactory = new InMemoryChannelFactory(bus, TimeProvider.System);
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Singleton;
            options.InboxConfiguration = new InboxConfiguration(new InMemoryInbox(TimeProvider.System));
        })
        .AddProducers((config) =>
        {
            config.ProducerRegistry = new InMemoryProducerRegistryFactory(bus, publications, InstrumentationOptions.All).Create(); 
            config.Outbox = new InMemoryOutbox(TimeProvider.System);
        })
        .AutoFromAssemblies();
        
        services.AddHostedService<ServiceActivatorHostedService>();
    })
    .UseConsoleLifetime()
    .Build();

var commandProcessor = host.Services.GetService<IAmACommandProcessor>();

commandProcessor?.Post(new GreetingCommand("Ian"));

await host.RunAsync();
