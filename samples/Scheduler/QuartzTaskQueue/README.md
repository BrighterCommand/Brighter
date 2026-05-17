# Quartz.NET Integration Guide for GreetingsPumper

This sample demonstrates how to use [Quartz.NET](https://www.quartz-scheduler.net/) with Brighter to schedule and process messages in a .NET application.

## Prerequisites

- [.NET 6.0 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)
- [Quartz.NET](https://www.nuget.org/packages/Quartz) (added via NuGet)
- AWS credentials (if using AWS SQS/SNS integration)
- Moto (optional, for local AWS service emulation)

## Getting Started

1. **In-Memory Quartz Store**

   The sample configures Quartz using the in-memory store and registers Quartz as a hosted service:

   ```csharp
   services
       .AddSingleton<QuartzBrighterJob>()
       .AddQuartz(opt =>
       {
           opt.SchedulerId = "QuartzBrighter";
           opt.SchedulerName = "QuartzBrighter";
           opt.UseSimpleTypeLoader();
           opt.UseInMemoryStore();
       })
       .AddQuartzHostedService(opt =>
       {
           opt.WaitForJobsToComplete = true;
       });
   ```

    - `AddQuartz`: Configures the scheduler and job store.
    - `AddQuartzHostedService`: Runs Quartz as a hosted service.

2. **Running the Application**

To run with Moto for AWS SQS/SNS emulation:

- Start Moto:

  ```sh
  docker-compose -f docker-compose-aws.yaml up
  ```

- Ensure the service URL is set to `http://localhost:4566/` in your configuration, or set the `AWS_SERVICE_URL` environment variable.

3. **Inspecting AWS Mock Services**

To inspect the queues and topics in Moto, you can use the AWS CLI pointed to the Moto endpoint:

- Ensure that you set the region to us-east-1

```sh
aws configure set region us-east-1
```

- Use the following commands to list queues and topics:

  ```sh
  aws --endpoint-url http://localhost:4566 sqs list-queues
  aws --endpoint-url http://localhost:4566 sns list-topics
  ```

## Resources

- [Quartz.NET Documentation](https://www.quartz-scheduler.net/documentation/)
- [Brighter Documentation](https://github.com/BrighterCommand/Brighter)
- [Moto Documentation](https://docs.getmoto.org/)
