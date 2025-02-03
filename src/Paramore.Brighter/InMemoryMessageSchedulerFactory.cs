using System;

namespace Paramore.Brighter;

public class InMemoryMessageSchedulerFactory(TimeProvider timerProvider) : IAmAMessageSchedulerFactory
{
    public InMemoryMessageSchedulerFactory()
        : this(TimeProvider.System)
    {
    }

    public IAmAMessageScheduler Create(IAmACommandProcessor processor)
    {
        return GetOrCreate(processor, timerProvider);
    }

    private static readonly object s_lock = new();
    private static InMemoryMessageScheduler? s_scheduler;

    private static InMemoryMessageScheduler GetOrCreate(IAmACommandProcessor processor, TimeProvider timeProvider)
    {
        if (s_scheduler == null)
        {
            lock (s_lock)
            {
                s_scheduler ??= new InMemoryMessageScheduler(processor, timeProvider);
            }
        }

        return s_scheduler;
    }
}
