using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using Paramore.Brighter.Scheduler.Events;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter;

public class InMemoryMessageScheduler : IAmAMessageSchedulerSync
{
    private readonly SchedulerMessageCollection _messages = new();
    private readonly IAmACommandProcessor _processor;

    private readonly Timer _timer;

    public InMemoryMessageScheduler(IAmACommandProcessor processor,
        TimeSpan initialDelay,
        TimeSpan period)
    {
        _processor = processor;
        _timer = new Timer(Consume, this, initialDelay, period);
    }

    private static void Consume(object? state)
    {
        var scheduler = (InMemoryMessageScheduler)state!;

        var now = DateTimeOffset.UtcNow;
        var schedulerMessage = scheduler._messages.Next(now);
        while (schedulerMessage != null)
        {
            BrighterAsyncContext.Run(async () => await scheduler._processor.SendAsync(new SchedulerMessageFired(schedulerMessage.Id)
            {
                FireType = schedulerMessage.FireType,
                MessageType = schedulerMessage.MessageType,
                MessageData = schedulerMessage.MessageData,
            }));

            // TODO Add log
            schedulerMessage = scheduler._messages.Next(now);
        }
    }

    public string Schedule<TRequest>(DateTimeOffset at, SchedulerFireType fireType, TRequest request)
        where TRequest : class, IRequest
    {
        var id = Guid.NewGuid().ToString();
        _messages.Add(new SchedulerMessage(id, at, fireType,
            typeof(TRequest).FullName!,
            JsonSerializer.Serialize(request, JsonSerialisationOptions.Options)));
        return id;
    }

    public string Schedule<TRequest>(TimeSpan delay, SchedulerFireType fireType, TRequest request)
        where TRequest : class, IRequest
        => Schedule(DateTimeOffset.UtcNow.Add(delay), fireType, request);

    public void CancelScheduler(string id)
        => _messages.Delete(id);

    public void Dispose() => _timer.Dispose();

    private record SchedulerMessage(
        string Id,
        DateTimeOffset At,
        SchedulerFireType FireType,
        string MessageType,
        string MessageData);

    private class SchedulerMessageCollection
    {
        // It's a sorted list
        private readonly object _lock = new();
        private readonly LinkedList<SchedulerMessage> _messages = new();

        public SchedulerMessage? Next(DateTimeOffset now)
        {
            lock (_lock)
            {
                var first = _messages.First?.Value;
                if (first == null || first.At >= now)
                {
                    return null;
                }

                _messages.RemoveFirst();
                return first;
            }
        }

        public void Add(SchedulerMessage message)
        {
            lock (_lock)
            {
                var node = _messages.First;
                while (node != null)
                {
                    if (node.Value.At > message.At)
                    {
                        _messages.AddBefore(node, message);
                        return;
                    }

                    node = node.Next;
                }

                _messages.AddLast(message);
            }
        }

        public void Delete(string id)
        {
            lock (_lock)
            {
                var node = _messages.First;
                while (node != null)
                {
                    if (node.Value.Id == id)
                    {
                        _messages.Remove(node);
                        return;
                    }

                    node = node.Next;
                }
            }
        }
    }
}
