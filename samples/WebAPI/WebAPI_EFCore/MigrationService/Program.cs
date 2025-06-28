using GreetingsApp.EntityGateway;
using MigrationService;
using SalutationApp.EntityGateway;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<GreetingsMigrator>();

builder.AddServiceDefaults();

builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing.AddSource(builder.Environment.ApplicationName));

builder.AddMySqlDbContext<GreetingsEntityGateway>("Greetings");
//builder.AddMySqlDbContext<SalutationsEntityGateway>("Salutations");

var app = builder.Build();

app.Run();


