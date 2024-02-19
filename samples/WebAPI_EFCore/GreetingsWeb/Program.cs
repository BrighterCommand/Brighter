using System;
using GreetingsPorts.EntityGateway;
using GreetingsPorts.Handlers;
using GreetingsPorts.Policies;
using GreetingsPorts.Requests;
using GreetingsWeb.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using MySqlConnector;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Hosting;
using Paramore.Brighter.MessagingGateway.RMQ;
using Paramore.Brighter.MySql;
using Paramore.Brighter.MySql.EntityFrameworkCore;
using Paramore.Brighter.Outbox.MySql;
using Paramore.Brighter.Outbox.Sqlite;
using Paramore.Brighter.Sqlite;
using Paramore.Brighter.Sqlite.EntityFrameworkCore;
using Paramore.Darker;
using Paramore.Darker.AspNetCore;
using Paramore.Darker.Policies;
using Paramore.Darker.QueryLogging;
using Polly;

const string _outBoxTableName = "Outbox";

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();

// Add service defaults & Aspire components.
builder.AddServiceDefaults();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.SwaggerDoc("v1", new OpenApiInfo { Title = "GreetingsAPI", Version = "v1" }));

if (builder.Environment.IsDevelopment())
{
    //NOTE: Sqlite needs to use a shared cache to allow Db writes to the Outbox as well as entities
    builder.Services.AddDbContext<GreetingsEntityGateway>(
        options =>
        {
            options.UseSqlite(DbConnectionString(),
                    optionsBuilder =>
                    {
                        optionsBuilder.MigrationsAssembly("Greetings_SqliteMigrations");
                    })
                .EnableDetailedErrors()
                .EnableSensitiveDataLogging();
        });
}
else
{
    builder.Services.AddDbContextPool<GreetingsEntityGateway>(options =>
    {
        options
            .UseMySql(DbConnectionString(), ServerVersion.AutoDetect(DbConnectionString()), optionsBuilder =>
            {
                optionsBuilder.MigrationsAssembly("Greetings_MySqlMigrations");
            })
            .EnableDetailedErrors()
            .EnableSensitiveDataLogging();
    });
}

string DbConnectionString()
{
    if (builder.Environment.IsDevelopment())
    {
        return "Filename=Greetings.db;Cache=Shared";
    }

    return builder.Configuration.GetConnectionString("Greetings");
}

(IAmAnOutbox outbox, Type transactionProvider, Type connectionProvider) = MakeOutbox();
var outboxConfiguration = new RelationalDatabaseConfiguration(DbConnectionString());

builder.Services.AddSingleton<IAmARelationalDatabaseConfiguration>(outboxConfiguration);
            
IAmAProducerRegistry producerRegistry = ConfigureProducerRegistry();

builder.Services.AddBrighter(options =>
    {
        //we want to use scoped, so make sure everything understands that which needs to
        options.HandlerLifetime = ServiceLifetime.Scoped;
        options.CommandProcessorLifetime = ServiceLifetime.Scoped;
        options.MapperLifetime = ServiceLifetime.Singleton;
        options.PolicyRegistry = new GreetingsPolicy();
    })
    .UseExternalBus((configure) =>
        {
            configure.ProducerRegistry = producerRegistry;
            configure.Outbox = outbox;
            configure.TransactionProvider = transactionProvider;
            configure.ConnectionProvider = connectionProvider;
        }
    )
    .UseOutboxSweeper(options =>
    {
        options.TimerInterval = 5;
        options.MinimumMessageAge = 5000;
    })
    .UseOutboxSweeper()
    .AutoFromAssemblies();


builder.Services.AddDarker(options =>
    {
        options.HandlerLifetime = ServiceLifetime.Scoped;
        options.QueryProcessorLifetime = ServiceLifetime.Scoped;
    })
    .AddHandlersFromAssemblies(typeof(FindPersonByNameHandlerAsync).Assembly)
    .AddJsonQueryLogging()
    .AddPolicies(new GreetingsPolicy());


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseHttpsRedirection();

//host.CheckDbIsUp();
string connectionString = DbConnectionString();

var policy = Policy.Handle<MySqlException>().WaitAndRetryForever(
    retryAttempt => TimeSpan.FromSeconds(2),
    (exception, timespan) =>
    {
        Console.WriteLine($"Healthcheck: Waiting for the database {connectionString} to come online - {exception.Message}");
    });

policy.Execute(() =>
{
    //don't check this for SQlite in development
    if (!app.Environment.IsDevelopment())
    {
        using var conn = new MySqlConnection(connectionString);
        conn.Open();
    }
});


//host.CreateOutbox();


//host.MigrateDatabase();
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<GreetingsEntityGateway>();
    dbContext.Database.Migrate();
}

// Greetings
app.MapGet("greetings", async (string name, IQueryProcessor queryProcessor) =>
{
    var personsGreetings = await queryProcessor.ExecuteAsync(new FindGreetingsForPerson(name));
    return personsGreetings == null ? Results.NotFound() : Results.Ok(personsGreetings);
}).WithName("GetGreetings").WithOpenApi();

app.MapPost("greetings/new", async (string name, NewGreeting newGreeting, IAmACommandProcessor commandProcessor, IQueryProcessor queryProcessor) => 
{
    await commandProcessor.SendAsync(new AddGreeting(name, newGreeting.Greeting));

    var personsGreetings = await queryProcessor.ExecuteAsync(new FindGreetingsForPerson(name));
    
    return personsGreetings == null ? Results.NotFound() : Results.Ok(personsGreetings);
});

// People
app.MapGet("people", async (string name, IQueryProcessor queryProcessor) =>
{
    var foundPerson = await queryProcessor.ExecuteAsync(new FindPersonByName(name));

    return foundPerson == null ? Results.NotFound() : Results.Ok(foundPerson);
});

app.MapDelete("people", async (string name, IAmACommandProcessor commandProcessor) =>
{
    await commandProcessor.SendAsync(new DeletePerson(name));

    return Results.Ok();
});

app.MapPost("people/new", async (NewPerson newPerson, IAmACommandProcessor commandProcessor, IQueryProcessor queryProcessor) =>
{
     await commandProcessor.SendAsync(new AddPerson(newPerson.Name));

     var addedPerson = await queryProcessor.ExecuteAsync(new FindPersonByName(newPerson.Name));

     return addedPerson == null ? Results.NotFound() : Results.Ok(addedPerson);
});

app.Run();
return;


(IAmAnOutbox outbox, Type transactionProvider, Type connectionProvider) MakeOutbox()
{
    if (builder.Environment.IsDevelopment())
    {
        var outbox = new SqliteOutbox(new RelationalDatabaseConfiguration(DbConnectionString(), _outBoxTableName));
        var transactionProvider = typeof(SqliteEntityFrameworkConnectionProvider<GreetingsEntityGateway>);
        var connectionProvider = typeof(SqliteConnectionProvider);
        return (outbox, transactionProvider, connectionProvider);
    }
    else
    {
        var outbox = new MySqlOutbox(new RelationalDatabaseConfiguration(DbConnectionString(), _outBoxTableName));
        var transactionProvider = typeof(MySqlEntityFrameworkConnectionProvider<GreetingsEntityGateway>);
        var connectionProvider = typeof(MySqlConnectionProvider);
        return (outbox, transactionProvider, connectionProvider);
    }
}

static IAmAProducerRegistry ConfigureProducerRegistry()
{
    var producerRegistry = new RmqProducerRegistryFactory(
        new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672")),
            Exchange = new Exchange("paramore.brighter.exchange"),
        },
        new RmqPublication[]
        {
            new RmqPublication
            {
                Topic = new RoutingKey("GreetingMade"),
                MaxOutStandingMessages = 5,
                MaxOutStandingCheckIntervalMilliSeconds = 500,
                WaitForConfirmsTimeOutInMilliseconds = 1000,
                MakeChannels = OnMissingChannel.Create
            }
        }
    ).Create();
    return producerRegistry;
}
