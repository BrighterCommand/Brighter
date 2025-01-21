# 24. Scoping dependencies inline with lifetime scope

Date: 2025-01-20

## Status

Proposed

## Context

Adding the ability to schedule message (by providing `TimeSpan` or `DateTimeOffset`) give to user flexibility to `Send`, `Publis` and `Post`.



## Decision

Giving support to schedule message, it's necessary breaking on `IAmACommandProcessor` by adding these methods:

```c#
public interface IAmACommandProcessor
{
    string SchedulerSend<TRequest>(TimeSpan delay, TRequest request) where TRequest : class, IRequest;
    string SchedulerSend<TRequest>(DateTimeOffset delay, TRequest request) where TRequest : class, IRequest;
    Task<string> SchedulerSendAsync<TRequest>(TimeSpan delay, TRequest request, bool continueOnCapturedContext = true, CancellationToken cancellationToken = default) where TRequest : class, IRequest;
    Task<string> SchedulerSendAsync<TRequest>(DateTimeOffset delay, TRequest request, bool continueOnCapturedContext = true, CancellationToken cancellationToken = default) where TRequest : class, IRequest;

    string SchedulerPublish<TRequest>(TimeSpan delay, TRequest request) where TRequest : class, IRequest;
    string SchedulerPublish<TRequest>(DateTimeOffset delay, TRequest request) where TRequest : class, IRequest;
    Task<string> SchedulerPublishAsync<TRequest>(TimeSpan delay, TRequest request, bool continueOnCapturedContext = true, CancellationToken cancellationToken = default) where TRequest : class, IRequest;
    Task<string> SchedulerPublishsync<TRequest>(DateTimeOffset delay, TRequest request, bool continueOnCapturedContext = true, CancellationToken cancellationToken = default) where TRequest : class, IRequest;

    string SchedulerPost<TRequest>(TimeSpan delay, TRequest request) where TRequest : class, IRequest;
    string SchedulerPost<TRequest>(DateTimeOffset delay, TRequest request) where TRequest : class, IRequest;
    Task<string> SchedulerPostAsync<TRequest>(TimeSpan delay, TRequest request, bool continueOnCapturedContext = true, CancellationToken cancellationToken = default) where TRequest : class, IRequest;
    Task<string> SchedulerPostAsync<TRequest>(DateTimeOffset delay, TRequest request, bool continueOnCapturedContext = true, CancellationToken cancellationToken = default) where TRequest : class, IRequest;
}
```

Scheduling can be break into 2 part (Producer & Consumer):
- Producer -> Producing a message we are going to have a new interface:

```c#
public interface IAmAMessageScheduler
{
}


public interface IAmAMessageSchedulerAsync : IAmAMessageScheduler, IDisposable
{
    Task<string> ScheduleAsync<TRequest>(DateTimeOffset at, SchedulerFireType fireType, TRequest request, CancellationToken cancellationToken = default) where TRequest : class, IRequest;
    Task<string> ScheduleAsync<TRequest>(TimeSpan delay, SchedulerFireType fireType, TRequest request, CancellationToken cancellationToken = default) where TRequest : class, IRequest;
    Task CancelSchedulerAsync(string id, CancellationToken cancellationToken = default);
}


public interface IAmAMessageSchedulerSync : IAmAMessageScheduler, IDisposable
{
    string Schedule<TRequest>(DateTimeOffset at, SchedulerFireType fireType, TRequest request) where TRequest : class, IRequest;
    string Schedule<TRequest>(TimeSpan delay, SchedulerFireType fireType, TRequest request) where TRequest : class, IRequest;
    void CancelScheduler(string id);
}
```

- Consumer -> To avoid duplication code we are going to introduce a new message and have a handler for that:

```c#
public class SchedulerMessageFired : Event
{
    .....
}


public class SchedulerMessageFiredHandlerAsync(IAmACommandProcessor processor) : RequestHandlerAsync<SchedulerMessageFired>
{
    ....
}
```

So on Scheduler implementation we need to send the SchedulerMessageFired

```c#
public class JobExecute(IAmACommandProcessor processor)
{
    public async Task ExecuteAsync(Arg arg)
    {
        await processor.SendAsync(new SchedulerMessageFired{ ... });
    }
}
```