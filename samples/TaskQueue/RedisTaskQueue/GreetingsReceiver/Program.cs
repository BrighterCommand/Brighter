using Microsoft.Extensions.Logging;
using System;
using Greetings.Ports.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.Redis;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

var subscriptions = new Subscription[]
{
    new RedisSubscription<GreetingEvent>(
        new SubscriptionName("paramore.example.greeting"),
        new ChannelName("greeting.event"),
        new RoutingKey("greeting.event"),
        timeOut: TimeSpan.FromSeconds(1))
};

//create the gateway
var redisConnection = new RedisMessagingGatewayConfiguration
{
    RedisConnectionString = "localhost:6379?connectTimeout=1&sendTImeout=1000&",
    MaxPoolSize = 10,
    MessageTimeToLive = TimeSpan.FromMinutes(10)
};

builder.Services.AddConsumers(provider =>
{
    var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
    var redisConsumerFactory = new RedisMessageConsumerFactory(redisConnection, loggerFactory: loggerFactory);
    var options = new ConsumersOptions();
    options.Subscriptions = subscriptions;
    options.DefaultChannelFactory = new ChannelFactory(redisConsumerFactory);
    return options;
})
// InMemorySchedulerFactory is the default — shown here explicitly to demonstrate scheduler configuration.
// Replace with HangfireMessageSchedulerFactory or QuartzSchedulerFactory for durable scheduling.
.UseScheduler(provider => new InMemorySchedulerFactory(provider.GetRequiredService<ILoggerFactory>()))
.AutoFromAssemblies();

builder.Services.AddHostedService<ServiceActivatorHostedService>();

var host = builder.Build();
await host.RunAsync();
