# Quartz.NET Integration Guide for GreetingsPumper

This sample demonstrates how to use [Quartz.NET](https://www.quartz-scheduler.net/) with Brighter to schedule and process messages in a .NET application.

## Prerequisites

- [.NET 6.0 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)
- [Quartz.NET](https://www.nuget.org/packages/Quartz) (added via NuGet)
- AWS credentials (if using AWS SQS/SNS integration)
- LocalStack (optional, for local AWS service emulation)

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

To run with LocalStack for AWS SQS/SNS emulation:

- Start LocalStack:

  ```sh
  docker-compose -f docker-compose-localstack.yaml up
  ```

- Ensure the service URL is set to `http://localhost:4566/` in your configuration.

3. **Inspecting LocalStack**

To inspect the queues and topics in LocalStack, you can use the LocalStack web UI or AWS CLI commands pointed to the LocalStack endpoint.

- Download https://github.com/localstack/awscli-local
- Ensure that you set the region to useast-1

```sh
 awslocal configure set region us-east-1
```

- Use the following commands to list queues and topics:

  ```sh
  awslocal sqs list-queues
  awslocal sns list-topics
  ```

## Resources

- [Quartz.NET Documentation](https://www.quartz-scheduler.net/documentation/)
- [Brighter Documentation](https://github.com/BrighterCommand/Brighter)
- [LocalStack Documentation](https://docs.localstack.cloud/)


