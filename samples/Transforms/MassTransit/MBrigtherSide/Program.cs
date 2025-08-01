using System.Text.Json;
using Amazon;
using Amazon.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;
using Paramore.Brighter.Transformers.MassTransit;
using IRequestContext = Paramore.Brighter.IRequestContext;

var host = new HostBuilder()
    .ConfigureLogging(builder => builder.AddConsole())
    .ConfigureServices(service =>
    {
        var connection = new AWSMessagingGatewayConnection(new BasicAWSCredentials("test", "teste"),
            RegionEndpoint.USEast1,
            cfg =>
            {
                cfg.ServiceURL = "http://localhost:4566";
            });
        
        service
            .AddHostedService<ServiceActivatorHostedService>()
            .AddConsumers(configure =>
            {
                configure.Subscriptions = [
                    new SqsSubscription<Greeting>(
                        channelName: new ChannelName("brighter-queue"),
                        routingKey: new RoutingKey("masstransit-topic"),
                        messagePumpType: MessagePumpType.Reactor)
                ];

                configure.DefaultChannelFactory = new ChannelFactory(connection);
            })
            .AddProducers(configure =>
            {
                configure.ProducerRegistry = new SnsProducerRegistryFactory(connection, 
                        [
                            new SnsPublication
                            {
                                Topic = "brighter-topic",
                                RequestType = typeof(Greeting)
                            }
                        ])
                    .Create();
            })
            .TransformsFromAssemblies([typeof(MassTransitWrapAttribute).Assembly])
            .AutoFromAssemblies();
    })
    .Build();


await host.StartAsync();

var cts = new CancellationTokenSource();
Console.CancelKeyPress  += (_,_) => cts.Cancel();

while (!cts.IsCancellationRequested)
{
    Console.Write("Say your name: ");
    var name = Console.ReadLine();

    if (string.IsNullOrEmpty(name))
    {
        continue;
    }
    
    using var scope = host.Services.CreateScope();
    var processor = scope.ServiceProvider.GetRequiredService<IAmACommandProcessor>();
    await processor.PostAsync(new Greeting { Name = name });
}

await host.StopAsync();


public class Greeting() : Event(Id.Random()) 
{
    public string Name { get; set; } = string.Empty;
}

public class GreetingMapper : IAmAMessageMapper<Greeting>, IAmAMessageMapperAsync<Greeting>
{
    public IRequestContext? Context { get; set; }
    
    [MassTransitWrap(0, MessageType = ["urn:message:MassTransitSide:Greeting"])]
    public Task<Message> MapToMessageAsync(Greeting request, Publication publication, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(MapToMessage(request, publication));
    }

    [MassTransitWrap(0)]
    public Task<Greeting> MapToRequestAsync(Message message, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(MapToRequest(message));
    }

    [MassTransitWrap(0)]
    public Message MapToMessage(Greeting request, Publication publication)
    {
        return new Message(new MessageHeader
            {
                MessageId = request.Id,
                CorrelationId = Id.Random(),
                MessageType = MessageType.MT_EVENT,
                Topic = publication.Topic!,
            }, 
            new MessageBody(JsonSerializer.SerializeToUtf8Bytes(request, JsonSerialisationOptions.Options)));
    }

    [MassTransitUnwrap(0)]
    public Greeting MapToRequest(Message message)
    {
        return JsonSerializer.Deserialize<Greeting>(message.Body.Value, JsonSerialisationOptions.Options)!;
    }
}


public class GreetingHandler(ILogger<GreetingHandler> logger) : RequestHandler<Greeting>
{
    public override Greeting Handle(Greeting command)
    {
        logger.LogInformation("Hello {Name}", command.Name);
        return base.Handle(command);
    }
}
