using System;

namespace Paramore.Brighter;

public class InMemoryMessageSchedulerFactory(TimeSpan initialDelay, TimeSpan period) : IAmAMessageSchedulerFactory
{
    public InMemoryMessageSchedulerFactory()
        : this(TimeSpan.Zero, TimeSpan.FromSeconds(1))
    {
    }

    public IAmAMessageScheduler Create(IAmACommandProcessor processor)
    {
        return GetOrCreate(processor, initialDelay, period);
    }

    private static readonly object s_lock = new();
    private static InMemoryMessageScheduler? s_scheduler;

    private static InMemoryMessageScheduler GetOrCreate(IAmACommandProcessor processor, TimeSpan initialDelay,
        TimeSpan period)
    {
        if (s_scheduler == null)
        {
            lock (s_lock)
            {
                s_scheduler ??= new InMemoryMessageScheduler(processor, initialDelay, period);
            }
        }

        return s_scheduler;
    }
}
