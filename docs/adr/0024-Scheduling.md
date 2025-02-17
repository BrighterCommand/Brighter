# 24. Scoping dependencies inline with lifetime scope

Date: 2025-01-20

## Status

Proposed

## Context

Adding the ability to schedule message (by providing `TimeSpan` or `DateTimeOffset`)

## Decision

Giving support to Brighter schedule a message, it's necessary breaking on `IAmACommandProcessor` by adding these methods:

```c#
public interface IAmACommandProcessor
{
    string Send<TRequest>(DateTimeOffset delay, TRequest request) where TRequest : class, IRequest;
    string Publish<TRequest>(DateTimeOffset delay, TRequest request) where TRequest : class, IRequest;
    string Post<TRequest>(DateTimeOffset delay, TRequest request) where TRequest : class, IRequest;
    
    ....
}
```

Where that we return `scheduler id` giving the user ability to cancel or reschedule a message if it's necessary

Scheduling can be break into 2 part (Producer & Consumer):

### Producer 
Who is responsible for scheduler the message, like for In-Memory scheduler we the producer will create a timer, for Quartz to create a job.

For producing a message we are going to have a 2 new interfaces:

```c#
public interface IAmAMessageScheduler
{
}


public interface IAmAMessageSchedulerAsync : IAmAMessageScheduler
{
    Task<string> ScheduleAsync(Message message, DateTimeOffset at, CancellationToken cancellationToken = default);
    Task<string> ScheduleAsync(Message message, TimeSpan delay, CancellationToken cancellationToken = default);
    Task<bool> ReSchedulerAsync(string schedulerId, DateTimeOffset at, CancellationToken cancellationToken = default);
    Task<bool> ReSchedulerAsync(string schedulerId, TimeSpan delay, CancellationToken cancellationToken = default);
    Task CancelAsync(string id, CancellationToken cancellationToken = default);
}


public interface IAmAMessageSchedulerSync : IAmAMessageScheduler
{
    string Schedule(Message message, DateTimeOffset at);
    string Schedule(Message message, TimeSpan delay);
    bool ReScheduler(string schedulerId, DateTimeOffset at);
    bool ReScheduler(string schedulerId, TimeSpan delay);
    void Cancel(string id);
}
```
And
```c#
public interface IAmARequestScheduler
{
}


public interface IAmARequestSchedulerAsync : IAmARequestScheduler
{
    Task<string> ScheduleAsync<TRequest>(TRequest request, RequestSchedulerType type, DateTimeOffset at, CancellationToken cancellationToken = default)
        where TRequest : class, IRequest;
    
    Task<string> ScheduleAsync<TRequest>(TRequest request, RequestSchedulerType type, TimeSpan delay, CancellationToken cancellationToken = default)
        where TRequest : class, IRequest;
    
    Task<bool> ReSchedulerAsync(string schedulerId, DateTimeOffset at, CancellationToken cancellationToken = default);
    Task<bool> ReSchedulerAsync(string schedulerId, TimeSpan delay, CancellationToken cancellationToken = default);
    Task CancelAsync(string id, CancellationToken cancellationToken = default);
}


public interface IAmARequestSchedulerSync : IAmAMessageScheduler
{
    string ScheduleAsync<TRequest>(TRequest request, RequestSchedulerType type, DateTimeOffset at, CancellationToken cancellationToken = default)
        where TRequest : class, IRequest;
    
    string ScheduleAsync<TRequest>(TRequest request, RequestSchedulerType type, TimeSpan delay, CancellationToken cancellationToken = default)
        where TRequest : class, IRequest;
    bool ReScheduler(string schedulerId, DateTimeOffset at);
    bool ReScheduler(string schedulerId, TimeSpan delay);
    void Cancel(string id);
}
```

### Consumer
After a message is scheduler we need a way to handle this message and route it to correct producer.
To facilitate the scheduler implementation we are going to have a 2 new message type called 

```c#
public class FireSchedulerMessage : Command 
{
     public Message Message { get; set; } = new();
    
    public bool Async { get; set; }
}
```

```c#
public class FireSchedulerRequest() : Command(Guid.NewGuid())
{
    public RequestSchedulerType SchedulerType { get; set; }

    public string RequestType { get; set; } = string.Empty;

    public string RequestData { get; set; } = string.Empty;
    
    public bool Async { get; set; }
}

```

On the scheduler handler (which framework/lib will have a different implementation)

```c#
public class MessageSchedulerJob(ICommandProcessor processor) : IJob
{
    public async Task ExecuteAsync(FireSchedulerMessage message)
    {
        await processor.SendAsync(message);
    }
    
    public async Task ExecuteAsync(FireSchedulerRequest message)
    {
        await processor.SendAsync(message);
    }
}
```

### Brighter API changes

#### Request to Message
One important change is necessary is how we map a request to `Message`, we are open an exception to `FireSchedulerMessage`, 
when we map tha message to `Message` we are going to use the `Message` property.

#### Message Producer
Now we are adding support for `IAmAMessageScheduler` for `IAmAMessageProducer`, 
so if the message producer doesn't support delay message native the message producer can use the provide message scheduler

```c#
public interface IAmAMessageProducer 
{
    IAmAMessageScheduler? Scheduler { get; set; }
}
```

### Default Message scheduler
By default, we are going to use In-Memory scheduler

### In Memory
We are going to offer in-memory scheduler(it should be used on test or demo), we are scheduling messages with `ITimerProvider`

### Hangfire
We won't support hangfire by default due [issue with Strong name](https://github.com/HangfireIO/Hangfire/issues/1076)

### Quartz
Brighter has support to [Quartz](https://www.quartz-scheduler.net/), for developers be able to publish the scheduler message it's necessary
register the `QuartzBrighterJob`

```c#
services
    .AddSingleton<QuartzBrighterJob>()
    .AddQuartzHostedService();

...

services.AddBrighter()
    .UseMessageScheduler(provider =>
   {
        var factory = provider.GetRequiredService<ISchedulerFactory>();
        return new QuartzMessageSchedulerFactory(factory.GetScheduler().GetAwaiter().GetResult());
  })
....
```

### AWS Scheduler
AWS has a service call [AWS Scheduler](https://docs.aws.amazon.com/scheduler/latest/UserGuide/what-is-scheduler.html), 
which allow us to scheduler a message to SNS & SQS directly without need to have an extra handler, because of this ability
on AWS Scheduler we can schedule a message directly to the specific SNS & SQS and/or via and extra SNS/SQS in case
you want to scheduler a message to Kafka for example.

- In case you only have AWS infra SNS/SQS and you want to scheduler a message to there directly
```c#
services.AddBrighter()
   .UseMessageScheduler(new AwsMessageSchedulerFactory(awsConnection, "brighter-scheduler")
   {
       OnConflict = OnSchedulerConflict.Overwrite,
       GetOrCreateSchedulerId = message => message.Id
   });
```

- In case you want for all scheduler message go through a specific SNS/SQS
```c#
var producerRegistry = new SnsProducerRegistryFactory(awsConnection,
[
    new SnsPublication
    {
        Topic = new RoutingKey(typeof(FireSchedulerMessage).FullName.ToValidSNSTopicName()),
        RequestType = typeof(FireSchedulerMessage)
    }
]).Create();

var subscriptions = new Subscription[]
{
    new SqsSubscription<FireSchedulerMessage>(
        new SubscriptionName("paramore.example.fire-scheduler"),
        new ChannelName(typeof(FireSchedulerMessage).FullName.ToValidSNSTopicName()),
        new RoutingKey(typeof(FireSchedulerMessage).FullName.ToValidSNSTopicName()),
        bufferSize: 10,
        timeOut: TimeSpan.FromMilliseconds(20),
        lockTimeout: 30),
 
};
...

services.AddServiceActivator(opt =>
    {
         opt.Subscriptions = subscriptions;
    })
    .UseExternalBus(configure =>
   {
       configure.ProducerRegistry = producerRegistry;
   })
   .UseMessageScheduler(new AwsMessageSchedulerFactory(awsConnection, "brighter-scheduler")
   {
       // This flags is true by default
       // Brighter will try to find a SNS/SQS for the provided Topic
       // If it exists we will publish to that SQS/SNS, otherwise it'll be via fire scheduler SNS/SQS
       UseMessageTopicAsTarget = false,
       SchedulerTopicOrQueue = new RoutingKey(typeof(FireSchedulerMessage).FullName.ToValidSNSTopicName()),
       OnConflict = OnSchedulerConflict.Overwrite,
       GetOrCreateSchedulerId = message => message.Id
   });
```

#### Requirement 
For AWS Scheduler it's necessary a role with this configuration:

Assume role
```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": {
        "Service": "scheduler.amazonaws.com"
      },
      "Action": "sts:AssumeRole"
    }
  ]
}
```

and this policy

```json
{
   "Version": "2012-10-17",
   "Statement": [
   {
       "Effect": "Allow",
       "Action": [
           "sqs:SendMessage",
           "sns:Publish"
       ],
       "Resource": ["*"]
   }]
}
```