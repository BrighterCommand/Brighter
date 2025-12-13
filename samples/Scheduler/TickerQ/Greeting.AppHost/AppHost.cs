var builder = DistributedApplication.CreateBuilder(args);

var username = builder.AddParameter("username", "guest", secret: true);
var password = builder.AddParameter("password", "guest", secret: true);

var rabbitmq = builder.AddRabbitMQ("messaging", username, password)
                      .WithManagementPlugin();

builder.AddProject<Projects.Greeting_Producer>("greeting-producer")
        .WithReference(rabbitmq);

builder.AddProject<Projects.Greeting_Consumer>("greeting-consumer")
       .WithReference(rabbitmq);

builder.Build().Run();
