namespace Paramore.Brighter;

public interface IAmAMessageSchedulerFactory
{
    IAmAMessageScheduler Create(IAmAnOutboxProducerMediator mediator);

    IAmAMessageScheduler Create<TTransaction>(IAmAnOutboxProducerMediator mediator,
        IAmABoxTransactionProvider<TTransaction>? transactionProvider);
}
