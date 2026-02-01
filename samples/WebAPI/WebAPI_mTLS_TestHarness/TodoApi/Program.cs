using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;
using TodoApi;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Get certificate path from configuration (defaults to test certificates)
var certPath = builder.Configuration.GetValue<string>("RabbitMQ:ClientCertPath")
    ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "..", "..", "tests", "certs", "client-cert.pfx");
var certPassword = builder.Configuration.GetValue<string>("RabbitMQ:ClientCertPassword") ?? "test-password";
var rabbitMqUri = builder.Configuration.GetValue<string>("RabbitMQ:Uri") ?? "amqps://localhost:5671";

// Configure RabbitMQ connection with mTLS
var rmqConnection = new RmqMessagingGatewayConnection
{
    AmpqUri = new AmqpUriSpecification(new Uri(rabbitMqUri)),
    Exchange = new Exchange("todo.exchange"),
    ClientCertificatePath = certPath,
    ClientCertificatePassword = certPassword,
    TrustServerSelfSignedCertificate = true // For test environment only
};

// Configure Brighter with RabbitMQ and mTLS
builder.Services.AddBrighter(options =>
    {
        options.HandlerLifetime = ServiceLifetime.Scoped;
    })
    .AddProducers(configure =>
    {
        configure.ProducerRegistry = new RmqProducerRegistryFactory(
            rmqConnection,
            new[]
            {
                new RmqPublication
                {
                    Topic = new RoutingKey(nameof(TodoCreated)),
                    RequestType = typeof(TodoCreated),
                    WaitForConfirmsTimeOutInMilliseconds = 1000,
                    MakeChannels = OnMissingChannel.Create
                }
            }
        ).Create();
    })
    .AutoFromAssemblies([typeof(TodoCreatedHandler).Assembly]);

// Configure Service Activator to consume messages
builder.Services.AddConsumers(options =>
{
    options.Subscriptions = new[]
    {
        new RmqSubscription<TodoCreated>(
            new SubscriptionName("TodoApiSubscription"),
            new ChannelName("TodoChannel"),
            new RoutingKey(nameof(TodoCreated)),
            messagePumpType: MessagePumpType.Proactor,
            timeOut: TimeSpan.FromMilliseconds(200),
            isDurable: true,
            makeChannels: OnMissingChannel.Create
        )
    };
    options.DefaultChannelFactory = new ChannelFactory(
        new RmqMessageConsumerFactory(rmqConnection)
    );
})
.AutoFromAssemblies();

// Add hosted service to run the message pump
builder.Services.AddHostedService<ServiceActivatorHostedService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Endpoint to publish a new Todo item
app.MapPost("/todos", async (IAmACommandProcessor commandProcessor, string title, bool isCompleted = false) =>
{
    var todoEvent = new TodoCreated(title, isCompleted);

    await commandProcessor.PostAsync(todoEvent);

    return Results.Ok(new {
        message = "Todo created and published",
        id = todoEvent.Id,
        title = todoEvent.Title,
        isCompleted = todoEvent.IsCompleted,
        createdAt = todoEvent.CreatedAt
    });
})
.WithName("CreateTodo");

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck");

app.Run();
