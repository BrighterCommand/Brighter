using System;
using System.Threading.Tasks;

namespace Paramore.Brighter;

public class SchedulerMessageConsumer<TTransaction>(
    IAmAnOutboxProducerMediator mediator,
    IAmABoxTransactionProvider<TTransaction>? transactionProvider) :
    IAmASchedulerMessageConsumerSync, 
    IAmASchedulerMessageConsumerAsync
{
    public async Task ConsumeAsync(Message message, RequestContext context)
    {
        if (!mediator.HasOutbox())
        {
            throw new InvalidOperationException("No outbox defined.");
        }

        var outbox = (IAmAnOutboxProducerMediator<Message, TTransaction>)mediator;
        await outbox.AddToOutboxAsync(message, context, transactionProvider);
        await outbox.ClearOutboxAsync([message.Id], context);
    }

    public void Consume(Message message, RequestContext context)
    {
        if (!mediator.HasOutbox())
        {
            throw new InvalidOperationException("No outbox defined.");
        }

        var outbox = (IAmAnOutboxProducerMediator<Message, TTransaction>)mediator;
        outbox.AddToOutbox(message, context, transactionProvider);
        outbox.ClearOutbox([message.Id], context);
    }
}
