
using DbMaker;
using TransportMaker;

var builder = DistributedApplication.CreateBuilder(args);


var mysql = builder.AddMySql("mysql")
    .WithLifetime(ContainerLifetime.Persistent);

var greetingsDb = mysql.AddDatabase("Greetings");
var salutationsDb = mysql.AddDatabase("Salutations");

var rabbitmq = builder.AddRabbitMQ("messaging");

var migrationService = builder.AddProject<Projects.MigrationService>("migration")
    .WithReference(greetingsDb)
    .WithReference(salutationsDb)
    .WaitFor(greetingsDb)
    .WaitFor(salutationsDb)
    .WithEnvironment(DatabaseGlobals.DATABASE_TYPE_ENV, DatabaseGlobals.MYSQL)
    .WithEnvironment(MessagingGlobals.BRIGHTER_TRANSPORT, MessagingGlobals.RMQ);

builder.AddProject<Projects.GreetingsWeb>("web")
    .WithReference(greetingsDb)
    .WithReference(rabbitmq)
    .WaitFor(migrationService)
    .WithEnvironment(DatabaseGlobals.DATABASE_TYPE_ENV, DatabaseGlobals.MYSQL)
    .WithEnvironment(MessagingGlobals.BRIGHTER_TRANSPORT, MessagingGlobals.RMQ);

builder.AddProject<Projects.Greetings_Sweeper>("sweeper")
    .WithReference(greetingsDb)
    .WithReference(rabbitmq)
    .WaitFor(migrationService)
    .WithEnvironment(DatabaseGlobals.DATABASE_TYPE_ENV, DatabaseGlobals.MYSQL)
    .WithEnvironment(MessagingGlobals.BRIGHTER_TRANSPORT, MessagingGlobals.RMQ);

builder.AddProject<Projects.SalutationAnalytics>("analytics")
    .WithReference(salutationsDb)
    .WithReference(rabbitmq)
    .WaitFor(salutationsDb)
    .WithEnvironment(DatabaseGlobals.DATABASE_TYPE_ENV, DatabaseGlobals.MYSQL)
    .WithEnvironment(MessagingGlobals.BRIGHTER_TRANSPORT, MessagingGlobals.RMQ);

builder.Build().Run();
