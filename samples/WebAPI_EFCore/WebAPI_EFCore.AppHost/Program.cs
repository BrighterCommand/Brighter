var builder = DistributedApplication.CreateBuilder(args);

var rabbit = builder.AddRabbitMQ("messaging");

var greetingsWeb = builder.AddProject<Projects.GreetingsWeb>("greetings_web")
    .WithReference(rabbit);
var salutation_analytics = builder.AddProject<Projects.SalutationAnalytics>("salutation_analytics")
    .WithReference(rabbit);

builder.Build().Run();
