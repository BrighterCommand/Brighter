using DbMaker;
using MigrationService;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();


builder.AddMySqlDataSource(connectionName: "Greetings");
builder.AddServiceDefaults();
//builder.Services.AddFluentMigratorCore();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(builder.Environment.ApplicationName));
GreetingsDbFactory.ConfigureMigration(builder.Configuration, builder.Services);
//builder.AddSqlServerDbContext<MyDb1Context>("db1");

var app = builder.Build();

app.Run();


