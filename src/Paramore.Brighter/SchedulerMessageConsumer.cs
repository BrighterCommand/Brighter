using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter;

public class SchedulerMessageConsumer<TTransaction>(
    IAmAnOutboxProducerMediator mediator,
    IAmABoxTransactionProvider<TTransaction>? transactionProvider) :
    IAmASchedulerMessageConsumerSync, 
    IAmASchedulerMessageConsumerAsync
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<SchedulerMessageConsumer<TTransaction>>();
    public async Task ConsumeAsync(Message message, RequestContext context)
    {
        s_logger.LogInformation("Publishing scheduler message");
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
        s_logger.LogInformation("Publishing scheduler message");
        if (!mediator.HasOutbox())
        {
            throw new InvalidOperationException("No outbox defined.");
        }

        var outbox = (IAmAnOutboxProducerMediator<Message, TTransaction>)mediator;
        outbox.AddToOutbox(message, context, transactionProvider);
        outbox.ClearOutbox([message.Id], context);
    }
}
