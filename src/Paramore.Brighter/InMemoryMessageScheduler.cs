using System;
using System.Collections.Generic;
using System.Threading;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter;

public class InMemoryMessageScheduler : IAmAMessageSchedulerSync
{
    private readonly SchedulerMessageCollection _messages = new();
    private readonly IAmASchedulerMessageConsumer _consumer;

    private readonly Timer _timer;

    public InMemoryMessageScheduler(IAmASchedulerMessageConsumer consumer,
        TimeSpan initialDelay,
        TimeSpan period)
    {
        _consumer = consumer;
        _timer = new Timer(Consume, this, initialDelay, period);
    }

    private static void Consume(object? state)
    {
        var scheduler = (InMemoryMessageScheduler)state!;

        var now = DateTimeOffset.UtcNow;
        var schedulerMessage = scheduler._messages.Next(now);
        while (schedulerMessage != null)
        {
            if (scheduler._consumer is IAmASchedulerMessageConsumerSync syncConsumer)
            {
                syncConsumer.Consume(schedulerMessage.Message, schedulerMessage.Context);
            }
            else if (scheduler._consumer is IAmASchedulerMessageConsumerAsync asyncConsumer)
            {
                var tmp = schedulerMessage;
                BrighterAsyncContext.Run(async () => await asyncConsumer.ConsumeAsync(tmp.Message, tmp.Context));
            }

            // TODO Add log
            schedulerMessage = scheduler._messages.Next(now);
        }
    }

    public string Schedule(DateTimeOffset at, Message message, RequestContext context)
    {
        var id = Guid.NewGuid().ToString();
        _messages.Add(new SchedulerMessage(id, message, context, at));
        return id;
    }

    public string Schedule(TimeSpan delay, Message message, RequestContext context) 
        => Schedule(DateTimeOffset.UtcNow.Add(delay), message, context);

    public void CancelScheduler(string id) 
        => _messages.Delete(id);

    public void Dispose() => _timer.Dispose();


    private record SchedulerMessage(string Id, Message Message, RequestContext Context, DateTimeOffset At);

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
