using Greeting.Consumer;
using Greeting.Models;
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
var cnstring = builder.Configuration.GetConnectionString("messaging");
var rmqConnection = new RmqMessagingGatewayConnection
{
    AmpqUri = new AmqpUriSpecification(new Uri(cnstring)),
    Exchange = new Exchange("paramore.brighter.exchange"),
};

builder.Services.AddTransient(typeof(GreetingHandler));
builder.Services.AddConsumers(opt =>
{
    opt.Subscriptions = new Subscription[]
        {
                        new RmqSubscription<GreetingEvent>(
                            new SubscriptionName("paramore.example.greeting"),
                            new ChannelName("greeting.event"),
                            new RoutingKey("greeting.event"),
                            makeChannels: OnMissingChannel.Create,
                            messagePumpType: MessagePumpType.Proactor
                        ),
        };

    opt.DefaultChannelFactory = new ChannelFactory(
        new RmqMessageConsumerFactory(rmqConnection)
    );

})
.AutoFromAssemblies();

builder.Services
    .AddHostedService<ServiceActivatorHostedService>();
// Add services to the container.

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.MapGet("/", () =>
{
   return "helloConsumer";
});

app.Run();

