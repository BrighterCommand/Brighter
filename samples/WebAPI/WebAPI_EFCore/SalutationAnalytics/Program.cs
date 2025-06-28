using Microsoft.Extensions.Hosting;
using SalutationAnalytics;
using SalutationApp.EntityGateway;

var builder = Host.CreateApplicationBuilder(args);

builder.AddMySqlDataSource(connectionName: "Salutations");
builder.AddServiceDefaults();
builder.AddMySqlDbContext<SalutationsEntityGateway>("Salutations");
//builder.Services.ConfigureEFCore(builder.Configuration, builder.Environment);
builder.Services.ConfigureBrighter(builder.Configuration);
var host = builder.Build();
await host.RunAsync();
