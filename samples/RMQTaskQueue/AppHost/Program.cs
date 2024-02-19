var builder = DistributedApplication.CreateBuilder(args);

var rabbit = builder.AddRabbitMQ("messaging");

var receiver = builder.AddProject<Projects.GreetingsReceiverConsole>("receiver")
    .WithReference(rabbit);
var sender = builder.AddProject<Projects.GreetingsSender>("sender")
    .WithReference(rabbit);

builder.Build().Run();
