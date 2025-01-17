using System;
using System.Collections.Generic;
using System.Transactions;

namespace Paramore.Brighter;

public class InMemoryMessageSchedulerFactory(TimeSpan initialDelay, TimeSpan period) : IAmAMessageSchedulerFactory
{
    public InMemoryMessageSchedulerFactory()
        : this(TimeSpan.Zero, TimeSpan.FromSeconds(1))
    {
    }

    private static readonly Dictionary<Type, IAmAMessageScheduler> s_schedulers = new();

    public IAmAMessageScheduler Create(IAmAnOutboxProducerMediator mediator)
        => Create<CommittableTransaction>(mediator, null);

    public IAmAMessageScheduler Create<TTransaction>(IAmAnOutboxProducerMediator mediator,
        IAmABoxTransactionProvider<TTransaction>? transactionProvider)
    {
        if (!s_schedulers.TryGetValue(typeof(TTransaction), out var scheduler))
        {
            var consumer = new SchedulerMessageConsumer<TTransaction>(mediator, transactionProvider);
            scheduler = new InMemoryMessageScheduler(consumer, initialDelay, period);
            s_schedulers[typeof(TTransaction)] = scheduler;
        }

        return scheduler;
    }
}
