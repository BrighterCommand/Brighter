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

For producing a message we are going to have 2 new interfaces:

`IAmAMessageScheduler` -> Responsible for scheduler a message, it'll be used by `IAmAMessageProduce` in case the producer
doesn't support delay message or the current it too long.

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
And `IAmARequestScheduler` -> It'll be used by `IAmACommandProcessor` to scheduler a `Send`, `Publish` and `Post`

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
After a message/request is scheduler we need a way to handle this message and route it to correct producer.
To facilitate the scheduler implementation we are going to have a 2 new message type called.

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

On the scheduler handler (which framework/lib will have a different implementation), 
we can just call the `ICommandProcessor.SendAsync`, like the sample bellow:

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
    ...
    IAmAMessageScheduler? Scheduler { get; set; }
}
```

### Default Message scheduler
By default, we are going to use In-Memory scheduler

### In Memory
We are going to offer in-memory scheduler(it should be used on test or demo), we are scheduling messages with `ITimerProvider`
which won't persistence the message, in-case the application shutdown or have a failure

### Hangfire
For Hangfire, we won't sign the assembly due [issue with Strong name](https://github.com/HangfireIO/Hangfire/issues/1076).

Using Hangfire for scheduler, it'll be necessary registry `BrighterHangfireSchedulerJob` in Hangfire `JobActivator`.

```c#
services
    .AddSingleton<BrighterHangfireSchedulerJob>()
    .AddHangfire(opt => { .... });

services.AddBrighter()
    .UseScheduler(new HangfireMessageSchedulerFactory());
 ....
```


### Quartz
Brighter has support to [Quartz](https://www.quartz-scheduler.net/), for developers be able to publish the scheduler message it's necessary
register the `QuartzBrighterJob`

```c#
services
    .AddSingleton<QuartzBrighterJob>()
    .AddQuartzHostedService();

...

services.AddBrighter()
    .UseScheduler(provider =>
    {
        var factory = provider.GetRequiredService<ISchedulerFactory>();
        return new QuartzMessageSchedulerFactory(factory.GetScheduler().GetAwaiter().GetResult());
    })
....
```

### AWS Scheduler
AWS has a service call [AWS Scheduler](https://docs.aws.amazon.com/scheduler/latest/UserGuide/what-is-scheduler.html), 
which allow us to scheduler a message to SNS & SQS directly without need to have an extra handler, because of this ability
on AWS Scheduler we can schedule a message directly to the specific SNS & SQS (when you scheduler message via `IAmAMessageScheduler`) and
via and extra SNS/SQS (when using `IAmARequestScheduler`).

```c#
// Ensure the topic/queue exists
var producerRegistry = new SnsProducerRegistryFactory(awsConnection,
[
    new SnsPublication
    {
        Topic = new RoutingKey("paramore.example.scheduler"),
        RequestType = typeof(AwsSchedulerFired)
    }
]).Create();

// Fire message handler
var subscriptions = new Subscription[]
{
    new SqsSubscription<AwsSchedulerFired>(
        new SubscriptionName("paramore.example.scheduler"),
        new ChannelName("paramore.example.scheduler"),
        new RoutingKey("paramore.example.scheduler"),
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
   .UseScheduler(new AwsMessageSchedulerFactory(awsConnection, "paramore.example.scheduler-role")
   {
       // This flags is true by default
       // Brighter will try to find a SNS/SQS for the provided Topic
       // If it exists we will publish to that SQS/SNS, otherwise it'll be via fire scheduler SNS/SQS
       UseMessageTopicAsTarget = false,
       SchedulerTopicOrQueue = new RoutingKey("paramore.example.scheduler"),
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
### Azure Services
Azure Queue already support long delay before publish and allow us to cancel them if it's necessary, 
but doesn't update reschedule it. So, for Azure Service bus we won't support to message reschedule (users should `Cancel` and `Scheduler`).
Also, because Azure doesn't have a scheduler api, we won't support reschedule a message/request to Topic/Queue directly, 
we are going to use a Topic/Queue for that.

```c#
// Ensure the topic/queue exists
var producerRegistry = new AzureServiceBusProducerRegistryFactory(asbClientProvider,
[
    new AzureServiceBusPublication
    {
        Topic = new RoutingKey("paramore.example.scheduler"),
        RequestType = typeof(AzureSchedulerFired)
    }
]).Create();

// Fire message handler
var subscriptions = new Subscription[]
{
    new AzureServiceBusSubscription<AzureSchedulerFired>(
        new SubscriptionName("paramore.example.fire-scheduler"),
        new ChannelName("paramore.example.scheduler"),
        new RoutingKey("paramore.example.scheduler"),
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


services.AddServiceActivator(opt =>
    {
         opt.Subscriptions = subscriptions;
    })
    .UseExternalBus(configure =>
   {
       configure.ProducerRegistry = producerRegistry;
   })
   .UseScheduler(new AzureServiceBusSchedulerFactory(asbClientProvider, "paramore.example.scheduler"));
```
