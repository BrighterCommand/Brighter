var builder = DistributedApplication.CreateBuilder(args);

var rmq = builder
    .AddRabbitMQ("messaging")
    .WithLifetime(ContainerLifetime.Persistent);

builder.AddProject<Projects.GreetingsSender>("sender")
    .WithReference(rmq);

builder.AddProject<Projects.GreetingsReceiverConsole>("receiver")
    .WithReference(rmq);

builder.Build().Run();
